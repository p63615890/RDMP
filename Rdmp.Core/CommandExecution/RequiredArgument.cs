// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Reflection;

namespace Rdmp.Core.CommandExecution
{
    /// <summary>
    /// Describes a single <see cref="ParameterInfo"/> or <see cref="PropertyInfo"/> required by a <see cref="CommandInvoker"/>
    /// </summary>
    public class RequiredArgument
    {
        public string Name { get; }
        public Type Type { get; }
        public object ReflectionObject { get; }
        public bool HasDefaultValue { get;}
        public object DefaultValue { get; }

        public RequiredArgument(PropertyInfo propertyInfo)
        {
            Name = propertyInfo.Name;
            Type = propertyInfo.PropertyType;
            ReflectionObject = propertyInfo;
            HasDefaultValue = false;
            DefaultValue = null;
        }

        public RequiredArgument(ParameterInfo parameterInfo)
        {
            Name = parameterInfo.Name;
            Type = parameterInfo.ParameterType;
            ReflectionObject = parameterInfo;
            HasDefaultValue = parameterInfo.HasDefaultValue;
            DefaultValue = parameterInfo.DefaultValue;
        }
    }
}