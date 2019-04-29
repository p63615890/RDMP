// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using FAnsi.Implementation;
using FAnsi.Implementations.MicrosoftSQL;
using FAnsi.Implementations.MySql;
using FAnsi.Implementations.Oracle;
using NUnit.Framework;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.QueryBuilding;
using Rdmp.Core.Repositories;

namespace Rdmp.Core.Tests.CatalogueLibrary.MemoryRepositoryTests
{
    class MemoryRepositoryTests
    {
        readonly MemoryCatalogueRepository _repo = new MemoryCatalogueRepository();

        [OneTimeSetUp]
        public void Setup()
        {
            ImplementationManager.Load(
                typeof(MicrosoftSQLImplementation).Assembly,
                typeof(MySqlImplementation).Assembly,
                typeof(OracleImplementation).Assembly);
        }

        [Test]
        public void TestMemoryRepository_CatalogueConstructor()
        {
            Catalogue memCatalogue = new Catalogue(_repo, "My New Catalogue");

            Assert.AreEqual(memCatalogue, _repo.GetObjectByID<Catalogue>(memCatalogue.ID));
        }

        [Test]
        public void TestMemoryRepository_QueryBuilder()
        {
            Catalogue memCatalogue = new Catalogue(_repo, "My New Catalogue");

            CatalogueItem myCol = new CatalogueItem(_repo,memCatalogue,"MyCol1");

            var ti = new TableInfo(_repo, "My table");
            var col = new ColumnInfo(_repo, "Mycol", "varchar(10)", ti);

            ExtractionInformation ei = new ExtractionInformation(_repo, myCol, col, col.Name);

            Assert.AreEqual(memCatalogue, _repo.GetObjectByID<Catalogue>(memCatalogue.ID));

            var qb = new QueryBuilder(null,null);
            qb.AddColumnRange(memCatalogue.GetAllExtractionInformation(ExtractionCategory.Any));

            Assert.AreEqual(@"
SELECT 

Mycol
FROM 
My table", qb.SQL);
        }
    }
}
