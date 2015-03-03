
namespace WowClientDB2MySQLTableGenerator
{
    public sealed class CStructureMemberInfo
    {
        public string TypeName { get; set; }
        public string Name { get; set; }

        public override string ToString()
        {
            return TypeName + ' ' + Name + ';';
        }
    }
}
