using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WowClientDB2MySQLTableGenerator
{
    public static class Program
    {
        private static HeaderParser _parser;

        public static readonly Dictionary<string, string> MySQLTypeMap = new Dictionary<string, string>()
        {
            { "uint32", "int(10) unsigned NOT NULL DEFAULT '0'" },
            { "int32", "int(11) NOT NULL DEFAULT '0'" },
            { "uint16", "smallint(5) unsigned NOT NULL DEFAULT '0'" },
            { "int16", "smallint(6) NOT NULL DEFAULT '0'" },
            { "uint8", "tinyint(3) unsigned NOT NULL DEFAULT '0'" },
            { "int8", "tinyint(4) NOT NULL DEFAULT '0'" },
            { "float", "float NOT NULL DEFAULT '0'"},
            { "LocalizedString*", "text" },
            { "char[4]", "varchar(4) NOT NULL"}
        };

        public static void Main(string[] args)
        {
            _parser = new HeaderParser("DB2Structure.h");
            _parser.Parse();
            using (var output = new StreamWriter(String.Format("{0}_00_hotfixes.sql", DateTime.Now.ToString("yyyy_MM_dd"))))
            {
                foreach (var structure in _parser.Structures)
                    DumpStructure(output, structure);
            }
        }

        private static void DumpStructure(StreamWriter output, CStructureInfo structure)
        {
            output.WriteLine("--");
            output.WriteLine("-- Table structure for table `" + structure.GetTableName() + "`");
            output.WriteLine("--");
            output.WriteLine();
            output.WriteLine("DROP TABLE IF EXISTS `" + structure.GetTableName() + "`;");
            output.WriteLine("/*!40101 SET @saved_cs_client     = @@character_set_client */;");
            output.WriteLine("/*!40101 SET character_set_client = utf8 */;");
            output.WriteLine("CREATE TABLE `" + structure.GetTableName() + "` (");
            foreach (var member in structure.Members)
                DumpStructureMember(output, member);

            if (!structure.Name.Contains("Locale"))
                output.WriteLine(String.Format("  PRIMARY KEY (`{0}`)", structure.Members.First().Name));
            else
                output.WriteLine(String.Format("  PRIMARY KEY (`{0}`,`locale`)", structure.Members.First().Name));

            output.WriteLine(") ENGINE=MyISAM DEFAULT CHARSET=utf8;");
            output.WriteLine("/*!40101 SET character_set_client = @saved_cs_client */;");
            output.WriteLine();
            output.WriteLine("--");
            output.WriteLine("-- Dumping data for table `" + structure.GetTableName() + "`");
            output.WriteLine("--");
            output.WriteLine();
            output.WriteLine("LOCK TABLES `" + structure.GetTableName() + "` WRITE;");
            output.WriteLine("/*!40000 ALTER TABLE `" + structure.GetTableName() + "` DISABLE KEYS */;");
            output.WriteLine("/*!40000 ALTER TABLE `" + structure.GetTableName() + "` ENABLE KEYS */;");
            output.WriteLine("UNLOCK TABLES;");
            output.WriteLine();
        }

        private static void DumpStructureMember(StreamWriter output, CStructureMemberInfo member)
        {
            var arraySize = 1;
            var indexOfArray = member.Name.IndexOf('[');
            if (indexOfArray != -1)
            {
                var arrayDef = member.Name.Substring(indexOfArray);
                if (!_parser.ArraySizes.TryGetValue(arrayDef, out arraySize))
                    if (!int.TryParse(arrayDef.Substring(1, arrayDef.Length - 2), out arraySize))
                        arraySize = 1;
            }

            string typeInfo;
            if (!MySQLTypeMap.TryGetValue(member.TypeName, out typeInfo))
                typeInfo = "ERROR TYPE" + member.TypeName;

            if (member.TypeName == "flag128")
            {
                arraySize = 4;
                typeInfo = MySQLTypeMap["uint32"];
            }

            for (var i = 0; i < arraySize; ++i)
            {
                var memberName = member.Name;
                if (indexOfArray != -1)
                    memberName = member.Name.Substring(0, indexOfArray);

                if (arraySize > 1)
                    memberName += (i + 1).ToString();

                if (!typeInfo.Contains("ERROR"))
                {
                    output.WriteLine(String.Format("  `{0}` {1},", memberName, typeInfo));
                }
                else
                {
                    switch (member.TypeName)
                    {
                        case "DBCPosition3D":
                            output.WriteLine(String.Format("  `{0}X` {1},", memberName, MySQLTypeMap["float"]));
                            output.WriteLine(String.Format("  `{0}Y` {1},", memberName, MySQLTypeMap["float"]));
                            output.WriteLine(String.Format("  `{0}Z` {1},", memberName, MySQLTypeMap["float"]));
                            break;
                        case "DBCPosition2D":
                            output.WriteLine(String.Format("  `{0}X` {1},", memberName, MySQLTypeMap["float"]));
                            output.WriteLine(String.Format("  `{0}Y` {1},", memberName, MySQLTypeMap["float"]));
                            break;
                        default:
                            output.WriteLine(String.Format("  `{0}` {1},", memberName, "ERROR TYPE " + member.TypeName));
                            break;
                    }
                }
            }
        }
    }
}
