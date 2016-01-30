using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WowClientDB2MySQLTableGenerator
{
    public sealed class HeaderParser
    {
        public HeaderParser(string path)
        {
            _stream = new StreamReader(path);
            Structures = new List<CStructureInfo>();
            ArraySizes = new Dictionary<string, int>();
        }

        private StreamReader _stream;

        public List<CStructureInfo> Structures { get; set; }

        public Dictionary<string, int> ArraySizes { get; set; }

        public void Parse()
        {
            var line = _stream.ReadLine();
            while (line != null)
            {
                var idxOf = line.IndexOf("struct");
                if (idxOf != -1)
                    ParseStructure(line.Substring(idxOf + 7));

                idxOf = line.IndexOf("#define");
                if (idxOf != -1)
                    ParseDefine(line);

                line = _stream.ReadLine();
            }
        }

        public void ParseStructure(string name)
        {
            var structure = new CStructureInfo();
            structure.Name = name.Replace("Entry", "");
            structure.Name = structure.Name.Replace("GameObject", "Gameobject");
            structure.Name = structure.Name.Replace("PvP", "Pvp");
            structure.Name = structure.Name.Replace("QuestXP", "QuestXp");
            structure.Members = new List<CStructureMemberInfo>();
            _stream.ReadLine();
            var line = _stream.ReadLine();
            while (line != "};")
            {
                line = line.Trim();
                var comment = line.IndexOf("//");
                if (comment != -1)
                    line = line.Substring(0, comment);

                line = line.Replace(" const", "");

                if (!line.Contains('('))
                {
                    var tokens = line.Split(' ', ';', '/').Where(t => t.Length != 0).ToArray();
                    if (tokens.Length >= 2)
                    {
                        var member = new CStructureMemberInfo();
                        member.TypeName = tokens[0];
                        member.Name = tokens[1].Replace("_lang", "").Replace("_loc", "");
                        structure.Members.Add(member);
                    }
                }

                line = _stream.ReadLine();
            }

            if (name.Contains("Entry"))
            {
                structure.Members.Add(new CStructureMemberInfo()
                {
                    TypeName = "int16",
                    Name = "VerifiedBuild"
                });
                Structures.Add(structure);
                var localeStruct = structure.CreateLocaleTable();
                if (localeStruct != null)
                    Structures.Add(localeStruct);
            }
        }

        public void ParseDefine(string line)
        {
            var tokens = line.Split(' ').Where(t => t.Length != 0).ToArray();
            if (tokens.Length >= 3)
            {
                int value;
                if (int.TryParse(tokens[2], out value))
                    ArraySizes.Add(string.Format("[{0}]", tokens[1]), value);
            }
        }
    }
}
