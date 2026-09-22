using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using static Vb6ToCSharp.Modules.ModConfig;


namespace Vb6ToCSharp.Modules;

/// <summary>
/// Migration report (as VB Migration Partner's): what the conversion could not settle, per category and per file, with
/// file:line links into the generated code. Written next to the converted project.
/// </summary>
public static class ModMigrationReport
{
    public const string ReportFile = "MigrationReport.md";

    public sealed class Issue
    {
        public string File = "";
        public int Line;
        public string Category = "";
        public string Message = "";
    }

    private static readonly (string Category, string Pattern)[] Categories =
    {
        ("GoSub", "GoSub"),
        ("Error handling", "Resume|On Error|Error "),
        ("Arrays", "lower bound|Array|ReDim"),
        ("Events", "WithEvents|handler signatures|event"),
        ("Interfaces", "Implements"),
        ("Pragmas", "pragma"),
        ("Conditional compilation", "#Const|#If|condition|DefineConstants"),
        ("Variables", "Static local|implicit|Fixed Length|fixed-length|DefInt|DefLng|DefStr"),
        ("Graphics", "Step \\(|\\bBF\\b|omitted argument"),
        ("Kept as VB6", "^VB6: "),
    };

    /// <summary>Category of a report line (the first matching one; Other otherwise).</summary>
    public static string Category(string message)
    {
        foreach (var c in Categories)
        {
            if (Regex.IsMatch(message, c.Pattern, RegexOptions.IgnoreCase)) return c.Category;
        }
        return "Other";
    }

    /// <summary>TODO comments and code kept as VB6 (ParseMode Off) in the generated C# files under <paramref name="folder"/>.</summary>
    public static List<Issue> Collect(string folder)
    {
        var r = new List<Issue>();
        if (!Directory.Exists(folder)) return r;
        foreach (var path in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var rel = path.Substring(folder.Length).TrimStart('\\', '/').Replace('\\', '/');
            var lines = File.ReadAllLines(path);
            for (var i = 0; i < lines.Length; i++)
            {
                var m = Regex.Match(lines[i], "//\\s*(TODO\\b[\\s:-]*|VB6: )(.*)$");
                if (!m.Success) continue;
                var message = (m.Groups[1].Value.StartsWith("VB6") ? "VB6: " : "") + m.Groups[2].Value.Trim();
                r.Add(new Issue { File = rel, Line = i + 1, Category = Category(message), Message = message });
            }
        }
        return r;
    }

    /// <summary>The report as Markdown.</summary>
    public static string Render(List<Issue> issues, int files, string project)
    {
        string Cell(string s) => s.Replace("|", "\\|");
        var b = new StringBuilder();
        b.AppendLine("# Migration report: " + project);
        b.AppendLine();
        b.AppendLine(files + " C# files, " + issues.Count + " items to review.");
        if (issues.Count == 0) return b.ToString();
        b.AppendLine();
        b.AppendLine("## By category");
        b.AppendLine();
        b.AppendLine("| Category | Items |");
        b.AppendLine("|---|---|");
        foreach (var g in issues.GroupBy(i => i.Category).OrderByDescending(g => g.Count()).ThenBy(g => g.Key)) b.AppendLine("| " + g.Key + " | " + g.Count() + " |");
        b.AppendLine();
        b.AppendLine("## By file");
        b.AppendLine();
        b.AppendLine("| File | Items |");
        b.AppendLine("|---|---|");
        foreach (var g in issues.GroupBy(i => i.File)) b.AppendLine("| [" + g.Key + "](" + g.Key + ") | " + g.Count() + " |");
        b.AppendLine();
        b.AppendLine("## Items");
        b.AppendLine();
        b.AppendLine("| Location | Category | Item |");
        b.AppendLine("|---|---|---|");
        foreach (var i in issues) b.AppendLine("| [" + i.File + ":" + i.Line + "](" + i.File + "#L" + i.Line + ") | " + i.Category + " | " + Cell(i.Message) + " |");
        return b.ToString();
    }

    /// <summary>Writes the report of the converted project into its output folder; returns the report path.</summary>
    public static string Write(string folder = null)
    {
        folder = folder ?? OutputFolder();
        var files = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories).Length : 0;
        var path = Path.Combine(folder, ReportFile);
        File.WriteAllText(path, Render(Collect(folder), files, Path.GetFileNameWithoutExtension(VbpFile)));
        return path;
    }
}
