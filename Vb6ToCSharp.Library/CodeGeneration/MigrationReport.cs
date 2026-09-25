using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Vb6ToCSharp.CodeGeneration.Model;
using static Vb6ToCSharp.Parsing.ProjectConfigurationParser;


namespace Vb6ToCSharp.CodeGeneration;

/// <summary>
/// Migration report (as VB Migration Partner's): what the conversion could not settle, per category and per file, with
/// file:line links into the generated code. Written next to the converted project.
/// </summary>
public static class MigrationReport
{
    public const string ReportFile = "MigrationReport.md";

    // most specific first (a pragma message may name any other feature); whole words only ("prevents" is no event)
    private static readonly (string Category, string Pattern)[] Categories =
    {
        ("Pragmas", "\\bpragma\\b"),
        ("GoSub", "\\bGoSub\\b"),
        ("Error handling", "\\bResume\\b|\\bOn Error\\b|\\bError "),
        ("Arrays", "lower bound|\\bArray\\b|\\bReDim\\b"),
        ("Events", "\\bWithEvents\\b|handler signatures|\\bevents?\\b"),
        ("Interfaces", "\\bImplements\\b"),
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
        foreach (var path in SourceFiles(folder))
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

    /// <summary>
    /// The converted C# of an output folder: what a build leaves behind in bin\ and obj\ is generated, not converted,
    /// and neither its count nor its own TODO comments belong in the report.
    /// </summary>
    private static IEnumerable<string> SourceFiles(string folder)
    {
        if (!Directory.Exists(folder)) return Enumerable.Empty<string>();
        var build = new[] { "\\bin\\", "\\obj\\" };
        return Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Where(p => !build.Any(b => p.Substring(folder.Length).Contains(b, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Writes the report of the converted project into its output folder; returns the report path.</summary>
    public static string Write(string folder = null)
    {
        folder = folder ?? OutputFolder();
        var files = SourceFiles(folder).Count();
        var path = Path.Combine(folder, ReportFile);
        File.WriteAllText(path, Render(Collect(folder), files, Path.GetFileNameWithoutExtension(VbpFile)));
        return path;
    }
}