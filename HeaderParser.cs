using ClangSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace WowClientDB2MySQLTableGenerator
{
    public sealed class HeaderParser
    {
        [DllImport("libclang.dll", EntryPoint = "clang_parseTranslationUnit2", CallingConvention = CallingConvention.Cdecl)]
        public static extern CXErrorCode parseTranslationUnit2(CXIndex @CIdx,
            [MarshalAs(UnmanagedType.LPStr)] string @source_filename,
            string[] @command_line_args,
            int @num_command_line_args,
            [MarshalAs(UnmanagedType.LPArray)] CXUnsavedFile[] @unsaved_files,
            uint @num_unsaved_files,
            uint @options,
            out CXTranslationUnit @out_TU);

        public string FileName { get; set; }
        public List<CStructureInfo> Structures { get; set; } = new List<CStructureInfo>();

        private StringBuilder CommonTypes = new StringBuilder()
            .AppendLine("#include <stdint.h>")
            .AppendLine("typedef int64_t int64;")
            .AppendLine("typedef int32_t int32;")
            .AppendLine("typedef int16_t int16;")
            .AppendLine("typedef int8_t int8;")
            .AppendLine("typedef uint64_t uint64;")
            .AppendLine("typedef uint32_t uint32;")
            .AppendLine("typedef uint16_t uint16;")
            .AppendLine("typedef uint8_t uint8;")
            .AppendLine("struct LocalizedString;")
            .AppendLine("struct DBCPosition2D { float X, Y; };")
            .AppendLine("struct DBCPosition3D { float X, Y, Z; };")
            .AppendLine("typedef int32 flag128[4];")
            .AppendLine("typedef uint32 BattlegroundBracketId;")
            .AppendLine("#define MAX_ITEM_PROTO_FLAGS 4")
            .AppendLine("#define MAX_ITEM_PROTO_ZONES 2")
            .AppendLine("#define MAX_ITEM_PROTO_SOCKETS 3")
            .AppendLine("#define MAX_ITEM_PROTO_STATS 10")
            .AppendLine("#define MAX_SPELL_AURA_INTERRUPT_FLAGS 2");

        public void Parse()
        {
            var index = clang.createIndex(0, 1);
            var args = new string[]
            {
                "-x",
                "c++"
            };

            // prepare input file
            // comment out existing includes
            // append required typedefs
            var hdr = CommonTypes.Append(File.ReadAllText(FileName).Replace("#include", "//#include")).ToString();

            var unsavedFiles = new CXUnsavedFile[]
            {
                new CXUnsavedFile()
                {
                    Contents = hdr,
                    Filename = FileName,
                    Length = hdr.Length
                }
            };

            CXTranslationUnit translationUnit;
            var result = parseTranslationUnit2(index, FileName, args, args.Length, unsavedFiles, (uint)unsavedFiles.Length, (uint)CXTranslationUnit_Flags.CXTranslationUnit_SkipFunctionBodies, out translationUnit);
            if (result != CXErrorCode.CXError_Success)
                return;

            clang.visitChildren(clang.getTranslationUnitCursor(translationUnit), VisitStruct, new CXClientData(IntPtr.Zero));

            clang.disposeTranslationUnit(translationUnit);
            clang.disposeIndex(index);
        }

        public CXChildVisitResult VisitStruct(CXCursor cursor, CXCursor parent, IntPtr data)
        {
            if (clang.Location_isInSystemHeader(clang.getCursorLocation(cursor)) != 0)
                return CXChildVisitResult.CXChildVisit_Continue;

            if (clang.getCursorKind(cursor) != CXCursorKind.CXCursor_StructDecl)
                return CXChildVisitResult.CXChildVisit_Continue;

            if (!clang.getCursorSpelling(cursor).ToString().Contains("Entry"))
                return CXChildVisitResult.CXChildVisit_Continue;

            var structure = new CStructureInfo();
            structure.Name = clang.getCursorSpelling(cursor).ToString();
            structure.NormalizedName = structure.Name
                .Replace("GameObject", "Gameobject")
                .Replace("PvP", "Pvp")
                .Replace("PVP", "Pvp")
                .Replace("QuestXP", "QuestXp")
                .Replace("WMO", "Wmo")
                .Replace("AddOn", "Addon")
                .Replace("LFG", "Lfg")
                .Replace("_", "");

            var suffixIndex = structure.NormalizedName.LastIndexOf("Entry");
            if (suffixIndex != -1)
                structure.NormalizedName = structure.NormalizedName.Remove(suffixIndex);

            var handle = GCHandle.Alloc(structure);
            clang.visitChildren(cursor, VisitField, new CXClientData(GCHandle.ToIntPtr(handle)));
            handle.Free();

            structure.Members.Add(new CStructureMemberInfo()
            {
                TypeName = "int32",
                Name = "VerifiedBuild"
            });

            Structures.Add(structure);
            var localeStruct = structure.CreateLocaleTable();
            if (localeStruct != null)
                Structures.Add(localeStruct);

            return CXChildVisitResult.CXChildVisit_Continue;
        }

        public CXChildVisitResult VisitField(CXCursor cursor, CXCursor parent, IntPtr data)
        {
            if (clang.getCursorKind(cursor) != CXCursorKind.CXCursor_FieldDecl)
                return CXChildVisitResult.CXChildVisit_Continue;

            GCHandle handle = GCHandle.FromIntPtr(data);
            var structure = (CStructureInfo)handle.Target;

            var cxType = clang.getCursorType(cursor);
            var arraySize = clang.getArraySize(cxType);
            structure.Members.Add(new CStructureMemberInfo()
            {
                TypeName = clang.getTypeSpelling(arraySize != -1 ? clang.getArrayElementType(cxType) : cxType).ToString(),
                Name = clang.getCursorSpelling(cursor).ToString().Replace("_lang", ""),
                ArraySize = Math.Max(arraySize, 1L)
            });

            return CXChildVisitResult.CXChildVisit_Continue;
        }
    }
}
