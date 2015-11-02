//
// DO NOT REMOVE COPYRIGHT NOTICES OR THIS FILE HEADER.
//
// @Authors:
//       timop
//
// Copyright 2004-2015 by OM International
//
// This file is part of OpenPetra.org.
//
// OpenPetra.org is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// OpenPetra.org is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with OpenPetra.org.  If not, see <http://www.gnu.org/licenses/>.
//
using System;
using System.IO;
using System.Collections.Generic;
using System.Xml;
using System.Threading;
using System.Text;
using Ict.Common;

namespace Ict.Petra.Plugins.BankimportCAMT.Client
{
    /// <summary>
    /// parses bank statement files (ISO 20022 CAMT.053) in Germany;
    /// for the structure of the file see
    /// https://www.rabobank.com/nl/images/Format%20description%20CAMT.053.pdf
    /// http://www.national-bank.de/fileadmin/user_upload/nationalbank/Service_Center/Electronic_Banking_Center/Downloads/Handbuecher_und_Bedingungen/SRZ-Anlage_5b_Kontoauszug_ISO_20022_camt_2010-06-15b.pdf
    /// http://www.hettwer-beratung.de/sepa-spezialwissen/sepa-technische-anforderungen/camt-format-camt-053/
    /// </summary>
    public class TCAMTParser
    {
        /// <summary>
        /// the parsed bank statements
        /// </summary>
        public List <TStatement>statements;
        private TStatement currentStatement = null;

        private static string WithoutLeadingZeros(string ACode)
        {
            // cut off leading zeros
            try
            {
                return Convert.ToInt64(ACode).ToString();
            }
            catch (Exception)
            {
                // IBAN or BIC
                return ACode;
            }
        }

        /// <summary>
        /// processing CAMT file
        /// </summary>
        /// <param name="filename"></param>
        public void ProcessFile(string filename)
        {
            Console.WriteLine("Read file " + filename);

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(filename);
                string namespaceName = "urn:iso:std:iso:20022:tech:xsd:camt.053.001.02";
                XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
                nsmgr.AddNamespace("camt", namespaceName);

                XmlNode nodeDocument = doc.DocumentElement;

                if ((nodeDocument == null) || (nodeDocument.Attributes["xmlns"].Value != namespaceName))
                {
                    throw new Exception("expecting xmlns = '" + namespaceName + "'");
                }

                XmlNodeList stmts = nodeDocument.SelectNodes("camt:BkToCstmrStmt/camt:Stmt", nsmgr);

                foreach (XmlNode nodeStatement in stmts)
                {
                    TStatement stmt = new TStatement();
                    currentStatement = stmt;

                    stmt.id = nodeStatement.SelectSingleNode("camt:Id", nsmgr).InnerText;
                    stmt.accountCode = nodeStatement.SelectSingleNode("camt:Acct/camt:Id/camt:IBAN", nsmgr).InnerText;
                    stmt.bankCode = nodeStatement.SelectSingleNode("camt:Acct/camt:Svcr/camt:FinInstnId/camt:BIC", nsmgr).InnerText;
                    stmt.currency = nodeStatement.SelectSingleNode("camt:Acct/camt:Ccy", nsmgr).InnerText;
                    string ownName = nodeStatement.SelectSingleNode("camt:Acct/camt:Ownr/camt:Nm", nsmgr).InnerText;
                    XmlNodeList nodeBalances = nodeStatement.SelectNodes("camt:Bal", nsmgr);

                    foreach (XmlNode nodeBalance in nodeBalances)
                    {
                        // PRCD: PreviouslyClosedBooked
                        if (nodeBalance.SelectSingleNode("camt:Tp/camt:CdOrPrtry/camt:Cd", nsmgr).InnerText == "PRCD")
                        {
                            stmt.startBalance = Decimal.Parse(nodeBalance.SelectSingleNode("camt:Amt", nsmgr).InnerText);

                            // CreditDebitIndicator: CRDT or DBIT for credit or debit
                            if (nodeBalance.SelectSingleNode("camt:CdtDbtInd", nsmgr).InnerText == "DBIT")
                            {
                                stmt.startBalance *= -1.0m;
                            }
                        }
                        // CLBD: ClosingBooked
                        else if (nodeBalance.SelectSingleNode("camt:Tp/camt:CdOrPrtry/camt:Cd", nsmgr).InnerText == "CLBD")
                        {
                            stmt.endBalance = Decimal.Parse(nodeBalance.SelectSingleNode("camt:Amt", nsmgr).InnerText);

                            // CreditDebitIndicator: CRDT or DBIT for credit or debit
                            if (nodeBalance.SelectSingleNode("camt:CdtDbtInd", nsmgr).InnerText == "DBIT")
                            {
                                stmt.endBalance *= -1.0m;
                            }

                            stmt.date = DateTime.Parse(nodeBalance.SelectSingleNode("camt:Dt", nsmgr).InnerText);
                        }

                        // ITBD: InterimBooked
                        // CLAV: ClosingAvailable
                        // FWAV: ForwardAvailable
                    }

                    XmlNodeList nodeEntries = nodeStatement.SelectNodes("camt:Ntry", nsmgr);

                    foreach (XmlNode nodeEntry in nodeEntries)
                    {
                        TTransaction tr = new TTransaction();
                        tr.inputDate = DateTime.Parse(nodeEntry.SelectSingleNode("camt:BookgDt/camt:Dt", nsmgr).InnerText);
                        tr.valueDate = DateTime.Parse(nodeEntry.SelectSingleNode("camt:ValDt/camt:Dt", nsmgr).InnerText);
                        tr.amount = Decimal.Parse(nodeEntry.SelectSingleNode("camt:Amt", nsmgr).InnerText);

                        if (nodeEntry.SelectSingleNode("camt:Amt", nsmgr).Attributes["Ccy"].Value != stmt.currency)
                        {
                            throw new Exception("transaction currency " + nodeEntry.SelectSingleNode("camt:Amt",
                                    nsmgr).Attributes["Ccy"].Value + " does not match the bank statement currency");
                        }

                        if (nodeEntry.SelectSingleNode("camt:CdtDbtInd", nsmgr).InnerText == "DBIT")
                        {
                            tr.amount *= -1.0m;
                        }

                        tr.description = nodeEntry.SelectSingleNode("camt:NtryDtls/camt:TxDtls/camt:RmtInf/camt:Ustrd", nsmgr).InnerText;
                        XmlNode partnerName = nodeEntry.SelectSingleNode("camt:NtryDtls/camt:TxDtls/camt:RltdPties/camt:Dbtr/camt:Nm", nsmgr);

                        if (partnerName != null)
                        {
                            tr.partnerName = partnerName.InnerText;
                        }

                        XmlNode accountCode = nodeEntry.SelectSingleNode("camt:NtryDtls/camt:TxDtls/camt:RltdPties/camt:DbtrAcct/camt:Id/camt:IBAN",
                            nsmgr);

                        if (accountCode != null)
                        {
                            tr.accountCode = accountCode.InnerText;
                        }

                        XmlNode CrdtName = nodeEntry.SelectSingleNode("camt:NtryDtls/camt:TxDtls/camt:RltdPties/camt:Cdtr/camt:Nm", nsmgr);

                        if ((CrdtName != null) && (CrdtName.InnerText != ownName))
                        {
                            // sometimes donors write the project or recipient in the field where the organisation is supposed to be
                            tr.description += " " + CrdtName.InnerText;
                        }

                        tr.typecode = nodeEntry.SelectSingleNode("camt:Sts", nsmgr).InnerText;
                        stmt.transactions.Add(tr);

                        TLogging.LogAtLevel(2, "count : " + stmt.transactions.Count.ToString());
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception(
                    "problem with file " + filename + "; " + e.Message + Environment.NewLine + e.StackTrace);
            }
        }
    }

    /// todoComment
    public class TTransaction
    {
        /// todoComment
        public DateTime valueDate;

        /// todoComment
        public DateTime inputDate;

        /// todoComment
        public decimal amount;

        /// todoComment
        public string text;

        /// todoComment
        public string typecode;

        /// todoComment
        public string description;

        /// todoComment
        public string bankCode;

        /// todoComment
        public string accountCode;

        /// todoComment
        public string partnerName;
    }

    /// todoComment
    public class TStatement
    {
        /// todoComment
        public string id;

        /// todoComment
        public string bankCode;

        /// todoComment
        public string accountCode;

        /// todoComment
        public string currency;

        /// todoComment
        public decimal startBalance;

        /// todoComment
        public decimal endBalance;

        /// todoComment
        public DateTime date;

        /// todoComment
        public List <TTransaction>transactions = new List <TTransaction>();
    }
}