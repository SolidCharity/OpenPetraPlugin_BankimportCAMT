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

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Data.Odbc;
using NUnit.Framework;
using Ict.Testing.NUnitTools;
using Ict.Common;
using Ict.Common.Data;
using Ict.Common.DB;
using Ict.Common.Remoting.Server;
using Ict.Common.Remoting.Shared;
using Ict.Common.Verification;
using Ict.Petra.Plugins.BankimportCAMT.Client;

namespace Ict.Petra.Plugins.Testing.BankimportCAMT
{
    /// <summary>
    /// a couple of tests for CAMT bankimport files
    /// </summary>
    [TestFixture]
    public class TestCAMT
    {
        /// <summary>
        /// TestFixtureSetUp
        /// </summary>
        [TestFixtureSetUp]
        public void Init()
        {
            new TAppSettingsManager("../../etc/TestClient.config");
            new TLogging("../../log/TestClient.log");
        }

        /// <summary>
        /// TearDown the test
        /// </summary>
        [TestFixtureTearDown]
        public void TearDownTest()
        {
        }

        /// <summary>
        /// Import a sample file
        /// </summary>
        [Test]
        public void ImportFile()
        {
            TCAMTParser p = new TCAMTParser();

            p.ProcessFile("../../csharp/ICT/Petra/Plugins/BankimportCAMT/Testing/test-data/test1.xml");
        }
    }
}