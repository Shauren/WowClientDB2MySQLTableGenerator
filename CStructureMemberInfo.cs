namespace WowClientDB2MySQLTableGenerator
{
    public sealed class CStructureMemberInfo
    {
        public string TypeName { get; set; }
        public string Name { get; set; }
        public long ArraySize { get; set; } = 1L;

        public string FormattedTypeName { get { return TypeName.Replace("const", "").Replace(" ", ""); } }

        public override string ToString()
        {
            return TypeName + ' ' + Name + (ArraySize > 1 ? $"[{ArraySize}];" : ";");
        }
    }
}
