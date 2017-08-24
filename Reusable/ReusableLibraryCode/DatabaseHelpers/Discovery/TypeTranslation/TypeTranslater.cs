﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReusableLibraryCode.DatabaseHelpers.Discovery.TypeTranslation
{
    public class TypeTranslater:ITypeTranslater
    {
        public string GetSQLDBTypeForCSharpType(DatabaseTypeRequest request)
        {
            var t = request.CSharpType;

            if (t == typeof(int) || t == typeof(Int64) || t == typeof(Int32) || t == typeof(Int16) || t == typeof(int?))
                return GetIntDataType();
            
            if (t == typeof(bool) || t == typeof(bool?)) 
                return GetBoolDataType();
            
            if (t == typeof(TimeSpan) || t == typeof(TimeSpan?))
                return GetTimeDataType();

            if (t == typeof (string))
                return GetStringDataType(request.MaxWidthForStrings);

            if (t == typeof (DateTime) || t == typeof (DateTime?))
                return GetDateDateTimeDataType();

            if (t == typeof (float) || t == typeof (float?) || t == typeof (double) ||
                t == typeof (double?) || t == typeof (decimal) ||
                t == typeof (decimal?))
                return GetFloatingPointDataType(request.DecimalPlacesBeforeAndAfter);

            if (t == typeof (byte))
                return GetByteDataType();

            if (t == typeof (byte[]))
                return GetByteArrayDataType();


            throw new NotImplementedException("Unsure what SQL Database type to use for Property Type " + t.Name);

        }

        protected virtual string GetByteArrayDataType()
        {
            return "varbinary(max)";
        }

        protected virtual string GetByteDataType()
        {
            return "tinyint";
        }

        protected virtual string GetFloatingPointDataType(Tuple<int, int> decimalPlacesBeforeAndAfter)
        {
            if (decimalPlacesBeforeAndAfter == null)
                return "decimal(20,10)";
            
            return "decimal(" + (decimalPlacesBeforeAndAfter.Item1 + decimalPlacesBeforeAndAfter.Item2) + "," + decimalPlacesBeforeAndAfter.Item2 + ")";
        }

        protected virtual string GetDateDateTimeDataType()
        {
            return "datetime";
        }

        protected virtual string GetStringDataType(int? maxExpectedStringWidth)
        {
            if (maxExpectedStringWidth == null)
                return "varchar(4000)";

            if (maxExpectedStringWidth > 8000)
                return "varchar(max)";
            
            return "varchar(" + maxExpectedStringWidth + ")";
        }

        protected virtual string GetTimeDataType()
        {
            return "time";
        }

        protected virtual string GetBoolDataType()
        {
            return "bit";
        }

        protected virtual string GetIntDataType()
        {
            return "int";
        }
    }
}
