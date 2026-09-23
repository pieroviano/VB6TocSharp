using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Vb6ToCSharp.CodeConversion;
using Vb6ToCSharp.Infrastructure;
using Vb6ToCSharp.Parsing.Model;
using Vb6ToCSharp.Tests.Infrastructure;

namespace Vb6ToCSharp.Tests;

/// <summary>
/// VB6 conditional compilation (#If/#ElseIf/#Else/#End If/#Const, project CondComp). In the ConverterTests collection:
/// the file's constants are converter state, so these must not run in parallel with other conversions.
/// </summary>
public partial class ConverterTests
{
    /// <summary>Sets the constants of the "file" being converted; an empty source resets them.</summary>
    private static string Begin(string vb = "", Dictionary<string, string>? project = null) => StatementsConverter.BeginFile(vb.Replace("\n", "\r\n"), project);

    private static string Directive(string vb, string module = "", Dictionary<string, string>? project = null)
    {
        Begin(module, project);
        try { return StatementsConverter.ConvertDirective(vb); }
        finally { Begin(); }
    }

    private static void AssertParsesWith(string members, params string[] symbols)
    {
        var tree = CSharpSyntaxTree.ParseText("class C {\r\n" + members + "\r\n}", new CSharpParseOptions(preprocessorSymbols: symbols));
        var errors = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, "symbols: " + string.Join(",", symbols) + "\n" + string.Join("\n", errors) + "\n" + members);
    }

    [Theory]
    [InlineData("#If Win32 Then", "#if true")]
    [InlineData("#If Win16 Or DBG Then", "#if false || DBG")]
    [InlineData("#If Not DBG And X = 0 Then", "#if !DBG && !X")]
    [InlineData("#ElseIf X <> 0 Then", "#elif X")]
    [InlineData("#If DBG = True Then ' trailing comment", "#if DBG")]
    [InlineData("#Else", "#else")]
    [InlineData("#End If", "#endif")]
    public void BooleanConditions_StayLive(string vb, string cs) => Assert.Equal(cs, Directive(vb));

    [Fact]
    public void ModuleConst_BecomesDefineAtTheFileTop()
    {
        var header = Begin("#Const DBG = 1\n#Const OLD = False\n#Const LEVEL = DBG + 2\nSub A()\nEnd Sub\n");
        Begin();
        Assert.Equal("#define DBG\r\n#undef OLD\r\n#define LEVEL\r\n", header);
        Assert.StartsWith("// VB6 #Const DBG = 1", StatementsConverter.ConvertDirective("#Const DBG = 1"));
    }

    [Fact]
    public void NumericConditions_AreEvaluatedWithTheKnownConstants()
    {
        Assert.Equal("#if true // VB6: VER >= 2", Directive("#If VER >= 2 Then", project: new Dictionary<string, string> { ["VER"] = "3" }));
        Assert.Equal("#if false // VB6: VER >= 2", Directive("#If VER >= 2 Then")); // undefined: Empty (0)
        Assert.Equal("#elif true // VB6: LEVEL * 2 = 6", Directive("#ElseIf LEVEL * 2 = 6 Then", "#Const LEVEL = 3\n"));
    }

    [Fact]
    public void SymbolTest_ThatDiffersFromVb_IsEvaluatedInstead()
    {
        // DBG = 2: "DBG = 1" is False in VB6, but the symbol DBG is defined
        Assert.Equal("#if false // VB6: DBG = 1", Directive("#If DBG = 1 Then", "#Const DBG = 2\n"));
        // Not is bitwise in VB6: Not 2 = -3, which is True
        Assert.Equal("#if true // VB6: Not DBG", Directive("#If Not DBG Then", "#Const DBG = 2\n"));
        Assert.Equal("#if DBG", Directive("#If DBG = 1 Then", "#Const DBG = 1\n"));
    }

    [Theory]
    [InlineData("1 + 2 * 3 = 7", -1.0)]
    [InlineData("Not 0", -1.0)]
    [InlineData("&HFF And 15", 15.0)]
    [InlineData("-2 ^ 2", -4.0)]
    [InlineData("7 \\ 2 Mod 2", 1.0)]
    [InlineData("\"a\" & \"b\" = \"ab\"", -1.0)]
    [InlineData("True Xor False", -1.0)]
    [InlineData("UNDEFINED", 0.0)]
    public void Evaluate_UsesVbSemantics(string expr, double expected)
    {
        Begin();
        Assert.Equal(expected, StatementsConverter.Evaluate(expr));
    }

    [Fact]
    public void Evaluate_NotAnExpression_IsNull() => Assert.Null(StatementsConverter.Evaluate("1 +"));

    [Fact]
    public void ProjectConstants_TrueOnesAreSymbols()
    {
        var vbp = ProjectInfo.Parse("Type=Exe\r\nCondComp=\"DEBUG_MODE = 1 : LEGACY = 0 : VER = 3\"\r\n");
        Assert.Equal("3", vbp.CondComp["ver"]);
        Assert.Equal(new[] { "DEBUG_MODE", "VER" }, StatementsConverter.ProjectSymbols(vbp.CondComp));
    }

    [Fact]
    public void BranchesOpeningBlocksDifferently_ParseInEveryConfiguration()
    {
        var vb = "Public Sub T(ByVal x As Long, ByVal y As Long)\n" +
                 "  Dim n As Long\n" +
                 "#If A Then\n" +
                 "  Select Case x\n" +
                 "#Else\n" +
                 "  Select Case y\n" +
                 "#End If\n" +
                 "    Case 1\n" +
                 "      n = 1\n" +
                 "    Case 2\n" +
                 "      n = 2\n" +
                 "  End Select\n" +
                 "#If A Then\n" +
                 "  If x Then\n" +
                 "#Else\n" +
                 "  If y Then\n" +
                 "#End If\n" +
                 "    n = 3\n" +
                 "  End If\n" +
                 "End Sub\n";
        Begin();
        var cs = Segment(vb);
        Assert.Contains("switch (x)", cs);
        Assert.Contains("switch (y)", cs);
        AssertParsesWith(cs);
        AssertParsesWith(cs, "A");
        // the shared "Case 2" closes the section of the one open switch, not of both
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(cs, @"break;\s*case 2:"));
    }

    [Fact]
    public void DirectivesInsideEnumAndType()
    {
        var vb = "Public Enum E\n  eA\n#If A Then\n  eB = 2\n#End If\n  eC\nEnd Enum\n" +
                 "Private Type R\n  X As Long\n#If A Then\n  Y As String\n#End If\nEnd Type\n";
        Begin();
        var cs = TestUtil.WithTimeout(() => CodeConverter.ConvertGlobals(vb.Replace("\n", "\r\n"), true), 30000);
        Assert.Contains("#if A", cs);
        AssertParsesWith(cs);
        AssertParsesWith(cs, "A");
    }

    [Fact]
    public void ConvertFile_ModuleConstDefinesTheSymbol()
    {
        var bas = Path.Combine(fixture.Dir, "modPP.bas");
        File.WriteAllText(bas, "Attribute VB_Name = \"modPP\"\r\nOption Explicit\r\n#Const TRACE_ON = 1\r\n\r\n" +
                               "Public Sub T()\r\n#If TRACE_ON Then\r\n  Debug.Print 1\r\n#End If\r\nEnd Sub\r\n");
        var ok = false;
        CaptureNotify(() => ok = TestUtil.WithTimeout(() => CodeConverter.ConvertFile(bas), 30000));
        Assert.True(ok);
        var cs = File.ReadAllText(Out(fixture, @"Modules\modPP.cs"));
        Assert.StartsWith("#define TRACE_ON", cs.TrimStart());
        Assert.Contains("#if TRACE_ON", cs);
    }
}
