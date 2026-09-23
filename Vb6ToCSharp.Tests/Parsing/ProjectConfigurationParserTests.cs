using System.IO;
using Vb6ToCSharp.CodeConversion;
using Vb6ToCSharp.Infrastructure;
using Vb6ToCSharp.Parsing;

using Vb6ToCSharp.Tests.Fixtures;

namespace Vb6ToCSharp.Tests.Parsing;

/// <summary>Project-specific rules read from the INI; each test removes the sections it writes.</summary>
public class ProjectConfigurationParserTests : IClassFixture<ConverterFixture>
{
    private readonly ConverterFixture fixture;
    private static string Ini => ProjectConfigurationParser.IniFile();

    public ProjectConfigurationParserTests(ConverterFixture fixture) => this.fixture = fixture;

    private static void WithSection(string section, Action a, params (string key, string value)[] entries)
    {
        foreach (var (k, v) in entries) IniInterop.IniWrite(section, k, v, Ini);
        ProjectConfigurationParser.LoadSettings(true);
        try { a(); }
        finally
        {
            IniInterop.IniWrite(section, null!, null!, Ini); // deletes the section
            ProjectConfigurationParser.LoadSettings(true);
        }
    }

    [Fact]
    public void DataTypes_ConfigExtendsAndOverridesMapping()
    {
        WithSection(ProjectConfigurationParser.iniSectionDataTypes, () =>
        {
            Assert.Equal("MyCs", Vb6ToCsConverter.ConvertDataType("MyType"));
            Assert.Equal("decimal", Vb6ToCsConverter.ConvertDataType("Double"));
            Assert.Equal("int", Vb6ToCsConverter.ConvertDataType("Long"));
        }, ("MyType", "MyCs"), ("Double", "decimal"));
        Assert.Equal("double", Vb6ToCsConverter.ConvertDataType("Double")); // VB6 Double is a binary double
    }

    [Fact]
    public void DataTypes_WinCdsTypesAreNoLongerBuiltIn() => Assert.Equal("int", Vb6ToCsConverter.ConvertDataType("Long"));

    [Fact]
    public void Controls_ConfigProvidesAllFields()
    {
        WithSection(ProjectConfigurationParser.iniSectionControls, () =>
        {
            Vb6ToCsConverter.ControlData("Acme.Grid", out var name, out var cont, out var def, out var features);
            Assert.Equal(("DataGrid", true, "Text", "Tooltiptext"), (name, cont, def, features));
            Vb6ToCsConverter.ControlData("Acme.Btn", out name, out cont, out def, out features);
            Assert.Equal(("Button", false, "Caption", ""), (name, cont, def, features));
        }, ("Acme.Grid", "DataGrid;1;Text;Tooltiptext"), ("Acme.Btn", "Button"));
    }

    [Fact]
    public void Controls_BuiltInsStillWork()
    {
        Vb6ToCsConverter.ControlData("VB.CommandButton", out var name, out _, out _, out _);
        Assert.Equal("Button", name);
        Vb6ToCsConverter.ControlData("WinCDS.CandyButton", out name, out _, out _, out _);
        Assert.Equal("Label", name); // unknown without the WinCDS config
    }

    [Fact]
    public void FormRenames_AppliesToVbpForms()
    {
        WithSection(ProjectConfigurationParser.iniSectionFormRenames, () => Assert.Equal("frmRenamed", ProjectFiles.VbpForms(ProjectConfigurationParser.VbpFile)),
            ("FRMA.frm", "frmRenamed"));
        Assert.Equal("frmA.frm", ProjectFiles.VbpForms(ProjectConfigurationParser.VbpFile));
    }

    [Theory]
    [InlineData("a ref b", "replace|ref |", "a b")]
    [InlineData("IsIn(ref x)", "ifcontains|IsIn(|ref |", "IsIn(x)")]
    [InlineData("Other(ref x)", "ifcontains|IsIn(|ref |", "Other(ref x)")]
    [InlineData("F(x) + F ", "regex|F([ ,)])|F()$1", "F(x) + F() ")]
    [InlineData("SetCustomFrame(1);", "blankif|SetCustomFrame", "")]
    [InlineData("keep", "bogus|x|y", "keep")]
    [InlineData("keep", "replace||y", "keep")]
    public void PostCodeLineRule_Kinds(string line, string rule, string expected) => Assert.Equal(expected, ProjectSpecificConverter.ApplyPostCodeLineRule(line, rule));

    [Fact]
    public void PostCodeLine_ConfigRulesRunAfterGenericFixes()
    {
        WithSection(ProjectConfigurationParser.iniSectionPostCodeLine, () =>
        {
            Assert.Equal("x = w.hWnd(); // gone", ProjectSpecificConverter.ProjectSpecificPostCodeLineConvert("x = w.hwnd; DisposeDA"));
        }, ("1", "replace|DisposeDA|// gone"));
        Assert.Equal("x = w.hWnd(); DisposeDA", ProjectSpecificConverter.ProjectSpecificPostCodeLineConvert("x = w.hwnd; DisposeDA"));
    }

    [Fact]
    public void WinCdsSample_IsValidConfig()
    {
        var sample = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Vb6ToCSharp.sample.ini");
        var keys = IniInterop.IniSectionKeys(sample, ProjectConfigurationParser.iniSectionPostCodeLine).Where(k => !k.StartsWith(";")).ToArray();
        Assert.Equal(17, keys.Length);
        foreach (var k in keys)
        {
            var rule = IniInterop.IniRead(ProjectConfigurationParser.iniSectionPostCodeLine, k, sample);
            Assert.Contains(rule.Split('|')[0], new[] { "replace", "ifcontains", "regex", "blankif" });
        }
        Assert.Equal("// DisposeDA()", ProjectSpecificConverter.ApplyPostCodeLineRule("DisposeDA()", IniInterop.IniRead(ProjectConfigurationParser.iniSectionPostCodeLine, "1", sample)));
        Assert.Equal("IsIn(x, a)", ProjectSpecificConverter.ApplyPostCodeLineRule("IsIn(ref x, a)", IniInterop.IniRead(ProjectConfigurationParser.iniSectionPostCodeLine, "2", sample)));
        Assert.Equal("UGridIO", IniInterop.IniRead(ProjectConfigurationParser.iniSectionControls, "WinCDS.UGridIO", sample));
    }

    [Fact]
    public void OutputFolder_DefaultsUnderProjectFolder()
    {
        var saved = IniInterop.IniRead(ProjectConfigurationParser.iniSectionSettings, ProjectConfigurationParser.iniKeyOutputFolder, Ini);
        IniInterop.IniWrite(ProjectConfigurationParser.iniSectionSettings, ProjectConfigurationParser.iniKeyOutputFolder, null!, Ini);
        ProjectConfigurationParser.LoadSettings(true);
        try
        {
            Assert.Equal(Path.Combine(fixture.Dir, "converted") + "\\", ProjectConfigurationParser.OutputFolder(), StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            IniInterop.IniWrite(ProjectConfigurationParser.iniSectionSettings, ProjectConfigurationParser.iniKeyOutputFolder, saved, Ini);
            ProjectConfigurationParser.LoadSettings(true);
        }
    }
}
