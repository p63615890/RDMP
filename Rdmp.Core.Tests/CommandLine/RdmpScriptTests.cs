﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Rdmp.Core.CommandLine.Options;
using Rdmp.Core.CommandLine.Runners;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.DataFlowPipeline;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.Progress;
using Tests.Common;

namespace Rdmp.Core.Tests.CommandLine
{
    class RdmpScriptTests : UnitTests
    {
        [TestCase("NewObject Catalogue 'trog dor'","trog dor")]
        [TestCase("NewObject Catalogue \"trog dor\"","trog dor")]
        [TestCase("NewObject Catalogue \"'trog dor'\"","'trog dor'")]
        [TestCase("NewObject Catalogue '\"trog dor\"'","\"trog dor\"")]

        public void RdmpScript_NewObject_Catalogue(string command, string expectedName)
        {
            foreach(var c in RepositoryLocator.CatalogueRepository.GetAllObjects<Catalogue>())
                c.DeleteInDatabase();

            var runner = new ExecuteCommandRunner(new ExecuteCommandOptions()
            {
                Script = new RdmpScript()
                {
                    Commands = new[] {command}
                }
            });
            
            SetupMEF();

            var exitCode = runner.Run(RepositoryLocator, new ThrowImmediatelyDataLoadEventListener(), new ThrowImmediatelyCheckNotifier(), new GracefulCancellationToken());

            Assert.AreEqual(0,exitCode);
            Assert.AreEqual(1,RepositoryLocator.CatalogueRepository.GetAllObjects<Catalogue>().Length);

            Assert.AreEqual(expectedName,RepositoryLocator.CatalogueRepository.GetAllObjects<Catalogue>().Single().Name);
        }

        [TestCase("NewObject Catalogue 'fffff'","NewObject CatalogueItem Catalogue:*fff* 'bbbb'","bbbb")]
        [TestCase("NewObject Catalogue '\"fff\"'","NewObject CatalogueItem 'Catalogue:\"fff\"' 'bbbb'","bbbb")]
        [TestCase("NewObject Catalogue '\"ff ff\"'","NewObject CatalogueItem 'Catalogue:\"ff ff\"' 'bb bb'","bb bb")]
        [TestCase("NewObject Catalogue '\"ff ff\"'","NewObject CatalogueItem 'Catalogue:\"ff ff\"' bb'bb","bb'bb")]
        [TestCase("NewObject Catalogue '\"ff ff\"'","NewObject CatalogueItem 'Catalogue:\"ff ff\"' b\"b'bb'","b\"b'bb'")]
        public void RdmpScript_NewObject_CatalogueItem(string cataCommand,string cataItemCommand, string expectedCataItemName)
        {
            foreach(var c in RepositoryLocator.CatalogueRepository.GetAllObjects<Catalogue>())
                c.DeleteInDatabase();

            var runner = new ExecuteCommandRunner(new ExecuteCommandOptions()
            {
                Script = new RdmpScript()
                {
                    Commands = new[]
                    {
                        cataCommand,
                        cataItemCommand
                    }
                }
            });
            
            SetupMEF();

            var exitCode = runner.Run(RepositoryLocator, new ThrowImmediatelyDataLoadEventListener(), new ThrowImmediatelyCheckNotifier(), new GracefulCancellationToken());

            Assert.AreEqual(0,exitCode);
            Assert.AreEqual(1,RepositoryLocator.CatalogueRepository.GetAllObjects<Catalogue>().Length);
            var ci = RepositoryLocator.CatalogueRepository.GetAllObjects<Catalogue>().Single().CatalogueItems.Single();

            Assert.AreEqual(expectedCataItemName,ci.Name);
            
        }

        [Test]
        public void Test_SplitCommandLine()
        {
            var vals = ExecuteCommandRunner.SplitCommandLine("NewObject CatalogueItem 'Catalogue:\"fff\"' 'bbbb'").ToArray();
            Assert.AreEqual("NewObject",vals[0]);
            Assert.AreEqual("CatalogueItem",vals[1]);
            Assert.AreEqual("Catalogue:\"fff\"",vals[2]);
            Assert.AreEqual("bbbb",vals[3]);
        }
        [Test]
        public void Test_SplitCommandLine_QuotesInStrings()
        {
            var vals = ExecuteCommandRunner.SplitCommandLine("NewObject CatalogueItem bb\"'bb'").ToArray();
            Assert.AreEqual("NewObject",vals[0]);
            Assert.AreEqual("CatalogueItem",vals[1]);
            Assert.AreEqual("bb\"'bb'",vals[2]);
        }
    }
}
