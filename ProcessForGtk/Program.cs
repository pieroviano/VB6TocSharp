using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ProcessForGtk;

/// <summary>
/// Retargets converted projects at Gtk: drops the <c>-windows</c> platform and Windows Forms, and swaps the
/// WinForms UpgradeHelpers package for the Gtk one.
/// </summary>
internal static class Program
{
    private const int ExitOk = 0, ExitFailed = 1, ExitUsage = 2;

    private const string Usage = @"Usage: ProcessForGtk <file> [<file> ...]

  <file>   A project (.csproj, .vbproj, ... any .*proj) or a solution
           (.sln, .slnx); a solution is applied to every project it contains.

Each project is rewritten in place:
  * <TargetFramework(s)> loses its -windows platform (net10.0-windows -> net10.0)
  * <UseWindowsForms> is removed
  * the PackageReference " + ProjectRewriter.WinFormsPackage + @"
    becomes " + ProjectRewriter.GtkPackage + @"
  * a PackageReference to " + ProjectRewriter.GtkWinFormsPackage
                                 + " " + ProjectRewriter.GtkWinFormsVersion + @" is added

A project already in that shape is left untouched.";

    private static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "/?" or "help")
        {
            Console.WriteLine(Usage);
            return args.Length == 0 ? ExitUsage : ExitOk;
        }

        var projects = new List<string>();
        foreach (string argument in args)
        {
            if (!Collect(argument, projects))
            {
                return ExitUsage;
            }
        }

        int changed = 0, failed = 0;
        foreach (string project in projects)
        {
            switch (Process(project))
            {
                case null: failed++; break;
                case true: changed++; break;
            }
        }

        Console.WriteLine($"{projects.Count} project(s): {changed} changed, "
                          + $"{projects.Count - changed - failed} already Gtk, {failed} failed.");
        return failed == 0 ? ExitOk : ExitFailed;
    }

    /// <summary>Adds the projects an argument stands for to <paramref name="projects"/>.</summary>
    /// <returns>False when the argument is not a readable project or solution.</returns>
    private static bool Collect(string argument, List<string> projects)
    {
        string path = Path.GetFullPath(argument);
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"Not found: {argument}");
            return false;
        }

        if (SolutionReader.IsSolutionFile(path))
        {
            projects.AddRange(SolutionReader.ReadProjects(path));
            return true;
        }

        if (SolutionReader.IsProjectFile(path))
        {
            projects.Add(path);
            return true;
        }

        Console.Error.WriteLine($"Not a project or solution: {argument}");
        return false;
    }

    /// <summary>Rewrites one project file in place.</summary>
    /// <returns>True when it changed, false when it was already Gtk, null when it failed.</returns>
    private static bool? Process(string project)
    {
        try
        {
            if (!File.Exists(project))
            {
                Console.Error.WriteLine($"Missing project: {project}");
                return null;
            }

            Encoding encoding = DetectEncoding(project);
            string original = File.ReadAllText(project);
            string rewritten = ProjectRewriter.Rewrite(original);
            if (string.Equals(original, rewritten, StringComparison.Ordinal))
            {
                Console.WriteLine($"unchanged {project}");
                return false;
            }

            File.WriteAllText(project, rewritten, encoding);
            Console.WriteLine($"rewritten {project}");
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"{project}: {exception.Message}");
            return null;
        }
    }

    /// <summary>The file's encoding, so a rewrite keeps (or keeps out) its byte order mark.</summary>
    private static Encoding DetectEncoding(string path)
    {
        var preamble = new byte[3];
        using (FileStream stream = File.OpenRead(path))
        {
            int read = stream.Read(preamble, 0, preamble.Length);
            if (read == 3 && preamble[0] == 0xEF && preamble[1] == 0xBB && preamble[2] == 0xBF)
            {
                return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            }
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    }
}
