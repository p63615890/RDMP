using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using CatalogueLibrary.Data;
using CatalogueLibrary.Data.Aggregation;
using CatalogueLibrary.DataHelper;
using CatalogueLibrary.QueryBuilding.Parameters;
using MapsDirectlyToDatabaseTable;

using ReusableLibraryCode;
using ReusableLibraryCode.DatabaseHelpers.Discovery;
using ReusableLibraryCode.DatabaseHelpers.Discovery.QuerySyntax;
using ReusableLibraryCode.DatabaseHelpers.Discovery.QuerySyntax.Aggregation;

namespace CatalogueLibrary.QueryBuilding
{
    /// <summary>
    /// Assembles GROUP BY queries based on the specified IColumns, IContainers, IFilters etc.  There are three main types of query AggregateBuilder can create
    /// 
    /// <para>1. Basic GROUP BY e.g. Select chi,count(chi) from prescribing where LEN(chi) = 10 group by chi HAVING count(chi)> 10</para>
    /// 
    /// <para>2. Calendar Table GROUP BY.  This supports all of the features of 1. but also starts by using dynamic SQL to create a date range table to which the query is
    /// automatically Joined.  This means that you will not get gaps in days where there is no data.  To create one of these you must set AggregateContinuousDateAxis</para>
    /// 
    /// <para>3. Dynamic PIVOT GROUP BY.  This supports all of the features of 2. (it must have an axis) but also generates a dynamic PIVOT column for each unique value found
    /// in the AggregateConfiguration.PivotDimension.  This is (normally) done by running a pre query which includes all the IFilters and IContainers etc so to return the
    /// unique values that will appear in the final query only.  Then the final query is run with a PIVOT command over the column values found.  Since data can be a mile 
    /// wide and full of punctuation etc there is an adjustment operation on the values to qualify them as valid column names.</para>
    /// 
    /// <para>AggregateBuilder is cross database compatible.  This is achieved by assembling all the lines it thinks it needs for it's query and then passing off the exact 
    /// implementation into IAggregateHelper.BuildAggregate.  The implementation of the calendars/dynamic pivots vary wildly by database engine (See MySqlAggregateHelper vs
    /// MicrosoftSQLAggregateHelper).  </para>
    /// 
    /// <para>All IAggregateHelper.BuildAggregate implementations must produce the same result tables for the same column/axis/pivot settings.  This is rigidly enforced by 
    /// AggregateDataBasedTests </para>
    /// 
    /// <para>IMPORTANT: AggregateBuilder also powers the cohort identification system (See CohortQueryBuilderHelper) in which case the AggregateConfiguration will have
    /// only a single AggregateDimension (which must be the patient identifier column).</para>
    /// </summary>
    public class AggregateBuilder : ISqlQueryBuilder
    {
        private readonly TableInfo[] _forceJoinsToTheseTables;

        /// <inheritdoc/>
        public string SQL { get
        {
            if (SQLOutOfDate)
                RegenerateSQL();

            return _sql;
        } }

        /// <inheritdoc/>
        public string LimitationSQL { get; private set; }
        
        
        /// <summary>
        /// Text to add as an SQL comment before the SELECT section of the query e.g. "bob" would result in the text /*bob*/ appearing at the top of the SELECT
        /// </summary>
        public string LabelWithComment { get; set; }


        private AggregateCountColumn _countColumn;
        
        QueryTimeColumn _pivotDimension = null;
        AggregateContinuousDateAxis _axis = null;
        AggregateDimension _axisAppliesToDimension = null;
        bool _isCohortIdentificationAggregate;

        /// <summary>
        /// Optional, SQL to apply a HAVING clause to the GROUP BY query generated
        /// 
        /// <para>Do not include the word HAVING in the text since it will automatically be added</para>
        /// </summary>
        public string HavingSQL { get; set; }

        /// <summary>
        /// Optional, Limit the results returned.  Depending on the mode the 
        /// </summary>
        public IAggregateTopX AggregateTopX { get; set; }

        public List<QueryTimeColumn> SelectColumns { get; private set; }
        public List<TableInfo> TablesUsedInQuery { get; private set; }

        public List<JoinInfo> JoinsUsedInQuery { get; private set; }

        public List<CustomLine> CustomLines { get; private set; }
        public CustomLine TopXCustomLine { get; set; }

        public CustomLine AddCustomLine(string text, QueryComponent positionToInsert)
        {
            SQLOutOfDate = true;
            return SqlQueryBuilderHelper.AddCustomLine(this,text, positionToInsert);
        }


        public List<IFilter> Filters { get; private set; }

        public IContainer RootFilterContainer { get; set; }
        public bool CheckSyntax { get; set; }
        public TableInfo PrimaryExtractionTable { get; private set; }
        public ParameterManager ParameterManager { get; private set; }

        string _sql;
        /// <inheritdoc/>
        public bool SQLOutOfDate { get; set; }

        public ICollectSqlParameters QueryLevelParameterProvider { get; set; }

        
        public bool DoNotWriteOutOrderBy { get; set; }

        public bool DoNotWriteOutParameters
        {
            get { return _doNotWriteOutParameters; }
            set
            {
                //no change
                if(value == _doNotWriteOutParameters)
                    return;

                _doNotWriteOutParameters = value;

                //if the user is telling us not to write out parameters we had better clear any knowledge we had of parameters from previous runs
                if(value)
                    ParameterManager.ClearNonGlobals();

                SQLOutOfDate = true;
            }
        }

        /// <summary>
        /// when adding columns you have the option of either including them in groupby (default) or omitting them from groupby.  If ommitted then the columns will be used to decide how to build the FROM statement (which tables to join etc) but not included in the SELECT and GROUP BY sections of the query
        /// </summary>
        private readonly List<IColumn> _skipGroupByForThese = new List<IColumn>();

        public AggregateBuilder(string limitationSQL, string countSQL,AggregateConfiguration aggregateConfigurationIfAny,TableInfo[] forceJoinsToTheseTables)
            : this(limitationSQL, countSQL, aggregateConfigurationIfAny)
        {
            _forceJoinsToTheseTables = forceJoinsToTheseTables;
        }


        public AggregateBuilder(string limitationSQL, string countSQL, AggregateConfiguration aggregateConfigurationIfAny)
        {
            if (limitationSQL != null && limitationSQL.Contains("top"))
                throw new Exception("Use AggregateTopX property instead of limitation SQL to acheive this");

            _aggregateConfigurationIfAny = aggregateConfigurationIfAny;
            LimitationSQL = limitationSQL;
            ParameterManager = new ParameterManager();
            CustomLines = new List<CustomLine>();
            SQLOutOfDate = true;

            SelectColumns = new List<QueryTimeColumn>();

            if(!string.IsNullOrWhiteSpace(countSQL))
            {
                _countColumn = new AggregateCountColumn(countSQL);
                _countColumn.Order = int.MaxValue;//order these last
                AddColumn(_countColumn);
            }

            LabelWithComment = aggregateConfigurationIfAny != null ? aggregateConfigurationIfAny.Name : "";

            QueryLevelParameterProvider = aggregateConfigurationIfAny;
            
            if (aggregateConfigurationIfAny != null)
            {
                HavingSQL = aggregateConfigurationIfAny.HavingSQL;
                AggregateTopX = aggregateConfigurationIfAny.GetTopXIfAny();
            }
        }


        public void AddColumn(IColumn col)
        {
            AddColumn(col,true);
        }

        /// <summary>
        /// Overload lets you include columns for the purposes of FROM creation but not have them also appear in GROUP BY sections
        /// </summary>
        /// <param name="col"></param>
        /// <param name="includeAsGroupBy"></param>
        public void AddColumn(IColumn col, bool includeAsGroupBy)
        {
            SelectColumns.Add(new QueryTimeColumn(col));

            if (!includeAsGroupBy)
                _skipGroupByForThese.Add(col);
        }

        public void AddColumnRange(IColumn[] columnsToAdd)
        {
            AddColumnRange(columnsToAdd, true);
        }


        public void AddColumnRange(IColumn[] columnsToAdd, bool includeAsGroupBy)
        {
            foreach (IColumn column in columnsToAdd)
            {
                SelectColumns.Add(new QueryTimeColumn(column));

                if (!includeAsGroupBy)
                    _skipGroupByForThese.Add(column);
            }
        }
        
        private int _pivotID=-1;
        private bool _doNotWriteOutParameters;
        private IQuerySyntaxHelper _syntaxHelper;
        private AggregateConfiguration _aggregateConfigurationIfAny;

        public void SetPivotToDimensionID(AggregateDimension pivot)
        {
            //ensure it has an alias
            if (string.IsNullOrWhiteSpace(pivot.Alias))
            {
                pivot.Alias = "MyPivot";
                pivot.SaveToDatabase();
            }

            _pivotID = pivot.ID;
        }

        /// <summary>
        /// Populates _sql (SQL property) and resolves all parameters, filters containers etc.  Basically Finalizes this query builder 
        /// </summary>
        public void RegenerateSQL()
        {
            SelectColumns.Sort();

            //things we discover below, set them all to default values again
            _pivotDimension = null;
            _axisAppliesToDimension = null;
            _axis = null;
            _isCohortIdentificationAggregate = false;

            ParameterManager.ClearNonGlobals();

            if(QueryLevelParameterProvider != null)
                ParameterManager.AddParametersFor(QueryLevelParameterProvider,ParameterLevel.QueryLevel);

            TableInfo primary;
            TablesUsedInQuery = SqlQueryBuilderHelper.GetTablesUsedInQuery(this, out primary);

            //get the database language syntax based on the tables used in the query 
            _syntaxHelper = SqlQueryBuilderHelper.GetSyntaxHelper(
                _forceJoinsToTheseTables != null ?
                TablesUsedInQuery.Union(_forceJoinsToTheseTables).ToList() : TablesUsedInQuery);


            //tell the count column what language it is
            if (_countColumn != null)
            {    
                _isCohortIdentificationAggregate = _aggregateConfigurationIfAny != null && _aggregateConfigurationIfAny.IsCohortIdentificationAggregate;

                //if it is not a cic aggregate then make sure it has an alias e.g. count(*) AS MyCount.  cic aggregates take extreme liberties with this field like passing in 'distinct chi' and '*' and other wacky stuff that is so not cool
                _countColumn.SetQuerySyntaxHelper(_syntaxHelper,!_isCohortIdentificationAggregate);
            }
            

            IAggregateHelper aggregateHelper = _syntaxHelper.AggregateHelper;
            
            if(_pivotID != -1)
                try
                {
                    _pivotDimension = SelectColumns.Single(
                        qtc => qtc.IColumn is AggregateDimension 
                               &&
                               ((AggregateDimension)qtc.IColumn).ID == _pivotID);
                }
                catch (Exception e)
                {
                    throw new QueryBuildingException("Problem occurred when trying to find PivotDimension ID " + _pivotID + " in SelectColumns list",e);
                }

            foreach (AggregateDimension dimension in SelectColumns.Select(c=>c.IColumn).Where(e=>e is AggregateDimension))
            {
                var availableAxis =dimension.AggregateContinuousDateAxis;

                if(availableAxis != null)
                    if (_axis != null)
                        throw new QueryBuildingException(
                            "Multiple dimensions have an AggregateContinuousDateAxis within the same configuration (Dimensions " + _axisAppliesToDimension.GetRuntimeName() + " and " + dimension.GetRuntimeName() + ")");
                    else
                    {
                        _axis = availableAxis;
                        _axisAppliesToDimension = dimension;
                    }
            }

            if (_pivotDimension != null)
                if (_pivotDimension.IColumn == _axisAppliesToDimension)
                    throw new QueryBuildingException("Column " + _pivotDimension.IColumn + " is both a PIVOT and has an AXIS configured on it, you cannot have both.");
            
            //work out all the filters 
            Filters = SqlQueryBuilderHelper.GetAllFiltersUsedInContainerTreeRecursively(RootFilterContainer);

             //tell the manager about them
            ParameterManager.AddParametersFor(Filters);

            if (AggregateTopX != null)
                SqlQueryBuilderHelper.HandleTopX(this, _syntaxHelper, AggregateTopX.TopX);
            else
                SqlQueryBuilderHelper.ClearTopX(this);

            //if user wants to force join to some other tables that don't appear in the SELECT list, who are we to stop him!
            if (_forceJoinsToTheseTables != null)
                foreach (TableInfo t in _forceJoinsToTheseTables)
                {
                    if (!TablesUsedInQuery.Contains(t))
                    {
                        TablesUsedInQuery.Add(t);
                        ParameterManager.AddParametersFor(t);
                    }
                
                    //if user has force joined to a primary extraction table
                    if(t.IsPrimaryExtractionTable)
                        if (primary == null) //we don't currently know the primary (i.e. none of the SELECT columns were from primary tables so use this table as primary)
                            primary = t;
                        else if (primary.ID == t.ID) //we know the primary already but it is the same table so thats fine
                            continue;
                        else
                            //this isn't fine
                            throw new QueryBuildingException("You chose to FORCE a join to table " + t + " which is marked IsPrimaryExtractionTable but you have also selected a column called " + primary + " which is also an IsPrimaryExtractionTable (cannot have 2 different primary extraction tables)");
                }

            this.PrimaryExtractionTable = primary;

            SqlQueryBuilderHelper.FindLookups(this);

            JoinsUsedInQuery = SqlQueryBuilderHelper.FindRequiredJoins(this);

            var queryLines = new List<CustomLine>();
            _sql = "";
            
            ValidateDimensions();

            //assuming we were not told to ignore the writing out of parameters!
            if (!DoNotWriteOutParameters)
                foreach (ISqlParameter parameter in ParameterManager.GetFinalResolvedParametersList())
                    queryLines.Add(new CustomLine(QueryBuilder.GetParameterDeclarationSQL(parameter),QueryComponent.VariableDeclaration));

            CompileCustomLinesInStageAndAddToList(QueryComponent.VariableDeclaration, queryLines);
            
            //put the name in as SQL comments followed by the SQL e.g. the name of an AggregateConfiguration or whatever
            GetSelectSQL(queryLines);
            
            queryLines.Add(new CustomLine(SqlQueryBuilderHelper.GetFROMSQL(this), QueryComponent.FROM));
            CompileCustomLinesInStageAndAddToList(QueryComponent.JoinInfoJoin, queryLines);
            
            queryLines.Add(new CustomLine(SqlQueryBuilderHelper.GetWHERESQL(this),QueryComponent.WHERE));

            CompileCustomLinesInStageAndAddToList(QueryComponent.WHERE,queryLines);
            
            GetGroupBySQL(queryLines,aggregateHelper);
            
            queryLines = queryLines.Where(l => !string.IsNullOrWhiteSpace(l.Text)).ToList();

            _sql = aggregateHelper.BuildAggregate(queryLines, _axis, _pivotDimension != null);
        }
        
        private void ValidateDimensions()
        {
            //axis but no pivot
            if(_axis != null && _pivotDimension == null && SelectColumns.Count !=2 )
                throw new QueryBuildingException("You must have two columns in an AggregateConfiguration that contains an axis.  These must be the axis column and the count/sum column.  Your query had " + SelectColumns.Count + " (" + string.Join(",", SelectColumns.Select(c => "'" + c.IColumn.ToString() + "'")) + ")");
            
            //axis and pivot
            if(_axis != null && _pivotDimension != null && SelectColumns.Count !=3 )
                throw new QueryBuildingException("You must have three columns in an AggregateConfiguration that contains a pivot.  These must be the axis column, the pivot column and the count/sum column.  Your query had " + SelectColumns.Count + " (" + string.Join(",", SelectColumns.Select(c => "'" + c.IColumn.ToString() + "'")) + ")");
        }
        
        private void CompileCustomLinesInStageAndAddToList( QueryComponent stage,List<CustomLine> list)
        {
            list.AddRange(SqlQueryBuilderHelper.GetCustomLinesSQLForStage(this, stage));
        }

        private void GetGroupBySQL(List<CustomLine> queryLines,IAggregateHelper aggregateHelper)
        {
            //now are there columns that...
            if (SelectColumns.Count(col =>
                !(col.IColumn is AggregateCountColumn)  //are not count(*) style columns
                &&
                !_skipGroupByForThese.Contains(col.IColumn)) > 0) //and are not being skipped for GROUP BY
            {

                //yes there are! better group by then!
                queryLines.Add(new CustomLine("group by ",QueryComponent.GroupBy));

                foreach (var col in SelectColumns)
                {
                    if (col.IColumn is AggregateCountColumn)
                        continue;

                    //was added with skip for group by enabled
                    if (_skipGroupByForThese.Contains(col.IColumn))
                        continue;

                    string select;
                    string alias;

                    _syntaxHelper.SplitLineIntoSelectSQLAndAlias(col.GetSelectSQL(null, null,_syntaxHelper), out select, out alias);

                    var line = new CustomLine(select + ",",QueryComponent.GroupBy);

                    FlagLineBasedOnIcolumn(line,col.IColumn);

                    queryLines.Add(line);

                }

                //clear trailing last comma
                queryLines.Last().Text = queryLines.Last().Text.TrimEnd('\n', '\r', ',');
                
                queryLines.Add(new CustomLine(GetHavingSql(),QueryComponent.Having));

                CompileCustomLinesInStageAndAddToList(QueryComponent.GroupBy, queryLines);

                //order by only if we are not pivotting
                if (!DoNotWriteOutOrderBy)
                {
                    queryLines.Add(new CustomLine("order by " ,QueryComponent.OrderBy));

                    //if theres a top X (with an explicit order by)
                    if (AggregateTopX != null)
                    {
                        queryLines.Add(new CustomLine(GetOrderBySQL(AggregateTopX), QueryComponent.OrderBy) { Role = CustomLineRole.TopX });
                    }
                    else
                        foreach (var col in SelectColumns)
                        {
                            if (col.IColumn is AggregateCountColumn)
                                continue;

                            //was added with skip for group by enabled
                            if (_skipGroupByForThese.Contains(col.IColumn))
                                continue;
                            
                            string select;
                            string alias;

                            _syntaxHelper.SplitLineIntoSelectSQLAndAlias(col.GetSelectSQL(null, null, _syntaxHelper),
                                out select,
                                out alias);

                            var line = new CustomLine(select + ",", QueryComponent.OrderBy);

                            FlagLineBasedOnIcolumn(line,col.IColumn);

                            queryLines.Add(line);
                        }

                    queryLines.Last().Text = queryLines.Last().Text.TrimEnd(',');
                }
            }
            else
                queryLines.Add(new CustomLine(GetHavingSql(),QueryComponent.GroupBy));

            queryLines.Last().Text = queryLines.Last().Text.TrimEnd('\n', '\r', ',');

            CompileCustomLinesInStageAndAddToList(QueryComponent.Postfix, queryLines);
        }

        private void FlagLineBasedOnIcolumn(CustomLine line, IColumn column)
        {
            //if it is an axis column tag it as an axis
            if (Equals(column, _axisAppliesToDimension))
                line.Role = CustomLineRole.Axis;
            
            //if it is a count column then flag it as that (cic aggregates take extreme liberties with count columns like hijacking them in a most dispicable way so don't even bother with this flag for them)
            if (column is AggregateCountColumn && !_isCohortIdentificationAggregate)
                line.Role = CustomLineRole.CountFunction;

            if(_pivotDimension != null)
                if(Equals(column,_pivotDimension.IColumn))
                    line.Role = CustomLineRole.Pivot;
        }

        private void GetSelectSQL(List<CustomLine> lines)
        {
            lines.Add(new CustomLine("/*" + LabelWithComment + "*/",QueryComponent.SELECT));
            lines.Add(new CustomLine("SELECT ",QueryComponent.SELECT));

            //if there is no top X or an axis is specified (in which case the TopX applies to the PIVOT if any not the axis)
            if (!string.IsNullOrWhiteSpace(LimitationSQL))
                lines.Add(new CustomLine(LimitationSQL ,QueryComponent.SELECT));
            
            CompileCustomLinesInStageAndAddToList(QueryComponent.SELECT,lines);

            CompileCustomLinesInStageAndAddToList(QueryComponent.QueryTimeColumn, lines);
            
            //put in all the selected columns (which are not being skipped because they aren't a part of group by)
            foreach (QueryTimeColumn col in SelectColumns.Where(col => !_skipGroupByForThese.Contains(col.IColumn)))
            {
                if (col.IColumn.HashOnDataRelease)
                    throw new QueryBuildingException("Column " + col.IColumn.GetRuntimeName() + " is marked as HashOnDataRelease and therefore cannot be used as an Aggregate dimension");


                var line = new CustomLine(col.GetSelectSQL(null, null, _syntaxHelper) + ",", QueryComponent.QueryTimeColumn);
                FlagLineBasedOnIcolumn(line,col.IColumn);

                //it's the axis dimension tag it with the axis tag
                lines.Add(line);
            }

            //get rid of the trailing comma
            lines.Last().Text = lines.Last().Text.TrimEnd('\n', '\r', ',');
        }


        private string GetOrderBySQL(IAggregateTopX aggregateTopX)
        {
            var dimension = aggregateTopX.OrderByColumn;
            if (dimension == null)
                return _countColumn.SelectSQL
                           + (aggregateTopX.OrderByDirection == AggregateTopXOrderByDirection.Ascending
                               ? " asc"
                               : " desc");
        
            return dimension.SelectSQL
                + (aggregateTopX.OrderByDirection == AggregateTopXOrderByDirection.Ascending
                ? " asc"
                : " desc");
        }


        private string GetHavingSql()
        {
            string toReturn = "";

            //HAVING
            if (!string.IsNullOrWhiteSpace(HavingSQL))
            {
                toReturn += "HAVING" + Environment.NewLine;
                toReturn += HavingSQL;
            }
            return toReturn;
        }

        public IEnumerable<Lookup> GetDistinctRequiredLookups()
        {
            throw new NotImplementedException();
        }
    }
}
