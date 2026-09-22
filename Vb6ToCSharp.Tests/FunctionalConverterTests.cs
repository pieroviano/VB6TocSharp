using System.Collections.Generic;
using System.Linq;
using Vb6ToCSharp.FormConversion;
using Vb6ToCSharp.Modules;

namespace Vb6ToCSharp.Tests;

/// <summary>Edges of the statement / expression / class / pragma helpers (they call ConvertValue: need the project fixture).</summary>
public partial class ConverterTests
{
    // ---------------------------------------------------------------- file statements

    [Theory]
    [InlineData("Open P For Append As 3", "FileOpen(3, P, OpenMode.Append);")]
    [InlineData("Open P As #1 Len = 64", "FileOpen(1, P, OpenMode.Random, OpenAccess.Default, OpenShare.Default, 64);")] // no For: Random
    [InlineData("Open P For Output Access Write Shared As #f", "FileOpen(f, P, OpenMode.Output, OpenAccess.Write, OpenShare.Shared);")]
    [InlineData("Close 2", "FileClose(2);")]
    [InlineData("Print #1, Spc(3); s", "PrintLine(1, string.Concat(SPC(3), s));")]
    [InlineData("Print #1, Tab(10), s", "PrintLine(1, TAB(10), s);")]
    [InlineData("Print #1", "PrintLine(1);")]
    [InlineData("Write #1,", "WriteLine(1);")]
    [InlineData("Input #1, a, b", "Input(1, ref a);\r\n  Input(1, ref b);")]
    [InlineData("Get 1, 7, a", "FileGet(1, ref a, 7);")]
    [InlineData("Lock #1", "Lock(1);")]
    [InlineData("Lock #1, To 5", "Lock(1, 1, 5);")]
    [InlineData("Unlock #1, 3", "Unlock(1, 3);")]
    [InlineData("Width #1, 80", "FileWidth(1, 80);")]
    public void FileStatement_Forms(string vb, string expected) => Assert.Equal("  " + expected, ModConvertStatements.ConvertFileStatement(vb, 2));

    [Theory]
    [InlineData("Name = x")]
    [InlineData("Closed = True")]
    [InlineData("Printer.Print x")]
    [InlineData("Line = 5")]
    public void FileStatement_OtherStatementsAreNotFileIo(string vb) => Assert.Null(ModConvertStatements.ConvertFileStatement(vb, 0));

    // ---------------------------------------------------------------- Select Case / jumps / errors

    [Theory]
    [InlineData("1, -2, &HFF", "case 1: case -2: case 0xFF:")]
    [InlineData("Is >= 5", "case var vbCase_ when vbCase_ >= 5:")]
    [InlineData("Is <> 3", "case var vbCase_ when vbCase_ != 3:")]
    [InlineData("x, 4", "case var vbCase_ when vbCase_ == x || vbCase_ == 4:")] // a variable is not a constant
    public void CaseLabels_Forms(string vb, string expected) => Assert.Equal(expected, ModConvertStatements.ConvertCaseLabels(vb));

    [Fact]
    public void CaseLabels_StringRangeComparesStrings()
    {
        // C# has no >= on strings: a VB6 string range must compare with string ordering
        ModConvertUtils.InitDeString();
        var a = ModConvertUtils.DeString("\"a\"");
        var m = ModConvertUtils.DeString("\"m\"");
        var cs = ModConvertStatements.ConvertCaseLabels(a + " To " + m + ", Is > " + m);
        Assert.Contains("string.CompareOrdinal(vbCase_, " + a + ") >= 0 && string.CompareOrdinal(vbCase_, " + m + ") <= 0", cs);
        Assert.Contains("string.CompareOrdinal(vbCase_, " + m + ") > 0", cs);
    }

    [Theory]
    [InlineData("On x GoSub A, , B", "switch (Conversions.ToInteger(x)) { case 1: A(); break; case 3: B(); break; }")]
    [InlineData("On x GoTo 10, 20", "switch (Conversions.ToInteger(x)) { case 1: goto L10; case 2: goto L20; }")]
    public void OnGoTo_Forms(string vb, string expected) => Assert.Equal(expected, ModConvertStatements.ConvertOnGoTo(vb, 0));

    [Fact]
    public void OnGoTo_NotAComputedJump_IsNull() => Assert.Null(ModConvertStatements.ConvertOnGoTo("On Error Resume Next", 0));

    [Theory]
    [InlineData("On Local Error Resume Next", ModConvertStatements.ErrorScope.Modes.ResumeNext)]
    [InlineData("On Error GoTo 100", ModConvertStatements.ErrorScope.Modes.GoTo)]
    [InlineData("On Error GoTo 0", ModConvertStatements.ErrorScope.Modes.None)]
    public void OnError_SetsTheMode(string vb, ModConvertStatements.ErrorScope.Modes mode)
    {
        var scope = new ModConvertStatements.ErrorScope { Mode = ModConvertStatements.ErrorScope.Modes.ResumeNext };
        var ind = 0;
        ModConvertStatements.ConvertOnError(vb, scope, ref ind);
        Assert.Equal(mode, scope.Mode);
        if (mode == ModConvertStatements.ErrorScope.Modes.GoTo) Assert.Equal("L100", scope.Handler);
    }

    [Fact]
    public void HandlerGoTo_JumpsInsteadOfRetrying()
    {
        // a handler leaving with GoTo must jump to the label, not resume (retry) the failing statement
        var cs = Segment("Public Sub T()\n  On Error GoTo EH\n  Kill \"a\"\nCleanup:\n  Exit Sub\nEH:\n  If Err.Number = 53 Then Resume Next\n  GoTo Cleanup\nEnd Sub\n");
        Assert.Contains("case 2: goto Cleanup;", cs);
        Assert.Contains("return 2; // VB6 GoTo Cleanup", cs);
        Assert.DoesNotContain("return 1;", cs);
        AssertCompiles(cs);
    }

    // ---------------------------------------------------------------- procedure plan

    [Fact]
    public void ProcedurePlan_FindsRoutinesHandlersAndTargets()
    {
        var plan = ModConvertStatements.ProcedurePlan.Scan(new[]
        {
            "Public Sub T()", "  On Error GoTo EH", "  GoSub R1", "  On n GoSub R2, R3", "L10:", "  GoTo Done", "R1:", "  Return",
            "EH:", "  Resume Next", "Done:", "End Sub",
        });
        Assert.Equal(new[] { "R1", "R2", "R3" }, plan.GoSubTargets.OrderBy(x => x));
        Assert.True(plan.Handlers.ContainsKey("EH"));
        Assert.False(plan.Handlers["EH"].Retry);
        Assert.Contains("Done", plan.JumpTargets);
        Assert.True(plan.HasLineNumbers);
        Assert.True(plan.HasErrorHandling);
    }

    [Fact]
    public void ProcedurePlan_HandlerWithoutResume_IsNotRouted()
    {
        var plan = ModConvertStatements.ProcedurePlan.Scan(new[] { "  On Error GoTo EH", "EH:", "  MsgBox 1", "End Sub" });
        Assert.False(plan.IsRouted("EH")); // the try/catch form is enough
    }

    // ---------------------------------------------------------------- expression types and conversions

    [Theory]
    [InlineData("n + 1", "Long")]
    [InlineData("i * i", "Integer")]
    [InlineData("n / 2", "Double")]
    [InlineData("2 ^ i", "Double")]
    [InlineData("s & n", "String")]
    [InlineData("n > 1", "Boolean")]
    [InlineData("c + d", "Double")] // Currency with Double
    [InlineData("c + n", "Currency")]
    [InlineData("(n)", "Long")]
    [InlineData("-d", "Double")]
    [InlineData("n \\ 2", "Long")]
    [InlineData("unknownThing + 1", "")]
    public void ExprType_FollowsVbPromotion(string vb, string expected)
    {
        var cs = Convert(Sub("  Dim n As Long, i As Integer, s As String, c As Currency, d As Double", "  n = 0")); // declares the locals
        Assert.NotNull(cs);
        Assert.Equal(expected, ModConvertStatements.ExprType(vb));
    }

    [Theory]
    [InlineData("Integer", "5", "5", "5")]
    [InlineData("Integer", "-5", "-5", "-5")]
    [InlineData("Integer", "n", "n", "Conversions.ToShort(n)")]
    [InlineData("Long", "i", "i", "i")] // Integer widens implicitly
    [InlineData("Long", "d", "d", "Conversions.ToInteger(d)")]
    [InlineData("Long", "b", "b", "Conversions.ToInteger(b)")] // True is -1
    [InlineData("String", "n", "n", "Conversions.ToString(n)")]
    [InlineData("Double", "c", "c", "Conversions.ToDouble(c)")]
    [InlineData("Double", "n", "n", "n")]
    [InlineData("Variant", "n", "n", "n")]
    [InlineData("Currency", "d", "d", "Conversions.ToDecimal(d)")]
    public void ImplicitConversion_Matrix(string target, string raw, string cs, string expected)
    {
        Convert(Sub("  Dim n As Long, i As Integer, d As Double, b As Boolean, c As Currency", "  n = 0"));
        Assert.Equal(expected, ModConvertStatements.ImplicitConversion(target, raw, cs));
    }

    [Fact]
    public void Condition_NotOnDoubleRoundsLikeVb()
    {
        // VB6 Not converts to Long with rounding: Not -0.6 is Not -1 = 0 (False); truncation would give ~0 = -1 (True)
        var cs = Convert(Sub("  Dim d As Double", "  d = 1"));
        Assert.NotNull(cs);
        Assert.Equal("~(Conversions.ToLong(d)) != 0", ModConvertStatements.ConditionValue("Not d"));
    }

    [Theory]
    [InlineData("b", "b")]
    [InlineData("Not b", "!b")]
    [InlineData("s", "Conversions.ToBoolean(s)")]
    [InlineData("n > 0", "n > 0")]
    public void Condition_Forms(string vb, string expected)
    {
        Convert(Sub("  Dim b As Boolean, s As String, n As Long", "  n = 0"));
        Assert.Equal(expected, ModConvertStatements.ConditionValue(vb));
    }

    // ---------------------------------------------------------------- statements

    [Theory]
    [InlineData("Mid(s, 1) = t", "MidStmt(ref s, 1, t);")]
    [InlineData("MidB(s, 2, 1) = t", "MidStmt(ref s, 2, 1, t);")]
    public void MidStatement_Forms(string vb, string expected) => Assert.Equal(expected, ModConvertStatements.ConvertMidStatement(vb, 0));

    [Theory]
    [InlineData("Mid(s, 1)")]
    [InlineData("x = Mid(s, 1)")]
    [InlineData("Mid(s, 1) & t")]
    public void MidStatement_MidFunctionIsNotTheStatement(string vb) => Assert.Null(ModConvertStatements.ConvertMidStatement(vb, 0));

    [Theory]
    [InlineData("", "Console.WriteLine()")]
    [InlineData("a", "Console.WriteLine(a)")]
    [InlineData("a,", "Console.Write(string.Concat(a, \"\\t\"))")]
    [InlineData("a, b", "Console.WriteLine(string.Concat(a, \"\\t\", b))")]
    public void DebugPrint_Forms(string list, string expected) => Assert.Equal(expected, ModConvertStatements.ConvertDebugPrint(list));

    [Theory]
    [InlineData("Line (1, 2)-Step(3, 4)", "Line(1, 2, 3, 4); // TODO: VB6 Step (relative coordinates)")]
    [InlineData("pic.Circle (1, 1), 5, , , , 0.5", "pic.Circle(1, 1, 5, default, default, default, 0.5); // TODO: VB6 omitted argument // TODO: VB6 omitted argument // TODO: VB6 omitted argument")]
    public void Graphics_Forms(string vb, string expected) => Assert.Equal(expected, ModConvertStatements.ConvertGraphicsStatement(vb, 0));

    [Theory]
    [InlineData("Line Input #1, s")]
    [InlineData("LineCount = 3")]
    [InlineData("Circle")]
    public void Graphics_OtherStatementsAreNull(string vb) => Assert.Null(ModConvertStatements.ConvertGraphicsStatement(vb, 0));

    // ---------------------------------------------------------------- classes

    [Fact]
    public void ClassModel_ReadsAttributesAndMembers()
    {
        var src = "Attribute VB_Name = \"C\"\r\nAttribute VB_PredeclaredId = True\r\nImplements IA\r\nImplements Lib.IB\r\nPublic Event Changed(ByVal x As Long)\r\n" +
                  "Public Property Get Item(ByVal i As Long) As String\r\nAttribute Item.VB_UserMemId = 0\r\nEnd Property\r\n" +
                  "Public Property Let Item(ByVal i As Long, ByVal v As String)\r\nEnd Property\r\n" +
                  "' Private Sub Class_Initialize()\r\nPrivate Sub Class_Terminate()\r\nEnd Sub\r\n";
        var m = ModConvertClasses.ClassModel.Scan("C", src);
        Assert.Equal(new[] { "IA", "Lib.IB" }, m.Implements);
        Assert.True(m.Predeclared);
        Assert.False(m.HasInitialize); // commented out
        Assert.True(m.HasTerminate);
        Assert.Equal("Item", m.DefaultMember);
        Assert.Equal("ByVal i As Long", m.DefaultParams);
        Assert.Equal("String", m.DefaultType);
        Assert.True(m.DefaultHasSetter);
        Assert.Equal(new[] { "Changed" }, m.Events);
        Assert.Contains("Item", m.ParameterizedSetters);
    }

    [Fact]
    public void ImplementedInterface_OnlyForImplementedPrefixes()
    {
        ModConvertClasses.Current = ModConvertClasses.ClassModel.Scan("C", "Implements IFoo\r\n");
        try
        {
            Assert.Equal("IFoo", ModConvertClasses.ImplementedInterface("IFoo_Bar"));
            Assert.Null(ModConvertClasses.ImplementedInterface("IFoo_"));
            Assert.Null(ModConvertClasses.ImplementedInterface("IFooBar"));
            Assert.Null(ModConvertClasses.ImplementedInterface("Other_Bar"));
        }
        finally
        {
            ModConvertClasses.Current = null;
        }
    }

    [Fact]
    public void InterfaceDeclaration_ClassWithCodeStaysAClass() =>
        Assert.Null(ModConvertClasses.InterfaceDeclaration(ModConvertClasses.ClassModel.Scan("I", "Public Sub A()\r\n  Beep\r\nEnd Sub\r\n")));

    [Fact]
    public void InterfaceDeclaration_PublicFieldsAndLetOnlyProperties()
    {
        var cs = ModConvertClasses.InterfaceDeclaration(ModConvertClasses.ClassModel.Scan("I",
            "Public Count As Long\r\nPublic Property Let Name(ByVal v As String)\r\nEnd Property\r\nPublic Sub Run(ByVal n As Long)\r\nEnd Sub\r\n"));
        Assert.Contains("int Count { get; set; }", cs);
        Assert.Contains("string Name { set; }", cs);
        Assert.Contains("void Run(int n);", cs);
    }

    // ---------------------------------------------------------------- pragmas, VBP, report

    [Theory]
    [InlineData("'## ArrayBounds ForceZero", "", "ArrayBounds", "ForceZero")]
    [InlineData("  '##x.SetType Long", "x", "SetType", "Long")]
    [InlineData("'## Note   some text ", "", "Note", "some text")]
    public void Pragma_Parse(string line, string target, string name, string args)
    {
        var p = ModConvertPragmas.Parse(line);
        Assert.Equal(target, p.Target);
        Assert.Equal(name, p.Name);
        Assert.Equal(args, p.Args);
    }

    [Theory]
    [InlineData("' ## Note x")]
    [InlineData("'##")]
    [InlineData("x = 1 '## Note y")]
    public void Pragma_ParseRejectsNonPragmas(string line) => Assert.Null(ModConvertPragmas.Parse(line));

    [Fact]
    public void PreProcess_IgnoresMalformedPatternsAndLaterPragmas()
    {
        const string src = "'## PreProcess \"a\"\r\nPublic Sub T()\r\n'## PreProcess \"T\", \"U\"\r\nEnd Sub\r\n";
        Assert.Equal(src, ModConvertPragmas.PreProcess(src)); // malformed, and procedure-level PreProcess is not file-level
    }

    [Fact]
    public void VbpCondComp_ParsesUnquotedAndSpaced()
    {
        var v = VbpInfo.Parse("CondComp=A=1:B = -1 :  C=0\r\n");
        Assert.Equal("1", v.CondComp["A"]);
        Assert.Equal("-1", v.CondComp["b"]);
        Assert.Equal("0", v.CondComp["C"]);
    }

    [Theory]
    [InlineData("VB6 Resume Next (continue after the failing statement) has no C# equivalent", "Error handling")]
    [InlineData("VB Migration Partner pragma not supported: ArrayBoundz x", "Pragmas")]
    [InlineData("this prevents nothing", "Other")] // "event" only as a word
    [InlineData("check the handler signatures against the events of X", "Events")]
    [InlineData("VB6 lower bound 1", "Arrays")]
    public void ReportCategory(string message, string category) => Assert.Equal(category, ModMigrationReport.Category(message));

    [Fact]
    public void Evaluate_DivisionByZeroIsNotAValue() => Assert.Null(ModConvertStatements.Evaluate("1 / 0"));

    [Theory]
    [InlineData("n = a Or b", "n = a | b;")] // two Longs: bitwise
    [InlineData("ok = a = 0 Or b <> 0", "ok = a == 0 || b != 0;")] // comparisons bind tighter: logical
    [InlineData("ok = (a Or b) = 0 Or (a Xor b) <> 0", "ok = (a | b) == 0 || (a ^ b) != 0;")]
    [InlineData("n = a And &HFF", "n = a & 0xFF;")]
    public void AndOr_BitwiseOnNumbersLogicalOnComparisons(string vb, string expected) =>
        Assert.Contains(expected, Convert(Sub("  Dim a As Long, b As Long, n As Long, ok As Boolean", "  " + vb)));

    [Fact]
    public void ReportRender_NoItems() => Assert.Equal("# Migration report: p\r\n\r\n0 C# files, 0 items to review.\r\n", ModMigrationReport.Render(new List<ModMigrationReport.Issue>(), 0, "p"));
}
