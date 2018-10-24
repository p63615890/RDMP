using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CatalogueLibrary;
using CatalogueLibrary.Data;
using CatalogueLibrary.DataFlowPipeline;
using DataLoadEngine.Attachers;
using DataLoadEngine.Job;
using LoadModules.Generic.Exceptions;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.DatabaseHelpers.Discovery;
using ReusableLibraryCode.Progress;
using DataTable = System.Data.DataTable;


namespace LoadModules.Generic.Attachers
{
    /// <summary>
    /// Base class for an Attacher which expects to be passed a Filepath which is the location of a textual file in which values for a single DataTable are stored
    ///  (e.g. csv or fixed width etc).  This attacher requires that the RAW database server be setup and contain the correct tables for loading (it is likely that 
    /// the DataLoadEngine handles all this - as a user you dont need to worry about this).
    /// </summary>
    public abstract class FlatFileAttacher : Attacher, IPluginAttacher
    {

        [DemandsInitialization("The file to attach, e.g. \"*hic*.csv\" - this is NOT a Regex", Mandatory = true)]
        public string FilePattern { get; set; }


        [DemandsInitialization("The table name to load with data from the file (this will be the RAW version of the table)")]
        public TableInfo TableToLoad { get; set; }

        [DemandsInitialization("Alternative to `TableToLoad`, type table name in if you want to load a custom table e.g. one created by another load component (that doesn't exist in LIVE).  The table name should should not contain wrappers such as square brackets (e.g. \"My Table1\")")]
        public string TableName { get; set; }

        [DemandsInitialization("Determines the behaviour of the system when no files are matched by FilePattern.  If true the entire data load process immediately stops with exit code LoadNotRequired, if false then the load proceeds as normal (useful if for example if you have multiple Attachers and some files are optional)")]
        public bool SendLoadNotRequiredIfFileNotFound { get; set; }
        
        public FlatFileAttacher() : base(true)
        {
            
        }

        public override ExitCodeType Attach(IDataLoadJob job, GracefulCancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(TableName) && TableToLoad != null)
                TableName = TableToLoad.GetRuntimeName(LoadBubble.Raw,job.Configuration.DatabaseNamer);

            if(TableName != null)
                TableName = TableName.Trim();

            Stopwatch timer = new Stopwatch();
            timer.Start();


            if(string.IsNullOrWhiteSpace(TableName))
                throw new ArgumentNullException("TableName has not been set, set it in the DataCatalogue");

            DiscoveredTable table = _dbInfo.ExpectTable(TableName);

            //table didnt exist!
            if (!table.Exists())
                if (!_dbInfo.DiscoverTables(false).Any())//maybe no tables existed
                    throw new FlatFileLoadException("Raw database had 0 tables we could load");
                else//no there are tables just not the one we were looking for
                    throw new FlatFileLoadException("RAW database did not have a table called:" + TableName);

            
            //load the flat file
            var filepattern = FilePattern ?? "*";

            var filesToLoad = HICProjectDirectory.ForLoading.EnumerateFiles(filepattern).ToList();

            if (!filesToLoad.Any())
            {
                job.OnNotify(this,new NotifyEventArgs(ProgressEventType.Warning,  "Did not find any files matching pattern " + filepattern + " in forLoading directory"));
                
                if(SendLoadNotRequiredIfFileNotFound)
                    return ExitCodeType.OperationNotRequired;

                return ExitCodeType.Success;
            }

            foreach (var fileToLoad in filesToLoad)
                LoadFile(table, fileToLoad, _dbInfo, timer, job);

            timer.Stop();

            return ExitCodeType.Success;
        }

        private void LoadFile(DiscoveredTable tableToLoad, FileInfo fileToLoad, DiscoveredDatabase dbInfo, Stopwatch timer, IDataLoadJob job)
        {
            using (var con = dbInfo.Server.GetConnection())
            {
                DataTable dt = tableToLoad.GetDataTable(0);

                using (var insert = tableToLoad.BeginBulkInsert())
                {
                    // setup bulk insert it into destination
                    insert.Timeout = 500000;

                    //bulk insert ito destination
                    job.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "About to open file " + fileToLoad.FullName));
                    OpenFile(fileToLoad,job);

                    //confirm the validity of the headers
                    ConfirmFlatFileHeadersAgainstDataTable(dt,job);

                    con.Open();

                    //now we will read data out of the file in batches
                    int batchNumber = 1;
                    int maxBatchSize = 10000;
                    int recordsCreatedSoFar = 0;
                
                    try
                    {
                        //while there is data to be loaded into table 
                        while (IterativelyBatchLoadDataIntoDataTable(dt, maxBatchSize) != 0)
                        {
                            DropEmptyColumns(dt);
                            ConfirmFitToDestination(dt, tableToLoad, job);
                            try
                            {
                                recordsCreatedSoFar += insert.Upload(dt); 
                                
                                dt.Rows.Clear(); //very important otherwise we add more to the end of the table but still insert last batches records resulting in exponentially multiplying upload sizes of duplicate records!

                                job.OnProgress(this,
                                    new ProgressEventArgs(dbInfo.GetRuntimeName(),
                                        new ProgressMeasurement(recordsCreatedSoFar, ProgressType.Records), timer.Elapsed));
                            }
                            catch (Exception e)
                            {
                                throw new Exception("Error processing batch number " + batchNumber + " (of batch size " + maxBatchSize+")",e);
                            } 
                        }
                    }
                    catch (Exception e)
                    {
                        throw new FlatFileLoadException("Error processing file " + fileToLoad, e);
                    }
                    finally
                    {
                        CloseFile();
                    }
                }
            }
        }

        protected abstract void OpenFile(FileInfo fileToLoad,IDataLoadEventListener listener);
        protected abstract void CloseFile();
        
        public override void Check(ICheckNotifier notifier)
        {
            if (string.IsNullOrWhiteSpace(TableName) && TableToLoad == null)
                notifier.OnCheckPerformed(new CheckEventArgs("Either argument TableName or TableToLoad must be set " + this + ", you should specify this value." ,CheckResult.Fail));

            if (string.IsNullOrWhiteSpace(FilePattern))
                notifier.OnCheckPerformed(new CheckEventArgs("Argument FilePattern has not been set on " + this + ", you should specify this value in the LoadMetadataUI", CheckResult.Fail));

            if (!string.IsNullOrWhiteSpace(TableName) && TableToLoad != null)
                notifier.OnCheckPerformed(new CheckEventArgs("You should only specify argument TableName or TableToLoad, not both", CheckResult.Fail));
        }
        
        private void ConfirmFitToDestination(DataTable dt, DiscoveredTable tableToLoad,IDataLoadJob job)
        {

            var columnsAtDestination = tableToLoad.DiscoverColumns().Select(c=>c.GetRuntimeName()).ToArray();

            //see if there is a shape problem between stuff that is on the server and stuff that is in the flat file
            if (dt.Columns.Count != columnsAtDestination.Length)
                job.OnNotify(this,new NotifyEventArgs(ProgressEventType.Warning,"There was a mismatch between the number of columns in the flat file (" +
                    columnsAtDestination.Aggregate((s, n) => s + Environment.NewLine + n) +
                    ") and the number of columns in the RAW database table (" + dt.Columns.Count + ")"));
            
            foreach (DataColumn column in dt.Columns)
                if (!columnsAtDestination.Contains(column.ColumnName,StringComparer.CurrentCultureIgnoreCase))
                    throw new FlatFileLoadException("Column in flat file called " + column.ColumnName +
                                                    " does not appear in the RAW database table (after fixing potentially silly names)");

       }


        /// <summary>
        /// DataTable dt is a copy of what is in RAW, your job (if you choose to accept it) is to look in your file and work out what headers you can see
        /// and then complain to job (or throw) if what you see in the file does not match the RAW target
        /// </summary>
        protected abstract void ConfirmFlatFileHeadersAgainstDataTable(DataTable loadTarget,IDataLoadJob job);


        /// <summary>
        /// 
        /// </summary>
        /// <param name="dt"></param>
        /// <param name="maxBatchSize"></param>
        /// <returns>return the number of rows read, if you return >0 then you will be called again to get more data (if during this second or subsequent call there is no more data to read from source, return 0)</returns>
        protected abstract int IterativelyBatchLoadDataIntoDataTable(DataTable dt, int maxBatchSize);
        

        private void DropEmptyColumns(DataTable dt)
        {
            Regex emptyColumnsSyntheticNames = new Regex("^Column[0-9]+$");

            //deal with any ending columns which have nothing but whitespace
            for (int i = dt.Columns.Count - 1; i >= 0; i--)
            {
                if (emptyColumnsSyntheticNames.IsMatch(dt.Columns[i].ColumnName) || string.IsNullOrWhiteSpace(dt.Columns[i].ColumnName)) //is synthetic column or blank, nuke it
                {
                    bool foundValue = false;
                    foreach (DataRow dr in dt.Rows)
                    {
                        if (dr.ItemArray[i] == null)
                            continue;

                        if (string.IsNullOrWhiteSpace(dr.ItemArray[i].ToString()))
                            continue;

                        foundValue = true;
                        break;
                    }
                    if (!foundValue)
                        dt.Columns.Remove(dt.Columns[i]);
                }
            }
        }
        
        protected virtual object HackValueReadFromFile(string s)
        {
            
            return s;
        }

        public override void LoadCompletedSoDispose(ExitCodeType exitCode,IDataLoadEventListener postLoadEventListener)
        {
            
        }
    }
}
