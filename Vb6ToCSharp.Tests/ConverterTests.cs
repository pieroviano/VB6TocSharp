using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Vb6ToCSharp.Modules;

namespace Vb6ToCSharp.Tests;

/// <summary>
/// Points ModConfig (INI next to the test assembly) at a tiny temp VB6 project so ModRefScan can scan it.
/// </summary>
public sealed class ConverterFixture : IDisposable
{
    public readonly string Dir = TestUtil.TempDir();
    private readonly string ini = ModConfig.IniFile();
    private readonly string refs = AppDomain.CurrentDomain.BaseDirectory + "\\refs.txt";
    private readonly string? iniBackup;

    public ConverterFixture()
    {
        iniBackup = File.Exists(ini) ? File.ReadAllText(ini) : null;
        var vbp = Path.Combine(Dir, "prj.vbp");
        File.WriteAllText(vbp, "Type=Exe\r\nForm=frmA.frm\r\nModule=modA; modA.bas\r\n");
        File.WriteAllText(Path.Combine(Dir, "frmA.frm"),
            "VERSION 5.00\r\n" +
            "Begin VB.Form frmA \r\n" +
            "   Caption         =   \"Hello\"\r\n" +
            "   ClientHeight    =   3000\r\n" +
            "   ClientWidth     =   4000\r\n" +
            "   Begin VB.CommandButton cmdOK \r\n" +
            "      Caption         =   \"OK\"\r\n" +
            "      Height          =   495\r\n" +
            "      Left            =   120\r\n" +
            "      TabIndex        =   0\r\n" +
            "      Top             =   120\r\n" +
            "      Width           =   1215\r\n" +
            "   End\r\n" +
            "End\r\n" +
            "Attribute VB_Name = \"frmA\"\r\n" +
            "Attribute VB_GlobalNameSpace = False\r\n" +
            "Option Explicit\r\n\r\n" +
            "Private Sub cmdOK_Click()\r\n  Unload Me\r\nEnd Sub\r\n");
        File.WriteAllText(Path.Combine(Dir, "modA.bas"),
            "Attribute VB_Name = \"modA\"\r\nOption Explicit\r\n\r\n" +
            "Public Function Twice(ByVal X As Long) As Long\r\n  Twice = X * 2\r\nEnd Function\r\n\r\n" +
            "Public Function Add2(ByVal A As Long, ByVal B As Long) As Long\r\n  Add2 = A + B\r\nEnd Function\r\n");
        ModIni.IniWrite(ModConfig.iniSectionSettings, ModConfig.iniKeyVbpFile, vbp, ini);
        ModIni.IniWrite(ModConfig.iniSectionSettings, ModConfig.iniKeyOutputFolder, Path.Combine(Dir, "out") + "\\", ini);
        ModIni.IniWrite(ModConfig.iniSectionSettings, ModConfig.iniKeyAssemblyName, "TestAsm", ini);
        ModConfig.LoadSettings(true);
        ModRefScan.ScanRefs();
    }

    public void Dispose()
    {
        if (iniBackup == null) File.Delete(ini); else File.WriteAllText(ini, iniBackup);
        ModConfig.LoadSettings(true);
        try { File.Delete(refs); } catch { }
        try { Directory.Delete(Dir, true); } catch { }
    }
}

public class ConverterTests : IClassFixture<ConverterFixture>
{
    private readonly ConverterFixture fixture;

    public ConverterTests(ConverterFixture fixture)
    {
        this.fixture = fixture;
        ModConvertUtils.ReComment("");
        ModConvertUtils.InitDeString();
    }

    private static string Sub(params string[] body) => "Public Sub T()\r\n" + string.Join("\r\n", body) + "\r\nEnd Sub";
    private static string Convert(string vb) => TestUtil.WithTimeout(() => ModConvert.ConvertSub(vb, true), 20000);
    private static string Flat(string s) => Regex.Replace(s, @"\s+", " ");

    [Fact] public void Config_UsesIniSettings() => Assert.Equal("TestAsm", ModConfig.AssemblyName());

    [Fact]
    public void ScanRefs_IndexesProjectFunctions()
    {
        Assert.True(ModRefScan.IsFuncRef("Twice"));
        Assert.Equal("modA", ModRefScan.FuncRefModule("Twice"));
        Assert.False(ModRefScan.IsFuncRef("NoSuchThing"));
    }

    [Fact] public void FuncRefDeclArgCnt_CountsArguments() => Assert.Equal(2, TestUtil.WithTimeout(() => ModRefScan.FuncRefDeclArgCnt("Add2")));

    [Fact]
    public void ConvertSub_LoopWhile_KeepsCondition()
    {
        var cs = Flat(Convert(Sub("  Dim i As Long", "  Do", "    i = i + 1", "  Loop While i < 10")));
        Assert.Matches(@"\} while\( ?i < 10 ?\);", cs);
        Assert.DoesNotContain("while(!(", cs);
    }

    [Fact]
    public void ConvertSub_LoopUntil_NegatesCondition()
    {
        var cs = Flat(Convert(Sub("  Dim i As Long", "  Do", "    i = i + 1", "  Loop Until i >= 10")));
        Assert.Matches(@"\} while\(!\( ?i >= 10 ?\)\);", cs);
    }

    [Fact]
    public void ConvertSub_ForIsInclusive()
    {
        var cs = Flat(Convert(Sub("  Dim i As Long", "  For i = 1 To 3", "    Debug.Print i", "  Next")));
        Assert.Matches(@"for\(i ?= ?1; i ?<= ?3; i\+\+\)", cs);
    }

    [Fact]
    public void ConvertSub_CaseList_EmitsEveryValue()
    {
        var cs = Flat(Convert(Sub("  Dim x As Long", "  Dim y As Long", "  Select Case x", "    Case 1, 2, 3", "      y = 1", "    Case Else", "      y = 2", "  End Select")));
        Assert.Contains("case 1:", cs);
        Assert.Contains("case 2:", cs);
        Assert.Contains("case 3:", cs);
    }

    [Fact]
    public void ConvertCodeLine_SubCall_ConvertsAllArguments()
    {
        var cs = TestUtil.WithTimeout(() => ModConvert.ConvertCodeLine("Foo a, b, c"));
        Assert.Matches(@"Foo\(a, ?b, ?c\)", cs);
    }

    [Fact]
    public void ConvertApiDef_ConvertsAllArguments()
    {
        var vb = ModConvertUtils.DeString("Private Declare Function SendMessage Lib \"user32\" Alias \"SendMessageA\" (ByVal hWnd As Long, ByVal wMsg As Long) As Long");
        var cs = TestUtil.WithTimeout(() => ModConvert.ConvertApiDef(vb));
        Assert.Contains("[DllImport(\"user32.dll\", EntryPoint = \"SendMessageA\")]", cs);
        Assert.Contains("private static extern", cs);
        Assert.Contains("hWnd", cs);
        Assert.Contains("wMsg", cs);
    }

    [Fact]
    public void ConvertEvent_ConvertsAllArguments()
    {
        var cs = TestUtil.WithTimeout(() => ModConvert.ConvertEvent("Public Event Changed(ByVal A As Long, ByVal B As String)"));
        Assert.Contains("ChangedHandler(", cs);
        Assert.Matches(@"ChangedHandler\([^)]*\bA\b[^)]*\bB\b[^)]*\)", cs);
    }

    [Fact]
    public void SanitizeCode_SplitsColonStatementsOnce()
    {
        var s = TestUtil.WithTimeout(() => ModConvert.SanitizeCode("a = 1: b = 2: c = 3"));
        var lines = s.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(new[] { "a = 1", "b = 2", "c = 3" }, lines);
    }

    [Fact]
    public void ConvertCodeSegment_ConvertsEveryProcedure()
    {
        const string vb = "Public Sub First()\r\n  Dim x As Long\r\n  x = 1\r\nEnd Sub\r\n\r\nPublic Sub Second()\r\n  Dim y As Long\r\n  y = 2\r\nEnd Sub\r\n";
        var cs = TestUtil.WithTimeout(() => ModConvert.ConvertCodeSegment(vb, true), 20000);
        Assert.Contains("First(", cs);
        Assert.Contains("Second(", cs);
    }

    [Fact]
    public void ConvertSub_ForNegativeStep_CountsDown()
    {
        var cs = Flat(Convert(Sub("  Dim i As Long", "  For i = 10 To 1 Step -1", "    Debug.Print i", "  Next")));
        Assert.Matches(@"for\(i ?= ?10; i ?>= ?1; i \+= ?-1\)", cs);
        Assert.DoesNotContain("Step", cs);
    }

    [Fact]
    public void ConvertSub_ForPositiveStep()
    {
        var cs = Flat(Convert(Sub("  Dim i As Long", "  For i = 0 To 10 Step 2", "    Debug.Print i", "  Next")));
        Assert.Matches(@"for\(i ?= ?0; i ?<= ?10; i \+= ?2\)", cs);
    }

    [Fact]
    public void ConvertSub_ForVariableStep_ChecksSignAtRuntime()
    {
        var cs = Flat(Convert(Sub("  Dim i As Long", "  Dim n As Long", "  Dim s As Long", "  For i = 1 To n Step s", "    Debug.Print i", "  Next")));
        Assert.Contains("(s >= 0 ? i <= n : i >= n)", cs);
        Assert.Contains("i += s", cs);
    }

    private static string Out(ConverterFixture f, string rel) => Path.Combine(f.Dir, "out", rel);

    private static List<string> CaptureNotify(Action a)
    {
        var got = new List<string>();
        var oldNotify = ModUtils.Notify;
        var oldProgress = ModUtils.Progress;
        ModUtils.Notify = got.Add;
        ModUtils.Progress = (_, _, _) => { };
        try { a(); }
        finally
        {
            ModUtils.Notify = oldNotify;
            ModUtils.Progress = oldProgress;
        }
        return got;
    }

    private void CleanOut()
    {
        var o = Path.Combine(fixture.Dir, "out");
        if (Directory.Exists(o)) Directory.Delete(o, true);
    }

    [Fact]
    public void ConvertFile_Module_WritesConvertedClass()
    {
        CleanOut();
        var ok = false;
        var notes = CaptureNotify(() => ok = TestUtil.WithTimeout(() => ModConvert.ConvertFile(Path.Combine(fixture.Dir, "modA.bas")), 30000));
        Assert.True(ok);
        Assert.Empty(notes);
        var cs = File.ReadAllText(Out(fixture, @"Modules\modA.cs"));
        Assert.Contains("public static class modA", cs);
        Assert.Contains("Twice(", cs);
        Assert.Contains("Add2(", cs);
    }

    [Fact]
    public void ConvertFile_AlreadyConverted_ReturnsFalse()
    {
        CleanOut();
        var f = Path.Combine(fixture.Dir, "modA.bas");
        CaptureNotify(() => Assert.True(TestUtil.WithTimeout(() => ModConvert.ConvertFile(f), 30000)));
        File.WriteAllText(Out(fixture, @"Modules\modA.cs"), "// ### CONVERTED\r\n");
        CaptureNotify(() => Assert.False(TestUtil.WithTimeout(() => ModConvert.ConvertFile(f), 30000)));
    }

    [Fact]
    public void ConvertFile_Form_WritesXamlAndCodeBehind()
    {
        CleanOut();
        var ok = false;
        CaptureNotify(() => ok = TestUtil.WithTimeout(() => ModConvert.ConvertFile(Path.Combine(fixture.Dir, "frmA.frm")), 30000));
        Assert.True(ok);
        var xaml = File.ReadAllText(Out(fixture, @"Forms\frmA.xaml"));
        Assert.Contains("<Window", xaml);
        Assert.Contains("Hello", xaml);
        Assert.Contains("cmdOK", xaml);
        var cs = File.ReadAllText(Out(fixture, @"Forms\frmA.xaml.cs"));
        Assert.Contains("partial class frmA", cs);
        Assert.Contains("cmdOK_Click", cs);
    }

    [Fact]
    public void ConvertFile_UnknownType_NotifiesAndFails()
    {
        var ok = true;
        var notes = CaptureNotify(() => ok = ModConvert.ConvertFile(Path.Combine(fixture.Dir, "x.txt")));
        Assert.False(ok);
        Assert.Single(notes);
        Assert.Contains("UNKNOWN VB TYPE", notes[0]);
    }

    [Fact]
    public void ConvertModule_MissingFile_NotifiesAndFails()
    {
        var ok = true;
        var notes = CaptureNotify(() => ok = ModConvert.ConvertModule(Path.Combine(fixture.Dir, "nope.bas")));
        Assert.False(ok);
        Assert.Contains("File not found", Assert.Single(notes));
    }

    [Fact]
    public void ConvertProject_ConvertsEverythingHeadless()
    {
        CleanOut();
        var notes = CaptureNotify(() => TestUtil.WithTimeout(() => { ModConvert.ConvertProject(ModConfig.VbpFile); return 0; }, 60000));
        Assert.Equal(new[] { "Complete." }, notes);
        Assert.True(File.Exists(Out(fixture, @"Modules\modA.cs")));
        Assert.True(File.Exists(Out(fixture, @"Forms\frmA.xaml")));
        Assert.True(File.Exists(Out(fixture, @"Forms\frmA.xaml.cs")));
    }
}
