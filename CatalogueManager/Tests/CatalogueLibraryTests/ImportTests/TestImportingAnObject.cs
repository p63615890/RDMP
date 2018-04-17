﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using CatalogueLibrary.Data;
using CatalogueLibrary.Data.ImportExport;
using CatalogueLibraryTests.Integration;
using MapsDirectlyToDatabaseTable;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NUnit.Framework;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.Serialization;
using Rhino.Mocks;
using Rhino.Mocks.Utilities;
using Sharing.Dependency;
using Tests.Common;

namespace CatalogueLibraryTests.ImportTests
{
    public class TestImportingAnObject : DatabaseTests
    {
        /*
        [Test]
        public void ImportACatalogue()
        {
            var c = new Catalogue(CatalogueRepository, "omg cata");
            Assert.AreEqual(CatalogueRepository.GetAllCatalogues().Count(), 1);

            var shareManager = new ShareManager(RepositoryLocator);

            var c2 = (Catalogue)new ShareDefinition<Catalogue>(c).ImportObject(RepositoryLocator);

            Assert.AreEqual(c.Name, c2.Name);
            Assert.AreNotEqual(c.ID,c2.ID);

            Assert.AreEqual(CatalogueRepository.GetAllCatalogues().Count(),2);

        }

        [Test]
        public void TestSharingAPluginIgnoringCollisions()
        {
            foreach (var oldP in CatalogueRepository.GetAllObjects<Plugin>())
                oldP.DeleteInDatabase();

            var fi = new FileInfo("CommitAssemblyEmptyAssembly.dll");
            Assert.IsTrue(fi.Exists);

            var p = new Plugin(CatalogueRepository, fi);
            var lma = new LoadModuleAssembly(CatalogueRepository, fi, p);
            
            var n = new SharedPluginImporter(p);
            
            //reject the reuse of an existing one
            var p2 = n.Import(RepositoryLocator, new ThrowImmediatelyCheckNotifier());

            Assert.AreEqual(p.LoadModuleAssemblies.Count(),p2.LoadModuleAssemblies.Count());
            Assert.AreEqual(p.LoadModuleAssemblies.First().Dll,p2.LoadModuleAssemblies.First().Dll);
            Assert.AreNotEqual(p.ID, p2.ID);
        }

        [Test]
        public void TestSharingAPluginReplaceBinary()
        {
            foreach (var oldP in CatalogueRepository.GetAllObjects<Plugin>())
                oldP.DeleteInDatabase();

            var fi = new FileInfo("CommitAssemblyEmptyAssembly.dll");
            Assert.IsTrue(fi.Exists);

            var p = new Plugin(CatalogueRepository, fi);
            var lma = new LoadModuleAssembly(CatalogueRepository, fi, p);

            Assert.IsTrue(lma.Exists());

            Dictionary<string,object> newDll = new Dictionary<string, object>();
            newDll.Add("Name","AmagadNewPluginLma");
            newDll.Add("Dll",new byte[]{0,1,0,1});
            newDll.Add("Pdb",new byte[]{0,1,0,1});
            newDll.Add("Committer","Frankenstine");
            newDll.Add("Description","Stuff");
            newDll.Add("Plugin_ID",999);
            newDll.Add("DllFileVersion","5.0.0.1");
            newDll.Add("UploadDate",new DateTime(2001,1,1));
            newDll.Add("SoftwareVersion", "2.5.0.1");

            var n = new SharedPluginImporter(new ShareDefinition<Plugin>(p), new[] { new ShareDefinition<LoadModuleAssembly>(newDll) });
            
            //accept that it is an update
            var p2 = n.Import(RepositoryLocator, new AcceptAllCheckNotifier());

            Assert.IsFalse(lma.Exists());
            Assert.AreEqual(p,p2);
            Assert.AreEqual(p.LoadModuleAssemblies.Single().Dll, new byte[] { 0, 1, 0, 1 });
        }

        [Test]
        [TestCase(false)]
        [TestCase(true)]
        public void TestSharingAPluginReplaceDllBinary(bool fiddleIds)
        {
            foreach (var oldP in CatalogueRepository.GetAllObjects<Plugin>())
                oldP.DeleteInDatabase();

            var fi = new FileInfo("CommitAssemblyEmptyAssembly.dll");
            Assert.IsTrue(fi.Exists);

            var p = new Plugin(CatalogueRepository, fi);
            var lma = new LoadModuleAssembly(CatalogueRepository, fi, p);

            var pStateless = new ShareDefinition<Plugin>(p);
            var lmaStateless = new ShareDefinition<LoadModuleAssembly>(lma);

            if (fiddleIds)
            {
                //make it look like a new object we haven't seen before (but which collides on Name) this is the case when sharing with someone that isn't yourself
                pStateless.Properties["ID"] = -80;
                lmaStateless.Properties["Plugin_ID"] = -80;
            }

            //edit the binary data to represent a new version of the dll that should be imported
            lmaStateless.Properties["Dll"] =  new byte[] { 0, 1, 0, 1 };
            
            var n = new SharedPluginImporter(pStateless,new []{lmaStateless});
            
            //accept that it is an update
            var p2 = n.Import(RepositoryLocator, new AcceptAllCheckNotifier());

            Assert.AreEqual(p, p2);
            Assert.AreEqual(new byte[] { 0, 1, 0, 1 }, p2.LoadModuleAssemblies.Single().Dll);
        }


        [Test]
        public void JsonTest()
        {
            foreach (var oldP in CatalogueRepository.GetAllObjects<Plugin>())
                oldP.DeleteInDatabase();

            var fi = new FileInfo("CommitAssemblyEmptyAssembly.dll");
            Assert.IsTrue(fi.Exists);

            var p = new Plugin(CatalogueRepository, fi);
            var lma = new LoadModuleAssembly(CatalogueRepository, fi, p);

            var pStateless = new ShareDefinition<Plugin>(p);
            var lmaStatelessArray = new[] {new ShareDefinition<LoadModuleAssembly>(lma)};

            BinaryFormatter bf = new BinaryFormatter();
            string s;
            string sa;

            using (MemoryStream ms = new MemoryStream())
            {
                bf.Serialize(ms, pStateless);
                s = Convert.ToBase64String(ms.ToArray());
            }

            using (MemoryStream ms = new MemoryStream())
            {
                bf.Serialize(ms, lmaStatelessArray);
                sa = Convert.ToBase64String(ms.ToArray());
            }

            var import = new SharedPluginImporter(s, sa);

            var p2 = import.Import(RepositoryLocator, new AcceptAllCheckNotifier());

            Assert.AreEqual(p.LoadModuleAssemblies.Count(),p2.LoadModuleAssemblies.Count());
            Assert.AreEqual(p.LoadModuleAssemblies.First().Dll,p2.LoadModuleAssemblies.First().Dll);
            Assert.AreEqual(p.ID, p2.ID);

        }*/
    }
}
