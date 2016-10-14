using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WowClientDB2MySQLTableGenerator
{
    public sealed class CStructureInfo
    {
        public string Name { get; set; }
        public string NormalizedName { get; set; }
        public List<CStructureMemberInfo> Members { get; set; } = new List<CStructureMemberInfo>();
        public bool IsLocale { get; set; }

        public override string ToString()
        {
            return "struct " + NormalizedName;
        }

        public string GetTableName()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < NormalizedName.Length; ++i)
            {
                char c = NormalizedName[i];
                if (char.IsUpper(c) && i > 0)
                    sb.Append('_');

                sb.Append(char.ToLower(c));
            }

            return sb.ToString();
        }

        public CStructureInfo CreateLocaleTable()
        {
            var stringFields = Members.Where(m => m.FormattedTypeName == "LocalizedString*")
                .Select(m =>
                {
                    var m2 = new CStructureMemberInfo()
                    {
                        TypeName = m.TypeName,
                        Name = m.Name.Replace("_lang", "").Replace("_loc", ""),
                        ArraySize = m.ArraySize
                    };

                    var indexOfArray = m.Name.LastIndexOf('[');
                    if (indexOfArray != -1)
                        m2.Name = m2.Name.Insert(indexOfArray, "_lang");
                    else
                        m2.Name += "_lang";

                    return m2;
                }).ToList();

            if (stringFields.Count == 0)
                return null;

            stringFields.Insert(0, new CStructureMemberInfo()
            {
                TypeName = "uint32",
                Name = "ID"
            });
            stringFields.Insert(1, new CStructureMemberInfo()
            {
                TypeName = "char[4]",
                Name = "locale"
            });
            stringFields.Add(new CStructureMemberInfo()
            {
                TypeName = "int16",
                Name = "VerifiedBuild"
            });

            var localeStruct = new CStructureInfo();
            localeStruct.NormalizedName = NormalizedName + "Locale";
            localeStruct.Members = stringFields;
            localeStruct.IsLocale = true;
            return localeStruct;
        }
    }
}
