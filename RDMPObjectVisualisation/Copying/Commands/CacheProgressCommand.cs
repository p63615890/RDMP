﻿using CatalogueLibrary.Data.Cache;
using ReusableLibraryCode.CommandExecution;

namespace RDMPObjectVisualisation.Copying.Commands
{
    public class CacheProgressCommand : ICommand
    {
        public CacheProgress CacheProgress { get; private set; }

        public CacheProgressCommand(CacheProgress cacheProgress)
        {
            CacheProgress = cacheProgress;
        }

        public string GetSqlString()
        {
            return null;
        }
    }
}