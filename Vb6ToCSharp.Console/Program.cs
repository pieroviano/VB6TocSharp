using System;
using System.Collections.Generic;
using System.IO;
using Vb6ToCSharp.Modules;

namespace Vb6ToCSharp.ConsoleApp;

/// <summary>Command-line front end: the same operations as the WPF main window.</summary>
internal static class Program
{
    private const int ExitOk = 0, ExitFailed = 1, ExitUsage = 2;

    private const string Usage = @"Usage: Vb6ToCSharp.Console <command> [arguments] [options]

Commands:
  all                  Scan, generate project/support files and convert the whole project
  forms                Convert the project's forms
  modules              Convert the project's modules
  classes              Convert the project's classes
  file <file>          Convert one .bas/.cls/.frm (a bare name is taken from the project folder)
  scan                 Scan the project's references
  support [project|files]
                       Generate the project file and/or the support files (default: both)
  lint [file]          Lint one file (bare name: project folder) or the whole project
  config               Show the settings; with --vbp/--out/--assembly, save them to the INI
  help                 Show this help

Options:
  --ini <file>         Settings file (default: VB6toCS.INI next to the exe)
  --vbp <file>         Project file         (overrides the INI for this run)
  --out <folder>       Output folder        (overrides the INI for this run)
  --assembly <name>    Assembly name        (overrides the INI for this run)
  --quiet              No progress output";

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (UsageException e)
        {
            Console.Error.WriteLine(e.Message);
            Console.Error.WriteLine();
            Console.Error.WriteLine(Usage);
            return ExitUsage;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("Error: " + e.Message);
            return ExitFailed;
        }
    }

    private static int Run(string[] args)
    {
        var positional = new List<string>();
        string ini = null, vbp = null, output = null, assembly = null;
        var quiet = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--ini": ini = FullPath(Value(args, ref i)); break;
                case "--vbp": vbp = FullPath(Value(args, ref i)); break;
                case "--out": output = FullPath(Value(args, ref i)); break;
                case "--assembly": assembly = Value(args, ref i); break;
                case "--quiet": quiet = true; break;
                case "-h": case "--help": case "/?": positional.Insert(0, "help"); break;
                default:
                    if (args[i].StartsWith("--")) throw new UsageException("Unknown option: " + args[i]);
                    positional.Add(args[i]);
                    break;
            }
        }
        if (positional.Count == 0 || positional[0].ToLowerInvariant() == "help")
        {
            Console.WriteLine(Usage);
            return positional.Count == 0 ? ExitUsage : ExitOk;
        }

        var command = positional[0].ToLowerInvariant();
        var argument = positional.Count > 1 ? positional[1] : "";
        if (positional.Count > 2) throw new UsageException("Too many arguments.");

        ModUtils.Notify = Console.WriteLine;
        ModUtils.Progress = quiet ? (_, _, _) => { } : new ConsoleProgress().Report;
        ModConfig.IniFilePath = ini;
        ModConfig.hush = true;

        if (command == "config")
        {
            return Config(vbp, output, assembly);
        }
        ModConfig.OverrideSettings(vbp, output, assembly);

        switch (command)
        {
            case "all":
                if (!ConfigValid()) return ExitFailed;
                ModConvert.ConvertProject(ModConfig.VbpFile);
                return ExitOk;
            case "forms":
                return ConvertList(ModProjectFiles.VbpForms(ModConfig.VbpFile));
            case "modules":
                return ConvertList(ModProjectFiles.VbpModules(ModConfig.VbpFile));
            case "classes":
                return ConvertList(ModProjectFiles.VbpClasses(ModConfig.VbpFile));
            case "file":
                if (argument == "") throw new UsageException("Enter a file to convert.");
                if (!ConfigValid()) return ExitFailed;
                var file = ProjectRelative(argument);
                if (!ModConvert.ConvertFile(file)) return ExitFailed;
                Console.WriteLine("Converted " + file + ".");
                return ExitOk;
            case "scan":
                if (!ConfigValid()) return ExitFailed;
                Console.WriteLine("Scanned " + ModRefScan.ScanRefs() + " references.");
                return ExitOk;
            case "support":
                return Support(argument.ToLowerInvariant());
            case "lint":
                if (!ConfigValid()) return ExitFailed;
                var results = ModQuickLint.LintFileOrProject(ProjectRelative(argument));
                Console.WriteLine(results == "" ? "Done." : results);
                return results == "" ? ExitOk : ExitFailed;
            default:
                throw new UsageException("Unknown command: " + positional[0]);
        }
    }

    private static int Config(string vbp, string output, string assembly)
    {
        if (vbp != null || output != null || assembly != null)
        {
            ModConfig.SaveSettings(vbp, output, assembly);
        }
        Console.WriteLine("Settings file:  " + ModConfig.IniFile());
        Console.WriteLine("Project file:   " + ModConfig.VbpFile);
        Console.WriteLine("Output folder:  " + ModConfig.OutputFolder());
        Console.WriteLine("Assembly name:  " + ModConfig.AssemblyName());
        return ExitOk;
    }

    private static int ConvertList(string list)
    {
        if (!ConfigValid()) return ExitFailed;
        return ModConvert.ConvertFileList(ModUtils.FilePath(ModConfig.VbpFile), list) ? ExitOk : ExitFailed;
    }

    private static int Support(string which)
    {
        if (which != "" && which != "project" && which != "files")
        {
            throw new UsageException("support takes 'project', 'files' or nothing.");
        }
        if (!ConfigValid()) return ExitFailed;
        if (which != "files")
        {
            ModSupportFiles.CreateProjectFile(ModConfig.VbpFile);
            Console.WriteLine("Generated the project file.");
        }
        if (which != "project")
        {
            ModSupportFiles.CreateProjectSupportFiles();
            Console.WriteLine("Generated the support files.");
        }
        return ExitOk;
    }

    private static bool ConfigValid()
    {
        var error = ModConfig.ValidateSettings();
        if (error == "") return true;
        Console.Error.WriteLine(error);
        return false;
    }

    /// <summary>A bare file name stays project-relative (as in the GUI); any other path is taken from the current folder.</summary>
    private static string ProjectRelative(string file) =>
        file == "" || file.IndexOfAny(new[] { '\\', '/' }) < 0 ? file : Path.GetFullPath(file);

    private static string FullPath(string path) => Path.GetFullPath(path);

    private static string Value(string[] args, ref int i)
    {
        if (i + 1 >= args.Length) throw new UsageException("Missing value for " + args[i] + ".");
        return args[++i];
    }

    private sealed class UsageException(string message) : Exception(message);

    /// <summary>Renders ModUtils.Prg calls (val, max, caption) as a progress line on stderr.</summary>
    private sealed class ConsoleProgress
    {
        private int max;
        private string caption = "";
        private bool open;
        private readonly int width = WindowWidth();

        private static int WindowWidth()
        {
            try { return Console.WindowWidth; }
            catch (IOException) { return 80; }
        }

        public void Report(int val, int newMax, string cap)
        {
            if (newMax >= 0) max = newMax;
            if (cap != "#") caption = cap;
            if (val < 0)
            {
                if (open) Console.Error.WriteLine();
                open = false;
                return;
            }
            var line = max > 0 ? $"{caption} {val}/{max}" : caption;
            if (Console.IsErrorRedirected)
            {
                if (cap != "#") Console.Error.WriteLine(line);
                return;
            }
            Console.Error.Write("\r" + line.PadRight(Math.Max(line.Length, width - 1)));
            open = true;
        }
    }
}
