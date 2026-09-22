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

    /// <summary>Empties <paramref name="folder"/> (creating it when missing); .vs (IDE state, locked while the project is open) is kept.</summary>
    private static void Empty(string folder)
    {
        Directory.CreateDirectory(folder);
        foreach (var f in Directory.GetFiles(folder)) File.Delete(f);
        foreach (var d in Directory.GetDirectories(folder))
        {
            if (!string.Equals(Path.GetFileName(d), ".vs", StringComparison.OrdinalIgnoreCase)) Directory.Delete(d, true);
        }
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
        var exe = Path.Combine(output, "bin", "Debug", "net48", "Showcase.exe");
        Assert.True(File.Exists(exe), "no Showcase.exe:\n" + build.Output);

        // run it: the converted code of every module and class (RunAll; Main would also show the form).
        // Loaded from bytes, so the exe stays deletable for the next run.
        var assembly = System.Reflection.Assembly.Load(File.ReadAllBytes(exe));
        object? Call(string module, string procedure, params object[] args)
        {
            try
            {
                return assembly.GetType(module, true)!.GetMethod(procedure)!.Invoke(null, args);
            }
            catch (System.Reflection.TargetInvocationException e)
            {
                throw new Xunit.Sdk.XunitException(module + "." + procedure + " failed at run time: " + e.InnerException);
            }
        }
        Assert.Equal(true, Call("modMain", "RunAll"));
        // results that depend on VB6 semantics being kept
        Assert.Equal(43, Call("modMain", "Classes")); // events, default member c(1), For Each on the class, interface method, CApp.Version
        Assert.Equal(40, Call("modMain", "Udts")); // q = p copies the UDT: p.Age stays 36
        Assert.Equal(23, Call("modMain", "Selects", 3)); // Case 3 To 5, string ranges, Select Case True
        Assert.Equal(3, Call("modMain", "GoSubs", 3)); // GoSub, On ... GoSub, On ... GoTo
        Assert.Equal(12, Call("modLegacy", "Legacy")); // Option Compare Text ("abc" = "ABC"), Option Base 1
        Assert.Equal(42, Call("modLegacy", "Pragmas")); // InsertStatement + ReplaceStatement

        // the form: Form_Load, a click through the real WinForms events, Unload Me
        Exception? uiError = null;
        var ui = new System.Threading.Thread(() =>
        {
            try
            {
                var formType = assembly.GetType("Showcase.Forms.frmMain", true)!;
                var form = (System.Windows.Forms.Form)formType.GetProperty("instance")!.GetValue(null)!;
                T Control<T>(string name) => (T)formType.GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!.GetValue(form)!;
                form.Show(); // Form_Load
                Assert.Equal("Showcase", form.Text); // Me.Caption = APP_TITLE
                Control<System.Windows.Forms.Button>("cmdRun").PerformClick();
                Assert.Equal("54", Control<System.Windows.Forms.Label>("lblResult").Text); // CStr(Legacy() + Pragmas())
                Assert.Equal(1, Control<System.Windows.Forms.ListBox>("lstLog").Items.Count); // "Run 1"
                Control<System.Windows.Forms.Button>("cmdClose").PerformClick(); // Unload Me
                Assert.False(form.Visible);
            }
            catch (Exception e)
            {
                uiError = e;
            }
        });
        ui.SetApartmentState(System.Threading.ApartmentState.STA);
        ui.Start();
        Assert.True(ui.Join(60000), "the form did not respond");
        if (uiError != null) throw new Xunit.Sdk.XunitException("the converted form failed: " + uiError);
    }

    [Fact]
    public void Group_ConvertsWithTheConsole_AndTheSolutionBuilds()
    {
        var root = RepoRoot();
        var vbg = Path.Combine(root, "VB6Group", "Group.vbg");
        var output = Path.Combine(root, "ConvertedGroup");
        Assert.True(File.Exists(vbg), vbg);
        Empty(output);

        var console = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Vb6ToCSharp.Console.exe");
        var ini = Path.Combine(TestUtil.TempDir(), "VB6toCS.INI");
        var convert = Run(console, $"all --ini \"{ini}\" --vbp \"{vbg}\" --out \"{output}\" --ui winforms --quiet", output, 300000);
        Assert.True(convert.Code == 0, "conversion failed (" + convert.Code + "):\n" + convert.Output);
        var solution = Path.Combine(output, "Group.sln");
        Assert.True(File.Exists(solution), "no solution generated:\n" + convert.Output);

        // the solution builds: the App references the ActiveX DLL's project
        var feed = Path.Combine(root, "Packages");
        var props = $"-restore -nologo -v:m -p:Configuration=Debug \"-p:RestoreSources={feed}\" \"-p:RestorePackagesPath={Path.Combine(output, ".packages")}\"";
        var msbuild = FindMsBuild();
        var build = msbuild != null
            ? Run(msbuild, $"\"{solution}\" {props}", output, 600000)
            : Run("dotnet", $"msbuild \"{solution}\" {props}", output, 600000);
        Assert.True(build.Code == 0, "the converted solution does not build (" + build.Code + "):\n"
                                     + string.Join("\n", build.Output.Split('\n').Where(l => l.Contains(" error ")).Distinct().Take(50)));

        // run it: loaded from bytes (the files stay deletable); Lib.dll is resolved from the App's output folder
        var bin = Path.Combine(output, "App", "bin", "Debug", "net48");
        System.Reflection.Assembly? lib = null;
        ResolveEventHandler resolve = (_, e) => new System.Reflection.AssemblyName(e.Name).Name == "Lib"
            ? lib ??= System.Reflection.Assembly.Load(File.ReadAllBytes(Path.Combine(bin, "Lib.dll")))
            : null;
        AppDomain.CurrentDomain.AssemblyResolve += resolve;
        try
        {
            var app = System.Reflection.Assembly.Load(File.ReadAllBytes(Path.Combine(bin, "App.exe")));
            object? Call(string procedure) => app.GetType("modApp", true)!.GetMethod(procedure)!.Invoke(null, null);
            Assert.Equal(21.566, (double)Call("RunGroup")!, 3); // the DLL's class and interface, used from the EXE
            Assert.Equal("App+Lib", Call("Owners")); // same-named modules: each project calls its own
        }
        finally
        {
            AppDomain.CurrentDomain.AssemblyResolve -= resolve;
        }
    }
}
