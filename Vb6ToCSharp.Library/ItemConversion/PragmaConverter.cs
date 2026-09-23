using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Vb6ToCSharp.Convert;
using static Microsoft.VisualBasic.Strings;
using static Vb6ToCSharp.Parsing.ProjectConfigurationParser;
using static Vb6ToCSharp.Modules.ModUtils;

namespace Vb6ToCSharp.ItemConversion;

/// <summary>
/// VB Migration Partner pragmas: '## comments in the VB6 source (or in VBMigrationPartner.pragmas next to the .vbp, for
/// the whole project) that steer the conversion. File-level pragmas stand outside procedures; inside a procedure they
/// act from that point.
/// </summary>
public static class PragmaConverter
{
    /// <summary>File of project-level pragmas, next to the .vbp (as VB Migration Partner).</summary>
    public const string ProjectPragmaFile = "VBMigrationPartner.pragmas";

    /// <summary>A '## pragma line, or null.</summary>
    public static Pragma Parse(string line)
    {
        var m = Regex.Match(line ?? "", "^\\s*'##\\s*(?:([A-Za-z_][A-Za-z0-9_]*)\\.)?([A-Za-z]+)\\b\\s*(.*)$");
        if (!m.Success) return null;
        return new Pragma { Target = m.Groups[1].Value, Name = m.Groups[2].Value, Args = Trim(m.Groups[3].Value) };
    }

    /// <summary>AutoDispose: No (default), Yes (objects of classes with Class_Terminate), Force (every object).</summary>
    public static string AutoDispose = "No";

    /// <summary>SetType: variable name to the VB6 type it is declared with.</summary>
    public static readonly Dictionary<string, string> TypeOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static readonly List<Pragma> postProcess = new List<Pragma>();

    private static string projectFile;
    private static readonly List<Pragma> projectPragmas = new List<Pragma>();

    /// <summary>Forgets the project pragmas read so far (a new conversion run).</summary>
    public static void ResetCaches() => projectFile = null;

    private static List<Pragma> ProjectPragmas()
    {
        var path = FilePath(VbpFile) + ProjectPragmaFile;
        var key = VbpFile + "|" + (System.IO.File.Exists(path) ? System.IO.File.GetLastWriteTimeUtc(path).Ticks.ToString() : "");
        if (projectFile == key) return projectPragmas;
        projectFile = key;
        projectPragmas.Clear();
        try
        {
            if (System.IO.File.Exists(path))
            {
                foreach (var raw in System.IO.File.ReadAllLines(path))
                {
                    var p = Parse(LMatch(Trim(raw), "'##") ? raw : "'## " + raw);
                    if (p != null && Trim(raw) != "") projectPragmas.Add(p);
                }
            }
        }
        catch (Exception)
        {
            // no project
        }
        return projectPragmas;
    }

    /// <summary>File-level pragmas: those before the first procedure (after the project's).</summary>
    private static List<Pragma> FilePragmas(string vbSource)
    {
        var r = new List<Pragma>(ProjectPragmas());
        foreach (var line in (vbSource ?? "").Replace("\r\n", "\n").Split('\n'))
        {
            if (Regex.IsMatch(line, "^(Public |Private |Friend )?(Static )?(Sub|Function|Property (Get|Let|Set)) ")) break;
            var p = Parse(line);
            if (p != null) r.Add(p);
        }
        return r;
    }

    /// <summary>PreProcess "regex", "replacement": applied to the VB6 source before it is converted.</summary>
    public static string PreProcess(string vbSource)
    {
        foreach (var p in FilePragmas(vbSource))
        {
            if (p.Name == "PreProcess" && TryPattern(p.Args, out var find, out var repl)) vbSource = Regex.Replace(vbSource, find, repl);
        }
        return vbSource;
    }

    /// <summary>PostProcess "regex", "replacement": applied to the generated C#.</summary>
    public static string PostProcess(string cs)
    {
        foreach (var p in postProcess)
        {
            if (TryPattern(p.Args, out var find, out var repl)) cs = Regex.Replace(cs, find, repl);
        }
        return cs;
    }

    private static bool TryPattern(string args, out string find, out string repl)
    {
        var m = Regex.Match(args, "^\"((?:[^\"]|\"\")*)\"\\s*,\\s*\"((?:[^\"]|\"\")*)\"");
        find = m.Success ? m.Groups[1].Value.Replace("\"\"", "\"") : "";
        repl = m.Success ? m.Groups[2].Value.Replace("\"\"", "\"") : "";
        return m.Success;
    }

    /// <summary>Starts a file: project and file-level settings (after the per-file options were reset).</summary>
    public static void BeginFile(string vbSource)
    {
        AutoDispose = "No";
        TypeOverrides.Clear();
        postProcess.Clear();
        foreach (var p in FilePragmas(vbSource))
        {
            if (p.Name == "PostProcess") postProcess.Add(p);
            else ApplySetting(p);
        }
    }

    /// <summary>A setting pragma (ArrayBounds, AutoNew, AutoDispose, SetType); false for anything else.</summary>
    public static bool ApplySetting(Pragma p)
    {
        switch (p.Name)
        {
            case "ArrayBounds": // Unchanged (VB6Array for non-zero bounds), ForceZero / Shift (zero-based arrays), VB6Array
                StatementsConverter.ForceZeroBounds = p.Args == "ForceZero" || p.Args == "Shift";
                return true;
            case "AutoNew":
                StatementsConverter.AutoNew = !Regex.IsMatch(p.Args, "^(False|No|0)$", RegexOptions.IgnoreCase);
                return true;
            case "AutoDispose":
                AutoDispose = p.Args == "" ? "Yes" : p.Args;
                return true;
            case "SetType":
                if (p.Target != "") TypeOverrides[p.Target] = p.Args;
                return true;
            case "PreProcess":
            case "PostProcess":
                return true; // file-level, applied around the conversion
            default:
                return false;
        }
    }

    /// <summary>The VB6 type a SetType pragma forces for <paramref name="name"/>, or null.</summary>
    public static string TypeOverride(string name) => TypeOverrides.TryGetValue(name ?? "", out var t) ? t : null;
}