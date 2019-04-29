// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using Rdmp.Core.Curation.Data.Cohort;
using Rdmp.Core.DataExport.Data;

namespace Rdmp.Core.Providers.Nodes
{
    /// <summary>
    /// Collection of all <see cref="ExtractionConfiguration"/> in a given <see cref="Project"/>
    /// </summary>
    public class ExtractionConfigurationsNode:Node,IOrderable
    {
        public Project Project { get; set; }

        public ExtractionConfigurationsNode(Project project)
        {
            Project = project;
        }

        public override string ToString()
        {
            return "Extraction Configurations";
        }

        protected bool Equals(ExtractionConfigurationsNode other)
        {
            return Equals(Project, other.Project);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ExtractionConfigurationsNode) obj);
        }

        public override int GetHashCode()
        {
            return (Project != null ? Project.GetHashCode() : 0);
        }

        public int Order { get { return 3; } set{} }

    }
}
