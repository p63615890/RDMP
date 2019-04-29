// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using Rdmp.Core.Curation.Data;

namespace Rdmp.Core.DataExport.ExtractionTime
{
    /// <summary>
    /// Documents the extraction time data type of an extracted column.  This is done by inspecting the Type of the DataTable column fetched when executing the
    /// extraction SQL.  This can be different from the Database/Catalogue Type because there can be transformation SQL entered (e.g. LEFT etc).
    /// </summary>
    public class ExtractTimeTransformationObserved
    {
        public bool FoundAtExtractTime { get; set; }
        public CatalogueItem CatalogueItem { get; set; }
        public string DataTypeInCatalogue { get; set; }
        public Type DataTypeObservedInRuntimeBuffer { get; set; }
        public string RuntimeName { get; set; }

        public ExtractTimeTransformationObserved()
        {
            FoundAtExtractTime = false;
        }
    }
}