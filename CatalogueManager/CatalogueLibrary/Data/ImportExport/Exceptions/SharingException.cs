﻿using System;

namespace CatalogueLibrary.Data.ImportExport.Exceptions
{
    /// <summary>
    /// Thrown when there are problems importing or exporting objects from RDMP (either in gathering dependencies or loading <see cref="CatalogueLibrary.Data.Serialization.ShareDefinition"/>
    /// </summary>
    public class SharingException:Exception
    {
        /// <inheritdoc/>
        public SharingException(string msg) : base(msg)
        {
            
        }

        /// <inheritdoc/>
        public SharingException(string msg, Exception ex):base(msg,ex)
        {
            
        }
    }
}
