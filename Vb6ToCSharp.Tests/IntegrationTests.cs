using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Vb6ToCSharp.Tests;

/// <summary>
/// End to end: Vb6ToCSharp.Console converts the VB6 sample project (VB6\Showcase.vbp) into Converted\ under the
/// repository root, and the converted C# project builds.
/// </summary>
[Trait("Category", "Integration")]
public class IntegrationTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Vb6ToCSharp.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root (Vb6ToCSharp.slnx) not found.");
    }

    /// <summary>Runs a process to completion; returns the exit code and the whole output.</summary>
    private static (int Code, string Output) Run(string exe, string args, string workDir, int timeoutMs)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            WorkingDirectory = workDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(timeoutMs))
        {
            p.Kill();
            throw new TimeoutException(exe + " " + args + " did not finish in " + timeoutMs / 1000 + " s.");
        }
        return (p.ExitCode, stdout.Result + stderr.Result);
    }

    /// <summary>MSBuild of the newest Visual Studio (via vswhere), else null (dotnet msbuild is used).</summary>
    private static string? FindMsBuild()
    {
        var vswhere = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft Visual Studio\Installer\vswhere.exe");
        if (!File.Exists(vswhere)) return null;
        var (code, output) = Run(vswhere, @"-latest -prerelease -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe", ".", 60000);
        var path = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(File.Exists);
        return code == 0 ? path : null;
    }

    /// <summary>Empties <paramref name="folder"/> (creating it when missing).</summary>
    private static void Empty(string folder)
    {
        Directory.CreateDirectory(folder);
        foreach (var f in Directory.GetFiles(folder)) File.Delete(f);
        foreach (var d in Directory.GetDirectories(folder)) Directory.Delete(d, true);
    }

    [Fact]
    public void Showcase_ConvertsWithTheConsole_AndTheConvertedProjectBuilds()
    {
        var root = RepoRoot();
        var vbp = Path.Combine(root, "VB6", "Showcase.vbp");
        var output = Path.Combine(root, "Converted");
        Assert.True(File.Exists(vbp), vbp);
        Empty(output);

        // convert with the console (its own settings file: the INI next to the exe stays untouched)
        var console = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Vb6ToCSharp.Console.exe");
        var ini = Path.Combine(TestUtil.TempDir(), "VB6toCS.INI");
        var convert = Run(console, $"all --ini \"{ini}\" --vbp \"{vbp}\" --out \"{output}\" --assembly Showcase --ui winforms --quiet", output, 300000);
        Assert.True(convert.Code == 0, "conversion failed (" + convert.Code + "):\n" + convert.Output);
        var project = Path.Combine(output, "Showcase.csproj");
        Assert.True(File.Exists(project), "no project generated:\n" + convert.Output);
        Assert.True(File.Exists(Path.Combine(output, "MigrationReport.md")));

        // build it: restore only from the repository feed (the runtime package built with this solution), into a fresh folder
        var feed = Path.Combine(root, "Packages");
        var props = $"-restore -nologo -v:m -p:Configuration=Debug \"-p:RestoreSources={feed}\" \"-p:RestorePackagesPath={Path.Combine(output, ".packages")}\"";
        var msbuild = FindMsBuild();
        var build = msbuild != null
            ? Run(msbuild, $"\"{project}\" {props}", output, 600000)
            : Run("dotnet", $"msbuild \"{project}\" {props}", output, 600000);
        Assert.True(build.Code == 0, "the converted project does not build (" + build.Code + "):\n"
                                     + string.Join("\n", build.Output.Split('\n').Where(l => l.Contains(" error ")).Distinct().Take(50)));
        Assert.True(File.Exists(Path.Combine(output, "bin", "Debug", "net48", "Showcase.exe")), "no Showcase.exe:\n" + build.Output);
    }
}
