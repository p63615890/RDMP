﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MapsDirectlyToDatabaseTable;
using Rdmp.Core.Repositories;

namespace Rdmp.Core.CommandLine.Interactive.Picking
{
    public class PickObjectByID :PickObjectBase
    {
        /*
            Console.WriteLine("Format \"\" e.g. \"Catalogue:*mysql*\" or \"Catalogue:12,23,34\"");

            */
        public override string Format => "{Type}:{ID}[,{ID2},{ID3}...]";
        public override string Help => 
@"Type: must be an RDMP object type e.g. Catalogue, Project etc.
ID: must reference an object that exists
ID2+: (optional) only allowed if you are being prompted for multiple objects, allows you to specify multiple objects of the same Type using comma separator";
        
        public override IEnumerable<string> Examples => new []
        {
            "Catalogue:1", 
            "Catalogue:1,2,3"
        };

        public PickObjectByID(IRDMPPlatformRepositoryServiceLocator repositoryLocator)
            :base(repositoryLocator,
                new Regex("^([A-Za-z]+):([0-9,]+)$",RegexOptions.IgnoreCase))
        {
                
        }
        
        public override CommandLineObjectPickerArgumentValue Parse(string arg, int idx)
        {
            var objByID = MatchOrThrow(arg, idx);

            string objectType = objByID.Groups[1].Value;
            string objectId = objByID.Groups[2].Value;

            Type dbObjectType = ParseDatabaseEntityType(objectType, arg, idx);
                
            var objs = objectId.Split(',').Select(id=>GetObjectByID(dbObjectType,int.Parse(id))).Distinct();
                
            return new CommandLineObjectPickerArgumentValue(arg,idx,objs.Cast<IMapsDirectlyToDatabaseTable>().ToArray());
        }
    }
}