// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using FAnsi;
using FAnsi.Discovery;
using ReusableLibraryCode;

namespace CatalogueLibrary.Triggers.Implementations
{
    /// <summary>
    /// Handles the creation of the appropriate <see cref="ITriggerImplementer"/> for any given <see cref="DatabaseType"/>
    /// </summary>
    public class TriggerImplementerFactory
    {
        private readonly DatabaseType _databaseType;

        public TriggerImplementerFactory(DatabaseType databaseType)
        {
            _databaseType = databaseType;
        }

        public ITriggerImplementer Create(DiscoveredTable table, bool createDataLoadRunIDAlso = true)
        {
            switch (_databaseType)
            {
                case DatabaseType.MicrosoftSQLServer:
                    return new MicrosoftSQLTriggerImplementer(table, createDataLoadRunIDAlso);
                case DatabaseType.MySql:
                    return new MySqlTriggerImplementer(table, createDataLoadRunIDAlso);
                case DatabaseType.Oracle:
                    return new MySqlTriggerImplementer(table, createDataLoadRunIDAlso);
                default:
                    throw new ArgumentOutOfRangeException("databaseType");
            }
        }
    }
}