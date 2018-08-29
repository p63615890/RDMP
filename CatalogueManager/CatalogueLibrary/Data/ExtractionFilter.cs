using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using CatalogueLibrary.FilterImporting.Construction;
using CatalogueLibrary.Repositories;
using MapsDirectlyToDatabaseTable;
using ReusableLibraryCode;

namespace CatalogueLibrary.Data
{
    /// <summary>
    /// Defines as a single line SQL Where statement, a way of reducing the scope of a data extraction / aggregation etc.  For example, 
    /// 'Only prescriptions for diabetes medications'.  An ExtractionFilter can have 0 or more ExtractionFilterParameters which allows
    /// you to define a more versatile filter e.g. 'Only prescriptions for drug @bnfCode'
    /// 
    /// <para>Typically an ExtractionFilter is cloned out as either a DeployedExtractionFilter or an AggregateFilter and either used as is or
    /// customised in it's new state (where it's parameters might have values populated into them).</para>
    /// 
    /// <para>It is not uncommon for an extraction to involve multiple customised copies of the same Extraction filter for example a user might
    /// take the filter 'Prescriptions of drug @Drugname' and make 3 copies in a given project in DataExportManager (this would result in
    /// 3 DeployedExtractionFilters) and set the value of the first to 'Paracetamol' the second to 'Aspirin' and the third to 'Ibuprofen'
    /// and then put them all in a single AND container.</para>
    /// 
    /// <para>At query building time QueryBuilder rationalizes all the various containers, subcontainers, filters and parameters into one extraction
    /// SQL query (including whatever columns/transforms it was setup with).</para>
    /// </summary>
    public class ExtractionFilter : ConcreteFilter, IHasDependencies
    {
     
        #region Database Properties
        private int _extractionInformationID;

        /// <summary>
        /// The column in the <see cref="Catalogue"/> which is best/most associated with this filter.  A filter can query any column in any of the table(s) under
        /// the <see cref="Catalogue"/> but must always be associated with only one specific extractable column (<see cref="ExtractionInformation"/>)
        /// </summary>
        public int ExtractionInformation_ID
        {
            get { return _extractionInformationID; }
            set { SetField(ref _extractionInformationID , value); }
        }

        #endregion

        #region Relationships

        /// <inheritdoc/>
        [NoMappingToDatabase]
        public override IContainer FilterContainer { get { return null; } }

        #endregion

        /// <inheritdoc/>
        [NoMappingToDatabase]
        public override int? FilterContainer_ID
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }
        /// <inheritdoc/>
        public override ColumnInfo GetColumnInfoIfExists()
        {
            return ExtractionInformation.ColumnInfo;
        }

        /// <inheritdoc/>
        public override IFilterFactory GetFilterFactory()
        {
            return new ExtractionFilterFactory(ExtractionInformation);
        }

        /// <inheritdoc/>
        public override Catalogue GetCatalogue()
        {
            return ExtractionInformation.CatalogueItem.Catalogue;
        }

        /// <inheritdoc/>
        public override ISqlParameter[] GetAllParameters()
        {
            return ExtractionFilterParameters.ToArray();
        }

        ///<inheritdoc cref="IRepository.FigureOutMaxLengths"/>
        public static int Name_MaxLength = -1;
        ///<inheritdoc cref="IRepository.FigureOutMaxLengths"/>
        public static int Description_MaxLength = -1;

        #region Relationships

        /// <inheritdoc cref="ExtractionInformation_ID"/>
        [NoMappingToDatabase]
        public ExtractionInformation ExtractionInformation {get { return Repository.GetObjectByID<ExtractionInformation>(ExtractionInformation_ID); }}

        /// <inheritdoc cref="ConcreteFilter.GetAllParameters"/>
        [NoMappingToDatabase]
        public IEnumerable<ExtractionFilterParameter> ExtractionFilterParameters { get { return Repository.GetAllObjectsWithParent<ExtractionFilterParameter>(this); } }

        #endregion

        /// <summary>
        /// Creates a new WHERE SQL block for reuse with the <see cref="Catalogue"/> in which the <see cref="parent"/> resides.  This is a top level master filter and can be
        /// copied out in <see cref="CatalogueLibrary.Data.Cohort.CohortIdentificationConfiguration"/>, ExtractionConfiguration etc.  This ensures a single curated block of
        /// logic that everyone shares.
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="name"></param>
        /// <param name="parent"></param>
        public ExtractionFilter(ICatalogueRepository repository, string name, ExtractionInformation parent)
        {
            name = name ?? "New Filter " + Guid.NewGuid();

            repository.InsertAndHydrate(this,new Dictionary<string, object>
            {
                {"Name", name},
                {"ExtractionInformation_ID", parent.ID}
            });
        }

        internal ExtractionFilter(ICatalogueRepository repository, DbDataReader r)
            : base(repository, r)
        {
            ExtractionInformation_ID = int.Parse(r["ExtractionInformation_ID"].ToString());
            WhereSQL = r["WhereSQL"] as string;
            Description = r["Description"] as string;
            Name = r["Name"] as string;
            IsMandatory = (bool) r["IsMandatory"];
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Name;
        }
        
        //we are an extraction filter ourselves! so obviously we werent cloned from one! (this is for aggregate and data export filters and satisfies IFilter).  Actually we can
        //be cloned via the publishing (elevation) from a custom filter defined at Aggregate level for example.  But in this case we don't need to know the ID anyway since we 
        //become the new master anyway since we are at the highest level for filters

        /// <summary>
        /// Returns null, <see cref="ExtractionFilter"/> are master level filters and therefore never cloned from another filter
        /// </summary>
        [NoMappingToDatabase]
        public override int? ClonedFromExtractionFilter_ID
        {
            get
            {
                return null; 
            }
            set
            {
                throw new NotSupportedException("ClonedFromExtractionFilter_ID is only supported on lower level filters e.g. DeployedExtractionFilter and AggregateFilter");
            }
        }
        
        /// <inheritdoc/>
        public IHasDependencies[] GetObjectsThisDependsOn()
        {
            return new IHasDependencies[] { ExtractionInformation };
        }

        /// <inheritdoc/>
        public IHasDependencies[] GetObjectsDependingOnThis()
        {
            return ExtractionFilterParameters.ToArray();
        }

    
    }
}
