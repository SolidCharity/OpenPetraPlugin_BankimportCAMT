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
using System.Data;
using System.Windows.Forms;
using System.Threading;
using System.Text.RegularExpressions;
using Ict.Common;
using Ict.Common.Data; // Implicit reference
using Ict.Common.Verification;
using Ict.Common.Remoting.Shared;
using Ict.Common.Remoting.Client;
using Ict.Petra.Client.CommonDialogs;
using Ict.Petra.Shared.MFinance;
using Ict.Petra.Shared.MFinance.Account.Data;
using Ict.Petra.Shared.MFinance.Gift.Data;
using Ict.Petra.Plugins.Bankimport.Data;
using Ict.Petra.Client.App.Core.RemoteObjects;
using Ict.Petra.Plugins.Bankimport.Client;
using GNU.Gettext;
using Ict.Petra.Plugins.Bankimport.RemoteObjects;

namespace Ict.Petra.Plugins.BankimportCAMT.Client
{
    /// <summary>
    /// import a bank statement from a CAMT Swift file
    /// </summary>
    public class TBankStatementImport : IImportBankStatement
    {
        /// <summary>
        /// asks the user to open a csv file and imports the contents according to the config file
        /// </summary>
        /// <param name="AStatementKey">this returns the first key of a statement that was imported. depending on the implementation, several statements can be created from one file</param>
        /// <param name="ALedgerNumber">the current ledger number</param>
        /// <param name="ABankAccountCode">the bank account against which the statement should be stored</param>
        /// <returns></returns>
        public bool ImportBankStatement(out Int32 AStatementKey, Int32 ALedgerNumber, string ABankAccountCode)
        {
            AStatementKey = -1;

            // each time the button btnImportNewStatement is clicked, do a split and move action
            SplitFilesAndMove();
            ArchiveFilesLastMonth(ALedgerNumber);


            OpenFileDialog DialogOpen = new OpenFileDialog();

            DialogOpen.Filter = Catalog.GetString("bank statement MT940 (*.sta)|*.sta");

            if (TAppSettingsManager.HasValue("BankimportPath" + ALedgerNumber.ToString()))
            {
                DialogOpen.InitialDirectory = TAppSettingsManager.GetValue("BankimportPath" + ALedgerNumber.ToString());
            }
            else
            {
                DialogOpen.RestoreDirectory = true;
            }

            DialogOpen.Multiselect = true;
            DialogOpen.Title = Catalog.GetString("Please select the bank statement to import");

            if (DialogOpen.ShowDialog() != DialogResult.OK)
            {
                return false;
            }

            BankImportTDS MainDS = new BankImportTDS();

            // import several files at once
            foreach (string BankStatementFilename in DialogOpen.FileNames)
            {
                if (!ImportFromFile(BankStatementFilename,
                        ABankAccountCode,
                        ref MainDS))
                {
                    return false;
                }
            }

            if (MainDS.AEpStatement.Count > 0)
            {
                foreach (AEpStatementRow stmt in MainDS.AEpStatement.Rows)
                {
                    MainDS.AEpTransaction.DefaultView.RowFilter =
                        String.Format("{0}={1}",
                            AEpTransactionTable.GetStatementKeyDBName(),
                            stmt.StatementKey);

                    stmt.LedgerNumber = ALedgerNumber;
                }

                Thread t = new Thread(() => ProcessStatementsOnServer(MainDS));

                using (TProgressDialog dialog = new TProgressDialog(t))
                {
                    if (dialog.ShowDialog() == DialogResult.Cancel)
                    {
                        return false;
                    }
                    else
                    {
                        AStatementKey = FStatementKey;
                        return FStatementKey != -1;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// this non interactive function can be used from the unit tests
        /// </summary>
        public BankImportTDS ImportBankStatementNonInteractive(Int32 ALedgerNumber, string ABankAccountCode,
            string ABankStatementFilename)
        {
            BankImportTDS MainDS = new BankImportTDS();

            // import file
            if (!ImportFromFile(ABankStatementFilename,
                    ABankAccountCode,
                    ref MainDS))
            {
                return null;
            }

            foreach (AEpStatementRow stmt in MainDS.AEpStatement.Rows)
            {
                stmt.LedgerNumber = ALedgerNumber;
            }

            return MainDS;
        }

        private int FStatementKey = -1;

        private void ProcessStatementsOnServer(BankImportTDS AMainDS)
        {
            TMBankimportNamespace PluginRemote = new TMBankimportNamespace();

            if (PluginRemote.WebConnectors.StoreNewBankStatement(
                    AMainDS,
                    out FStatementKey) == TSubmitChangesResult.scrOK)
            {
            }
        }

        /// <summary>
        /// open the file and return a typed datatable
        /// </summary>
        private bool ImportFromFile(string AFilename,
            string ABankAccountCode,
            ref BankImportTDS AMainDS)
        {
            // TODO
            return true;
        }

        /// create the output directories if they don't exist yet
        static private void CreateDirectories(string AOutputPath, string[] ABankAccountData)
        {
            if (!Directory.Exists(AOutputPath))
            {
                Directory.CreateDirectory(AOutputPath);
            }

            if (!Directory.Exists(AOutputPath + Path.DirectorySeparatorChar + "imported"))
            {
                Directory.CreateDirectory(AOutputPath + Path.DirectorySeparatorChar + "imported");
            }

            for (Int32 bankCounter = 0; bankCounter < ABankAccountData.Length / 3; bankCounter++)
            {
                string legalEntityPath = AOutputPath + Path.DirectorySeparatorChar + ABankAccountData[bankCounter * 3 + 2];

                if (!Directory.Exists(legalEntityPath))
                {
                    Directory.CreateDirectory(legalEntityPath);
                }

                if (!Directory.Exists(legalEntityPath + Path.DirectorySeparatorChar + "imported"))
                {
                    Directory.CreateDirectory(legalEntityPath + Path.DirectorySeparatorChar + "imported");
                }
            }
        }

        /// check for xml files in RawCAMT.Path
        /// there are files from several banks, possibly for several legal entities
        /// one file can contain several bank statements from several days
        /// split the files into one file per statement, and move the file to a separate directory for each legal entity
        private bool SplitFilesAndMove()
        {
            if (!TAppSettingsManager.HasValue("BankAccounts"))
            {
                TLogging.Log("missing parameter BankAccounts in config file");
                return false;
            }

            // BankAccounts contains a comma separated list of bank accounts,
            // each with bank account number, bank id, name for legal entity
            string[] bankAccountData = TAppSettingsManager.GetValue("BankAccounts").Split(new char[] { ',' });
            string RawPath = TAppSettingsManager.GetValue("RawCAMT.Path");
            string OutputPath = TAppSettingsManager.GetValue("CAMT.Output.Path");
            string[] RawXMLFiles = Directory.GetFiles(RawPath, "*.xml");

            CreateDirectories(OutputPath, bankAccountData);

            foreach (string RawFile in RawXMLFiles)
            {
                TLogging.Log("BankImport CAMT plugin: splitting file " + RawFile);
            }

            return true;
        }

        private bool ArchiveFilesLastMonth(Int32 ALedgerNumber)
        {
            string MyPath = TAppSettingsManager.GetValue("BankimportPath" + ALedgerNumber.ToString() + Path.DirectorySeparatorChar);
            string MyPath2 = MyPath + Path.DirectorySeparatorChar + "imported" + Path.DirectorySeparatorChar;

            if (DateTime.Today.Day >= 8)
            {
                DateTime LastMonth = DateTime.Today.AddMonths(-1);

                for (int counter = 1; counter <= 31; counter++)
                {
                    string filename = "EKK_" + LastMonth.ToString("yyMM") + counter.ToString("00") + ".sta";
                    string filename2 = "SPK_" + LastMonth.ToString("yyMM") + counter.ToString("00") + ".sta";

                    if (File.Exists(MyPath + filename))
                    {
                        System.IO.File.Move(MyPath + filename, MyPath2 + filename);
                    }

                    if (File.Exists(MyPath + filename2))
                    {
                        System.IO.File.Move(MyPath + filename2, MyPath2 + filename2);
                    }
                }
            }

            return false;
        }
    }
}
