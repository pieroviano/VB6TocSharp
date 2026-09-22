using System.IO;
using Vb6ToCSharp.Modules;

namespace Vb6ToCSharp.Tests;

/// <summary>Project-specific rules read from the INI; each test removes the sections it writes.</summary>
public class ConfigTests : IClassFixture<ConverterFixture>
{
    private readonly ConverterFixture fixture;
    private static string Ini => ModConfig.IniFile();

    public ConfigTests(ConverterFixture fixture) => this.fixture = fixture;

    private static void WithSection(string section, Action a, params (string key, string value)[] entries)
    {
        foreach (var (k, v) in entries) ModIni.IniWrite(section, k, v, Ini);
        ModConfig.LoadSettings(true);
        try { a(); }
        finally
        {
            ModIni.IniWrite(section, null!, null!, Ini); // deletes the section
            ModConfig.LoadSettings(true);
        }
    }

    [Fact]
    public void DataTypes_ConfigExtendsAndOverridesMapping()
    {
        WithSection(ModConfig.iniSectionDataTypes, () =>
        {
            Assert.Equal("MyCs", ModVb6ToCs.ConvertDataType("MyType"));
            Assert.Equal("double", ModVb6ToCs.ConvertDataType("Double"));
            Assert.Equal("int", ModVb6ToCs.ConvertDataType("Long"));
        }, ("MyType", "MyCs"), ("Double", "double"));
        Assert.Equal("decimal", ModVb6ToCs.ConvertDataType("Double"));
    }

    [Fact]
    public void DataTypes_WinCdsTypesAreNoLongerBuiltIn() => Assert.Equal("int", ModVb6ToCs.ConvertDataType("Long"));

    [Fact]
    public void Controls_ConfigProvidesAllFields()
    {
        WithSection(ModConfig.iniSectionControls, () =>
        {
            ModVb6ToCs.ControlData("Acme.Grid", out var name, out var cont, out var def, out var features);
            Assert.Equal(("DataGrid", true, "Text", "Tooltiptext"), (name, cont, def, features));
            ModVb6ToCs.ControlData("Acme.Btn", out name, out cont, out def, out features);
            Assert.Equal(("Button", false, "Caption", ""), (name, cont, def, features));
        }, ("Acme.Grid", "DataGrid;1;Text;Tooltiptext"), ("Acme.Btn", "Button"));
    }

    [Fact]
    public void Controls_BuiltInsStillWork()
    {
        ModVb6ToCs.ControlData("VB.CommandButton", out var name, out _, out _, out _);
        Assert.Equal("Button", name);
        ModVb6ToCs.ControlData("WinCDS.CandyButton", out name, out _, out _, out _);
        Assert.Equal("Label", name); // unknown without the WinCDS config
    }

    [Fact]
    public void FormRenames_AppliesToVbpForms()
    {
        WithSection(ModConfig.iniSectionFormRenames, () => Assert.Equal("frmRenamed", ModProjectFiles.VbpForms(ModConfig.VbpFile)),
            ("FRMA.frm", "frmRenamed"));
        Assert.Equal("frmA.frm", ModProjectFiles.VbpForms(ModConfig.VbpFile));
    }

    [Theory]
    [InlineData("a ref b", "replace|ref |", "a b")]
    [InlineData("IsIn(ref x)", "ifcontains|IsIn(|ref |", "IsIn(x)")]
    [InlineData("Other(ref x)", "ifcontains|IsIn(|ref |", "Other(ref x)")]
    [InlineData("F(x) + F ", "regex|F([ ,)])|F()$1", "F(x) + F() ")]
    [InlineData("SetCustomFrame(1);", "blankif|SetCustomFrame", "")]
    [InlineData("keep", "bogus|x|y", "keep")]
    [InlineData("keep", "replace||y", "keep")]
    public void PostCodeLineRule_Kinds(string line, string rule, string expected) => Assert.Equal(expected, ModProjectSpecific.ApplyPostCodeLineRule(line, rule));

    [Fact]
    public void PostCodeLine_ConfigRulesRunAfterGenericFixes()
    {
        WithSection(ModConfig.iniSectionPostCodeLine, () =>
        {
            Assert.Equal("x = w.hWnd(); // gone", ModProjectSpecific.ProjectSpecificPostCodeLineConvert("x = w.hwnd; DisposeDA"));
        }, ("1", "replace|DisposeDA|// gone"));
        Assert.Equal("x = w.hWnd(); DisposeDA", ModProjectSpecific.ProjectSpecificPostCodeLineConvert("x = w.hwnd; DisposeDA"));
    }

    [Fact]
    public void WinCdsSample_IsValidConfig()
    {
        var sample = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VB6toCS.WinCDS.sample.ini");
        var keys = ModIni.IniSectionKeys(sample, ModConfig.iniSectionPostCodeLine).Where(k => !k.StartsWith(";")).ToArray();
        Assert.Equal(17, keys.Length);
        foreach (var k in keys)
        {
            var rule = ModIni.IniRead(ModConfig.iniSectionPostCodeLine, k, sample);
            Assert.Contains(rule.Split('|')[0], new[] { "replace", "ifcontains", "regex", "blankif" });
        }
        Assert.Equal("// DisposeDA()", ModProjectSpecific.ApplyPostCodeLineRule("DisposeDA()", ModIni.IniRead(ModConfig.iniSectionPostCodeLine, "1", sample)));
        Assert.Equal("IsIn(x, a)", ModProjectSpecific.ApplyPostCodeLineRule("IsIn(ref x, a)", ModIni.IniRead(ModConfig.iniSectionPostCodeLine, "2", sample)));
        Assert.Equal("UGridIO", ModIni.IniRead(ModConfig.iniSectionControls, "WinCDS.UGridIO", sample));
    }

    [Fact]
    public void OutputFolder_DefaultsUnderProjectFolder()
    {
        var saved = ModIni.IniRead(ModConfig.iniSectionSettings, ModConfig.iniKeyOutputFolder, Ini);
        ModIni.IniWrite(ModConfig.iniSectionSettings, ModConfig.iniKeyOutputFolder, null!, Ini);
        ModConfig.LoadSettings(true);
        try
        {
            Assert.Equal(Path.Combine(fixture.Dir, "converted") + "\\", ModConfig.OutputFolder(), StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            ModIni.IniWrite(ModConfig.iniSectionSettings, ModConfig.iniKeyOutputFolder, saved, Ini);
            ModConfig.LoadSettings(true);
        }
    }
}
