// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using CommandLine;
using CommandLine.Text;

namespace DatabaseCreation
{
    /// <summary>
    /// Command line arguments for DatabaseCreation.exe
    /// </summary>
    public class DatabaseCreationProgramOptions
    {
        [Value(0, Required = true, HelpText = "The Microsoft SQL Server on which to create the platform databases (does not have to be the same as your data repository server)")]
        public string ServerName { get; set; }
        
        [Value(1, Required = true, HelpText = "The prefix to append to all databases created")]
        public string Prefix { get; set; }

        [Option('u', HelpText = "Username for sql authentication (Optional)")]
        public string Username { get; set; }

        [Option('p', HelpText = "Password for sql authentication (Optional)")]
        public string Password { get; set; }

        [Option('b', "Binary Collation", Default = false, HelpText = "Create the databases with Binary Collation")]
        public bool BinaryCollation { get; set; }

        [Option('d', "Drop Databases First",  Default = false, HelpText = "Drop the databases before attempting to create them")]
        public bool DropDatabases { get; set; }

        [Option('k', "Skip Pipelines", Default = false, HelpText = "Skips creating the default Pipelines and Managed Server References in the Catalogue database once created.")]
        public bool SkipPipelines { get; set; }
        
        [Usage]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example("Normal Scenario", new DatabaseCreationProgramOptions { ServerName = @"localhost\sqlexpress", Prefix = "TEST_"});
                yield return new Example("Drop existing", new DatabaseCreationProgramOptions { ServerName = @"localhost\sqlexpress", Prefix = "TEST_", DropDatabases = true });
                yield return new Example("Binary Collation", new DatabaseCreationProgramOptions { ServerName = @"localhost\sqlexpress",Prefix =  "TEST_" , DropDatabases = true,BinaryCollation =true });
                yield return new Example("Drop existing", new DatabaseCreationProgramOptions { ServerName = @"localhost\sqlexpress", Prefix = "TEST_", Username = "sa", Password = "lawl", DropDatabases = true });
            }
        }

        public SqlConnectionStringBuilder GetBuilder(string databaseName)
        {
         var builder = new SqlConnectionStringBuilder();
            builder.DataSource = ServerName;
            builder.InitialCatalog = (Prefix ?? "") + databaseName;

            if (!string.IsNullOrWhiteSpace(Username))
            {
                builder.UserID = Username;
                builder.Password = Password;
            }
            else
                builder.IntegratedSecurity = true;

            return builder;
        }
    }
}