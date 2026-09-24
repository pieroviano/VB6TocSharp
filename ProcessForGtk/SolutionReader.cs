using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ProcessForGtk;

/// <summary>Lists the project files a solution (<c>.sln</c>) or an XML solution (<c>.slnx</c>) contains.</summary>
internal static class SolutionReader
{
    /// <summary>A <c>Project("{type}") = "name", "path", "{id}"</c> entry of a classic <c>.sln</c>.</summary>
    private static readonly Regex SlnProjectEntry =
        new(@"^Project\(""\{[^}]*\}""\)\s*=\s*""[^""]*""\s*,\s*""(?<path>[^""]*)""",
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>True when the path names a project file this tool can rewrite (<c>.*proj</c>).</summary>
    internal static bool IsProjectFile(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Length > 4
            && extension.EndsWith("proj", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".shproj", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>True when the path names a solution this tool can expand.</summary>
    internal static bool IsSolutionFile(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The absolute paths of the projects of a solution, solution folders excluded.</summary>
    internal static IReadOnlyList<string> ReadProjects(string solutionPath)
    {
        string folder = Path.GetDirectoryName(Path.GetFullPath(solutionPath)) ?? string.Empty;
        IEnumerable<string> relative =
            Path.GetExtension(solutionPath).Equals(".slnx", StringComparison.OrdinalIgnoreCase)
                ? ReadSlnx(solutionPath)
                : ReadSln(solutionPath);

        var projects = new List<string>();
        foreach (string entry in relative)
        {
            if (!IsProjectFile(entry))
            {
                continue;
            }

            string full = Path.GetFullPath(Path.Combine(folder, entry.Replace('\\', Path.DirectorySeparatorChar)));
            if (!projects.Contains(full, StringComparer.OrdinalIgnoreCase))
            {
                projects.Add(full);
            }
        }

        return projects;
    }

    private static IEnumerable<string> ReadSln(string solutionPath)
    {
        foreach (Match match in SlnProjectEntry.Matches(File.ReadAllText(solutionPath)))
        {
            yield return match.Groups["path"].Value;
        }
    }

    /// <summary>Every <c>&lt;Project Path="..."/&gt;</c> of a <c>.slnx</c>, at any folder depth.</summary>
    private static IEnumerable<string> ReadSlnx(string solutionPath)
    {
        foreach (XElement element in XDocument.Load(solutionPath).Descendants("Project"))
        {
            string? path = (string?)element.Attribute("Path");
            if (!string.IsNullOrWhiteSpace(path))
            {
                yield return path!;
            }
        }
    }
}
