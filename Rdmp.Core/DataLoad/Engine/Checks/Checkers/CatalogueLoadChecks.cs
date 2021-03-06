// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using FAnsi.Discovery;
using Rdmp.Core.Curation;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.DataLoad;
using Rdmp.Core.DataLoad.Engine.DatabaseManagement.EntityNaming;
using Rdmp.Core.DataLoad.Engine.LoadProcess;
using Rdmp.Core.DataLoad.Engine.Migration;
using Rdmp.Core.DataLoad.Triggers;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.DataAccess;

namespace Rdmp.Core.DataLoad.Engine.Checks.Checkers
{
    class CatalogueLoadChecks:ICheckable
    {
        private readonly ILoadMetadata _loadMetadata;
        private readonly HICLoadConfigurationFlags _loadConfigurationFlags;
        private readonly HICDatabaseConfiguration _databaseConfiguration;

        public CatalogueLoadChecks(ILoadMetadata loadMetadata, HICLoadConfigurationFlags loadConfigurationFlags, HICDatabaseConfiguration databaseConfiguration)
        {
            _loadMetadata = loadMetadata;
            _loadConfigurationFlags = loadConfigurationFlags;
            _databaseConfiguration = databaseConfiguration;
        }

        public void Check(ICheckNotifier notifier)
        {
            Catalogue[] catalogueMetadatas = null;

            try
            {
                //check there are catalogues and we can retrieve them
                catalogueMetadatas = _loadMetadata.GetAllCatalogues().Cast<Catalogue>().ToArray();
            }
            catch (Exception e)
            {
                notifier.OnCheckPerformed(new CheckEventArgs( "Crashed trying to fetch Catalogues for metadata",
                    CheckResult.Fail, e));
                return;
            }

            //there no catalogues
            if (catalogueMetadatas.Length == 0)
                notifier.OnCheckPerformed(new CheckEventArgs( "There are no Catalogues associated with this metadata",
                    CheckResult.Fail, null));

            List<ITableInfo> tablesFound  = new List<ITableInfo>();

            //check each catalogue is sufficiently configured to perform a migration
            foreach (Catalogue catalogue in catalogueMetadatas)
            {
                notifier.OnCheckPerformed(new CheckEventArgs( "Found Catalogue:" + catalogue, CheckResult.Success, null));

                ITableInfo[] tableInfos = catalogue.GetTableInfoList(true).Distinct().ToArray();

                if (tableInfos.Length == 0)
                    notifier.OnCheckPerformed(new CheckEventArgs(
                        "Catalogue " + catalogue.Name + " does not have any TableInfos", CheckResult.Fail, null));

                tablesFound.AddRange(tableInfos.Where(tableInfo => !tablesFound.Contains(tableInfo)));
            }

            
            //check regular tables
            foreach (TableInfo regularTable in tablesFound)
            {
                notifier.OnCheckPerformed(new CheckEventArgs("About To check configuration of TableInfo:" + regularTable, CheckResult.Success, null));
                CheckTableInfo(regularTable, notifier);

                //check anonymisation
                AnonymisationChecks anonymisationChecks = new AnonymisationChecks(regularTable);
                anonymisationChecks.Check(notifier);
            }
        }


        #region These methods check TableInfos (Lookup including lookups)
  
        private void CheckTableInfo(TableInfo tableInfo, ICheckNotifier notifier)
        {
            //get all columns
            ColumnInfo[] columnInfos;
            ColumnInfo[] columnInfosWhichArePrimaryKeys;

            //check whether the live database and staging databases have appropriate columns and triggers etc on them
            DiscoveredDatabase staging = _databaseConfiguration.DeployInfo[LoadBubble.Staging];
            DiscoveredDatabase live = DataAccessPortal.GetInstance().ExpectDatabase(tableInfo, DataAccessContext.DataLoad);

            var liveTable = live.ExpectTable(tableInfo.GetRuntimeName(LoadBubble.Live,_databaseConfiguration.DatabaseNamer),tableInfo.Schema);
            var liveCols = liveTable.DiscoverColumns();

            CheckTableInfoSynchronization(tableInfo,notifier);

            CheckTableHasColumnInfosAndPrimaryKeys(live, tableInfo, out columnInfos, out columnInfosWhichArePrimaryKeys,notifier);
            
            try
            {

                //if trigger is created as part of this check then it is likely to have resulted in changes to the underlying table (e.g. added hic_validFrom field) in which case we should resynch the TableInfo to pickup these new columns
                bool runSynchronizationAgain;
                CheckTriggerIntact(liveTable,notifier,out runSynchronizationAgain);

                if(runSynchronizationAgain)
                    CheckTableInfoSynchronization(tableInfo, notifier);
                
                if (!_databaseConfiguration.RequiresStagingTableCreation)
                {
                    //Important:
                    //Regarding this if block: None of the current loading systems in RDMP have RequiresStagingTableCreation as false but in theory you could build some kind of multi threaded horror
                    //which had multiple simultaneous loads all populating a single STAGING bubble therefore this code is left in the codebase, it probably works ok but you will need a
                    //fair bit of work if you want to realise such a monstrosity (for one cleanup on each run probably cleans up STAGING on exit)

                    //live can have additional hic_ columns which do not appear in staging (lookups cannot)
                    var stagingTable = staging.ExpectTable(tableInfo.GetRuntimeName(LoadBubble.Staging, _databaseConfiguration.DatabaseNamer));

                    if (!stagingTable.Exists())
                        notifier.OnCheckPerformed(new CheckEventArgs("RequiresStagingTableCreation is false but staging does not exist, this flag should indicate that you anticipate STAGING to be already setup before you kick off the load", CheckResult.Fail));

                    var stagingCols = stagingTable.DiscoverColumns();

                    ConfirmStagingAndLiveHaveSameColumns(tableInfo.GetRuntimeName(), stagingCols, liveCols, false,notifier);

                    CheckStagingToLiveMigrationForTable(stagingTable, stagingCols, liveTable, liveCols,notifier);
                }
            }
            catch (Exception e)
            {
                notifier.OnCheckPerformed(new CheckEventArgs("Load Checks Crashed " + tableInfo, CheckResult.Fail, e));
            }
        }

        private void CheckTableInfoSynchronization(TableInfo tableInfo, ICheckNotifier notifier)
        {
            //live is the current data load's (possilby overridden server/database)
            var tableInfoSynchronizer = new TableInfoSynchronizer(tableInfo);

            string problemList = "";

            //synchronize but refuse to apply all fixes, problems are instead added to problemList
            if (tableInfoSynchronizer.Synchronize(notifier))
            {
                notifier.OnCheckPerformed(new CheckEventArgs("TableInfo " + tableInfo + " passed Synchronization check", CheckResult.Success, null)); //passed synchronization
            }
            else
            {
                bool launchSyncFixer = notifier.OnCheckPerformed(new CheckEventArgs(
                    "TableInfo " + tableInfo + " failed Synchronization check with following problems:" + problemList, CheckResult.Fail,
                    null,"Launch Synchronization Fixing")); //failed syncronization

                if (launchSyncFixer)
                {
                    bool userFixed;

                    //if silent running accept all changes
                    userFixed = tableInfoSynchronizer.Synchronize(notifier);

                    if(!userFixed)
                        notifier.OnCheckPerformed(new CheckEventArgs("TableInfo " + tableInfo + " still failed Synchronization check", CheckResult.Fail, null)); //passed synchronization
                }
            }
        }

        private void CheckTableHasColumnInfosAndPrimaryKeys(DiscoveredDatabase live, TableInfo tableInfo, out ColumnInfo[] columnInfos, out ColumnInfo[] columnInfosWhichArePrimaryKeys, ICheckNotifier notifier)
        {
            columnInfos = tableInfo.ColumnInfos.ToArray();
            columnInfosWhichArePrimaryKeys = columnInfos.Where(col => col.IsPrimaryKey).ToArray();


            //confirm there are at least 1
            if (!columnInfos.Any())
                notifier.OnCheckPerformed(new CheckEventArgs( "TableInfo " + tableInfo.Name + " has no columninfos",
                    CheckResult.Fail, null));


            if (!columnInfosWhichArePrimaryKeys.Any())
                notifier.OnCheckPerformed(new CheckEventArgs(
                    "TableInfo " + tableInfo.Name + " has no IsPrimaryKey columns", CheckResult.Fail, null));

            var primaryKeys = live.ExpectTable(tableInfo.GetRuntimeName(),tableInfo.Schema).DiscoverColumns().Where(c => c.IsPrimaryKey).ToArray();

            if(primaryKeys.Any(k=>k.IsAutoIncrement))
                notifier.OnCheckPerformed(new CheckEventArgs("AutoIncrement columns "+string.Join(",",primaryKeys.Where(k=>k.IsAutoIncrement))+" are not allowed as Primary Keys for your table because there is no way to differentiate new data being loaded from duplicate old data being loaded (the entire purpose of the RDMP DLE)",CheckResult.Fail));

            //confirm primary keys match underlying table
            //sort pks alphabetically and confirm they match the underlying live system table
            string[] actualPks = primaryKeys.Select(c=>c.GetRuntimeName()).OrderBy(s => s).ToArray();
            string[] pksWeExpect = columnInfosWhichArePrimaryKeys.Select(c => c.GetRuntimeName()).OrderBy(s => s).ToArray();


            if (actualPks.Length != pksWeExpect.Length)
                notifier.OnCheckPerformed(new CheckEventArgs(
                    "Primary keys in Catalogue for database table " + tableInfo.GetRuntimeName() +
                    " does not match Catalogue entry (difference in number of keys)", CheckResult.Fail, null));
            else
                for (int i = 0; i < pksWeExpect.Length; i++)
                    if (!pksWeExpect[i].Equals(actualPks[i]))
                        notifier.OnCheckPerformed(new CheckEventArgs(
                            "Mismatch between primary key defined in Catalogue " + pksWeExpect[i] + " and one found in live table " + tableInfo.GetRuntimeName(), CheckResult.Fail, null));
                    else
                        notifier.OnCheckPerformed(new CheckEventArgs(
                            "Found primary key " + pksWeExpect[i] + " in LIVE table " + tableInfo.GetRuntimeName(), CheckResult.Success, null));


        }

        private void CheckTriggerIntact(DiscoveredTable table, ICheckNotifier notifier, out bool runSynchronizationAgain)
        {
            TriggerChecks checker = new TriggerChecks(table);
            checker.Check(notifier);

            runSynchronizationAgain = checker.TriggerCreated;
        }


        private void ConfirmStagingAndLiveHaveSameColumns(string tableName, DiscoveredColumn[] stagingCols, DiscoveredColumn[] liveCols, bool requireSameNumberAndOrder, ICheckNotifier notifier)
        {
            //in LIVE but not STAGING
            foreach (var missingColumn in liveCols.Select(c=>c.GetRuntimeName()).Except(stagingCols.Select(c=>c.GetRuntimeName())))
                //column is in live but not in staging, but it is hic_
                if (SpecialFieldNames.IsHicPrefixed(missingColumn)) //this is permitted
                    continue;
                else
                    notifier.OnCheckPerformed(new CheckEventArgs(
                        "Column " + missingColumn + " is missing from STAGING", CheckResult.Fail, null));

            //in STAGING but not LIVE
            foreach (var missingColumn in stagingCols.Except(liveCols))
                notifier.OnCheckPerformed(new CheckEventArgs(
                    "Column " + missingColumn + " is in STAGING but not LIVE", CheckResult.Fail, null));



            if (requireSameNumberAndOrder)
            {
                bool passedColumnOrderCheck = true;

                if (stagingCols.Length != liveCols.Length)
                {
                    notifier.OnCheckPerformed(new CheckEventArgs(
                        "Column count mismatch between staging and live in table " + tableName, CheckResult.Fail, null));
                    passedColumnOrderCheck = false;
                }
                else
                    //check they are in the same order
                    for (int i = 0; i < stagingCols.Length; i++)
                        if (!stagingCols[i].Equals(liveCols[i]))
                        {
                            notifier.OnCheckPerformed(new CheckEventArgs(
                                "Column name/order mismatch between staging and live in table " + tableName +
                                ", column " + i + " is " + stagingCols[i] + " in staging but is " + liveCols[i] +
                                " in live.", CheckResult.Fail, null));
                            passedColumnOrderCheck = false;
                            break;
                        }

                if (passedColumnOrderCheck)
                    notifier.OnCheckPerformed(new CheckEventArgs( "Column order match confirmed between staging and live on table " + tableName, CheckResult.Success, null));
            }
        }

        private void CheckStagingToLiveMigrationForTable(DiscoveredTable stagingTable, DiscoveredColumn[] stagingCols, DiscoveredTable liveTable, DiscoveredColumn[] liveCols, ICheckNotifier notifier)
        {
            try
            {
                new MigrationColumnSet(stagingTable,liveTable,new StagingToLiveMigrationFieldProcessor());
                notifier.OnCheckPerformed(new CheckEventArgs("TableInfo " + liveTable + " passed " + typeof(MigrationColumnSet).Name + " check ", CheckResult.Success, null));
            }
            catch (Exception e)
            {

                notifier.OnCheckPerformed(new CheckEventArgs(
                    typeof(MigrationColumnSet).Name + " reports a problem with the configuration of columns on STAGING/LIVE or in the ColumnInfos for TableInfo " + liveTable, CheckResult.Fail, e));
            }
            
            //live columns 
            foreach (DiscoveredColumn col in liveCols)
                if (!SpecialFieldNames.IsHicPrefixed(col) && col.IsAutoIncrement)    //must start hic_ if they are identities
                    notifier.OnCheckPerformed(new CheckEventArgs("Column " + col + " is an identity column in the LIVE database but does not start with hic_", CheckResult.Fail, null));//this one does not

            //staging columns
            foreach (DiscoveredColumn col in stagingCols) //staging columns
                if (col.IsAutoIncrement) //if there are any auto increments
                    notifier.OnCheckPerformed(new CheckEventArgs(
                        "Column " + col + " is an identity column and is in STAGING, the identity flag must be removed from the STAGING table", CheckResult.Fail, null));//complain since don't want a mismatch between IDs in staging and live or complaints about identity insert from SQL server

            //staging must allow null dataloadrunids and validfroms
            ConfirmNullability(stagingTable.DiscoverColumn(SpecialFieldNames.DataLoadRunID), true, notifier);
            ConfirmNullability(stagingTable.DiscoverColumn(SpecialFieldNames.ValidFrom), true, notifier);

            //live must allow nulls in validFrom
            ConfirmNullability(liveTable.DiscoverColumn(SpecialFieldNames.ValidFrom), true, notifier);
        }

        private void ConfirmNullability(DiscoveredColumn column, bool expectedNullability, ICheckNotifier notifier)
        {
            bool nullability = column.AllowNulls;
            notifier.OnCheckPerformed(new CheckEventArgs("Nullability of " + column + " is AllowNulls=" + nullability + ", (expected " + expectedNullability + ")", nullability == expectedNullability ? CheckResult.Success : CheckResult.Fail, null));
        }
        #endregion
    }
}
