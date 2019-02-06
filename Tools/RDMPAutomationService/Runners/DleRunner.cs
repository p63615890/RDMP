// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Linq;
using CatalogueLibrary;
using CatalogueLibrary.Data;
using CatalogueLibrary.Data.DataLoad;
using CatalogueLibrary.DataFlowPipeline;
using CatalogueLibrary.Repositories;
using DataLoadEngine.Checks;
using DataLoadEngine.DatabaseManagement.EntityNaming;
using DataLoadEngine.Job.Scheduling;
using DataLoadEngine.LoadExecution;
using DataLoadEngine.LoadProcess;
using DataLoadEngine.LoadProcess.Scheduling;
using DataLoadEngine.LoadProcess.Scheduling.Strategy;
using HIC.Logging;
using RDMPAutomationService.Options;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.Progress;

namespace RDMPAutomationService.Runners
{
    /// <summary>
    /// <see cref="IRunner"/> for the Data Load Engine.  Supports both check and execute commands.
    /// </summary>
    public class DleRunner:IRunner
    {
        private readonly DleOptions _options;

        public DleRunner(DleOptions options)
        {
            _options = options;
        }
        
        public int Run(IRDMPPlatformRepositoryServiceLocator locator, IDataLoadEventListener listener, ICheckNotifier checkNotifier,GracefulCancellationToken token)
        {
            ILoadProgress loadProgress = locator.CatalogueRepository.GetObjectByID<LoadProgress>(_options.LoadProgress);
            ILoadMetadata loadMetadata = locator.CatalogueRepository.GetObjectByID<LoadMetadata>(_options.LoadMetadata);

            if (loadMetadata == null && loadProgress != null)
                    loadMetadata = loadProgress.LoadMetadata;
                
            if(loadMetadata == null)
                throw new ArgumentException("No Load Metadata specified");
            
            if(loadProgress != null && loadProgress.LoadMetadata_ID != loadMetadata.ID)
                throw new ArgumentException("The supplied LoadProgress does not belong to the supplied LoadMetadata load");
            
            var databaseConfiguration = new HICDatabaseConfiguration(loadMetadata);
            var flags = new HICLoadConfigurationFlags();
            
            flags.ArchiveData = !_options.DoNotArchiveData;
            flags.DoLoadToStaging = !_options.StopAfterRAW;
            flags.DoMigrateFromStagingToLive = !_options.StopAfterSTAGING;

            var checkable = new CheckEntireDataLoadProcess(loadMetadata, databaseConfiguration, flags, locator.CatalogueRepository.MEF); 

            switch (_options.Command)
            {
                case CommandLineActivity.run:
                    
                    var loggingServer = loadMetadata.GetDistinctLoggingDatabase();
                    var logManager = new LogManager(loggingServer);
                    
                    // Create the pipeline to pass into the DataLoadProcess object
                    var dataLoadFactory = new HICDataLoadFactory(loadMetadata, databaseConfiguration,flags,locator.CatalogueRepository, logManager);

                    IDataLoadExecution execution = dataLoadFactory.Create(listener);
                    IDataLoadProcess dataLoadProcess;

                    if (loadMetadata.LoadProgresses.Any())
                    {
                        //Then the load is designed to run X days of source data at a time
                        //Load Progress
                        ILoadProgressSelectionStrategy whichLoadProgress = loadProgress != null ? (ILoadProgressSelectionStrategy) new SingleLoadProgressSelectionStrategy(loadProgress) : new AnyAvailableLoadProgressSelectionStrategy(loadMetadata);

                        var jobDateFactory = new JobDateGenerationStrategyFactory(whichLoadProgress);
                    
                        dataLoadProcess = _options.Iterative
                            ? (IDataLoadProcess)new IterativeScheduledDataLoadProcess(locator, loadMetadata, checkable, execution, jobDateFactory, whichLoadProgress, _options.DaysToLoad, logManager, listener, databaseConfiguration) :
                                new SingleJobScheduledDataLoadProcess(locator, loadMetadata, checkable, execution, jobDateFactory, whichLoadProgress, _options.DaysToLoad, logManager, listener, databaseConfiguration);
                    }
                    else
                        //OnDemand
                        dataLoadProcess = new DataLoadProcess(locator, loadMetadata, checkable, logManager, listener, execution, databaseConfiguration);

                    var exitCode = dataLoadProcess.Run(token);
            
                    //return 0 for success or load not required otherwise return the exit code (which will be non zero so error)
                    return exitCode == ExitCodeType.Success || exitCode == ExitCodeType.OperationNotRequired? 0: (int)exitCode;
                case CommandLineActivity.check:
                    
                    checkable.Check(checkNotifier);
                    
                    return 0;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
