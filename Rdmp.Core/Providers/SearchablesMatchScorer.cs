// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using MapsDirectlyToDatabaseTable;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.Cohort;
using Rdmp.Core.Curation.Data.DataLoad;
using Rdmp.Core.Curation.Data.Pipelines;
using Rdmp.Core.DataExport.Data;
using Rdmp.Core.Providers.Nodes.PipelineNodes;
using ReusableLibraryCode;

namespace Rdmp.Core.Providers
{
    /// <summary>
    /// Scores objects as to how relevant they are to a given search string 
    /// </summary>
    public class SearchablesMatchScorer
    {
        private static readonly int[] Weights = new int[] { 64, 32, 16, 8, 4, 2, 1 };

        public HashSet<string> TypeNames { get; set; }
        
        /// <summary>
        /// When the user types one of these they get a filter on the full Type
        /// </summary>
        public static Dictionary<string, Type> ShortCodes =
            new Dictionary<string, Type> (StringComparer.CurrentCultureIgnoreCase){

                {"c",typeof (Catalogue)},
                {"ci",typeof (CatalogueItem)},
                {"sd",typeof (SupportingDocument)},
                {"p",typeof (Project)},
                {"ec",typeof (ExtractionConfiguration)},
                {"co",typeof (ExtractableCohort)},
                {"cic",typeof (CohortIdentificationConfiguration)},
                {"t",typeof (TableInfo)},
                {"col",typeof (ColumnInfo)},
                {"lmd",typeof (LoadMetadata)},
                {"pipe",typeof(Pipeline)}

            };

        /// <summary>
        /// When the user types one of these Types (or a <see cref="ShortCodes"/> for one) they also get the value list for free.
        /// This lets you serve up multiple object Types e.g. <see cref="IMasqueradeAs"/> objects as though they were the same as thier
        /// Key Type.
        /// </summary>
        public static Dictionary<string, Type[]> AlsoIncludes =
            new Dictionary<string, Type[]> (StringComparer.CurrentCultureIgnoreCase){

                {"Pipeline",new Type[]{ typeof(PipelineCompatibleWithUseCaseNode)}}

            };


        public SearchablesMatchScorer()
        {
            TypeNames = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        }

        /// <summary>
        /// Performs a free text search on all <paramref name="searchables"/>.  The <paramref name="searchText"/> will match on both the object
        /// and it's parental hierarchy e.g. "chi" "biochemistry" matches column "chi" in Catalogue "biochemistry" strongly.
        /// </summary>
        /// <param name="searchables">All available objects that can be searched (see <see cref="ICoreChildProvider.GetAllSearchables")/></param>
        /// <param name="searchText">Tokens to use separated by space e.g. "chi biochemistry CatalogueItem"</param>
        /// <param name="cancellationToken">Token for cancelling match scoring.  This method will return null if cancellation is detected</param>
        /// <param name="showOnlyTypes">Optional (can be null) list of types to return results from.  Not respected if <paramref name="searchText"/> includes type names</param>
        /// <returns></returns>
        public Dictionary<KeyValuePair<IMapsDirectlyToDatabaseTable, DescendancyList>, int> ScoreMatches(Dictionary<IMapsDirectlyToDatabaseTable, DescendancyList> searchables, string searchText, CancellationToken cancellationToken, List<Type> showOnlyTypes)
        {
           //do short code substitutions e.g. ti for TableInfo
            if(!string.IsNullOrWhiteSpace(searchText))
                foreach(var kvp in ShortCodes)
                    searchText = Regex.Replace(searchText,$@"\b{kvp.Key}\b",kvp.Value.Name);
            
            //if user hasn't typed any explicit Type filters
            if(showOnlyTypes != null)
                //add the explicit types only if the search text does not contain any explicit type names
                if(string.IsNullOrWhiteSpace(searchText) || !TypeNames.Intersect(searchText.Split(' '),StringComparer.CurrentCultureIgnoreCase).Any())
                    foreach (var showOnlyType in showOnlyTypes) 
                        searchText = searchText + " " + showOnlyType.Name;

            //Search the tokens for also inclusions e.g. "Pipeline" becomes "Pipeline PipelineCompatibleWithUseCaseNode"
            if (!string.IsNullOrWhiteSpace(searchText))
                foreach(var s in searchText.Split(' ').ToArray())
                    if (AlsoIncludes.ContainsKey(s))
                        foreach(var v in AlsoIncludes[s])
                            searchText += " " + v.Name;
 
            //if we have nothing to search for return no results
            if(string.IsNullOrWhiteSpace(searchText))
                return new Dictionary<KeyValuePair<IMapsDirectlyToDatabaseTable, DescendancyList>, int>();
            
            var tokens = (searchText??"").Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            
            var regexes = new List<Regex>();
            
            //any token that 100% matches a type name is an explicitly typed token
            string[] explicitTypesRequested;

            if (TypeNames != null)
            {
                explicitTypesRequested = TypeNames.Intersect(tokens,StringComparer.CurrentCultureIgnoreCase).ToArray();

                //else it's a regex
                foreach (string token in tokens.Except(TypeNames,StringComparer.CurrentCultureIgnoreCase))
                    regexes.Add(new Regex(Regex.Escape(token), RegexOptions.IgnoreCase));

            }
            else
                explicitTypesRequested = new string[0];

            if (cancellationToken.IsCancellationRequested)
                return null;
            
            return searchables.ToDictionary(
           s => s,
           score => ScoreMatches(score, regexes,explicitTypesRequested, cancellationToken)
           );
        }

        private int ScoreMatches(KeyValuePair<IMapsDirectlyToDatabaseTable, DescendancyList> kvp, List<Regex> regexes, string[] explicitTypeNames, CancellationToken cancellationToken)
        {
            int score = 0;

            if (cancellationToken.IsCancellationRequested)
                return 0;

            if (explicitTypeNames.Any())
                if (!explicitTypeNames.Contains(kvp.Key.GetType().Name))
                    return 0;

           //don't suggest AND/OR containers it's not helpful to navigate to these
            if (kvp.Key is IContainer)
                return 0;

            //don't suggest AND/OR containers it's not helpful to navigate to these
            if (kvp.Key is CohortAggregateContainer)
                return 0;

            //if there are no tokens
            if (!regexes.Any())
                if (explicitTypeNames.Any()) //if they have so far just typed a TypeName
                    return 1;
                else
                    return 1;//no regexes AND no TypeName what did they type! whatever everyone scores the same
            
            //make a new list so we can destructively read it
            regexes = new List<Regex>(regexes);
            
            //match on the head vs the regex tokens
            score += Weights[0] * CountMatchToString(regexes, kvp.Key);

            score += Weights[0] * CountMatchType(regexes, kvp.Key);

            //match on the parents if theres a decendancy list
            if (kvp.Value != null)
            {
                var parents = kvp.Value.Parents;
                int numberOfParents = parents.Length;

                //for each prime after the first apply it as a multiple of the parent match
                for (int i = 1; i < Weights.Length; i++)
                {
                    //if we have run out of parents
                    if (i > numberOfParents)
                        break;

                    var parent = parents[parents.Length - i];

                    if (parent != null)
                    {
                        if (!(parent is IContainer))
                        {
                            score += Weights[i] * CountMatchToString(regexes, parent);
                            score += Weights[i] * CountMatchType(regexes, parent);
                        }
                    }
                }
            }

            //if there were unmatched regexes
            if (regexes.Any())
                return 0;

            Catalogue catalogueIfAny = GetCatalogueIfAnyInDescendancy(kvp);

            if (catalogueIfAny != null && catalogueIfAny.IsDeprecated)
                return score /10;
            
            return score;
        }

        private Catalogue GetCatalogueIfAnyInDescendancy(KeyValuePair<IMapsDirectlyToDatabaseTable, DescendancyList> kvp)
        {
            if (kvp.Key is Catalogue)
                return (Catalogue) kvp.Key;

            if (kvp.Value != null)
                return (Catalogue)kvp.Value.Parents.FirstOrDefault(p => p is Catalogue);

            return null;
        }

        private int CountMatchType(List<Regex> regexes, object key)
        {
            return MatchCount(regexes, key.GetType().Name);
        }
        private int CountMatchToString(List<Regex> regexes, object key)
        {
            var s = key as ICustomSearchString;
            string matchOn = s != null ? s.GetSearchString() : key.ToString();

            return MatchCount(regexes, matchOn);
        }
        private int MatchCount(List<Regex> regexes, string str)
        {
            int matches = 0;
            foreach (var match in regexes.Where(r => r.IsMatch(str)).ToArray())
            {
                regexes.Remove(match);
                matches++;
            }

            return matches;
        }
    }
}