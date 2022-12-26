using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace WowClientDB2MySQLTableGenerator
{
    public static class Program
    {
        private static HeaderParser _parser;

        private static readonly Dictionary<string, string> MySQLTypeMap = new Dictionary<string, string>()
        {
            { "uint32", "int(10) unsigned NOT NULL DEFAULT '0'" },
            { "unionAssetNameAlias", "int(10) NOT NULL DEFAULT '0'" },
            { "int32", "int(11) NOT NULL DEFAULT '0'" },
            { "uint16", "smallint(5) unsigned NOT NULL DEFAULT '0'" },
            { "int16", "smallint(6) NOT NULL DEFAULT '0'" },
            { "uint8", "tinyint(3) unsigned NOT NULL DEFAULT '0'" },
            { "int8", "tinyint(4) NOT NULL DEFAULT '0'" },
            { "float", "float NOT NULL DEFAULT '0'"},
            { "LocalizedString", "text" },
            { "LocalizedString*", "text" },
            { "char*", "text" },
            { "char[4]", "varchar(4) NOT NULL"},
            { "uint64", "bigint(20) unsigned NOT NULL DEFAULT '0'" },
            { "int64", "bigint(20) NOT NULL DEFAULT '0'" }
        };

        private static readonly Regex SignedIntRegex = new Regex("^(int[0-9]{1,2})|(unionAssetNameAlias)|(flag128)$", RegexOptions.Compiled);
        private static readonly Dictionary<string, string> DbcFormatEnumTypeMap = new Dictionary<string, string>()
        {
            { "uint32", "FT_INT" },
            { "unionAssetNameAlias", "FT_INT" },
            { "int32", "FT_INT" },
            { "uint16", "FT_SHORT" },
            { "int16", "FT_SHORT" },
            { "uint8", "FT_BYTE" },
            { "int8", "FT_BYTE" },
            { "float", "FT_FLOAT"},
            { "LocalizedString", "FT_STRING" },
            { "LocalizedString*", "FT_STRING" },
            { "char*", "FT_STRING_NOT_LOCALIZED" },
            { "uint64", "FT_LONG" },
            { "int64", "FT_LONG" }
        };

        public static void Main()
        {
            _parser = new HeaderParser() { FileName = "DB2Structure.h" };
            _parser.Parse();
            using (var hotfixesSql = new StreamWriter($"{DateTime.Now:yyyy_MM_dd}_00_hotfixes.sql"))
            using (var hotfixesCpp = new StreamWriter($"{DateTime.Now:yyyy_MM_dd}_HotfixDatabase.cpp"))
            using (var hotfixesH = new StreamWriter($"{DateTime.Now:yyyy_MM_dd}_HotfixDatabase.h"))
            using (var infoH = new StreamWriter($"{DateTime.Now:yyyy_MM_dd}_DB2LoadInfo.h"))
            {
                WriteLicense(hotfixesCpp);
                hotfixesCpp.WriteLine("#include \"HotfixDatabase.h\"");
                hotfixesCpp.WriteLine("#include \"MySQLPreparedStatement.h\"");
                hotfixesCpp.WriteLine();
                hotfixesCpp.WriteLine("// Force max id statements to appear exactly right after normal data fetch statement");
                hotfixesCpp.WriteLine("#define PREPARE_MAX_ID_STMT(stmtBase, sql, con) \\");
                hotfixesCpp.WriteLine("    static_assert(stmtBase + HOTFIX_MAX_ID_STMT_OFFSET == stmtBase##_MAX_ID, \"Invalid prepared statement index for \" #stmtBase \"_MAX_ID\"); \\");
                hotfixesCpp.WriteLine("    PrepareStatement(stmtBase##_MAX_ID, sql, con);");
                hotfixesCpp.WriteLine();
                hotfixesCpp.WriteLine("// Force locale statements to be right after max id fetch statement");
                hotfixesCpp.WriteLine("#define PREPARE_LOCALE_STMT(stmtBase, sql, con) \\");
                hotfixesCpp.WriteLine("    static_assert(stmtBase + HOTFIX_LOCALE_STMT_OFFSET == stmtBase##_LOCALE, \"Invalid prepared statement index for \" #stmtBase \"_LOCALE\"); \\");
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
                hotfixesH.WriteLine("#include \"MySQLConnection.h\"");
                hotfixesH.WriteLine("");
                hotfixesH.WriteLine("enum HotfixDatabaseStatements : uint32");
                hotfixesH.WriteLine("{");
                hotfixesH.WriteLine("    /*  Naming standard for defines:");
                hotfixesH.WriteLine("        {DB}_{SEL/INS/UPD/DEL/REP}_{Summary of data changed}");
                hotfixesH.WriteLine("        When updating more than one field, consider looking at the calling function");
                hotfixesH.WriteLine("        name for a suiting suffix.");
                hotfixesH.WriteLine("    */");

                WriteLicense(infoH);
                infoH.WriteLine("#ifndef DB2LoadInfo_h__");
                infoH.WriteLine("#define DB2LoadInfo_h__");
                infoH.WriteLine("");
                infoH.WriteLine("#include \"DB2DatabaseLoader.h\"");
                infoH.WriteLine("#include \"DB2Metadata.h\"");
                infoH.WriteLine("");

                foreach (var structure in _parser.Structures)
                    DumpStructure(hotfixesSql, hotfixesCpp, hotfixesH, infoH, structure);

                hotfixesCpp.WriteLine("}");
                hotfixesCpp.WriteLine();
                hotfixesCpp.WriteLine("HotfixDatabaseConnection::HotfixDatabaseConnection(MySQLConnectionInfo& connInfo) : MySQLConnection(connInfo)");
                hotfixesCpp.WriteLine("{");
                hotfixesCpp.WriteLine("}");
                hotfixesCpp.WriteLine();
                hotfixesCpp.WriteLine("HotfixDatabaseConnection::HotfixDatabaseConnection(ProducerConsumerQueue<SQLOperation*>* q, MySQLConnectionInfo& connInfo) : MySQLConnection(q, connInfo)");
                hotfixesCpp.WriteLine("{");
                hotfixesCpp.WriteLine("}");
                hotfixesCpp.WriteLine();
                hotfixesCpp.WriteLine("HotfixDatabaseConnection::~HotfixDatabaseConnection()");
                hotfixesCpp.WriteLine("{");
                hotfixesCpp.WriteLine("}");

                hotfixesH.WriteLine("");
                hotfixesH.WriteLine("    MAX_HOTFIXDATABASE_STATEMENTS");
                hotfixesH.WriteLine("};");
                hotfixesH.WriteLine("");
                hotfixesH.WriteLine("uint32 constexpr HOTFIX_MAX_ID_STMT_OFFSET = 1;");
                hotfixesH.WriteLine("uint32 constexpr HOTFIX_LOCALE_STMT_OFFSET = 2;");
                hotfixesH.WriteLine("");
                hotfixesH.WriteLine("class TC_DATABASE_API HotfixDatabaseConnection : public MySQLConnection");
                hotfixesH.WriteLine("{");
                hotfixesH.WriteLine("public:");
                hotfixesH.WriteLine("    typedef HotfixDatabaseStatements Statements;");
                hotfixesH.WriteLine("");
                hotfixesH.WriteLine("    //- Constructors for sync and async connections");
                hotfixesH.WriteLine("    HotfixDatabaseConnection(MySQLConnectionInfo& connInfo);");
                hotfixesH.WriteLine("    HotfixDatabaseConnection(ProducerConsumerQueue<SQLOperation*>* q, MySQLConnectionInfo& connInfo);");
                hotfixesH.WriteLine("    ~HotfixDatabaseConnection();");
                hotfixesH.WriteLine("");
                hotfixesH.WriteLine("    //- Loads database type specific prepared statements");
                hotfixesH.WriteLine("    void DoPrepareStatements() override;");
                hotfixesH.WriteLine("};");
                hotfixesH.WriteLine("");
                hotfixesH.WriteLine("#endif");

                infoH.WriteLine("#endif // DB2LoadInfo_h__");
            }
        }

        private static void WriteLicense(StreamWriter stream)
        {
            stream.WriteLine("/*");
            stream.WriteLine($" * This file is part of the TrinityCore Project. See AUTHORS file for Copyright information");
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

        private static void DumpStructure(StreamWriter output, StreamWriter hotfixesCpp, StreamWriter hotfixesH, StreamWriter infoH, CStructureInfo structure)
        {
            output.WriteLine("--");
            output.WriteLine($"-- Table structure for table `{structure.GetTableName()}`");
            output.WriteLine("--");
            output.WriteLine();
            output.WriteLine($"DROP TABLE IF EXISTS `{structure.GetTableName()}`;");
            output.WriteLine("/*!40101 SET @saved_cs_client     = @@character_set_client */;");
            output.WriteLine("/*!50503 SET character_set_client = utf8mb4 */;");
            output.WriteLine($"CREATE TABLE `{structure.GetTableName()}` (");

            var cppBuilder = new LimitedLineLengthStringBuilder()
            {
                WrappedLinePrefix = "        \"",
                WrappedLineSuffix = "\""
            };

            var infoBuilder = new StringBuilder();

            if (!structure.IsLocale)
            {
                cppBuilder.AppendLine();
                cppBuilder.AppendLine($"    // {structure.NormalizedName}.db2");
                cppBuilder.Append($"    PrepareStatement(HOTFIX_SEL_{structure.GetTableName().ToUpperInvariant()}");
                hotfixesH.WriteLine();
                infoH.WriteLine($"struct {structure.NormalizedName}LoadInfo");
                infoH.WriteLine("{");
            }
            else
                cppBuilder.Append($"    PREPARE_LOCALE_STMT(HOTFIX_SEL_{structure.GetTableName().ToUpperInvariant().Replace("_LOCALE", "")}");

            cppBuilder.Append(", \"SELECT ");

            var memberCount = 0;
            foreach (var member in structure.Members)
                memberCount += DumpStructureMember(output, cppBuilder, infoBuilder, member);

            if (!structure.IsLocale)
            {
                infoH.WriteLine($"    static constexpr DB2FieldMeta Fields[{memberCount}] =");
                infoH.WriteLine("    {");
            }

            cppBuilder.Remove(cppBuilder.Length - 2, 2);
            if (!structure.GetTableName().IsSqlKeyword())
                cppBuilder.Append($" FROM {structure.GetTableName()}");
            else
                cppBuilder.Append($" FROM `{structure.GetTableName()}`");

            cppBuilder.Append(" WHERE (`VerifiedBuild` > 0) = ?");

            if (!structure.IsLocale)
                output.WriteLine($"  PRIMARY KEY (`ID`,`VerifiedBuild`)");
            else
            {
                cppBuilder.Append(" AND locale = ?");
                output.WriteLine("  PRIMARY KEY (`ID`,`locale`,`VerifiedBuild`)");
            }

            output.Write(") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
            if (!structure.IsLocale)
                output.WriteLine(";");
            else
            {
                output.WriteLine();
                output.WriteLine("/*!50500 PARTITION BY LIST  COLUMNS(locale)");
                output.WriteLine("(PARTITION deDE VALUES IN ('deDE') ENGINE = InnoDB,");
                output.WriteLine(" PARTITION esES VALUES IN ('esES') ENGINE = InnoDB,");
                output.WriteLine(" PARTITION esMX VALUES IN ('esMX') ENGINE = InnoDB,");
                output.WriteLine(" PARTITION frFR VALUES IN ('frFR') ENGINE = InnoDB,");
                output.WriteLine(" PARTITION itIT VALUES IN ('itIT') ENGINE = InnoDB,");
                output.WriteLine(" PARTITION koKR VALUES IN ('koKR') ENGINE = InnoDB,");
                output.WriteLine(" PARTITION ptBR VALUES IN ('ptBR') ENGINE = InnoDB,");
                output.WriteLine(" PARTITION ruRU VALUES IN ('ruRU') ENGINE = InnoDB,");
                output.WriteLine(" PARTITION zhCN VALUES IN ('zhCN') ENGINE = InnoDB,");
                output.WriteLine(" PARTITION zhTW VALUES IN ('zhTW') ENGINE = InnoDB) */;");
            }

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
            if (!structure.IsLocale)
            {
                if (!structure.GetTableName().IsSqlKeyword())
                    hotfixesCpp.WriteLine($"    PREPARE_MAX_ID_STMT(HOTFIX_SEL_{structure.GetTableName().ToUpperInvariant()}, \"SELECT MAX(ID) + 1 FROM {structure.GetTableName()}\", CONNECTION_SYNCH);");
                else
                    hotfixesCpp.WriteLine($"    PREPARE_MAX_ID_STMT(HOTFIX_SEL_{structure.GetTableName().ToUpperInvariant()}, \"SELECT MAX(ID) + 1 FROM `{structure.GetTableName()}`\", CONNECTION_SYNCH);");

                hotfixesH.WriteLine($"    HOTFIX_SEL_{structure.GetTableName().ToUpperInvariant()}_MAX_ID,");
            }

            if (!structure.IsLocale)
            {
                infoH.Write(infoBuilder.ToString());
                infoH.WriteLine("    };");
                infoH.WriteLine();
                infoH.WriteLine($"    static constexpr DB2LoadInfo Instance{{ Fields, {memberCount}, &{structure.Name}Meta::Instance, HOTFIX_SEL_{structure.GetTableName().ToUpperInvariant().Replace("_LOCALE", "")} }};");
                infoH.WriteLine("};");
                infoH.WriteLine();
            }
        }

        private static int DumpStructureMember(StreamWriter output, LimitedLineLengthStringBuilder query, StringBuilder infoH, CStructureMemberInfo member)
        {
            var memberCount = 0;
            var arraySize = member.ArraySize;

            if (!MySQLTypeMap.TryGetValue(member.FormattedTypeName, out string typeInfo))
                typeInfo = "ERROR TYPE" + member.TypeName;

            if (!DbcFormatEnumTypeMap.TryGetValue(member.FormattedTypeName, out string @enum))
                @enum = "FT_FUCK_YOU";

            if (member.TypeName == "flag128")
            {
                arraySize = 4;
                typeInfo = MySQLTypeMap["int32"];
                @enum = DbcFormatEnumTypeMap["int32"];
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
                    memberCount += DumpStructureMemberName(output, query, infoH, memberName, typeInfo, member.FormattedTypeName, @enum);
                }
                else
                {
                    switch (member.TypeName)
                    {
                        case "DBCPosition3D":
                            memberCount += DumpStructureMemberName(output, query, infoH, memberName + "X", MySQLTypeMap["float"], member.FormattedTypeName, DbcFormatEnumTypeMap["float"]);
                            memberCount += DumpStructureMemberName(output, query, infoH, memberName + "Y", MySQLTypeMap["float"], member.FormattedTypeName, DbcFormatEnumTypeMap["float"]);
                            memberCount += DumpStructureMemberName(output, query, infoH, memberName + "Z", MySQLTypeMap["float"], member.FormattedTypeName, DbcFormatEnumTypeMap["float"]);
                            break;
                        case "DBCPosition2D":
                            memberCount += DumpStructureMemberName(output, query, infoH, memberName + "X", MySQLTypeMap["float"], member.FormattedTypeName, DbcFormatEnumTypeMap["float"]);
                            memberCount += DumpStructureMemberName(output, query, infoH, memberName + "Y", MySQLTypeMap["float"], member.FormattedTypeName, DbcFormatEnumTypeMap["float"]);
                            break;
                        default:
                            output.WriteLine($"  `{memberName}` ERROR TYPE {member.TypeName},");
                            infoH.AppendLine($"        {{ false, {@enum}, \"{memberName}\" }},");
                            ++memberCount;
                            break;
                    }
                }
            }

            return memberCount;
        }

        private static int DumpStructureMemberName(StreamWriter output, LimitedLineLengthStringBuilder query, StringBuilder fieldsMetaH, string memberName, string typeName, string cpptype, string @enum)
        {
            output.WriteLine($"  `{memberName}` {typeName},");
            if (memberName != "VerifiedBuild" && memberName != "locale")
            {
                if (!memberName.IsSqlKeyword())
                    query.Append($"{memberName}, ");
                else
                    query.Append($"`{memberName}`, ");

                fieldsMetaH.AppendLine($"        {{ {SignedIntRegex.IsMatch(cpptype).ToString().ToLowerInvariant()}, {@enum}, \"{memberName}\" }},");
                return 1;
            }

            return 0;
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
                case "ORDER":
                case "SYSTEM":
                case "RANK":
                case "TEXT":
                    return true;
            }

            return false;
        }
    }
}
