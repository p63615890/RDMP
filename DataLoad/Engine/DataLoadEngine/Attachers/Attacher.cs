// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using CatalogueLibrary;
using CatalogueLibrary.DataFlowPipeline;
using DataLoadEngine.Job;
using FAnsi.Discovery;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.Progress;

namespace DataLoadEngine.Attachers
{
    /// <summary>
    /// A Class which will run during Data Load Engine execution and result in the creation or population of a RAW database, the database may or not require 
    /// to already exist (e.g. MDFAttacher would expect it not to exist but AnySeparatorFileAttacher would require the tables/databases already exist).
    /// </summary>
    public abstract class Attacher : IAttacher
    {
        protected DiscoveredDatabase _dbInfo;

        public abstract ExitCodeType Attach(IDataLoadJob job, GracefulCancellationToken cancellationToken);

        public IHICProjectDirectory HICProjectDirectory { get; set; }
        
        public bool RequestsExternalDatabaseCreation { get; private set; }

        public virtual void Initialize(IHICProjectDirectory hicProjectDirectory, DiscoveredDatabase dbInfo)
        {
            HICProjectDirectory = hicProjectDirectory;
            _dbInfo = dbInfo;
        }
        
        protected Attacher(bool requestsExternalDatabaseCreation)
        {
            RequestsExternalDatabaseCreation = requestsExternalDatabaseCreation;
        }

        public abstract void Check(ICheckNotifier notifier);

        
        public abstract void LoadCompletedSoDispose(ExitCodeType exitCode,IDataLoadEventListener postLoadEventListener);
    }
}