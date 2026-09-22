using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Vb6ToCSharp.FormConversion;
using static Vb6ToCSharp.Modules.ModConfig;
using static Vb6ToCSharp.Modules.ModUtils;


namespace Vb6ToCSharp.Modules;

/// <summary>A VB6 project group (.vbg): its projects, the startup project first.</summary>
public sealed class VbgInfo
{
    public string Path { get; private set; } = "";
    public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);

    /// <summary>Full paths of the group's .vbp files: the startup project first, then in file order.</summary>
    public List<string> Projects { get; } = new();

    public static VbgInfo Load(string path)
    {
        var full = System.IO.Path.GetFullPath(path);
        var res = Parse(File.Exists(full) ? File.ReadAllText(full, Encoding.Default) : "", System.IO.Path.GetDirectoryName(full) ?? "");
        res.Path = full;
        return res;
    }

    /// <summary>Reads <c>StartupProject=</c> / <c>Project=</c> lines; paths are relative to <paramref name="folder"/>.</summary>
    public static VbgInfo Parse(string text, string folder)
    {
        var res = new VbgInfo();
        string startup = null;
        foreach (var raw in (text ?? "").Replace("\r\n", "\n").Split('\n'))
        {
            var l = raw.Trim();
            var eq = l.IndexOf('=');
            if (eq <= 0) continue;
            var key = l.Substring(0, eq).Trim();
            var value = l.Substring(eq + 1).Trim().Trim('"');
            if (value == "") continue;
            var isStartup = key.Equals("StartupProject", StringComparison.OrdinalIgnoreCase);
            if (!isStartup && !key.Equals("Project", StringComparison.OrdinalIgnoreCase)) continue;
            var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(folder, value));
            if (isStartup) startup = path;
            if (!res.Projects.Contains(path, StringComparer.OrdinalIgnoreCase)) res.Projects.Add(path);
        }
        if (startup != null)
        {
            res.Projects.RemoveAll(p => p.Equals(startup, StringComparison.OrdinalIgnoreCase));
            res.Projects.Insert(0, startup);
        }
        return res;
    }
}

/// <summary>
/// Project groups and what VB6 projects see of each other: a .vbg becomes a .sln with one .csproj per .vbp
/// (<c>&lt;out&gt;\&lt;Name&gt;\</c>), project references become ProjectReferences, and an ActiveX project exposes only
/// its public classes and user controls.
/// </summary>
public static class ModProjectGroup
{
    public const string GroupExtension = ".vbg";

    /// <summary>SDK-style C# project type in a .sln.</summary>
    private const string CSharpProjectType = "{9A19103F-16F7-4668-BE54-9A1E7A4F7556}";

    /// <summary>The projects of the group being converted (null outside <see cref="ConvertGroup"/>).</summary>
    public static IReadOnlyList<VbpInfo> Current { get; private set; }

    public static bool IsGroupFile(string path) =>
        string.Equals(Path.GetExtension(path ?? ""), GroupExtension, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Converts every project of the group into <c>&lt;output folder&gt;\&lt;project name&gt;\</c> (assembly and root
    /// namespace = the .vbp's Name) and writes <c>&lt;group&gt;.sln</c> next to them; returns the solution's path.
    /// </summary>
    public static string ConvertGroup(string vbgFile)
    {
        var group = VbgInfo.Load(vbgFile);
        if (group.Projects.Count == 0) throw new InvalidOperationException("The project group lists no projects: " + group.Path);
        var missing = group.Projects.FirstOrDefault(p => !File.Exists(p));
        if (missing != null) throw new FileNotFoundException("Project of the group not found: " + missing, missing);
        var projects = group.Projects.Select(VbpInfo.Load).ToList();
        var twice = projects.GroupBy(ProjectName, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (twice != null) throw new InvalidOperationException("Two projects of the group are named " + twice.Key + ".");
        if (AssemblyNameOverridden) Notify("Assembly name ignored for a project group: each project keeps its .vbp name.");

        var root = OutputFolder(); // the group's own output folder (default: converted\ next to the .vbg)
        Current = projects;
        referencesKey = null;
        try
        {
            foreach (var p in projects)
            {
                using (ProjectScope(p.Path, root + ProjectName(p) + "\\", ProjectName(p)))
                {
                    ModConvert.ConvertSingleProject(p.Path);
                }
            }
            var sln = root + group.Name + ".sln";
            File.WriteAllText(sln, SolutionFile(group.Name, projects), new UTF8Encoding(true));
            return sln;
        }
        finally
        {
            Current = null;
            referencesKey = null;
        }
    }

    /// <summary>The .vbp's Name (its file name when unset): folder, assembly and root namespace of the converted project.</summary>
    public static string ProjectName(VbpInfo vbp) => vbp.Name != "" ? vbp.Name : Path.GetFileNameWithoutExtension(vbp.Path);

    /// <summary>A group project's .csproj, relative to the group's output folder (the .csproj is named after the .vbp file).</summary>
    public static string ProjectFilePath(VbpInfo vbp) => ProjectName(vbp) + "\\" + Path.GetFileNameWithoutExtension(vbp.Path) + ".csproj";

    /// <summary>Visual Studio solution of the converted group; the startup project comes first (Visual Studio starts it).</summary>
    public static string SolutionFile(string groupName, IEnumerable<VbpInfo> projects)
    {
        var list = projects.ToList();
        var n = "\r\n";
        var s = new StringBuilder();
        s.Append(n + "Microsoft Visual Studio Solution File, Format Version 12.00" + n);
        s.Append("# Visual Studio Version 17" + n);
        s.Append("VisualStudioVersion = 17.0.31903.59" + n);
        s.Append("MinimumVisualStudioVersion = 10.0.40219.1" + n);
        foreach (var p in list)
        {
            s.Append("Project(\"" + CSharpProjectType + "\") = \"" + ProjectName(p) + "\", \"" + ProjectFilePath(p) + "\", \"" + ProjectGuid(groupName, p) + "\"" + n);
            s.Append("EndProject" + n);
        }
        s.Append("Global" + n);
        s.Append("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution" + n);
        s.Append("\t\tDebug|Any CPU = Debug|Any CPU" + n);
        s.Append("\t\tRelease|Any CPU = Release|Any CPU" + n);
        s.Append("\tEndGlobalSection" + n);
        s.Append("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution" + n);
        foreach (var p in list)
        {
            var g = ProjectGuid(groupName, p);
            foreach (var c in new[] { "Debug", "Release" })
            {
                s.Append("\t\t" + g + "." + c + "|Any CPU.ActiveCfg = " + c + "|Any CPU" + n);
                s.Append("\t\t" + g + "." + c + "|Any CPU.Build.0 = " + c + "|Any CPU" + n);
            }
        }
        s.Append("\tEndGlobalSection" + n);
        s.Append("\tGlobalSection(SolutionProperties) = preSolution" + n);
        s.Append("\t\tHideSolutionNode = FALSE" + n);
        s.Append("\tEndGlobalSection" + n);
        s.Append("EndGlobal" + n);
        return s.ToString();
    }

    /// <summary>A stable project GUID (the same on every conversion of the group).</summary>
    public static string ProjectGuid(string groupName, VbpInfo vbp)
    {
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(groupName.ToUpperInvariant() + "|" + ProjectName(vbp).ToUpperInvariant()));
        return "{" + new Guid(hash).ToString("D").ToUpperInvariant() + "}";
    }

    private static string referencesKey;
    private static List<VbpInfo> referencesCache;

    /// <summary>Forgets the project references read so far (a new conversion run: the .vbp may have changed).</summary>
    public static void ResetCaches() => referencesKey = null;

    /// <summary>
    /// The VB6 projects <paramref name="vbp"/> references: source references (<c>*\A</c>, read even outside a group) and,
    /// in a group, compiled references (<c>*\G</c>) whose file is another group project's output.
    /// </summary>
    public static List<VbpInfo> ReferencedProjects(VbpInfo vbp)
    {
        var key = vbp.Path + "|" + (Current == null ? "" : RuntimeHelpersHash(Current));
        if (referencesKey == key) return referencesCache;
        var r = new List<VbpInfo>();
        foreach (var path in vbp.ProjectReferences)
        {
            var p = Current?.FirstOrDefault(g => SamePath(g.Path, path)) ?? (File.Exists(path) ? VbpInfo.Load(path) : null);
            if (p != null && !SamePath(p.Path, vbp.Path) && !r.Any(x => SamePath(x.Path, p.Path))) r.Add(p);
        }
        if (Current != null)
        {
            foreach (var file in vbp.CompiledReferences)
            {
                var p = Current.FirstOrDefault(g => !SamePath(g.Path, vbp.Path) && string.Equals(CompiledName(g), file, StringComparison.OrdinalIgnoreCase));
                if (p != null && !r.Any(x => SamePath(x.Path, p.Path))) r.Add(p);
            }
        }
        referencesKey = key;
        referencesCache = r;
        return r;
    }

    /// <summary>The group projects that reference <paramref name="vbp"/> (none outside a group).</summary>
    public static IEnumerable<VbpInfo> ReferencingProjects(VbpInfo vbp)
    {
        if (Current == null) return Enumerable.Empty<VbpInfo>();
        var referencing = new List<VbpInfo>();
        foreach (var g in Current)
        {
            if (SamePath(g.Path, vbp.Path)) continue;
            if (g.ProjectReferences.Any(p => SamePath(p, vbp.Path))
                || g.CompiledReferences.Any(f => string.Equals(f, CompiledName(vbp), StringComparison.OrdinalIgnoreCase)))
            {
                referencing.Add(g);
            }
        }
        return referencing;
    }

    /// <summary>ProjectReference paths (relative to the project's own folder) of the group projects <paramref name="vbp"/> references.</summary>
    public static List<string> CSharpProjectReferences(VbpInfo vbp)
    {
        if (Current == null) return new List<string>();
        return ReferencedProjects(vbp).Where(p => Current.Any(g => SamePath(g.Path, p.Path))).Select(p => "..\\" + ProjectFilePath(p)).ToList();
    }

    /// <summary>
    /// <c>Lib.CFoo</c> → <c>CFoo</c> when <c>Lib</c> is this project or one it references: converted classes live in the
    /// global namespace. Other qualified names (type libraries: <c>ADODB.Recordset</c>) are kept.
    /// </summary>
    public static string StripProjectQualifier(string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return typeName;
        var m = Regex.Match(typeName, "^\\s*([A-Za-z_][A-Za-z0-9_]*)\\.([A-Za-z_][A-Za-z0-9_]*)\\s*$");
        if (!m.Success) return typeName;
        var qualifier = m.Groups[1].Value;
        var vbp = ModConvert.ProjectInfo();
        bool Named(VbpInfo p) => string.Equals(ProjectName(p), qualifier, StringComparison.OrdinalIgnoreCase);
        return (vbp.Path != "" && Named(vbp)) || ReferencedProjects(vbp).Any(Named) ? m.Groups[2].Value : typeName;
    }

    /// <summary>
    /// C# accessibility of a converted module, class, form or user control: in an ActiveX project only exposed ones
    /// (<c>VB_Exposed = True</c>) are public, as in VB6; in any other project everything stays public.
    /// </summary>
    public static string TypeModifier(bool exposed) => ModConvert.ProjectInfo().ExposesTypes && !exposed ? "internal" : "public";

    /// <summary>Whether a class / user control source says <c>Attribute VB_Exposed = True</c>.</summary>
    public static bool IsExposed(string vbSource) => Regex.IsMatch(vbSource ?? "", "(?mi)^\\s*Attribute\\s+VB_Exposed\\s*=\\s*True\\b");

    /// <summary>Whether a parsed form / user control is exposed (<c>Attribute VB_Exposed = True</c>).</summary>
    public static bool IsExposed(VbFormFile file) =>
        file != null && file.Attributes.TryGetValue("VB_Exposed", out var v) && v.Trim().Equals("True", StringComparison.OrdinalIgnoreCase);

    /// <summary>File a VB6 project compiles to (its ExeName32, else Name + the type's extension).</summary>
    private static string CompiledName(VbpInfo vbp)
    {
        if (vbp.ExeName32 != "") return Path.GetFileName(vbp.ExeName32);
        var ext = Regex.IsMatch(vbp.Type ?? "", "^OleDll$", RegexOptions.IgnoreCase) ? ".dll"
            : Regex.IsMatch(vbp.Type ?? "", "^Control$", RegexOptions.IgnoreCase) ? ".ocx" : ".exe";
        return ProjectName(vbp) + ext;
    }

    private static bool SamePath(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        try
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string RuntimeHelpersHash(object o) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o).ToString();
}
