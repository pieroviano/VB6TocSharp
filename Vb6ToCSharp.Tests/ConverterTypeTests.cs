using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Vb6ToCSharp.CodeConversion;
using Vb6ToCSharp.Tests.Infrastructure;

namespace Vb6ToCSharp.Tests;

/// <summary>VB6 data types as VB Migration Partner maps them: widths, implicit conversions, arrays, UDTs, fixed strings.</summary>
public partial class ConverterTests
{
    private const string CompileUsings =
        "using System;\r\nusing System.Runtime.InteropServices;\r\nusing Microsoft.VisualBasic;\r\nusing Microsoft.VisualBasic.CompilerServices;\r\n" +
        "using static Microsoft.VisualBasic.Strings;\r\nusing static Microsoft.VisualBasic.Information;\r\nusing static Microsoft.VisualBasic.Interaction;\r\n" +
        "using static Microsoft.VisualBasic.Conversion;\r\nusing static Microsoft.VisualBasic.FileSystem;\r\nusing static Microsoft.VisualBasic.DateAndTime;\r\n" +
        "using static System.Math;\r\nusing Vb6ToCSharp.UpgradeHelpers;\r\nusing static Vb6ToCSharp.UpgradeHelpers.VbRuntime;\r\n";

    /// <summary>The generated members compile (types and conversions, not only syntax) against the VB runtime and the helpers.</summary>
    private static void AssertCompiles(string members) => AssertCompilesTop("public static class M {\r\n" + members + "\r\n}");

    /// <summary>Top-level declarations (classes, interfaces) compile against the VB runtime and the helpers.</summary>
    private static void AssertCompilesTop(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(CompileUsings + code);
        var refs = new[] { typeof(object), typeof(Microsoft.VisualBasic.Strings), typeof(Vb6ToCSharp.UpgradeHelpers.VbRuntime), typeof(System.Linq.Enumerable) }
            .Select(t => MetadataReference.CreateFromFile(t.Assembly.Location))
            .Concat(new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location.Replace("mscorlib.dll", "System.dll")) });
        var comp = CSharpCompilation.Create("check", new[] { tree }, refs, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var errors = comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors) + "\n" + code);
    }

    /// <summary>Globals + procedures of one module, with its per-file options.</summary>
    private static string Module(string globals, string code)
    {
        Begin();
        try
        {
            var g = TestUtil.WithTimeout(() => CodeConverter.ConvertGlobals(globals.Replace("\r\n", "\n").Replace("\n", "\r\n"), true), 30000);
            return g + "\r\n" + Segment(code);
        }
        finally { Begin(); }
    }

    [Fact]
    public void IntegerIsShort_AndAssignmentsConvertAsVb6()
    {
        var cs = Convert(Sub("  Dim i As Integer, n As Long, d As Double, b As Byte", "  i = 5", "  i = i + 1", "  n = d * 2", "  b = n", "  d = n / 2"));
        Assert.Contains("short i = 0;", cs);
        Assert.Contains("i = 5;", cs); // a constant fits
        Assert.Contains("i = Conversions.ToShort(i + 1);", cs);
        Assert.Contains("n = Conversions.ToInteger(d * 2);", cs); // VB6 rounds to even
        Assert.Contains("b = Conversions.ToByte(n);", cs);
        Assert.Contains("d = (double)n / 2;", cs); // VB6 "/" is never an integer division
    }

    [Theory]
    [InlineData("If n Then x = 1", "if (n != 0)")]
    [InlineData("If Not n Then x = 1", "if (~(n) != 0)")] // VB6 Not is bitwise: Not 5 is True
    [InlineData("If InStr(s, \"a\") Then x = 1", "!= 0)")]
    [InlineData("If v Then x = 1", "if (Conversions.ToBoolean(v))")]
    [InlineData("If ok Then x = 1", "if (ok)")]
    public void NumericConditions_AreTestedAgainstZero(string vb, string expected)
    {
        Assert.Contains(expected, Segment(Sub("  Dim n As Long, x As Long, s As String, v As Variant, ok As Boolean", "  " + vb)));
    }

    [Fact]
    public void FixedLengthStrings_KeepTheirLength()
    {
        var local = Segment(Sub("  Dim s As String * 5", "  s = \"ab\""));
        Assert.Contains("string s = new string(' ', 5);", local);
        Assert.Contains("s = FixedLen(\"ab\", 5);", local);
        var field = Module("Private mCode As String * 8\n", "Public Sub T()\n  mCode = \"x\"\nEnd Sub\n");
        Assert.Contains("private static string _mCode = new string(' ', 8);", field);
        Assert.Contains("string mCode { get => _mCode; set => _mCode = FixedLen(value, 8); }", field);
    }

    [Fact]
    public void AsNewField_IsAutoInstancing()
    {
        var cs = Module("Private mCol As New Collection\n", "Public Sub T()\n  mCol.Add 1\nEnd Sub\n");
        Assert.Contains("private static Collection _mCol;", cs);
        Assert.Contains("Collection mCol { get => _mCol ?? (_mCol = new Collection()); set => _mCol = value; }", cs);
    }

    [Fact]
    public void LowerBoundsAndOptionBase_UseVb6Array()
    {
        var cs = Module("Option Base 1\nPrivate mA(3) As Long\n",
            "Public Sub T(ByVal n As Long)\n  Dim b(0 To 2) As String, c(5 To 9) As Long, d() As Long\n  ReDim d(n)\n  mA(1) = c(5)\n  Erase c\nEnd Sub\n");
        Assert.Contains("VB6Array<int> mA = new VB6Array<int>(1, 3);", cs);
        Assert.Contains("string[] b = NewArray<string>(3);", cs); // explicit zero lower bound
        Assert.Contains("VB6Array<int> c = new VB6Array<int>(5, 9);", cs);
        Assert.Contains("VB6Array<int> d = new VB6Array<int>(1);", cs);
        Assert.Contains("d.ReDim(1, n);", cs);
        Assert.Contains("mA[1] = c[5];", cs); // module arrays are known in procedures
        Assert.Contains("c.Erase(true);", cs);
        AssertCompiles(cs);
    }

    [Fact]
    public void Udt_IsAStructInitializedLikeVb6()
    {
        var cs = Module("Private Type Rec\n  Name As String * 10\n  Tags() As String\n  Score As Double\nEnd Type\n",
            "Public Sub T()\n  Dim r As Rec, list(2) As Rec, other As Rec\n  r.Score = 1.5\n  list(1).Name = \"a\"\n  other = r\nEnd Sub\n");
        Assert.Contains("[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 10)] public string Name;", cs);
        Assert.Contains("Rec r = NewStruct<Rec>();", cs);
        Assert.Contains("Rec[] list = NewArray<Rec>(3);", cs);
        Assert.Contains("list[1].Name = \"a\";", cs);
        AssertCompiles(cs);
    }

    [Fact]
    public void DefType_TypesUndeclaredTypes()
    {
        var cs = Module("DefLng A-M\nDefStr N-Z\n", "Public Function F(a, s)\n  Dim k, x\n  x = s\n  F = k + a\nEnd Function\n");
        Assert.Contains("int k = 0;", cs);
        Assert.Contains("string x = \"\";", cs);
        Assert.Contains("int F(int a, string s)", cs.Replace("ref ", "")); // return and parameter types too
    }

    [Fact]
    public void NullIsDbNull() => Assert.Contains("v = DBNull.Value;", Convert(Sub("  Dim v As Variant", "  v = Null")));

    [Fact]
    public void ArgumentsAreConvertedToTheParameterType() => Assert.Contains("Twice(Conversions.ToInteger(", TestUtil.WithTimeout(() => CodeConverter.ConvertCodeLine("x = Twice(1.5)")));

    [Fact]
    public void ConvertedArithmetic_Compiles()
    {
        var cs = Module("Private mTotal As Currency\n",
            "Public Function Avg(ByVal a As Integer, ByVal b As Integer) As Single\n" +
            "  Dim s As Single, n As Long, i As Integer, t As String * 4\n" +
            "  For i = a To b\n    n = n + i\n  Next\n" +
            "  s = n / (b - a + 1)\n  mTotal = mTotal + s\n  t = CStr(n)\n" +
            "  If n Then Avg = s Else Avg = 0\n" +
            "End Function\n");
        AssertCompiles(cs);
    }
}
