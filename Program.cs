using System;
using System.Collections.Generic;
using System.IO;

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
            { "char*", "text" },
            { "char[4]", "varchar(4) NOT NULL"},
            { "uint64", "bigint(20) unsigned NOT NULL DEFAULT '0'" },
            { "int64", "bigint(20) NOT NULL DEFAULT '0'" }
        };

        public static void Main(string[] args)
        {
            _parser = new HeaderParser() { FileName = "DB2Structure.h" };
            _parser.Parse();
            using (var output = new StreamWriter($"{DateTime.Now.ToString("yyyy_MM_dd")}_00_hotfixes.sql"))
            using (var hotfixesCpp = new StreamWriter($"{DateTime.Now.ToString("yyyy_MM_dd")}_HotfixDatabase.cpp"))
            using (var hotfixesH = new StreamWriter($"{DateTime.Now.ToString("yyyy_MM_dd")}_HotfixDatabase.h"))
            {
                WriteLicense(hotfixesCpp);
                hotfixesCpp.WriteLine("#include \"HotfixDatabase.h\"");
                hotfixesCpp.WriteLine();
                hotfixesCpp.WriteLine("// Force locale statments to appear exactly in locale declaration order, right after normal data fetch statement");
                hotfixesCpp.WriteLine("#define PREPARE_LOCALE_STMT(stmtBase, sql, con) \\");
                hotfixesCpp.WriteLine("    static_assert(stmtBase + 1 == stmtBase##_LOCALE, \"Invalid prepared statement index for \" #stmtBase \"_LOCALE\"); \\");
                hotfixesCpp.WriteLine("    PrepareStatement(stmtBase##_LOCALE, sql, con);");
                hotfixesCpp.WriteLine();
                hotfixesCpp.WriteLine("void HotfixDatabaseConnection::DoPrepareStatements()");
                hotfixesCpp.WriteLine("{");
                hotfixesCpp.WriteLine("    if (!m_reconnecting)");
                hotfixesCpp.WriteLine("        m_stmts.resize(MAX_HOTFIXDATABASE_STATEMENTS);");

                WriteLicense(hotfixesH);
                hotfixesH.WriteLine("#ifndef _HOTFIXDATABASE_H");
                hotfixesH.WriteLine("#define _HOTFIXDATABASE_H");
                hotfixesH.WriteLine("");
                hotfixesH.WriteLine("#include \"DatabaseWorkerPool.h\"");
                hotfixesH.WriteLine("#include \"MySQLConnection.h\"");
                hotfixesH.WriteLine("");
                hotfixesH.WriteLine("enum HotfixDatabaseStatements");
                hotfixesH.WriteLine("{");
                hotfixesH.WriteLine("    /*  Naming standard for defines:");
                hotfixesH.WriteLine("        {DB}_{SEL/INS/UPD/DEL/REP}_{Summary of data changed}");
                hotfixesH.WriteLine("        When updating more than one field, consider looking at the calling function");
                hotfixesH.WriteLine("        name for a suiting suffix.");
                hotfixesH.WriteLine("    */");

                foreach (var structure in _parser.Structures)
                    DumpStructure(output, hotfixesCpp, hotfixesH, structure);

                hotfixesCpp.WriteLine("}");

                hotfixesH.WriteLine("");
                hotfixesH.WriteLine("    MAX_HOTFIXDATABASE_STATEMENTS");
                hotfixesH.WriteLine("};");
                hotfixesH.WriteLine("");
                hotfixesH.WriteLine("class TC_DATABASE_API HotfixDatabaseConnection : public MySQLConnection");
                hotfixesH.WriteLine("{");
                hotfixesH.WriteLine("public:");
                hotfixesH.WriteLine("    typedef HotfixDatabaseStatements Statements;");
                hotfixesH.WriteLine("");
                hotfixesH.WriteLine("    //- Constructors for sync and async connections");
                hotfixesH.WriteLine("    HotfixDatabaseConnection(MySQLConnectionInfo& connInfo) : MySQLConnection(connInfo) { }");
                hotfixesH.WriteLine("    HotfixDatabaseConnection(ProducerConsumerQueue<SQLOperation*>* q, MySQLConnectionInfo& connInfo) : MySQLConnection(q, connInfo) { }");
                hotfixesH.WriteLine("");
                hotfixesH.WriteLine("    //- Loads database type specific prepared statements");
                hotfixesH.WriteLine("    void DoPrepareStatements() override;");
                hotfixesH.WriteLine("};");
                hotfixesH.WriteLine("");
                hotfixesH.WriteLine("typedef DatabaseWorkerPool<HotfixDatabaseConnection> HotfixDatabaseWorkerPool;");
                hotfixesH.WriteLine("");
                hotfixesH.WriteLine("#endif");
            }
        }

        private static void WriteLicense(StreamWriter stream)
        {
            stream.WriteLine("/*");
            stream.WriteLine($" * Copyright (C) 2008-{DateTime.Now.ToString("yyyy")} TrinityCore <http://www.trinitycore.org/>");
            stream.WriteLine(" *");
            stream.WriteLine(" * This program is free software; you can redistribute it and/or modify it");
            stream.WriteLine(" * under the terms of the GNU General Public License as published by the");
            stream.WriteLine(" * Free Software Foundation; either version 2 of the License, or (at your");
            stream.WriteLine(" * option) any later version.");
            stream.WriteLine(" *");
            stream.WriteLine(" * This program is distributed in the hope that it will be useful, but WITHOUT");
            stream.WriteLine(" * ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or");
            stream.WriteLine(" * FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for");
            stream.WriteLine(" * more details.");
            stream.WriteLine(" *");
            stream.WriteLine(" * You should have received a copy of the GNU General Public License along");
            stream.WriteLine(" * with this program. If not, see <http://www.gnu.org/licenses/>.");
            stream.WriteLine(" */");
            stream.WriteLine();
            stream.WriteLine("// DO NOT EDIT!");
            stream.WriteLine("// Autogenerated from DB2Structure.h");
            stream.WriteLine();
        }

        private static void DumpStructure(StreamWriter output, StreamWriter hotfixesCpp, StreamWriter hotfixesH, CStructureInfo structure)
        {
            output.WriteLine("--");
            output.WriteLine($"-- Table structure for table `{structure.GetTableName()}`");
            output.WriteLine("--");
            output.WriteLine();
            output.WriteLine($"DROP TABLE IF EXISTS `{structure.GetTableName()}`;");
            output.WriteLine("/*!40101 SET @saved_cs_client     = @@character_set_client */;");
            output.WriteLine("/*!40101 SET character_set_client = utf8 */;");
            output.WriteLine($"CREATE TABLE `{structure.GetTableName()}` (");

            var cppBuilder = new LimitedLineLengthStringBuilder()
            {
                WrappedLinePrefix = "        \"",
                WrappedLineSuffix = "\""
            };

            if (!structure.IsLocale)
            {
                cppBuilder.AppendLine();
                cppBuilder.AppendLine($"    // {structure.Name}.db2");
                cppBuilder.Append($"    PrepareStatement(HOTFIX_SEL_{structure.GetTableName().ToUpperInvariant()}");
                hotfixesH.WriteLine();
            }
            else
                cppBuilder.Append($"    PREPARE_LOCALE_STMT(HOTFIX_SEL_{structure.GetTableName().ToUpperInvariant().Replace("_LOCALE", "")}");

            cppBuilder.Append(", \"SELECT ");

            foreach (var member in structure.Members)
                DumpStructureMember(output, cppBuilder, member);

            cppBuilder.Remove(cppBuilder.Length - 2, 2);
            if (!structure.GetTableName().IsSqlKeyword())
                cppBuilder.Append($" FROM {structure.GetTableName()}");
            else
                cppBuilder.Append($" FROM `{structure.GetTableName()}`");

            if (!structure.IsLocale)
            {
                output.WriteLine($"  PRIMARY KEY (`ID`)");
                cppBuilder.Append($" ORDER BY ID DESC");
            }
            else
            {
                cppBuilder.Append(" WHERE locale = ?");
                output.WriteLine($"  PRIMARY KEY (`ID`,`locale`)");
            }

            output.WriteLine(") ENGINE=MyISAM DEFAULT CHARSET=utf8;");
            output.WriteLine("/*!40101 SET character_set_client = @saved_cs_client */;");
            output.WriteLine();
            output.WriteLine("--");
            output.WriteLine($"-- Dumping data for table `{structure.GetTableName()}`");
            output.WriteLine("--");
            output.WriteLine();
            output.WriteLine($"LOCK TABLES `{structure.GetTableName()}` WRITE;");
            output.WriteLine($"/*!40000 ALTER TABLE `{structure.GetTableName()}` DISABLE KEYS */;");
            output.WriteLine($"/*!40000 ALTER TABLE `{structure.GetTableName()}` ENABLE KEYS */;");
            output.WriteLine("UNLOCK TABLES;");
            output.WriteLine();

            cppBuilder.Nonbreaking().Append("\", CONNECTION_SYNCH);");
            hotfixesCpp.WriteLine(cppBuilder.Finalize());
            hotfixesH.WriteLine($"    HOTFIX_SEL_{structure.GetTableName().ToUpperInvariant()},");
        }

        private static void DumpStructureMember(StreamWriter output, LimitedLineLengthStringBuilder query, CStructureMemberInfo member)
        {
            var arraySize = member.ArraySize;

            string typeInfo;
            if (!MySQLTypeMap.TryGetValue(member.FormattedTypeName, out typeInfo))
                typeInfo = "ERROR TYPE" + member.TypeName;

            if (member.TypeName == "flag128")
            {
                arraySize = 4;
                typeInfo = MySQLTypeMap["uint32"];
            }

            for (var i = 0; i < arraySize; ++i)
            {
                var memberName = member.Name;
                if (arraySize > 1)
                {
                    var langPos = memberName.IndexOf("_lang");
                    if (langPos == -1)
                        memberName += (i + 1).ToString();
                    else
                        memberName = memberName.Insert(langPos, (i + 1).ToString());
                }

                if (!typeInfo.Contains("ERROR"))
                {
                    DumpStructureMemberName(output, query, memberName, typeInfo);
                }
                else
                {
                    switch (member.TypeName)
                    {
                        case "DBCPosition3D":
                            DumpStructureMemberName(output, query, memberName + "X", MySQLTypeMap["float"]);
                            DumpStructureMemberName(output, query, memberName + "Y", MySQLTypeMap["float"]);
                            DumpStructureMemberName(output, query, memberName + "Z", MySQLTypeMap["float"]);
                            break;
                        case "DBCPosition2D":
                            DumpStructureMemberName(output, query, memberName + "X", MySQLTypeMap["float"]);
                            DumpStructureMemberName(output, query, memberName + "Y", MySQLTypeMap["float"]);
                            break;
                        default:
                            output.WriteLine($"  `{memberName}` ERROR TYPE {member.TypeName},");
                            break;
                    }
                }
            }
        }

        private static void DumpStructureMemberName(StreamWriter output, LimitedLineLengthStringBuilder query, string memberName, string typeName)
        {
            output.WriteLine($"  `{memberName}` {typeName},");
            if (memberName != "VerifiedBuild" && memberName != "locale")
            {
                if (!memberName.IsSqlKeyword())
                    query.Append($"{memberName}, ");
                else
                    query.Append($"`{memberName}`, ");
            }
        }

        private static bool IsSqlKeyword(this string str)
        {
            switch (str.ToUpperInvariant())
            {
                case "INDEX":
                case "TRIGGER":
                case "FROM":
                case "TO":
                case "LOCK":
                case "LIMIT":
                case "FLOAT4":
                case "FLOAT8":
                case "INT1":
                case "INT2":
                case "INT3":
                case "INT4":
                    return true;
            }

            return false;
        }
    }
}
