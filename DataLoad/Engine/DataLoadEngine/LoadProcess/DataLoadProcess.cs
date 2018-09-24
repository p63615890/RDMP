using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading.Tasks;
using CatalogueLibrary;
using CatalogueLibrary.Data.DataLoad;
using CatalogueLibrary.DataFlowPipeline;
using CatalogueLibrary.Repositories;
using DataLoadEngine.DataProvider;
using DataLoadEngine.Job;
using DataLoadEngine.LoadExecution;
using DataLoadEngine.LoadExecution.Delegates;
using HIC.Logging;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.Progress;

namespace DataLoadEngine.LoadProcess
{
    /// <summary>
    /// Container class for an IDataLoadExecution.  This class records the ILoadMetadata that is being executed and the current state (whether it has crashed etc).
    /// When you call run then an IDataLoadJob will be generated by the JobProvider will be executed by the LoadExecution (See IDataLoadExecution).
    /// </summary>
    public class DataLoadProcess : IDataLoadProcess, IDataLoadOperation
    {
        /// <summary>
        /// Provides jobs for the data load process, allows different strategies for what jobs will be loaded e.g. single job, scheduled
        /// </summary>
        public IJobFactory JobProvider { get; set; }

        /// <summary>
        /// The load execution that will be used to load the jobs provided by the JobProvider
        /// </summary>
        public IDataLoadExecution LoadExecution { get; private set; }

        public ExitCodeType? ExitCode { get; private set; }
        public Exception Exception { get; private set; }

        private readonly IRDMPPlatformRepositoryServiceLocator _repositoryLocator;
        protected readonly ILoadMetadata LoadMetadata;
        protected readonly IDataLoadEventListener DataLoadEventListener;
        protected readonly ILogManager LogManager;

        private readonly ICheckable _preExecutionChecker;
        
        public DataLoadProcess(IRDMPPlatformRepositoryServiceLocator repositoryLocator,ILoadMetadata loadMetadata, ICheckable preExecutionChecker, ILogManager logManager, IDataLoadEventListener dataLoadEventListener, IDataLoadExecution loadExecution)
        {
            _repositoryLocator = repositoryLocator;
            LoadMetadata = loadMetadata;
            DataLoadEventListener = dataLoadEventListener;
            LoadExecution = loadExecution;
            _preExecutionChecker = preExecutionChecker;
            LogManager = logManager;
            ExitCode = ExitCodeType.Success;

            JobProvider = new JobFactory(loadMetadata,logManager);
        }

        public virtual ExitCodeType Run(GracefulCancellationToken loadCancellationToken, object payload = null)
        {
            PerformPreExecutionChecks();

            // create job
            var job = JobProvider.Create(_repositoryLocator,DataLoadEventListener);

            // if job is null, there are no more jobs to submit
            if (job == null)
                return ExitCodeType.OperationNotRequired;

            job.Payload = payload;

            return LoadExecution.Run(job, loadCancellationToken);
        }

       private void PerformPreExecutionChecks()
        {
            try
            {
                DataLoadEventListener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "Performing pre-execution checks"));
                var thrower = new ThrowImmediatelyCheckNotifier(){WriteToConsole = false};
                _preExecutionChecker.Check(thrower);
            }
            catch (Exception e)
            {
                Exception = e;
                ExitCode = ExitCodeType.Error;
            }
        }
    }
}