// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using FAnsi.Discovery.QuerySyntax;
using MapsDirectlyToDatabaseTable;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.Aggregation;
using Rdmp.Core.Curation.Data.Spontaneous;
using Rdmp.Core.Curation.FilterImporting;
using Rdmp.Core.QueryBuilding.Parameters;

namespace Rdmp.Core.QueryBuilding
{
    /// <summary>
    /// Helper for CohortQueryBuilder which contains code for building individual cohort identification subqueries.  Subqueries are actually built by 
    /// AggregateBuilder but this class handles tab indentation, parameter renaming (where there are other subqueries with conflicting sql parameter names), 
    /// injecting globals etc.
    /// </summary>
    public class CohortQueryBuilderHelper
    {
        public ParameterManager ParameterManager { get; set; }
        
        public CohortQueryBuilderHelper(ParameterManager parameterManager)
        {
            ParameterManager = parameterManager;
        }

        /// <summary>
        /// Returns the SQL you need to include in your nested query (in UNION / EXCEPT / INTERSECT).  This does not include parameter declarations (which
        /// would appear at the very top) and includes rename operations dependant on what has been written out before by (tracked by <see cref="ParameterManager"/>).
        ///
        /// <para>Use <paramref name="isolatedSql"/> for the original un renamed / including parameter declarations e.g. to test for cache hits</para>
        /// </summary>
        /// <param name="aggregate"></param>
        /// <param name="args"></param>
        /// <param name="isolatedSql"></param>
        /// <returns></returns>
        public string GetSQLForAggregate(AggregateConfiguration aggregate, QueryBuilderArgs args, out string isolatedSql)
        {
            bool isJoinAggregate = aggregate.IsCohortIdentificationAggregate;

            //make sure it is a valid configuration
            string reason;
            if (!aggregate.IsAcceptableAsCohortGenerationSource(out reason))
                throw new QueryBuildingException("Cannot generate a cohort using AggregateConfiguration " + aggregate + " because:" + reason);

            //get the extraction identifier (method IsAcceptableAsCohortGenerationSource will ensure this linq returns 1 so no need to check again)
            AggregateDimension extractionIdentifier = aggregate.AggregateDimensions.Single(d => d.IsExtractionIdentifier);

            //create a builder but do it manually, we care about group bys etc or count(*) even 
            AggregateBuilder builder;

            //we are getting SQL for a cohort identification aggregate without a HAVING/count statement so it is actually just 'select patientIdentifier from tableX'
            if (string.IsNullOrWhiteSpace(aggregate.HavingSQL) && string.IsNullOrWhiteSpace(aggregate.CountSQL))
            {
                //select list is the extraction identifier
                string selectList;

                if (!isJoinAggregate)
                    selectList = extractionIdentifier.SelectSQL +  (extractionIdentifier.Alias != null ? " " + extractionIdentifier.Alias: "");
                else
                    //unless we are also including other columns because this is a patient index joinable inception query
                    selectList = string.Join("," + Environment.NewLine,
                        aggregate.AggregateDimensions.Select(e => e.SelectSQL + (e.Alias != null ? " " + e.Alias : ""))); //joinable patient index tables have patientIdentifier + 1 or more other columns

                if (args?.OverrideSelectList != null)
                    selectList = args.OverrideSelectList;

                string limitationSQL = args?.OverrideLimitationSQL ?? "distinct";

                //select list is either [chi] or [chi],[mycolumn],[myexcitingcol] (in the case of a patient index table)
                builder = new AggregateBuilder(limitationSQL, selectList, aggregate, aggregate.ForcedJoins);

                //false makes it skip them in the SQL it generates (it uses them only in determining JOIN requirements etc but since we passed in the select SQL explicitly it should be the equivellent of telling the query builder to generate a regular select 
                if(!isJoinAggregate)
                    builder.AddColumn(extractionIdentifier, false);
                else
                    builder.AddColumnRange(aggregate.AggregateDimensions.ToArray(), false);
            }
            else
            {
                if (args?.OverrideSelectList != null)
                    throw new NotSupportedException("Cannot override Select list on aggregates that have HAVING / Count SQL configured in them");

                builder = new AggregateBuilder("distinct", aggregate.CountSQL, aggregate, aggregate.ForcedJoins);

                //add the extraction information and do group by it
                if (!isJoinAggregate)
                    builder.AddColumn(extractionIdentifier, true);
                else
                    builder.AddColumnRange(aggregate.AggregateDimensions.ToArray(), true);//it's a joinable inception query (See JoinableCohortAggregateConfiguration) - these are allowed additional columns

                builder.DoNotWriteOutOrderBy = true;
            }

            if (args != null &&  args.TopX != -1)
                builder.AggregateTopX = new SpontaneouslyInventedAggregateTopX(new MemoryRepository(), args.TopX, AggregateTopXOrderByDirection.Descending, null);

            
            //Add the inception join
            if (args?.JoinIfAny != null)
                AddJoinToBuilder(aggregate,extractionIdentifier,builder, args);
            

            //set the where container
            builder.RootFilterContainer = aggregate.RootFilterContainer;

            //we will be harnessing the parameters via ImportAndElevate so do not add them to the SQL directly
            builder.DoNotWriteOutParameters = true;
            var builderSqlWithoutParameters = builder.SQL;

            //get the SQL from the builder (for the current configuration) - without parameters
            string currentBlock = builderSqlWithoutParameters;


            //we need to generate the full SQL with parameters (and no rename operations) so we can do cache hit tests
            string parameterSql = QueryBuilder.GetParameterDeclarationSQL(builder.ParameterManager.Clone().GetFinalResolvedParametersList());
            isolatedSql = parameterSql + currentBlock;

            //but we need to return the sql designed for nesting (i.e. no parameter declarations and including rename operations) for the depth
            //of nesting we are at (e.g. where 2 parameters share the same name but different values in separate sections of the tree).

            //import parameters unless caching was used
            Dictionary<string, string> renameOperations;
            ParameterManager.ImportAndElevateResolvedParametersFromSubquery(builder.ParameterManager, out renameOperations);

            //rename in the SQL too!
            foreach (KeyValuePair<string, string> kvp in renameOperations)
                currentBlock = ParameterCreator.RenameParameterInSQL(currentBlock, kvp.Key, kvp.Value);

            return currentBlock;
        }

        public void AddJoinToBuilder(AggregateConfiguration user,IColumn usersExtractionIdentifier,AggregateBuilder builder, QueryBuilderArgs args)
        {
            var joinableTableAlias = args.JoinIfAny.GetJoinTableAlias();
            string joinDirection = args.JoinIfAny.GetJoinDirectionSQL();
                
            var joinOn = args.JoinedTo.AggregateDimensions.SingleOrDefault(d => d.IsExtractionIdentifier);

            if (joinOn == null)
                throw new QueryBuildingException("AggregateConfiguration " + user + " uses a join aggregate (patient index aggregate) of " + args.JoinedTo + " but that AggregateConfiguration does not have an IsExtractionIdentifier dimension so how are we supposed to join these tables on the patient identifier?");

            // will end up with something like this where 51 is the ID of the joinTable:
            // LEFT Join (***INCEPTION QUERY***)ix51 on ["+TestDatabaseNames.Prefix+@"ScratchArea]..[BulkData].[patientIdentifier] = ix51.patientIdentifier

            builder.AddCustomLine(" " + joinDirection + " Join (" + Environment.NewLine + args.JoinSql + Environment.NewLine + ")" + joinableTableAlias + Environment.NewLine + "on " + usersExtractionIdentifier.SelectSQL + " = " + joinableTableAlias + "." + joinOn.GetRuntimeName(),QueryComponent.JoinInfoJoin);
        }
    }
}
