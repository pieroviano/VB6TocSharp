using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Vb6ToCSharp.Parsing.Model;

namespace Vb6ToCSharp.Parsing;

/// <summary>Maps .vbp/.frm <c>Object=</c> type libraries to their names and emits the AxHost <c>COMReference</c>s.</summary>
public static class OcxInterop
{
    // Fallback when the OCX is not registered on the converting machine: well-known Microsoft OCX file → type library name.
    private static readonly Dictionary<string, string> knownFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MSCOMCTL.OCX"] = "MSComctlLib", ["MSCOMCT2.OCX"] = "MSComCtl2", ["COMCT332.OCX"] = "ComCtl3",
        ["COMCTL32.OCX"] = "ComctlLib", ["COMCT232.OCX"] = "ComCtl2", ["COMDLG32.OCX"] = "MSComDlg",
        ["MSFLXGRD.OCX"] = "MSFlexGridLib", ["MSHFLXGD.OCX"] = "MSHierarchicalFlexGridLib", ["TABCTL32.OCX"] = "TabDlg",
        ["RICHTX32.OCX"] = "RichTextLib", ["MSCOMM32.OCX"] = "MSCommLib", ["MSWINSCK.OCX"] = "MSWinsockLib",
        ["MSINET.OCX"] = "InetCtlsObjects", ["MSCHRT20.OCX"] = "MSChart20Lib", ["MSDATGRD.OCX"] = "MSDataGridLib",
        ["MSADODC.OCX"] = "MSAdodcLib", ["DBGRID32.OCX"] = "MSDBGrid", ["MSMASK32.OCX"] = "MSMask", ["MCI32.OCX"] = "MCI",
        ["PICCLP32.OCX"] = "PicClip", ["SYSINFO.OCX"] = "SysInfoLib", ["MSDATLST.OCX"] = "MSDataListLib", ["DBLIST32.OCX"] = "MSDBCtls",
    };

    private static readonly Dictionary<string, string> cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Test seam: resolves a registered type library (LIBID, major, minor) to its name, or null.</summary>
    internal static Func<Guid, short, short, string> RegistryLookup = LoadRegTypeLibName;

    /// <summary>Type library name (the <c>Lib</c> of <c>Lib.Class</c> in forms) of an <c>Object=</c> reference, or "".</summary>
    public static string LibraryName(OcxRef o)
    {
        var key = o.Guid + "#" + o.Version;
        if (cache.TryGetValue(key, out var n)) return n;
        n = null;
        if (Guid.TryParse(o.Guid, out var g))
        {
            try { n = RegistryLookup(g, (short)o.VersionMajor, (short)o.VersionMinor); }
            catch { n = null; }
        }
        if (string.IsNullOrEmpty(n)) knownFiles.TryGetValue(System.IO.Path.GetFileName(o.File), out n);
        return cache[key] = n ?? "";
    }

    internal static void ClearCache() => cache.Clear();

    private static string LoadRegTypeLibName(Guid libid, short major, short minor)
    {
        if (LoadRegTypeLib(ref libid, major, minor, 0, out var lib) != 0 || lib == null) return null;
        try
        {
            lib.GetDocumentation(-1, out var name, out _, out _, out _);
            return name;
        }
        finally
        {
            Marshal.ReleaseComObject(lib);
        }
    }

    /// <summary>
    /// The one P/Invoke left in this codebase, deliberately. What is wanted is the type library's
    /// identifier - the <c>Lib</c> of <c>Lib.Class</c> in a .frm, e.g. <c>MSComctlLib</c> - and only
    /// the library itself carries it: the registry holds its display name and its file, not the
    /// identifier. Guessing it from a coclass ProgID would be a heuristic, and a wrong answer here
    /// silently mis-converts every control of that library, so the exact API is used instead.
    /// <see cref="RegistryLookup"/> is the seam that keeps this out of the tests.
    /// </summary>
    [DllImport("oleaut32.dll", PreserveSig = true)]
    private static extern int LoadRegTypeLib(ref Guid rguid, short wVerMajor, short wVerMinor, int lcid, out ITypeLib pptlib);

    /// <summary>
    /// <c>COMReference</c> items (aximp + tlbimp) for the libraries of controls hosted through AxHost.
    /// A reference whose name cannot be resolved is kept when some hosted library is unmatched (never silently dropped).
    /// </summary>
    public static string ComReferences(IEnumerable<OcxRef> objects, ICollection<string> hostedLibraries)
    {
        var sb = new StringBuilder();
        var refs = objects.GroupBy(o => o.Guid).Select(g => g.First()).ToList();
        var named = refs.Select(o => (o, name: LibraryName(o))).ToList();
        var unmatched = hostedLibraries.Any(h => named.All(x => !x.name.Equals(h, StringComparison.OrdinalIgnoreCase)));
        foreach (var (o, name) in named)
        {
            var used = name != "" ? hostedLibraries.Contains(name, StringComparer.OrdinalIgnoreCase) : unmatched;
            if (!used) continue;
            var lib = name != "" ? name : Path.GetFileNameWithoutExtension(o.File);
            foreach (var tool in new[] { "aximp", "tlbimp" })
            {
                sb.Append("    <COMReference Include=\"").Append(tool == "aximp" ? "Ax" + lib : lib).Append("\">\r\n");
                sb.Append("      <Guid>").Append(o.Guid).Append("</Guid>\r\n");
                sb.Append("      <VersionMajor>").Append(o.VersionMajor).Append("</VersionMajor>\r\n");
                sb.Append("      <VersionMinor>").Append(o.VersionMinor).Append("</VersionMinor>\r\n");
                sb.Append("      <Lcid>0</Lcid>\r\n");
                sb.Append("      <WrapperTool>").Append(tool).Append("</WrapperTool>\r\n");
                sb.Append("      <Isolated>False</Isolated>\r\n");
                if (tool == "tlbimp") sb.Append("      <EmbedInteropTypes>False</EmbedInteropTypes>\r\n");
                sb.Append("    </COMReference>\r\n");
            }
        }
        return sb.ToString();
    }
}
