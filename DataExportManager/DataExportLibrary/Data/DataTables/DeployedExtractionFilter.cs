// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using CatalogueLibrary.Checks;
using CatalogueLibrary.Data;
using CatalogueLibrary.Data.Aggregation;
using CatalogueLibrary.FilterImporting;
using CatalogueLibrary.FilterImporting.Construction;
using CatalogueLibrary.Repositories;
using DataExportLibrary.Data.LinkCreators;
using DataExportLibrary.Interfaces.Data.DataTables;
using DataExportLibrary.Repositories;
using MapsDirectlyToDatabaseTable;

using ReusableLibraryCode.Checks;
using IFilter = CatalogueLibrary.Data.IFilter;

namespace DataExportLibrary.Data.DataTables
{
    /// <summary>
    /// Sometimes it is necessary to restrict which records are extracted for a given ExtractionConfiguration beyond the linkage against a cohort.  For example you might want to extract
    /// 'only paracetamol prescriptions' for your cohort rather than the entire Prescribing dataset.  This is achieved by using a DeployedExtractionFilter.  DeployedExtractionFilters are
    /// curated pieces of WHERE SQL with a name and description.  These can either be written bespoke for your extract or copied from a master ExtractionFilter in the Catalogue database.
    /// In general if a filter concept is reusable and useful across multiple projects / over time then you should create it in the Catalogue database as an ExtractionFilter and then 
    /// import a copy into your ExtractionConfiguration each time you need it (or mark it as IsMandatory if it should always be used in data extraction of that Catalogue).
    /// 
    /// <para>DeployedExtractionFilter differs from ExtractionFilter in that DeployedExtractionFilters are 'per Catalogue in an ExtractionConfiguration' while ExtractionFilters are master copies
    /// stored in the Catalogue database (instead of the DataExportManager database).  When you import a master filter into your ExtractionConfiguration a copy of the WHERE SQL, any 
    /// parameters and the name and description will be made as a DeployedExtractionFilter which will also contain a reference back to the original (ClonedFromExtractionFilter_ID).  This
    /// allows you to ensure consistency over time and gives you a central location (the ExtractionFilter) to fix errors in the Filter implementation etc.  </para>
    /// 
    /// <para>When you open an DeployedExtractionFilter and it differs from the master (either because you have deliberately adjusted your copy or because the master has been updated to fix
    /// a problem) then you will be alerted via the Filter Checks. </para>
    /// </summary>
    public class DeployedExtractionFilter : ConcreteFilter
    {
        #region Database Properties

        private int? _clonedFromExtractionFilterID;
        private int? _filterContainerID;

        /// <inheritdoc/>
        public override int? ClonedFromExtractionFilter_ID
        {
            get { return _clonedFromExtractionFilterID; }
            set { SetField(ref _clonedFromExtractionFilterID , value); }
        }
        
        /// <inheritdoc/>
        public override int? FilterContainer_ID
        {
            get { return _filterContainerID; }
            set { SetField(ref _filterContainerID , value); }
        }

        #endregion
        #region Relationships

        [NoMappingToDatabase]
        public DeployedExtractionFilterParameter[] ExtractionFilterParameters
        {
            get
            {
                return
                Repository.GetAllObjects<DeployedExtractionFilterParameter>(
                    "WHERE ExtractionFilter_ID=" + ID)
                    .ToArray();
            }
        }

        /// <inheritdoc/>
        [NoMappingToDatabase]
        public override IContainer FilterContainer { get { return FilterContainer_ID.HasValue ? Repository.GetObjectByID<FilterContainer>(FilterContainer_ID.Value) : null; } }

        #endregion

        /// <inheritdoc/>
        public override ColumnInfo GetColumnInfoIfExists()
        {
            return null;
        }

        /// <inheritdoc/>
        public override IFilterFactory GetFilterFactory()
        {
            return new DeployedExtractionFilterFactory((IDataExportRepository)Repository);
        }

        /// <inheritdoc/>
        public override Catalogue GetCatalogue()
        {
            var ds = GetDataset().ExtractableDataSet;
            try
            {
                return (Catalogue)ds.Catalogue;
            }
            catch(Exception)
            {
                //could be that the catalogue has been deleted
                return null;
            }
        }

        /// <inheritdoc/>
        public override ISqlParameter[] GetAllParameters()
        {
            return ExtractionFilterParameters.Cast<ISqlParameter>().ToArray();

        }

        ///<inheritdoc cref="IRepository.FigureOutMaxLengths"/>
        public static int Name_MaxLength = -1;
        ///<inheritdoc cref="IRepository.FigureOutMaxLengths"/>
        public static int Description_MaxLength = -1;

        public DeployedExtractionFilter(IDataExportRepository repository, string name, FilterContainer container)
        {
            Repository = repository;
            Repository.InsertAndHydrate(this, new Dictionary<string, object>
            {
                {"Name", name != null ? (object) name : DBNull.Value},
                {"FilterContainer_ID", container != null ? (object) container.ID : DBNull.Value}
            });
        }

        internal DeployedExtractionFilter(IDataExportRepository repository, DbDataReader r)
            : base(repository, r)
        {
            WhereSQL = r["WhereSQL"] as string;
            Description = r["Description"] as string;
            Name = r["Name"] as string;
            IsMandatory = (bool)r["IsMandatory"];

            if (r["FilterContainer_ID"] != null && !string.IsNullOrWhiteSpace(r["FilterContainer_ID"].ToString()))
                FilterContainer_ID = int.Parse(r["FilterContainer_ID"].ToString());
            else
                FilterContainer_ID = null;

            ClonedFromExtractionFilter_ID = ObjectToNullableInt(r["ClonedFromExtractionFilter_ID"]);
        }

        public override string ToString()
        {
            return Name;
        }

        
        public override void Check(ICheckNotifier notifier)
        {
            base.Check(notifier);

            var checker = new ClonedFilterChecker(this, this.ClonedFromExtractionFilter_ID, ((DataExportRepository)Repository).CatalogueRepository);
            checker.Check(notifier);
        }

        public SelectedDataSets GetDataset()
        {
            if (FilterContainer_ID == null)
                return null;
            
            var container = Repository.GetObjectByID<FilterContainer>(FilterContainer_ID.Value);
            return container.GetSelectedDataSetsRecursively();
        }
    }
}
