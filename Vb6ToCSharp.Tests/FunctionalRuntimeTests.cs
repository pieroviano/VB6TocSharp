using System.Linq;
using Vb6ToCSharp.Modules;
using Vb6ToCSharp.UpgradeHelpers;

namespace Vb6ToCSharp.Tests;

/// <summary>Pure lexical helpers and the UpgradeHelpers runtime: edges (empty, bounds, repeated calls, invalid input).</summary>
public class FunctionalRuntimeTests
{
    private sealed class RefStruct : IVbStruct
    {
        public bool Initialized;
        public void Initialize() => Initialized = true;
    }

    // ---------------------------------------------------------------- lexical helpers

    [Theory]
    [InlineData("", new[] { "" })]
    [InlineData("a", new[] { "a" })]
    [InlineData("a,", new[] { "a", "" })]
    [InlineData(" a , b ", new[] { "a", "b" })]
    [InlineData("f(a, g(b, c)), d", new[] { "f(a, g(b, c))", "d" })]
    public void SplitTopLevel_Edges(string s, string[] expected) => Assert.Equal(expected, ModConvertStatements.SplitTopLevel(s));

    [Fact]
    public void SplitTopLevel_OtherSeparator() => Assert.Equal(new[] { "a", "f(b; c)", "d" }, ModConvertStatements.SplitTopLevel("a; f(b; c); d", ';'));

    [Theory]
    [InlineData("(a)", 0, 2)]
    [InlineData("f(a(b), c) + 1", 1, 9)]
    [InlineData("(a", 0, -1)]
    public void MatchParen_Edges(string s, int open, int expected) => Assert.Equal(expected, ModConvertStatements.MatchParen(s, open));

    [Theory]
    [InlineData("&H0", "0x0")]
    [InlineData("&H7FFF", "0x7FFF")]
    [InlineData("&H8000", "-32768")]
    [InlineData("&HFFFF", "-1")]
    [InlineData("&H10000", "0x10000")] // too big for Integer: a Long
    [InlineData("&H80000000", "-2147483648")]
    [InlineData("&O10", "8")]
    [InlineData("&O177777", "-1")] // octal follows the same Integer typing
    [InlineData("&HG1", null)]
    [InlineData("&O9", null)]
    [InlineData("&H", null)]
    public void RadixLiteral_Edges(string vb, string? cs) => Assert.Equal(cs, ModConvertStatements.ConvertRadixLiteral(vb));

    [Theory]
    [InlineData("s$", "s", "String")]
    [InlineData("n%", "n", "Integer")]
    [InlineData("x", "x", "")]
    [InlineData("$", "$", "")]
    [InlineData("1&", "1&", "")] // not an identifier
    public void StripSuffix_Edges(string name, string stripped, string type)
    {
        var n = name;
        Assert.Equal(type, ModConvertStatements.StripSuffix(ref n));
        Assert.Equal(stripped, n);
    }

    [Theory]
    [InlineData("10", "L10")]
    [InlineData(" Retry ", "Retry")]
    [InlineData("L10", "L10")]
    public void LabelName_Edges(string vb, string cs) => Assert.Equal(cs, ModConvertStatements.LabelName(vb));

    [Theory]
    [InlineData("#1/2/2003#", "DateTime.Parse(\"1/2/2003\", System.Globalization.CultureInfo.InvariantCulture)")]
    [InlineData("#2003-01-02 10:30#", "DateTime.Parse(\"2003-01-02 10:30\", System.Globalization.CultureInfo.InvariantCulture)")]
    [InlineData("Print #1, a", "Print #1, a")]
    [InlineData("#1#", "#1#")]
    public void DateLiterals_Edges(string vb, string cs) => Assert.Equal(cs, ModConvertStatements.ConvertDateLiterals(vb));

    [Theory]
    [InlineData("Not a = b And Not c = d", "Not (a = b) And Not (c = d)")]
    [InlineData("Not x", "Not x")]
    [InlineData("f(Not a = b)", "f(Not a = b)")] // inner expressions are grouped when they are converted
    [InlineData("Nothing = x", "Nothing = x")]
    [InlineData("IsNot a = b", "IsNot a = b")]
    public void GroupNot_Edges(string vb, string expected) => Assert.Equal(expected, ModConvertStatements.GroupNot(vb));

    [Fact]
    public void GroupNot_IsIdempotent()
    {
        var once = ModConvertStatements.GroupNot("Not a Is Nothing Or b");
        Assert.Equal(once, ModConvertStatements.GroupNot(once));
    }

    // ---------------------------------------------------------------- VbRuntime / VB6Array

    [Fact]
    public void DefaultOf_ClassImplementingIVbStructIsNull() => Assert.Null(VbRuntime.DefaultOf<RefStruct>());

    [Fact]
    public void NewArray_NegativeCountIsSubscriptOutOfRange()
    {
        Assert.Throws<IndexOutOfRangeException>(() => VbRuntime.NewArray<int>(-1));
        Assert.Throws<IndexOutOfRangeException>(() => VbRuntime.NewArray<int>(2, -1)); // same VB6 error for any rank
    }

    [Fact]
    public void ReDim_TwoDimensionsShrinkKeepsOverlap()
    {
        var g = VbRuntime.NewArray<string>(3, 3);
        g[0, 0] = "a";
        g[2, 2] = "z";
        var r = VbRuntime.ReDim(g, 2, 2, true);
        Assert.Equal("a", r[0, 0]);
        Assert.Equal("", r[1, 1]); // new strings are ""
    }

    [Fact]
    public void Vb6Array_PreserveAcrossALowerBoundChange()
    {
        var a = new VB6Array<int>(1, 3);
        a[1] = 10;
        a[3] = 30;
        a.ReDim(2, 5, true); // indexes 2..3 survive
        Assert.Equal(2, a.LBound);
        Assert.Equal(0, a[2]);
        Assert.Equal(30, a[3]);
        Assert.Equal(4, a.Length);
    }

    [Fact]
    public void Vb6Array_EnumeratesInIndexOrderAndCopies()
    {
        var a = new VB6Array<int>(-1, 1);
        a[-1] = 1;
        a[0] = 2;
        a[1] = 3;
        Assert.Equal(new[] { 1, 2, 3 }, a.ToArray());
        Assert.Equal(new[] { 1, 2, 3 }, a.ToList());
        var copy = a.ToArray();
        copy[0] = 99;
        Assert.Equal(1, a[-1]); // ToArray is a copy
    }

    [Fact]
    public void Vb6Array_EraseFixedResetsElementsKeepsBounds()
    {
        var a = new VB6Array<string>(1, 2);
        a[1] = "x";
        a.Erase(true);
        Assert.Equal("", a[1]);
        Assert.Equal(2, a.UBound);
    }

    [Fact]
    public void Vb6Array_EmptyWhenUpperBelowLower()
    {
        var a = new VB6Array<int>(5, 2);
        Assert.Equal(0, a.Length);
        Assert.Equal(4, a.UBound);
        Assert.Empty(a);
    }

    [Fact]
    public void FixedLen_ExactLengthIsUnchanged() => Assert.Equal("abc", VbRuntime.FixedLen("abc", 3));

    [Theory]
    [InlineData("a", "B", -1)]
    [InlineData("B", "a", 1)]
    [InlineData(null, "", 0)]
    public void TextCompare_OrdersIgnoringCase(string? a, string b, int expected) => Assert.Equal(expected, VbRuntime.TextCompare(a!, b));

    [Fact]
    public void MidStmt_NullTargetIsInvalidCall()
    {
        string? s = null;
        Assert.Throws<ArgumentException>(() => VbRuntime.MidStmt(ref s!, 1, "x")); // "" has no position 1
    }
}
