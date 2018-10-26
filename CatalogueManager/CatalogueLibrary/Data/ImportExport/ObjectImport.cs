using System;
using System.Collections.Generic;
using System.Data.Common;
using CatalogueLibrary.Data.Referencing;
using CatalogueLibrary.Repositories;
using MapsDirectlyToDatabaseTable;

namespace CatalogueLibrary.Data.ImportExport
{
    /// <summary>
    /// Identifies an object in the local Catalogue database (or DataExport database) which was imported from an external catalogue (See ObjectExport).  The SharingUID
    /// allows you to always identify which local object represents a remoted shared object (e.g. available from a web service).  The remote object will have a different
    ///  ID but the same SharingUID).  Sometimes you will import whole networks of objects which might have shared object dependencies in this case newly imported 
    /// networks will reference existing imported objects where they are already available.
    /// 
    /// <para>This table exists to avoid all the unmaintainability/scalability of IDENTITY INSERT whilst also ensuring referential integrity of object shares and preventing
    /// duplication of imported objects.</para>
    /// </summary>
    public class ObjectImport : ReferenceOtherObjectDatabaseEntity
    {
        #region Database Properties

        private string _sharingUID;
        
        #endregion

        public string SharingUID
        {
            get { return _sharingUID; }
            set { SetField(ref _sharingUID, value); }
        }

        
        
        [NoMappingToDatabase]
        public Guid SharingUIDAsGuid { get { return Guid.Parse(SharingUID); } }

        /// <summary>
        /// Use GetImportAs to access this
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="sharingUID"></param>
        /// <param name="localObject"></param>
        internal ObjectImport(ICatalogueRepository repository, string sharingUID,IMapsDirectlyToDatabaseTable localObject)
        {
            repository.InsertAndHydrate(this, new Dictionary<string, object>()
            {
                {"ReferencedObjectRepositoryType",localObject.Repository.GetType().Name},
                {"ReferencedObjectType",localObject.GetType().Name},
                {"ReferencedObjectID",localObject.ID},
                {"SharingUID",sharingUID}
                
            });

            if (ID == 0 || Repository != repository)
                throw new ArgumentException("Repository failed to properly hydrate this class");
        }
        public ObjectImport(IRepository repository, DbDataReader r)
            : base(repository, r)
        {
            SharingUID = r["SharingUID"].ToString();
        }

        public override string ToString()
        {
            return "I::" + ReferencedObjectType + "::" + SharingUID;
        }

        public bool IsImportedObject(IMapsDirectlyToDatabaseTable o)
        {
            return o.ID == ReferencedObjectID && o.GetType().Name.Equals(ReferencedObjectType) && o.Repository.GetType().Name.Equals(ReferencedObjectRepositoryType);
        }

        public bool LocalObjectStillExists(IRDMPPlatformRepositoryServiceLocator repositoryLocator)
        {
            return repositoryLocator.ArbitraryDatabaseObjectExists(ReferencedObjectRepositoryType, ReferencedObjectType, ReferencedObjectID);
        }

        public IMapsDirectlyToDatabaseTable GetLocalObject(IRDMPPlatformRepositoryServiceLocator repositoryLocator)
        {
            return repositoryLocator.GetArbitraryDatabaseObject(ReferencedObjectRepositoryType, ReferencedObjectType, ReferencedObjectID);
        }
    }
}
