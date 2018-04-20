﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DataExportLibrary.Data.DataTables;

namespace DataExportLibrary.Providers.Nodes
{
    public class ProjectCataloguesNode
    {
        public Project Project { get; set; }

        public ProjectCataloguesNode(Project project)
        {
            Project = project;
        }

        public override string ToString()
        {
            return "Project Specific Catalogues";
        }

        protected bool Equals(ProjectCataloguesNode other)
        {
            return Project.Equals(other.Project);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ProjectCataloguesNode) obj);
        }

        public override int GetHashCode()
        {
            return Project.GetHashCode();
        }
    }
}
