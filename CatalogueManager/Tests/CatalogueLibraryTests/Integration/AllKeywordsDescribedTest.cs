// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CatalogueLibrary.Repositories;
using DataQualityEngine.Data;
using FAnsi.Discovery;
using MapsDirectlyToDatabaseTable;
using NUnit.Framework;
using Tests.Common;

namespace CatalogueLibraryTests.Integration
{
    [TestFixture]
    public class AllKeywordsDescribedTest :DatabaseTests
    {

        [OneTimeSetUp]
        public void SetupCommentStore()
        {
            CatalogueRepository.SuppressHelpLoading = false;
            CatalogueRepository.LoadHelp(TestContext.CurrentContext.WorkDirectory);
            CatalogueRepository.SuppressHelpLoading = true;
        }

        [Test]
        public void AllTablesDescribed()
        {
            //ensures the DQERepository gets a chance to add it's help text
            new DQERepository(CatalogueRepository);

            List<string> problems = new List<string>();

            List<Exception> ex;
            var databaseTypes = CatalogueRepository.MEF.GetAllTypesFromAllKnownAssemblies(out ex).Where(t => typeof(IMapsDirectlyToDatabaseTable).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract && !t.Name.StartsWith("Spontaneous") && !t.Name.Contains("Proxy")).ToArray();


            foreach (var type in databaseTypes)
            {
                var docs = CatalogueRepository.CommentStore[type.Name]??CatalogueRepository.CommentStore["I"+type.Name];
                
                if(string.IsNullOrWhiteSpace(docs))
                    problems.Add("Type " + type.Name + " does not have an entry in the help dictionary (maybe the class doesn't have documentation? - try adding /// <summary> style comments to the class)");
                
            }
            foreach (string problem in problems)
                Console.WriteLine("Fatal Problem:" + problem);

            Assert.AreEqual(0,problems.Count);
        }

        [Test]
        public void AllForeignKeysDescribed()
        {
            List<string> allKeys = new List<string>();

            //ensures the DQERepository gets a chance to add it's help text
            new DQERepository(CatalogueRepository);

            allKeys.AddRange(GetForeignKeys(CatalogueRepository.DiscoveredServer));
            allKeys.AddRange(GetForeignKeys(DataExportRepository.DiscoveredServer));
            allKeys.AddRange(GetForeignKeys(new DiscoveredServer(DataQualityEngineConnectionString)));

            List<string> problems = new List<string>();
            foreach (string fkName in allKeys)
            {
                if (!CatalogueRepository.CommentStore.ContainsKey(fkName))
                    problems.Add(fkName + " is a foreign Key (which does not CASCADE) but does not have any HelpText");
            }
            
            foreach (string problem in problems)
                Console.WriteLine("Fatal Problem:" + problem);

            Assert.AreEqual(0, problems.Count, @"Add a description for each of these to \CatalogueManager\CatalogueLibrary\KeywordHelp.txt");
        }

        [Test]
        public void AllUserIndexesDescribed()
        {
            List<string> allIndexes = new List<string>();

            //ensures the DQERepository gets a chance to add it's help text
            new DQERepository(CatalogueRepository);

            allIndexes.AddRange(GetIndexes(CatalogueRepository.DiscoveredServer));
            allIndexes.AddRange(GetIndexes(DataExportRepository.DiscoveredServer));
            allIndexes.AddRange(GetIndexes(new DiscoveredServer(DataQualityEngineConnectionString)));

            List<string> problems = new List<string>();
            foreach (string idx in allIndexes)
            {
                if (!CatalogueRepository.CommentStore.ContainsKey(idx))
                    problems.Add(idx + " is an index but does not have any HelpText");
            }
            
            foreach (string problem in problems)
                Console.WriteLine("Fatal Problem:" + problem);

            Assert.AreEqual(0,problems.Count,@"Add a description for each of these to \CatalogueManager\CatalogueLibrary\KeywordHelp.txt");
            
        }

        private IEnumerable<string> GetForeignKeys(DiscoveredServer server)
        {
            using (var con = server.GetConnection())
            {
                con.Open();
                var r = server.GetCommand(@"select name from sys.foreign_keys where delete_referential_action = 0", con).ExecuteReader();

                while (r.Read())
                    yield return (string)r["name"];
            }
        }

        private IEnumerable<string> GetIndexes(DiscoveredServer server)
        {
            using (var con = server.GetConnection())
            {
                con.Open();
                var r = server.GetCommand(@"select si.name from sys.indexes si 
  JOIN sys.objects so ON si.[object_id] = so.[object_id]
  WHERE
  so.type = 'U'  AND is_primary_key = 0
  and si.name is not null
and so.name <> 'sysdiagrams'", con).ExecuteReader();

                while (r.Read())
                    yield return (string)r["name"];
            }
        }
        [OneTimeTearDown]
        public void unsetHelpDispel()
        {
            CatalogueRepository.SuppressHelpLoading = true;
        }
    }
}
