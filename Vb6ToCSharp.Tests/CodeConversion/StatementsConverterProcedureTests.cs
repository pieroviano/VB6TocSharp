using System.Text.RegularExpressions;
using Vb6ToCSharp.CodeConversion;

using Vb6ToCSharp.Tests.Fixtures;
using static Vb6ToCSharp.Tests.Fixtures.ConverterTestHelpers;

namespace Vb6ToCSharp.Tests.CodeConversion;

/// <summary>Procedure-level VB6 semantics as VB Migration Partner keeps them: GoSub, Resume in handlers, Erl, implicit variables.</summary>
public class StatementsConverterProcedureTests : IClassFixture<ConverterFixture>
{
    private readonly ConverterFixture fixture;

    public StatementsConverterProcedureTests(ConverterFixture fixture)
    {
        this.fixture = fixture;
        ConverterUtils.ReComment("");
        ConverterUtils.InitDeString();
    }

    [Fact]
    public void GoSub_RoutinesBecomeLocalFunctions()
    {
        var cs = Segment("Public Function Total(ByVal n As Long) As Long\n" +
                         "  Dim i As Long, acc As Long\n" +
                         "  For i = 1 To n\n    GoSub AddOne\n  Next\n" +
                         "  On n Mod 2 + 1 GoSub AddOne, AddTwo\n" +
                         "  Total = acc\n  Exit Function\n" +
                         "AddOne:\n  acc = acc + 1\n  Return\n" +
                         "AddTwo:\n  acc = acc + 2\n  Return\n" +
                         "End Function\n");
        Assert.Contains("AddOne();", cs);
        Assert.Contains("case 1: AddOne(); break; case 2: AddTwo(); break;", cs);
        Assert.Matches(@"void AddOne\(\) \{\s*acc = acc \+ 1;\s*return;\s*\}", cs);
        Assert.DoesNotContain("AddOne:", cs);
        Assert.DoesNotContain("TODO", cs);
        AssertCompiles(cs);
    }

    [Fact]
    public void ResumeNextInHandler_ContinuesAfterTheFailingStatement()
    {
        var cs = Segment("Public Function Parse(ByVal s As String) As Long\n" +
                         "  On Error GoTo EH\n" +
                         "  Parse = CLng(s)\n" +
                         "  Parse = Parse * 2\n" +
                         "  Exit Function\n" +
                         "EH:\n" +
                         "  If Err.Number = 13 Then Resume Next\n" +
                         "  Parse = -1\n" +
                         "End Function\n");
        Assert.Contains("switch (vbHandler_EH()) { case -1: return Parse; }", cs);
        Assert.Contains("return 0; // VB6 Resume Next", cs);
        Assert.Matches(@"int vbHandler_EH\(\) \{", cs);
        Assert.DoesNotContain("goto EH", cs);
        AssertCompiles(cs);
    }

    [Fact]
    public void ResumeAndResumeLabel_RetryOrJump()
    {
        var cs = Segment("Public Sub Save()\n" +
                         "  Dim tries As Long\n" +
                         "  On Error GoTo EH\n" +
                         "  Kill \"x.tmp\"\n" +
                         "Done:\n" +
                         "  Exit Sub\n" +
                         "EH:\n" +
                         "  tries = tries + 1\n" +
                         "  If tries < 3 Then Resume\n" +
                         "  Resume Done\n" +
                         "End Sub\n");
        var retry = Regex.Match(cs, @"(vbRetry\d+):").Groups[1].Value;
        Assert.NotEqual("", retry);
        Assert.Contains("case 1: goto " + retry + ";", cs);
        Assert.Contains("case 2: goto Done;", cs);
        Assert.Contains("return 1; // VB6 Resume", cs);
        Assert.Contains("return 2; // VB6 Resume Done", cs);
        AssertCompiles(cs);
    }

    [Fact]
    public void Erl_ReportsTheLastLineNumber()
    {
        var cs = Segment("Public Sub T()\n  On Error GoTo EH\n10 Kill \"a\"\n20 Kill \"b\"\n  Exit Sub\nEH:\n  Debug.Print Erl\nEnd Sub\n");
        Assert.Contains("int vbErl = 0;", cs);
        Assert.Contains("L20:; vbErl = 20;", cs);
        Assert.Contains("SetProjectError(vbErr_, vbErl);", cs);
        AssertCompiles(cs);
    }

    [Fact]
    public void NoOptionExplicit_AssignedNamesAreDeclared()
    {
        StatementsConverter.BeginFile("Attribute VB_Name = \"m\"\r\nDefLng I-N\r\n");
        string cs;
        try { cs = Segment("Public Sub T()\n  total = 5\n  For idx = 1 To 3\n  Next\n  Twice 2\nEnd Sub\n"); }
        finally { Begin(); }
        Assert.Contains("dynamic total = null; // VB6 implicit declaration", cs); // an implicit Variant is late-bound
        Assert.DoesNotContain("int idx", cs); // DefLng is applied by ConvertGlobals, not seen here
        Assert.Contains("dynamic idx = null;", cs);
        Assert.DoesNotContain("Twice =", cs);
    }

    [Fact]
    public void OptionExplicit_DeclaresNothingImplicitly() => Assert.DoesNotContain("implicit declaration", Segment("Public Sub T()\n  total = 5\nEnd Sub\n"));
}
