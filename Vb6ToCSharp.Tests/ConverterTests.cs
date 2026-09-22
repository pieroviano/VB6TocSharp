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
        File.WriteAllText(vbp, "Type=Exe\r\nModule=modA; modA.bas\r\n");
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
    public ConverterTests(ConverterFixture _)
    {
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
}
