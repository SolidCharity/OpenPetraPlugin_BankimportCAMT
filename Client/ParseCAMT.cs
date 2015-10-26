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
                TStatement stmt = new TStatement();
                XmlDocument doc = new XmlDocument();
                doc.Load(filename);
                XmlNode nodeDocument = doc.SelectSingleNode("/Document");
                if (nodeDocument.Attributes["xmlns"].Value != "urn:iso:std:iso:20022:tech:xsd:camt.053.001.02")
                {
                    throw new Exception("expecting xmlns = 'urn:iso:std:iso:20022:tech:xsd:camt.053.001.02'");
                }

                XmlNode nodeStatement = doc.SelectSingleNode("/Document/Stmt");
                stmt.accountCode = nodeStatement.SelectSingleNode("/Acct/Id/IBAN").Value;
                stmt.bankCode = nodeStatement.SelectSingleNode("/Acct/Svcr/FinInstnId/BIC").Value;
                stmt.currency = nodeStatement.SelectSingleNode("/Acct/Ccy").Value;
                XmlNode nodeBalances = nodeStatement.SelectNodes("/Bal");
                foreach (XmlNode nodeBalance in nodeBalances)
                {
                    // PRCD: PreviouslyClosedBooked
                    if (nodeBalance.SelectSingleNode("/Tp/CdOrPrtry/Cd").Value == "PRCD")
                    {
                        stmt.startBalance = Decimal.Parse(nodeBalance.SelectSingleNode("/Amt").Value);
                        // CreditDebitIndicator: CRDT or DBIT for credit or debit
                        if (nodeBalance.SelectSingleNode("/CdtDbtInd") == "DBIT")
                        {
                            stmt.startBalance *= -1.0m;
                        }
                    }
                    // CLBD: ClosingBooked
                    else if (nodeBalance.SelectSingleNode("/Tp/CdOrPrtry/Cd").Value == "CLBD")
                    {
                        stmt.endBalance = Decimal.Parse(nodeBalance.SelectSingleNode("/Amt").Value);
                        // CreditDebitIndicator: CRDT or DBIT for credit or debit
                        if (nodeBalance.SelectSingleNode("/CdtDbtInd") == "DBIT")
                        {
                            stmt.endBalance *= -1.0m;
                        }
                        stmt.date = DateTime.Parse(nodeBalance.SelectSingleNode("/Dt/Dt").Value);
                    }
                    // ITBD: InterimBooked
                    // CLAV: ClosingAvailable
                    // FWAV: ForwardAvailable
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
