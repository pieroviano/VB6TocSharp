using Vb6ToCSharp.Modules;

namespace Vb6ToCSharp.Tests;

/// <summary>VB6 class modules as VB Migration Partner converts them (Initialize/Terminate, Implements, default member, WithEvents...).</summary>
public partial class ConverterTests
{
    private static string Cls(string name, string body, string attributes = "") => TestUtil.WithTimeout(() =>
    {
        try
        {
            return ModConvert.ConvertClassSource(("VERSION 1.0 CLASS\nBEGIN\n  MultiUse = -1\nEND\nAttribute VB_Name = \"" + name + "\"\n" + attributes +
                                                  "Option Explicit\n" + body).Replace("\n", "\r\n"));
        }
        finally { Begin(); }
    }, 30000);

    [Fact]
    public void ClassInitializeAndTerminate_BecomeConstructorAndDispose()
    {
        var cs = Cls("CLog", "Private mOpen As Boolean\n\nPrivate Sub Class_Initialize()\n  mOpen = True\nEnd Sub\n\nPrivate Sub Class_Terminate()\n  mOpen = False\nEnd Sub\n");
        Assert.Contains("public class CLog : IDisposable {", cs);
        Assert.Contains("public CLog() {", cs);
        Assert.Contains("Class_Initialize();", cs);
        Assert.Contains("public void Dispose() {", cs);
        Assert.Contains("~CLog() {", cs);
        AssertParses(cs);
    }

    [Fact]
    public void Implements_EmptyClassIsAnInterface_AndMembersAreExplicit()
    {
        var impl = Cls("CCircle", "Implements IShape\nPrivate mR As Double\n\n" +
                                  "Private Function IShape_Area() As Double\n  IShape_Area = 3.14 * mR * mR\nEnd Function\n\n" +
                                  "Private Property Get IShape_Name() As String\n  IShape_Name = \"circle\"\nEnd Property\n");
        var iface = Cls("IShape", "Public Function Area() As Double\nEnd Function\n\nPublic Property Get Name() As String\nEnd Property\n");
        Assert.Contains("public class CCircle : IShape {", impl);
        Assert.Contains("double IShape.Area() {", impl);
        Assert.Contains("string IShape.Name {", impl);
        Assert.Contains("public interface IShape {", iface);
        Assert.Contains("double Area();", iface);
        Assert.Contains("string Name { get; }", iface);
        AssertCompilesTop(iface + "\r\n" + impl);
    }

    [Fact]
    public void DefaultMemberAndNewEnum_GiveIndexerAndForEach()
    {
        var cs = Cls("CItems",
            "Private mCol As New Collection\n\n" +
            "Public Property Get Item(ByVal Index As Variant) As Variant\n  Attribute Item.VB_UserMemId = 0\n  Item = mCol(Index)\nEnd Property\n\n" +
            "Public Property Get NewEnum() As IUnknown\n  Attribute NewEnum.VB_UserMemId = -4\n  Set NewEnum = mCol.[_NewEnum]\nEnd Property\n");
        Assert.DoesNotContain("DefaultMember", cs); // the indexer is the default member (C# forbids the attribute with an indexer)
        Assert.Contains("[System.Runtime.CompilerServices.IndexerName(\"DefaultItem\")]", cs); // not "Item": the method has that name
        Assert.Contains("public class CItems : System.Collections.IEnumerable {", cs);
        Assert.Contains("public dynamic this[dynamic Index] { get { return Item(Index); } }", cs); // Variant is late-bound: dynamic
        Assert.Contains("return ((System.Collections.IEnumerable)mCol).GetEnumerator();", cs);
        Assert.Contains("public dynamic Item(dynamic Index) {", cs); // a property with parameters is a method
        Assert.Contains("Item = mCol[Index];", cs); // Collection's default member
        AssertParses(cs);
    }

    [Fact]
    public void ParameterizedLet_IsASetterMethod()
    {
        var cs = Cls("CGrid", "Private mV(9) As Long\n\n" +
                              "Public Property Get Cell(ByVal i As Long) As Long\n  Cell = mV(i)\nEnd Property\n\n" +
                              "Public Property Let Cell(ByVal i As Long, ByVal v As Long)\n  mV(i) = v\nEnd Property\n\n" +
                              "Public Sub Reset()\n  Cell(0) = 5\nEnd Sub\n");
        Assert.Contains("public int Cell(int i) {", cs);
        Assert.Contains("public void set_Cell(int i, int value) {", cs);
        Assert.Contains("mV[i] = value;", cs);
        Assert.Contains("set_Cell(0, 5);", cs);
        AssertCompilesTop(cs);
    }

    [Fact]
    public void PredeclaredClass_HasADefaultInstance()
    {
        var cs = Cls("CApp", "Public Sub Run()\nEnd Sub\n", "Attribute VB_PredeclaredId = True\n");
        Assert.Contains("public static CApp instance { get => vbDefaultInstance ?? (vbDefaultInstance = new CApp());", cs);
    }

    [Fact]
    public void WithEvents_SubscribesTheHandlers()
    {
        var cs = Cls("CWatch", "Private WithEvents mTimer As CTicker\n\nPrivate Sub mTimer_Tick(ByVal n As Long)\n  Debug.Print n\nEnd Sub\n");
        Assert.Contains("private CTicker _mTimer;", cs);
        Assert.Contains("if (_mTimer != null) { _mTimer.Tick -= mTimer_Tick; }", cs); // not a project class: the event is used as named
        Assert.Contains("_mTimer.Tick += mTimer_Tick;", cs);
        Assert.Contains("TODO: check the handler signatures", cs);
        AssertParses(cs);
    }

    [Theory]
    [InlineData("b = s = t", "b = TextCompare(s, t) == 0;")]
    [InlineData("b = s < t", "b = TextCompare(s, t) < 0;")]
    [InlineData("b = s Like \"a*\"", "LikeOperator.LikeString(s, \"a*\", CompareMethod.Text)")]
    [InlineData("n = InStr(s, t)", "InStr(s, t, CompareMethod.Text)")]
    [InlineData("s = Replace(s, t, \"x\")", "Replace(s, t, \"x\", 1, -1, CompareMethod.Text)")]
    public void OptionCompareText_IgnoresCase(string vb, string expected)
    {
        var cs = Module("Option Compare Text\n", "Public Sub T()\n  Dim s As String, t As String, b As Boolean, n As Long\n  " + vb + "\nEnd Sub\n");
        Assert.Contains(expected, cs);
    }

    [Fact]
    public void BinaryCompare_RelationalOnStringsIsOrdinal()
    {
        var cs = Segment(Sub("  Dim s As String, t As String, b As Boolean", "  b = s < t", "  b = s = t"));
        Assert.Contains("b = string.CompareOrdinal(s, t) < 0;", cs);
        Assert.Contains("b = s == t;", cs);
    }
}
