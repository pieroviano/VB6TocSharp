using System.IO;
using System.Linq;
using Vb6ToCSharp.FormConversion;
using Vb6ToCSharp.Modules;

namespace Vb6ToCSharp.Tests;

/// <summary>Project groups (.vbg → .sln), project references and VB6 cross-project visibility.</summary>
public class ProjectGroupTests
{
    [Fact]
    public void Vbg_StartupProjectFirst_PathsResolvedAgainstTheGroupFolder()
    {
        var g = VbgInfo.Parse("VBGROUP 5.0\r\nProject=Lib\\Lib.vbp\r\nStartupProject=App\\App.vbp\r\nProject=Lib\\Lib.vbp\r\n", @"C:\src\Group");

        Assert.Equal(new[] { @"C:\src\Group\App\App.vbp", @"C:\src\Group\Lib\Lib.vbp" }, g.Projects);
    }

    [Fact]
    public void Vbp_ProjectAndCompiledReferences_ExeName_Type()
    {
        var vbp = VbpInfo.Parse("Type=OleDll\r\nReference=*\\A..\\Lib\\Lib.vbp\r\n" +
                                "Reference=*\\G{00020430-0000-0000-C000-000000000046}#2.0#0#..\\bin\\Other.dll#Other\r\n" +
                                "ExeName32=\"Core.dll\"\r\nStartup=\"(None)\"\r\n");
        Assert.Equal("Core.dll", vbp.ExeName32);
        Assert.Equal(new[] { "Other.dll" }, vbp.CompiledReferences);
        Assert.Single(vbp.ProjectReferences);
        Assert.EndsWith(@"Lib\Lib.vbp", vbp.ProjectReferences.Single());
        Assert.True(vbp.IsLibrary);
        Assert.True(vbp.ExposesTypes);
        Assert.True(vbp.StartsWithSubMain); // (None): no startup form
        Assert.False(VbpInfo.Parse("Type=Exe\r\n").ExposesTypes);
        Assert.True(VbpInfo.Parse("Type=OleExe\r\n").ExposesTypes);
        Assert.False(VbpInfo.Parse("Type=OleExe\r\n").IsLibrary);
    }

    [Fact]
    public void Solution_ListsEveryProject_StartupFirst_WithStableGuids()
    {
        var app = Vbp("App", @"C:\g\App\App.vbp");
        var lib = Vbp("Lib", @"C:\g\Lib\Core.vbp");

        var sln = ModProjectGroup.SolutionFile("Group", new[] { app, lib });

        Assert.Contains("Microsoft Visual Studio Solution File, Format Version 12.00", sln);
        var appLine = sln.IndexOf("= \"App\", \"App\\App.csproj\"", StringComparison.Ordinal);
        var libLine = sln.IndexOf("= \"Lib\", \"Lib\\Core.csproj\"", StringComparison.Ordinal); // folder = Name, file = .vbp file name
        Assert.True(appLine > 0 && libLine > appLine, sln);
        Assert.Equal(ModProjectGroup.ProjectGuid("Group", app), ModProjectGroup.ProjectGuid("group", Vbp("app", @"D:\x.vbp")));
        Assert.NotEqual(ModProjectGroup.ProjectGuid("Group", app), ModProjectGroup.ProjectGuid("Group", lib));
        Assert.Contains(ModProjectGroup.ProjectGuid("Group", lib) + ".Release|Any CPU.Build.0 = Release|Any CPU", sln);
    }

    [Fact]
    public void Exposure_FollowsVbExposed()
    {
        Assert.True(ModProjectGroup.IsExposed("Attribute VB_Name = \"C\"\r\nAttribute VB_Exposed = True\r\n"));
        Assert.False(ModProjectGroup.IsExposed("Attribute VB_Exposed = False\r\n"));
        Assert.False(ModProjectGroup.IsExposed("Attribute VB_Name = \"modMain\"\r\n"));
    }

    [Fact]
    public void Group_IsRefusedFileByFile_AcceptedAsAWhole()
    {
        var dir = TestUtil.TempDir();
        var vbg = Path.Combine(dir, "G.vbg");
        File.WriteAllText(vbg, "VBGROUP 5.0\r\n");
        WithSettings(Path.Combine(dir, "t.ini"), vbg, dir, () =>
        {
            Assert.Contains(".vbg", ModConfig.ValidateSettings());
            Assert.Equal("", ModConfig.ValidateSettings(allowGroup: true));
        });
    }

    [Fact]
    public void Group_ConvertsEveryProject_IntoASolution_WithVb6Visibility()
    {
        var src = Path.Combine(TestUtil.TempDir(), "VB6Group");
        CopyDirectory(Path.Combine(RepoRoot(), "VB6Group"), src);
        var output = TestUtil.TempDir();

        string sln = null!;
        WithSettings(Path.Combine(output, "t.ini"), Path.Combine(src, "Group.vbg"), output,
            () => sln = TestUtil.WithTimeout(() => ModProjectGroup.ConvertGroup(Path.Combine(src, "Group.vbg")), 120000));

        Assert.Equal(Path.Combine(output, "Group.sln"), sln);
        Assert.Null(ModProjectGroup.Current);
        string Read(string rel) => File.ReadAllText(Path.Combine(output, rel));

        // one project per .vbp, the startup project first, ProjectReference from the VB6 *\A reference
        Assert.True(Read("Group.sln").IndexOf("App\\App.csproj", StringComparison.Ordinal) < Read("Group.sln").IndexOf("Lib\\Lib.csproj", StringComparison.Ordinal));
        Assert.Contains("<ProjectReference Include=\"..\\Lib\\Lib.csproj\" />", Read(@"App\App.csproj"));
        Assert.Contains("<OutputType>Library</OutputType>", Read(@"Lib\Lib.csproj"));
        Assert.Contains("<AssemblyName>Lib</AssemblyName>", Read(@"Lib\Lib.csproj"));
        Assert.False(File.Exists(Path.Combine(output, @"Lib\Program.cs"))); // an ActiveX DLL has no entry point
        Assert.True(File.Exists(Path.Combine(output, @"App\Program.cs")));

        // VB6 visibility: the DLL exposes only its public classes
        Assert.Contains("internal static class modCommon", Read(@"Lib\Modules\modCommon.cs"));
        Assert.Contains("internal class CHelper", Read(@"Lib\Classes\CHelper.cs"));
        Assert.Contains("public class CCircle", Read(@"Lib\Classes\CCircle.cs"));
        Assert.Contains("public static class modCommon", Read(@"App\Modules\modCommon.cs")); // a standard EXE keeps everything public

        // implemented only by the App: still an interface in the DLL; the Lib qualifier dropped (global namespace)
        Assert.Contains("public interface IShape", Read(@"Lib\Classes\IShape.cs"));
        Assert.Contains("class CSquare : IShape", Read(@"App\Classes\CSquare.cs"));
        var app = Read(@"App\Modules\modApp.cs");
        Assert.Contains("CCircle c = null;", app);
        Assert.Contains("c = new CCircle();", app);
        Assert.Contains("c.Area() + s.Area()", app); // the DLL's class is known: obj.Method without parentheses is a call
    }

    private static VbpInfo Vbp(string name, string path)
    {
        var v = VbpInfo.Parse("Name=\"" + name + "\"\r\n");
        v.Path = path;
        return v;
    }

    /// <summary>Runs <paramref name="action"/> with its own settings file and project, then restores the process-wide settings.</summary>
    private static void WithSettings(string ini, string vbp, string output, Action action)
    {
        var notify = ModUtils.Notify;
        ModUtils.Notify = _ => { };
        ModConfig.IniFilePath = ini;
        ModConfig.OverrideSettings(vbp, output, null, UiTarget.WinForms);
        try
        {
            action();
        }
        finally
        {
            ModConfig.IniFilePath = null;
            ModConfig.OverrideSettings();
            ModConvertStatements.ResetProjectCaches();
            ModUtils.Notify = notify;
        }
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Vb6ToCSharp.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root (Vb6ToCSharp.slnx) not found.");
    }

    private static void CopyDirectory(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var f in Directory.GetFiles(from)) File.Copy(f, Path.Combine(to, Path.GetFileName(f)));
        foreach (var d in Directory.GetDirectories(from)) CopyDirectory(d, Path.Combine(to, Path.GetFileName(d)));
    }
}
