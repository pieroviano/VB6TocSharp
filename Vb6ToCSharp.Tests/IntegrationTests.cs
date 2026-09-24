using System.Diagnostics;
using System.IO;
using System.Linq;
using Vb6ToCSharp.CodeGeneration;
using Vb6ToCSharp.Tests.Fixtures;

namespace Vb6ToCSharp.Tests;

/// <summary>
/// End to end: Vb6ToCSharp.Console converts the VB6 samples under the repository root (VB6\Showcase.vbp into Converted\,
/// the project group VBG\Group.vbg into ConvertedGroup\, the ADO sample Vb6Ado\Vbb6Ado.vbp into ConvertedVb6Ado\);
/// the result builds and, where it can run unattended, runs.
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

    /// <summary>The target framework the converter emits (SupportFiles.ProjectFile): the converted output lands in bin\Debug\&lt;this&gt;.</summary>
    private const string ConvertedTargetFramework = "net10.0-windows";

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

    /// <summary>Converts <paramref name="source"/> (.vbp or .vbg) into <paramref name="output"/> with the console, using its own settings file.</summary>
    private static void ConvertWithConsole(string source, string output, string options)
    {
        Assert.True(File.Exists(source), source);
        Empty(output);
        // the console's own settings file: the INI next to the exe stays untouched
        var console = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Vb6ToCSharp.Console.exe");
        var ini = Path.Combine(TestUtil.TempDir(), "VB6toCS.INI");
        var convert = Run(console, $"all --ini \"{ini}\" --vbp \"{source}\" --out \"{output}\" {options} --ui winforms --quiet", output, 300000);
        Assert.True(convert.Code == 0, "conversion failed (" + convert.Code + "):\n" + convert.Output);
    }

    /// <summary>
    /// Builds a converted project or solution into a fresh folder. The packages this solution builds come from the
    /// repository feed, everything else a converted project references (Standard.AdoDb's dependencies) from nuget.org.
    /// </summary>
    private static void Build(string root, string output, string target)
    {
        WriteRestoreSources(root, output);
        var props = $"-restore -nologo -v:m -p:Configuration=Debug \"-p:RestorePackagesPath={Path.Combine(output, ".packages")}\"";
        var msbuild = FindMsBuild();
        var build = msbuild != null
            ? Run(msbuild, $"\"{target}\" {props}", output, 600000)
            : Run("dotnet", $"msbuild \"{target}\" {props}", output, 600000);
        Assert.True(build.Code == 0, "the converted code does not build (" + build.Code + "):\n"
                                     + string.Join("\n", build.Output.Split('\n').Where(l => l.Contains(" error ")).Distinct().Take(50)));
    }

    /// <summary>
    /// The NuGet sources of the converted project: source mapping pins the packages this solution builds to the
    /// repository feed, so a same-numbered build published on nuget.org cannot shadow the one under test.
    /// </summary>
    private static void WriteRestoreSources(string root, string output)
    {
        File.WriteAllText(Path.Combine(output, "NuGet.config"),
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
            "<configuration>\r\n" +
            "  <packageSources>\r\n" +
            "    <clear />\r\n" +
            "    <add key=\"Local\" value=\"" + Path.Combine(root, "Packages") + "\" />\r\n" +
            "    <add key=\"nuget.org\" value=\"https://api.nuget.org/v3/index.json\" />\r\n" +
            "  </packageSources>\r\n" +
            "  <packageSourceMapping>\r\n" +
            "    <packageSource key=\"Local\">\r\n" +
            "      <package pattern=\"Net4x.Vb6ToCSharp.*\" />\r\n" +
            "      <package pattern=\"" + SupportFiles.AdoPackage + "\" />\r\n" +
            "    </packageSource>\r\n" +
            "    <packageSource key=\"nuget.org\">\r\n" +
            "      <package pattern=\"*\" />\r\n" +
            "    </packageSource>\r\n" +
            "  </packageSourceMapping>\r\n" +
            "</configuration>\r\n");
    }

    /// <summary>Loads a built assembly from its bytes, so the file stays deletable for the next run.</summary>
    private static System.Reflection.Assembly LoadFromBytes(string file)
    {
        Assert.True(File.Exists(file), "not built: " + file);
        return System.Reflection.Assembly.Load(File.ReadAllBytes(file));
    }

    /// <summary>Calls a public static procedure of a converted module.</summary>
    private static object? Call(System.Reflection.Assembly assembly, string module, string procedure, params object[] args)
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

    /// <summary>
    /// Drives a converted WinForms form on an STA thread: <paramref name="test"/> gets the form's default instance and a
    /// lookup of its controls by name.
    /// </summary>
    private static void WithForm(System.Reflection.Assembly assembly, string formType, Action<System.Windows.Forms.Form, Func<string, object>> test)
    {
        Exception? uiError = null;
        var ui = new System.Threading.Thread(() =>
        {
            try
            {
                var type = assembly.GetType(formType, true)!;
                var form = (System.Windows.Forms.Form)type.GetProperty("instance")!.GetValue(null)!;
                object Control(string name) => type.GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!.GetValue(form)!;
                test(form, Control);
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
    public void Showcase_ConvertsWithTheConsole_AndTheConvertedProjectBuilds()
    {
        var root = RepoRoot();
        var output = Path.Combine(root, "Converted");
        ConvertWithConsole(Path.Combine(root, "VB6", "Showcase.vbp"), output, "--assembly Showcase");
        var project = Path.Combine(output, "Showcase.csproj");
        Assert.True(File.Exists(project), "no project generated");
        Assert.True(File.Exists(Path.Combine(output, "MigrationReport.md")));

        Build(root, output, project);

        // run it: the converted code of every module and class (RunAll; Main would also show the form).
        // The managed assembly is the .dll - on .NET the .exe is a native apphost.
        var assembly = LoadFromBytes(Path.Combine(output, "bin", "Debug", ConvertedTargetFramework, "Showcase.dll"));
        Assert.Equal(true, Call(assembly, "modMain", "RunAll"));
        // results that depend on VB6 semantics being kept
        Assert.Equal(43, Call(assembly, "modMain", "Classes")); // events, default member c(1), For Each on the class, interface method, CApp.Version
        Assert.Equal(40, Call(assembly, "modMain", "Udts")); // q = p copies the UDT: p.Age stays 36
        Assert.Equal(23, Call(assembly, "modMain", "Selects", 3)); // Case 3 To 5, string ranges, Select Case True
        Assert.Equal(3, Call(assembly, "modMain", "GoSubs", 3)); // GoSub, On ... GoSub, On ... GoTo
        Assert.Equal(4, Call(assembly, "modMain", "Files")); // Open Output/Append/Input, Print with ';', Write, Line Input, EOF, Close
        Assert.Equal(5, Call(assembly, "modMain", "Raised")); // Err.Raise reaches the handler, which reads Err.Number
        Assert.Equal(9, Call(assembly, "modMain", "Errors")); // On Error Resume Next / GoTo 0 / GoTo label, Resume, Resume label, Err.Raise
        Assert.Equal(12, Call(assembly, "modLegacy", "Legacy")); // Option Compare Text ("abc" = "ABC"), Option Base 1
        Assert.Equal(42, Call(assembly, "modLegacy", "Pragmas")); // InsertStatement + ReplaceStatement

        // the form: Form_Load, a click through the real WinForms events, Unload Me
        WithForm(assembly, "Showcase.Forms.frmMain", (form, control) =>
        {
            form.Show(); // Form_Load
            Assert.Equal("Showcase", form.Text); // Me.Caption = APP_TITLE
            ((System.Windows.Forms.Button)control("cmdRun")).PerformClick();
            Assert.Equal("54", ((System.Windows.Forms.Label)control("lblResult")).Text); // CStr(Legacy() + Pragmas())
            Assert.Single(((System.Windows.Forms.ListBox)control("lstLog")).Items); // "Run 1"
            ((System.Windows.Forms.Button)control("cmdClose")).PerformClick(); // Unload Me
            Assert.False(form.Visible);
        });
    }

    /// <summary>
    /// The same end to end for a project that uses ADO: Vb6Ado\Vbb6Ado.vbp into ConvertedVb6Ado\. Only convert and
    /// build - the converted code talks to SQL Server LocalDB and shows message boxes, so it is not run here.
    /// ADO comes from the managed Standard.AdoDb package, not from the COM type library.
    /// </summary>
    [Fact]
    public void Vb6Ado_ConvertsWithTheConsole_AndTheConvertedProjectBuilds()
    {
        var root = RepoRoot();
        var output = Path.Combine(root, "ConvertedVb6Ado");
        ConvertWithConsole(Path.Combine(root, "Vb6Ado", "Vbb6Ado.vbp"), output, "--assembly Vbb6Ado");
        var project = Path.Combine(output, "Vbb6Ado.csproj");
        Assert.True(File.Exists(project), "no project generated");
        Assert.True(File.Exists(Path.Combine(output, "MigrationReport.md")));
        // the VB6 project references Microsoft ActiveX Data Objects: the converted one references the managed ADODB package
        var csproj = File.ReadAllText(project);
        Assert.Contains("<PackageReference Include=\"Standard.AdoDb\"", csproj);
        Assert.DoesNotContain("COMReference", csproj);

        Build(root, output, project);
        Assert.True(File.Exists(Path.Combine(output, "bin", "Debug", ConvertedTargetFramework, "Vbb6Ado.dll")), "not built");
    }

    /// <summary>The same end to end for a project group: VBG\Group.vbg (an EXE referencing an ActiveX DLL) into ConvertedGroup\.</summary>
    [Fact]
    public void Group_ConvertsWithTheConsole_AndTheConvertedSolutionBuilds()
    {
        var root = RepoRoot();
        var output = Path.Combine(root, "ConvertedGroup");
        ConvertWithConsole(Path.Combine(root, "VBG", "Group.vbg"), output, "");
        var solution = Path.Combine(output, "Group.sln");
        Assert.True(File.Exists(solution), "no solution generated");
        foreach (var project in new[] { "Exe", "Lib" })
        {
            Assert.True(File.Exists(Path.Combine(output, project, project + ".csproj")), "no project generated: " + project);
            Assert.True(File.Exists(Path.Combine(output, project, "MigrationReport.md")));
        }

        Build(root, output, solution);

        // run it: Lib.dll comes from the EXE's output folder (loaded from bytes as well)
        var bin = Path.Combine(output, "Exe", "bin", "Debug", ConvertedTargetFramework);
        System.Reflection.Assembly? lib = null;
        ResolveEventHandler resolve = (_, e) => new System.Reflection.AssemblyName(e.Name).Name == "Lib" ? lib ??= LoadFromBytes(Path.Combine(bin, "Lib.dll")) : null;
        AppDomain.CurrentDomain.AssemblyResolve += resolve;
        try
        {
            var assembly = LoadFromBytes(Path.Combine(bin, "Exe.dll"));
            // results that depend on the projects seeing each other as in VB6
            Assert.Equal(21.566, (double)Call(assembly, "modExe", "RunGroup")!, 3); // the DLL's class (Lib.CCircle, obj.Method) and interface (Implements Lib.IShape)
            Assert.Equal("Exe+Lib", Call(assembly, "modExe", "Owners")); // same-named modules: each project calls its own

            // the form: Form_Load, a click that calls into the DLL, Unload Me
            WithForm(assembly, "Exe.Forms.frmMain", (form, control) =>
            {
                form.Show(); // Form_Load
                Assert.Equal("Group", form.Text); // Me.Caption = APP_TITLE
                ((System.Windows.Forms.Button)control("cmdRun")).PerformClick();
                Assert.Equal("Exe+Lib 314", ((System.Windows.Forms.Label)control("lblResult")).Text); // Owners() & " " & CStr(CLng(c.Area)): 314.159
                ((System.Windows.Forms.Button)control("cmdClose")).PerformClick(); // Unload Me
                Assert.False(form.Visible);
            });
        }
        finally
        {
            AppDomain.CurrentDomain.AssemblyResolve -= resolve;
        }
    }
}
