using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Vb6ToCSharp.ItemConversion;
using Vb6ToCSharp.Modules;
using Vb6ToCSharp.Parsing;
using Vb6ToCSharp.Tests.Infrastructure;

namespace Vb6ToCSharp.Tests;

/// <summary>VB6 statements and forms beyond the basic control flow: error handling, file I/O, arrays, labels, directives.</summary>
public partial class ConverterTests
{
    /// <summary>Full pipeline (SanitizeCode splits single-line Ifs, ':' lists and line numbers before ConvertSub).</summary>
    private static string Segment(string vb) => TestUtil.WithTimeout(() => CodeConverter.ConvertCodeSegment(vb.Replace("\r\n", "\n").Replace("\n", "\r\n"), true), 30000);

    /// <summary>The generated members are syntactically valid C#.</summary>
    private static void AssertParses(string members)
    {
        var tree = CSharpSyntaxTree.ParseText("class C {\r\n" + members + "\r\n}");
        var errors = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors) + "\n" + members);
    }

    private static int Pos(string s, string find)
    {
        var i = s.IndexOf(find, StringComparison.Ordinal);
        Assert.True(i >= 0, "missing: " + find + "\n" + s);
        return i;
    }

    // ---------------------------------------------------------------- error handling

    [Fact]
    public void OnErrorResumeNext_GuardsEachSimpleStatement()
    {
        var cs = Segment(Sub("  On Error Resume Next", "  Kill \"x.txt\"", "  If Err.Number <> 0 Then Debug.Print Err.Description"));
        Assert.Contains("Err().Clear(); // On Error Resume Next", cs);
        Assert.Matches(@"try \{\s*Kill\([^)]*\);\s*\} catch \(Exception vbErr_\) \{ " + Regex.Escape(StatementsConverter.SetProjectError) + @"\(vbErr_\); \}", cs);
        Assert.Contains("if (Err().Number != 0) {", cs); // the condition is not wrapped
        Assert.Matches(@"try \{\s*Console\.WriteLine\(Err\(\)\.Description\);", cs);
    }

    [Fact]
    public void OnErrorGoTo0_StopsGuarding()
    {
        var cs = Convert(Sub("  Dim n As Long", "  On Error Resume Next", "  n = 1", "  On Error GoTo 0", "  n = 2"));
        Assert.Matches(@"try \{\s*n = 1;", cs);
        Assert.DoesNotMatch(@"try \{\s*n = 2;", cs);
    }

    [Fact]
    public void OnErrorGoTo_ProtectsBodyAndKeepsLabelsOutsideTheTry()
    {
        var vb = "Public Function Load(ByVal P As String) As String\n" +
                 "  On Error GoTo EH\n" +
                 "  Dim s As String\n" +
                 "  s = Dir(P)\n" +
                 "  Load = s\n" +
                 "ExitHere:\n" +
                 "  Exit Function\n" +
                 "EH:\n" +
                 "  MsgBox Err.Description\n" +
                 "  Resume ExitHere\n" +
                 "End Function\n";
        var cs = Segment(vb);
        AssertParses(cs);
        Assert.Contains("goto EH;", cs);
        Assert.Contains(StatementsConverter.SetProjectError + "(vbErr_);", cs);
        // the declaration is hoisted out of the protected block
        Assert.True(Pos(cs, "string s = \"\";") < Pos(cs, "try {"));
        // ExitHere is reachable from the handler: it closes the first block, the code after it is protected again
        var firstCatch = Pos(cs, "} catch (Exception vbErr_)");
        var exitHere = Pos(cs, "ExitHere:;");
        Assert.True(firstCatch < exitHere);
        Assert.Contains("try {", cs.Substring(exitHere));
        // the handler runs unprotected and Resume label jumps back
        var handler = cs.Substring(Pos(cs, "EH:;"));
        Assert.DoesNotContain("try {", handler);
        Assert.Matches(@"Err\(\)\.Clear\(\);\s*goto ExitHere;", handler);
    }

    [Fact]
    public void OnErrorGoTo_InsideABlock_ClosesAtTheBlockEnd()
    {
        var vb = "Public Sub T(ByVal a As Boolean)\n" +
                 "  Dim n As Long\n" +
                 "  If a Then\n" +
                 "    On Error GoTo EH\n" +
                 "    n = 1\n" +
                 "  Else\n" +
                 "    n = 2\n" +
                 "  End If\n" +
                 "  n = 3\n" +
                 "  Exit Sub\n" +
                 "EH:\n" +
                 "  n = 4\n" +
                 "End Sub\n";
        var cs = Segment(vb);
        AssertParses(cs);
        Assert.Equal(3, Regex.Matches(cs, "catch \\(Exception vbErr_\\)").Count); // then-branch, else-branch, after End If
    }

    [Theory]
    [InlineData("Resume Next", "TODO: VB6 Resume Next")]
    [InlineData("Resume", "TODO: VB6 Resume (retry")]
    [InlineData("Resume 100", "goto L100;")]
    [InlineData("Error 53", "Err().Raise(53);")]
    [InlineData("On Error GoTo -1", "Err().Clear();")]
    public void ErrorStatements(string vb, string expected) => Assert.Contains(expected, Convert(Sub("  " + vb)));

    // ---------------------------------------------------------------- control flow

    [Fact]
    public void WhileWend_IsAWhileLoop()
    {
        var cs = Flat(Convert(Sub("  Dim i As Long", "  While i < 3", "    i = i + 1", "  Wend")));
        Assert.Matches(@"while\( ?i < 3 ?\) \{ i = i \+ 1; \}", cs);
    }

    [Fact]
    public void NextWithSeveralCounters_ClosesEveryLoop()
    {
        var cs = Flat(Convert(Sub("  Dim i As Long, j As Long", "  For i = 1 To 2", "    For j = 1 To 2", "      Debug.Print i", "  Next j, i", "  Debug.Print j")));
        Assert.Matches(@"\} \} Console\.WriteLine\(j\);", cs);
    }

    [Fact]
    public void SelectCase_IsAndToRanges_UseGuardedPatterns()
    {
        var cs = Flat(Convert(Sub("  Dim x As Long, y As Long", "  Select Case x", "    Case 1 To 3, Is > 10", "      y = 1", "    Case 5", "      y = 2", "  End Select")));
        Assert.Contains("case var vbCase_ when (vbCase_ >= 1 && vbCase_ <= 3) || vbCase_ > 10:", cs);
        Assert.Contains("case 5:", cs);
    }

    [Fact]
    public void ExitLoopInsideSelect_JumpsPastTheLoop()
    {
        var cs = Convert(Sub("  Dim i As Long", "  For i = 1 To 9", "    Select Case i", "      Case 3", "        Exit For", "    End Select", "  Next", "  Debug.Print i"));
        var label = Regex.Match(cs, @"goto (exitFor\d+);").Groups[1].Value;
        Assert.NotEqual("", label);
        Assert.Matches(@"\}\s*" + label + @":;\s*Console\.WriteLine", cs);
    }

    [Fact]
    public void ExitDoFromInnerFor_JumpsPastTheDo()
    {
        var cs = Segment(Sub("  Dim i As Long", "  Do", "    For i = 1 To 9", "      If i = 3 Then Exit Do", "    Next", "  Loop"));
        Assert.Matches(@"goto exitDo\d+;", cs);
    }

    [Fact]
    public void ExitLoopDirectly_IsABreak()
    {
        var cs = Segment(Sub("  Dim i As Long", "  For i = 1 To 9", "    If i = 3 Then Exit For", "  Next"));
        Assert.Contains("break;", cs);
        Assert.DoesNotContain("goto", cs);
    }

    [Fact]
    public void OnGoTo_IsAComputedJump()
    {
        var cs = Flat(Segment(Sub("  Dim n As Long", "  On n GoTo 10, Done", "10 n = 1", "Done:")));
        Assert.Contains("switch (Conversions.ToInteger(n)) { case 1: goto L10; case 2: goto Done; }", cs);
        Assert.Contains("L10:", cs);
    }

    [Fact]
    public void LineNumbersAndLabels_BecomeCSharpLabels()
    {
        var cs = Segment(Sub("  Dim n As Long", "100 n = 1", "  If n = 1 Then GoTo 100", "Again: n = n + 1", "  If n < 3 Then 100"));
        Assert.Contains("L100:", cs);
        Assert.Contains("Again:", cs);
        Assert.Equal(2, Regex.Matches(cs, "goto L100;").Count); // "Then 100" is an implicit GoTo
        Assert.DoesNotContain("Again()", cs);
    }

    [Fact]
    public void IndentedIdentifierBeforeColon_IsAStatement()
    {
        var cs = Segment(Sub("  DoEvents: Beep"));
        Assert.Contains("DoEvents()", cs);
        Assert.Contains("Beep()", cs);
    }

    [Fact]
    public void NestedSingleLineIf_ElseBindsToTheInnerIf()
    {
        var cs = Flat(Segment(Sub("  Dim a As Boolean, b As Boolean, c As Long", "  If a Then If b Then c = 1 Else c = 2")));
        Assert.Matches(@"if \(a\) \{ if \(b\) \{ c = 1; \} else \{ c = 2; \} \}", cs);
    }

    [Fact]
    public void SingleLineIf_WithStatementLists()
    {
        var cs = Flat(Segment(Sub("  Dim a As Boolean, c As Long", "  If a Then c = 1: c = 2 Else c = 3")));
        Assert.Matches(@"if \(a\) \{ c = 1; c = 2; \} else \{ c = 3; \}", cs);
    }

    // ---------------------------------------------------------------- declarations

    [Fact]
    public void Dim_IsHoistedToTheProcedureTop()
    {
        var cs = Convert(Sub("  Dim a As Boolean", "  If a Then", "    Dim n As Long", "    n = 1", "  End If"));
        Assert.True(Pos(cs, "int n = 0;") < Pos(cs, "if (a)"));
    }

    [Fact]
    public void StaticLocal_BecomesAFieldNamedAfterTheProcedure()
    {
        var cs = Segment("Public Sub Tick()\n  Static Count As Long\n  Count = Count + 1\nEnd Sub\n");
        Assert.Contains("private static int Tick_Count = 0;", cs);
        Assert.Contains("Tick_Count = Tick_Count + 1;", cs);
        AssertParses(cs);
    }

    [Fact]
    public void StaticAndFriendProcedures_AreFound()
    {
        var cs = Segment("Private Static Sub A()\n  Dim n As Long\n  n = n + 1\nEnd Sub\n\nFriend Function B() As Long\n  B = 1\nEnd Function\n");
        Assert.Contains("private static int A_n = 0;", cs);
        Assert.Contains("internal static int B(", cs);
        AssertParses(cs);
    }

    [Fact]
    public void TypeCharacters_DeclareTypesAndAreStripped()
    {
        var cs = Convert(Sub("  Dim s$, n%, l&", "  s = Left$(\"abc\", 2)", "  l = 5&"));
        Assert.Contains("string s = \"\";", cs);
        Assert.Contains("short n = 0;", cs); // % is Integer: 16-bit
        Assert.Contains("s = Left(", cs);
        Assert.Contains("l = 5;", cs);
    }

    [Fact]
    public void Arrays_SymbolicAndMultiDimensionalBounds()
    {
        var cs = Convert(Sub("  Dim a(MAXV) As Long, g(2, 3) As String, d() As Long"));
        Assert.Contains("int[] a = new int[MAXV + 1];", cs);
        Assert.Contains("string[,] g = NewArray<string>(3, 4);", cs); // VB6 strings start as ""
        Assert.Contains("int[] d = null;", cs);
    }

    [Fact]
    public void AssignmentToMultiIndexElement_IsAnAssignment()
    {
        var cs = Convert(Sub("  Dim g(2, 3) As Long", "  g(1, 2) = 5"));
        Assert.Contains("g[1, 2] = 5;", cs);
    }

    [Fact]
    public void ReDimAndErase()
    {
        var cs = Convert(Sub("  Dim d() As Long, n As Long", "  ReDim d(n)", "  ReDim Preserve d(n + 1)", "  Erase d"));
        Assert.Contains("d = ReDim(d, n + 1);", cs);
        Assert.Contains("d = ReDim(d, n + 1 + 1, true);", cs);
        Assert.Contains("d = null;", cs); // Erase releases a dynamic array
    }

    [Fact]
    public void ConstList_DeclaresEveryConstant()
    {
        var cs = Convert(Sub("  Const A = 1, B = \"x\", C = &HFF"));
        Assert.Contains("const int A = 1;", cs);
        Assert.Contains("const string B = ", cs);
        Assert.Contains("const int C = 0xFF;", cs);
    }

    [Fact]
    public void PropertyGet_ExitPropertyReturnsTheValue()
    {
        var cs = Segment("Public Property Get Name() As String\n  If mName = \"\" Then Exit Property\n  Name = mName\nEnd Property\n\n" +
                         "Public Property Let Name(ByVal V As String)\n  If V = \"\" Then Exit Property\n  mName = V\nEnd Property\n");
        Assert.Contains("return Name;", cs);
        Assert.Matches(@"set \{\s*if \(value == ""[^""]*""\) \{\s*return;", cs);
        Assert.DoesNotContain("Exit(Property)", cs);
    }

    // ---------------------------------------------------------------- statements and expressions

    [Theory]
    [InlineData("Open P For Input As #1", "FileOpen(1, P, OpenMode.Input);")]
    [InlineData("Open P For Binary Access Read Lock Write As #f Len = 128", "FileOpen(f, P, OpenMode.Binary, OpenAccess.Read, OpenShare.LockWrite, 128);")]
    [InlineData("Close", "FileClose();")]
    [InlineData("Close #1, #f", "FileClose(1, f);")]
    [InlineData("Line Input #f, s", "s = LineInput(f);")]
    [InlineData("Input #f, s, n", "Input(f, ref s);")]
    [InlineData("Print #f, s; n", "PrintLine(f, string.Concat(s, n));")]
    [InlineData("Print #f, s, n;", "Print(f, s, n);")]
    [InlineData("Print #f,", "PrintLine(f);")]
    [InlineData("Write #f, s, n", "WriteLine(f, s, n);")]
    [InlineData("Get #f, , n", "FileGet(f, ref n);")]
    [InlineData("Put #f, 3, n", "FilePut(f, n, 3);")]
    [InlineData("Seek #f, 10", "Seek(f, 10);")]
    [InlineData("Lock #f, 1 To 5", "Lock(f, 1, 5);")]
    [InlineData("Name P As \"b\"", "Rename(P, ")]
    [InlineData("Kill P", "Kill(P);")]
    public void FileStatements(string vb, string expected)
    {
        Assert.Contains(expected, Convert(Sub("  Dim P As String, s As String, n As Long, f As Integer", "  " + vb)));
    }

    [Theory]
    [InlineData("Mid(s, 2, 3) = \"abc\"", "MidStmt(ref s, 2, 3, ")]
    [InlineData("Mid$(s, 2) = t", "MidStmt(ref s, 2, t);")]
    [InlineData("LSet s = t", "s = LSet(t, Len(s));")]
    [InlineData("RSet s = t", "s = RSet(t, Len(s));")]
    [InlineData("Let n = 2", "n = 2;")]
    [InlineData("Stop", "System.Diagnostics.Debugger.Break();")]
    [InlineData("End", "Environment.Exit(0);")]
    [InlineData("Date = d", "DateAndTime.Today = d;")]
    [InlineData("n = 2 ^ 3", "n = Conversions.ToInteger(Pow(2, 3));")] // a Double assigned to a Long rounds as in VB6
    [InlineData("n = n + 2 ^ 3 * 4", "n = Conversions.ToInteger(n + Pow(2, 3) * 4);")]
    [InlineData("b = s Like \"a*\"", "b = LikeOperator.LikeString(s, ")]
    [InlineData("b = TypeOf o Is Collection", "b = o is Collection;")]
    [InlineData("n = n And &HFF", "n = n & 0xFF;")]
    [InlineData("n = n Or 4", "n = n | 4;")]
    [InlineData("b = b And b", "b = b && b;")]
    [InlineData("b = b Xor b", "b = b ^ b;")]
    [InlineData("b = b Imp b", "b = (!(b) || (b));")]
    [InlineData("n = &HFFFF", "n = -1;")]
    [InlineData("n = &HFFFF&", "n = 0xFFFF;")]
    [InlineData("n = &O17", "n = 15;")]
    [InlineData("d = #1/2/2003#", "d = DateTime.Parse(\"1/2/2003\", System.Globalization.CultureInfo.InvariantCulture);")]
    [InlineData("d = #10:30:00 PM#", "DateTime.Parse(\"10:30:00 PM\"")]
    [InlineData("s = Me.Caption", "s = this.Caption;")]
    [InlineData("n = Err", "n = Err().Number;")]
    [InlineData("s = Error", "s = Err().Description;")]
    [InlineData("RaiseEvent Changed(n, True)", "eventChanged?.Invoke(n, true);")]
    [InlineData("b = Not s = t", "b = !(s == t);")]
    [InlineData("b = Not o Is Nothing", "b = !(o == null);")]
    [InlineData("b = Not b And b", "b = !b && b;")]
    [InlineData("Rem a note", "// a note")]
    [InlineData("Debug.Print s; n, t;", "Console.Write(string.Concat(s, n, \"\\t\", t));")]
    [InlineData("Debug.Assert n > 0", "System.Diagnostics.Debug.Assert(n > 0);")]
    [InlineData("Line (0, 0)-(10, 20), n, BF", "Line(0, 0, 10, 20, n, true); // TODO: VB6 BF")]
    [InlineData("Line -(10, 20)", "Line(CurrentX, CurrentY, 10, 20);")]
    [InlineData("Me.PSet (1, 2), n", "this.PSet(1, 2, n);")]
    [InlineData("Circle (5, 5), 3", "Circle(5, 5, 3);")]
    public void StatementsAndExpressions(string vb, string expected)
    {
        Assert.Contains(expected, Convert(Sub("  Dim s As String, t As String, n As Long, b As Boolean, d As Date, o As Object", "  " + vb)));
    }

    [Fact]
    public void TimeLiteral_IsNotSplitAsAStatementList()
    {
        var cs = Segment(Sub("  Dim d As Date", "  d = #10:30:00#: d = d"));
        Assert.Contains("DateTime.Parse(\"10:30:00\"", cs);
    }

    [Fact]
    public void ConditionalCompilation_BecomesPreprocessorDirectives()
    {
        var cs = Convert(Sub("  Dim n As Long", "#If Win32 Then", "  n = 32", "#ElseIf DEBUG_ON = 1 Then", "  n = 1", "#Else", "  n = 16", "#End If"));
        Assert.Contains("#if true", cs);
        Assert.Contains("#elif DEBUG_ON", cs);
        Assert.Contains("#else", cs);
        Assert.Contains("#endif", cs);
    }

    [Fact]
    public void DirectivesBetweenProcedures_StayLive()
    {
        var cs = Segment("#If Win32 Then\nPublic Sub A()\nEnd Sub\n#Else\nPublic Sub A()\nEnd Sub\n#End If\n");
        Assert.Contains("#if true", cs);
        Assert.Contains("#else", cs);
        Assert.DoesNotContain("/*\r\n#", cs);
        AssertParses(cs);
    }

    [Fact]
    public void Globals_EnumTypeConstDefAndImplements()
    {
        var vb = "Option Explicit\nDefInt A-Z\nImplements IFoo\nPublic Const A As Long = 1, B% = 2\nGlobal G As Long\n" +
                 "Public Enum E\n  eA = -1\n  eB = &H8000&\n  [Two Words]\n  eC\nEnd Enum\n" +
                 "Private Type R\n  V(1 To 5) As Long\n  G(2, 3) As Double\nEnd Type\n";
        Begin();
        string cs;
        try { cs = TestUtil.WithTimeout(() => CodeConverter.ConvertGlobals(vb.Replace("\n", "\r\n"), true), 30000); }
        finally { Begin(); } // DefInt applies to the rest of that file only
        Assert.Contains("// Option Explicit", cs);
        Assert.Contains("// VB6 DefInt A-Z", cs);
        Assert.Contains("TODO: VB6 Implements IFoo", cs);
        Assert.Contains("public const int A = 1;", cs);
        Assert.Contains("public const short B = 2;", cs);
        Assert.Contains("public static int G = 0;", cs);
        Assert.Contains("eA = -1", cs);
        Assert.Contains("eB = 0x8000", cs);
        Assert.Contains("Two_Words", cs);
        Assert.Contains("eC", cs);
        // a UDT is a struct (VB6 copies it on assignment); fixed members are set up by Initialize and marshal as in VB6
        Assert.Contains("private struct R : IVbStruct {", cs);
        Assert.Contains("[MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public int[] V;", cs);
        Assert.Contains("public double[,] G;", cs);
        Assert.Contains("V = NewArray<int>(6);", cs);
        Assert.Contains("G = NewArray<double>(3, 4);", cs);
        AssertParses(cs);
    }

    [Fact]
    public void FullModule_ProducesValidCSharp()
    {
        var vb = "Public Function ReadAll(ByVal Path As String) As String\n" +
                 "  On Error GoTo EH\n" +
                 "  Dim f As Integer, s$, Line As String\n" +
                 "  f = FreeFile\n" +
                 "  Open Path For Input As #f\n" +
                 "  Do While Not EOF(f)\n" +
                 "    Line Input #f, Line\n" +
                 "    s = s & Line & vbCrLf\n" +
                 "  Loop\n" +
                 "  Close #f\n" +
                 "  ReadAll = s\n" +
                 "ExitHere:\n" +
                 "  Exit Function\n" +
                 "EH:\n" +
                 "  If Err.Number = 53 Then Resume Next\n" +
                 "  Resume ExitHere\n" +
                 "End Function\n\n" +
                 "Private Static Sub Misc(ByVal a As Long, b$)\n" +
                 "  Dim i%, j&, arr(1 To 10) As String, grid(3, 4) As Double\n" +
                 "  Const K = 3\n" +
                 "  On Error Resume Next\n" +
                 "10 i = 1\n" +
                 "  While i < 5: i = i + 1: Wend\n" +
                 "  For i = 1 To 3: For j = 1 To 3: grid(i, j) = i * j: Next j, i\n" +
                 "  Select Case a\n" +
                 "    Case 1, 2: b = \"low\"\n" +
                 "    Case 3 To 5, Is > 100: b = \"mid\"\n" +
                 "    Case Else: b = \"high\"\n" +
                 "  End Select\n" +
                 "  Mid(b, 2, 1) = \"Z\"\n" +
                 "  If b Like \"a*\" Then i = &HFF And a Else i = 2 ^ 3\n" +
                 "Retry: i = i + 1\n" +
                 "  On a GoTo 10, Retry\n" +
                 "#If Win32 Then\n" +
                 "  j = 32\n" +
                 "#End If\n" +
                 "  Print #1, a; b, \"x\";\n" +
                 "  Get #1, , i\n" +
                 "  Stop\n" +
                 "End Sub\n";
        AssertParses(Segment(vb));
    }
}

/// <summary>
/// Points ModConfig (INI next to the test assembly) at a tiny temp VB6 project so ModRefScan can scan it.
/// </summary>
public sealed class ConverterFixture : IDisposable
{
    public readonly string Dir = TestUtil.TempDir();
    private readonly string ini = ProjectConfigurationParser.IniFile();
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
        IniInterop.IniWrite(ProjectConfigurationParser.iniSectionSettings, ProjectConfigurationParser.iniKeyVbpFile, vbp, ini);
        IniInterop.IniWrite(ProjectConfigurationParser.iniSectionSettings, ProjectConfigurationParser.iniKeyOutputFolder, Path.Combine(Dir, "out") + "\\",
            ini);
        IniInterop.IniWrite(ProjectConfigurationParser.iniSectionSettings, ProjectConfigurationParser.iniKeyAssemblyName, "TestAsm", ini);
        ProjectConfigurationParser.LoadSettings(true);
        ModRefScan.ScanRefs();
    }

    public void Dispose()
    {
        if (iniBackup == null) File.Delete(ini);
        else File.WriteAllText(ini, iniBackup);
        ProjectConfigurationParser.LoadSettings(true);
        try
        {
            File.Delete(refs);
        }
        catch
        {
        }

        try
        {
            Directory.Delete(Dir, true);
        }
        catch
        {
        }
    }
}

public partial class ConverterTests : IClassFixture<ConverterFixture>
{
    private readonly ConverterFixture fixture;

    public ConverterTests(ConverterFixture fixture)
    {
        this.fixture = fixture;
        ConverterUtils.ReComment("");
        ConverterUtils.InitDeString();
    }

    private static string Sub(params string[] body) => "Public Sub T()\r\n" + string.Join("\r\n", body) + "\r\nEnd Sub";
    private static string Convert(string vb) => TestUtil.WithTimeout(() => CodeConverter.ConvertSub(vb, true), 20000);
    private static string Flat(string s) => Regex.Replace(s, @"\s+", " ");

    [Fact]
    public void Config_UsesIniSettings() => Assert.Equal("TestAsm", ProjectConfigurationParser.AssemblyName());

    [Fact]
    public void ScanRefs_IndexesProjectFunctions()
    {
        Assert.True(ModRefScan.IsFuncRef("Twice"));
        Assert.Equal("modA", ModRefScan.FuncRefModule("Twice"));
        Assert.False(ModRefScan.IsFuncRef("NoSuchThing"));
    }

    [Fact]
    public void FuncRefDeclArgCnt_CountsArguments() =>
        Assert.Equal(2, TestUtil.WithTimeout(() => ModRefScan.FuncRefDeclArgCnt("Add2")));

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
        var cs = Flat(Convert(Sub("  Dim x As Long", "  Dim y As Long", "  Select Case x", "    Case 1, 2, 3",
            "      y = 1", "    Case Else", "      y = 2", "  End Select")));
        Assert.Contains("case 1:", cs);
        Assert.Contains("case 2:", cs);
        Assert.Contains("case 3:", cs);
    }

    [Fact]
    public void ConvertCodeLine_SubCall_ConvertsAllArguments()
    {
        var cs = TestUtil.WithTimeout(() => CodeConverter.ConvertCodeLine("Foo a, b, c"));
        Assert.Matches(@"Foo\(a, ?b, ?c\)", cs);
    }

    [Fact]
    public void ConvertApiDef_ConvertsAllArguments()
    {
        var vb = ConverterUtils.DeString(
            "Private Declare Function SendMessage Lib \"user32\" Alias \"SendMessageA\" (ByVal hWnd As Long, ByVal wMsg As Long) As Long");
        var cs = TestUtil.WithTimeout(() => CodeConverter.ConvertApiDef(vb));
        Assert.Contains("[DllImport(\"user32.dll\", EntryPoint = \"SendMessageA\")]", cs);
        Assert.Contains("private static extern", cs);
        Assert.Contains("hWnd", cs);
        Assert.Contains("wMsg", cs);
    }

    [Fact]
    public void ConvertEvent_ConvertsAllArguments()
    {
        var cs = TestUtil.WithTimeout(() =>
            CodeConverter.ConvertEvent("Public Event Changed(ByVal A As Long, ByVal B As String)"));
        Assert.Contains("ChangedHandler(", cs);
        Assert.Matches(@"ChangedHandler\([^)]*\bA\b[^)]*\bB\b[^)]*\)", cs);
    }

    [Fact]
    public void SanitizeCode_SplitsColonStatementsOnce()
    {
        var s = TestUtil.WithTimeout(() => CodeConverter.SanitizeCode("a = 1: b = 2: c = 3"));
        var lines = s.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(new[] { "a = 1", "b = 2", "c = 3" }, lines);
    }

    [Fact]
    public void ConvertCodeSegment_ConvertsEveryProcedure()
    {
        const string vb =
            "Public Sub First()\r\n  Dim x As Long\r\n  x = 1\r\nEnd Sub\r\n\r\nPublic Sub Second()\r\n  Dim y As Long\r\n  y = 2\r\nEnd Sub\r\n";
        var cs = TestUtil.WithTimeout(() => CodeConverter.ConvertCodeSegment(vb, true), 20000);
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
        var cs = Flat(Convert(Sub("  Dim i As Long", "  Dim n As Long", "  Dim s As Long", "  For i = 1 To n Step s",
            "    Debug.Print i", "  Next")));
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
        try
        {
            a();
        }
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
        var notes = CaptureNotify(() =>
            ok = TestUtil.WithTimeout(() => CodeConverter.ConvertFile(Path.Combine(fixture.Dir, "modA.bas")), 30000));
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
        CaptureNotify(() => Assert.True(TestUtil.WithTimeout(() => CodeConverter.ConvertFile(f), 30000)));
        File.WriteAllText(Out(fixture, @"Modules\modA.cs"), "// ### CONVERTED\r\n");
        CaptureNotify(() => Assert.False(TestUtil.WithTimeout(() => CodeConverter.ConvertFile(f), 30000)));
    }

    [Fact]
    public void ConvertFile_Form_WritesXamlAndCodeBehind()
    {
        CleanOut();
        var ok = false;
        CaptureNotify(() =>
            ok = TestUtil.WithTimeout(() => CodeConverter.ConvertFile(Path.Combine(fixture.Dir, "frmA.frm")), 30000));
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
        var notes = CaptureNotify(() => ok = CodeConverter.ConvertFile(Path.Combine(fixture.Dir, "x.txt")));
        Assert.False(ok);
        Assert.Single(notes);
        Assert.Contains("UNKNOWN VB TYPE", notes[0]);
    }

    [Fact]
    public void ConvertModule_MissingFile_NotifiesAndFails()
    {
        var ok = true;
        var notes = CaptureNotify(() => ok = CodeConverter.ConvertModule(Path.Combine(fixture.Dir, "nope.bas")));
        Assert.False(ok);
        Assert.Contains("File not found", Assert.Single(notes));
    }

    [Fact]
    public void ConvertProject_ConvertsEverythingHeadless()
    {
        CleanOut();
        var notes = CaptureNotify(() => TestUtil.WithTimeout(() =>
        {
            CodeConverter.ConvertProject(ProjectConfigurationParser.VbpFile);
            return 0;
        }, 60000));
        Assert.Equal(new[] { "Complete." }, notes);
        Assert.True(File.Exists(Out(fixture, @"Modules\modA.cs")));
        Assert.True(File.Exists(Out(fixture, @"Forms\frmA.xaml")));
        Assert.True(File.Exists(Out(fixture, @"Forms\frmA.xaml.cs")));
    }
}