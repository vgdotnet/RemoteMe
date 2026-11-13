using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace TypeLibDump;

internal static class Program {
    private const int RegKindNone = 2;

    [DllImport("oleaut32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    [return: MarshalAs(UnmanagedType.Interface)]
    private static extern ITypeLib LoadTypeLibEx(string file, int regKind);

    private static int Main(string[] args) {
        if (args.Length != 2) {
            Console.Error.WriteLine("Usage: TypeLibDump <dll-or-tlb> <output-file>");
            return 1;
        }

        var typeLib = LoadTypeLibEx(args[0], RegKindNone);
        var output = new StringBuilder();
        DumpLibrary(typeLib, output);
        File.WriteAllText(args[1], output.ToString());
        Console.WriteLine($"Written {args[1]}");
        return 0;
    }

    private static void DumpLibrary(ITypeLib typeLib, StringBuilder output) {
        typeLib.GetDocumentation(-1, out var libName, out _, out _, out _);
        typeLib.GetLibAttr(out var libAttrPtr);
        var libAttr = Marshal.PtrToStructure<TYPELIBATTR>(libAttrPtr);
        typeLib.ReleaseTLibAttr(libAttrPtr);
        output.AppendLine($"library {libName} {{{libAttr.guid}}} {libAttr.wMajorVerNum}.{libAttr.wMinorVerNum}");

        var count = typeLib.GetTypeInfoCount();
        for (var i = 0; i < count; i++) {
            typeLib.GetTypeInfo(i, out var typeInfo);
            output.AppendLine();
            DumpType(typeInfo, output);
        }
    }

    private static void DumpType(ITypeInfo typeInfo, StringBuilder output) {
        typeInfo.GetDocumentation(-1, out var name, out _, out _, out _);
        typeInfo.GetTypeAttr(out var attrPtr);
        var attr = Marshal.PtrToStructure<TYPEATTR>(attrPtr);
        try {
            output.AppendLine($"{attr.typekind} {name} {{{attr.guid}}} flags={attr.wTypeFlags} vtbl={attr.cbSizeVft}");
            for (var i = 0; i < attr.cImplTypes; i++) {
                typeInfo.GetRefTypeOfImplType(i, out var href);
                typeInfo.GetRefTypeInfo(href, out var implInfo);
                implInfo.GetDocumentation(-1, out var implName, out _, out _, out _);
                typeInfo.GetImplTypeFlags(i, out var implFlags);
                output.AppendLine($"  implements {implName} [{implFlags}]");
            }

            switch (attr.typekind) {
                case TYPEKIND.TKIND_DISPATCH when attr.wTypeFlags.HasFlag(TYPEFLAGS.TYPEFLAG_FDUAL):
                    DumpFunctions(typeInfo, attr, output, "  ");
                    typeInfo.GetRefTypeOfImplType(-1, out var vtblHref);
                    typeInfo.GetRefTypeInfo(vtblHref, out var vtblInfo);
                    output.AppendLine("  vtable view:");
                    DumpVtable(vtblInfo, output);
                    break;
                case TYPEKIND.TKIND_DISPATCH:
                case TYPEKIND.TKIND_INTERFACE:
                    DumpFunctions(typeInfo, attr, output, "  ");
                    break;
                case TYPEKIND.TKIND_ENUM:
                case TYPEKIND.TKIND_RECORD:
                    DumpVariables(typeInfo, attr, output);
                    break;
                case TYPEKIND.TKIND_ALIAS:
                    output.AppendLine($"  = {TypeName(typeInfo, attr.tdescAlias)}");
                    break;
            }
        } finally {
            typeInfo.ReleaseTypeAttr(attrPtr);
        }
    }

    private static void DumpVtable(ITypeInfo typeInfo, StringBuilder output) {
        typeInfo.GetTypeAttr(out var attrPtr);
        var attr = Marshal.PtrToStructure<TYPEATTR>(attrPtr);
        try {
            DumpFunctions(typeInfo, attr, output, "    ");
        } finally {
            typeInfo.ReleaseTypeAttr(attrPtr);
        }
    }

    private static void DumpFunctions(ITypeInfo typeInfo, TYPEATTR attr, StringBuilder output, string indent) {
        for (var i = 0; i < attr.cFuncs; i++) {
            typeInfo.GetFuncDesc(i, out var funcPtr);
            var func = Marshal.PtrToStructure<FUNCDESC>(funcPtr);
            try {
                var names = new string[func.cParams + 1];
                typeInfo.GetNames(func.memid, names, names.Length, out var nameCount);
                var parameters = new List<string>();
                var elemSize = Marshal.SizeOf<ELEMDESC>();
                for (var p = 0; p < func.cParams; p++) {
                    var elem = Marshal.PtrToStructure<ELEMDESC>(func.lprgelemdescParam + p * elemSize);
                    var paramName = p + 1 < nameCount ? names[p + 1] : $"p{p}";
                    var flags = elem.desc.paramdesc.wParamFlags;
                    parameters.Add($"[{FormatParamFlags(flags)}] {TypeName(typeInfo, elem.tdesc)} {paramName}");
                }

                var returnType = TypeName(typeInfo, func.elemdescFunc.tdesc);
                output.AppendLine(
                    $"{indent}vft={func.oVft / IntPtr.Size,-3} dispid=0x{func.memid:X8} {func.invkind,-25} {returnType} {names[0]}({string.Join(", ", parameters)})");
            } finally {
                typeInfo.ReleaseFuncDesc(funcPtr);
            }
        }
    }

    private static void DumpVariables(ITypeInfo typeInfo, TYPEATTR attr, StringBuilder output) {
        for (var i = 0; i < attr.cVars; i++) {
            typeInfo.GetVarDesc(i, out var varPtr);
            var variable = Marshal.PtrToStructure<VARDESC>(varPtr);
            try {
                var names = new string[1];
                typeInfo.GetNames(variable.memid, names, 1, out _);
                var value = variable.varkind == VARKIND.VAR_CONST
                    ? " = " + Marshal.GetObjectForNativeVariant(variable.desc.lpvarValue)
                    : "";
                output.AppendLine($"  {TypeName(typeInfo, variable.elemdescVar.tdesc)} {names[0]}{value}");
            } finally {
                typeInfo.ReleaseVarDesc(varPtr);
            }
        }
    }

    private static string FormatParamFlags(PARAMFLAG flags) {
        var parts = new List<string>();
        if (flags.HasFlag(PARAMFLAG.PARAMFLAG_FIN)) {
            parts.Add("in");
        }
        if (flags.HasFlag(PARAMFLAG.PARAMFLAG_FOUT)) {
            parts.Add("out");
        }
        if (flags.HasFlag(PARAMFLAG.PARAMFLAG_FRETVAL)) {
            parts.Add("retval");
        }
        if (flags.HasFlag(PARAMFLAG.PARAMFLAG_FOPT)) {
            parts.Add("optional");
        }
        return string.Join(",", parts);
    }

    private static string TypeName(ITypeInfo typeInfo, TYPEDESC desc) {
        var vt = (VarEnum)desc.vt;
        switch (vt) {
            case VarEnum.VT_PTR:
                return TypeName(typeInfo, Marshal.PtrToStructure<TYPEDESC>(desc.lpValue)) + "*";
            case VarEnum.VT_SAFEARRAY:
                return "SAFEARRAY(" + TypeName(typeInfo, Marshal.PtrToStructure<TYPEDESC>(desc.lpValue)) + ")";
            case VarEnum.VT_USERDEFINED:
                typeInfo.GetRefTypeInfo((int)desc.lpValue, out var refInfo);
                refInfo.GetDocumentation(-1, out var refName, out _, out _, out _);
                return refName;
            default:
                return vt.ToString()[3..];
        }
    }
}
