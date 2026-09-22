using Vb6ToCSharp.UpgradeHelpers;

namespace Vb6ToCSharp.Tests;

/// <summary>Vb6ToCSharp.UpgradeHelpers runtime used by converted code: VB6 arrays, UDTs, fixed-length strings.</summary>
public class RuntimeTests
{
    private struct Rec : IVbStruct
    {
        public string Name;
        public int[] Values;

        public void Initialize()
        {
            Name = VbRuntime.FixedLen("", 3);
            Values = VbRuntime.NewArray<int>(2);
        }
    }

    [Fact]
    public void NewArray_InitializesStringsAndUdts()
    {
        Assert.Equal(new[] { "", "" }, VbRuntime.NewArray<string>(2));
        var recs = VbRuntime.NewArray<Rec>(2);
        Assert.Equal("   ", recs[1].Name);
        Assert.Equal(2, recs[1].Values.Length);
        Assert.Equal(new int[3], VbRuntime.NewArray<int>(3));
    }

    [Fact]
    public void ReDim_NewAndPreserve()
    {
        var a = VbRuntime.ReDim(new[] { 1, 2, 3 }, 5, true);
        Assert.Equal(new[] { 1, 2, 3, 0, 0 }, a);
        Assert.Equal(new[] { 0, 0 }, VbRuntime.ReDim(a, 2));
        Assert.Equal(new[] { "", "" }, VbRuntime.ReDim<string>(null, 2));
        Assert.Empty(VbRuntime.ReDim(a, 0));
        Assert.Throws<IndexOutOfRangeException>(() => VbRuntime.ReDim(a, -1));
    }

    [Fact]
    public void ReDim_TwoDimensionsPreserveOverlap()
    {
        var g = new int[2, 2];
        g[1, 1] = 7;
        var r = VbRuntime.ReDim(g, 3, 1, true);
        Assert.Equal(3, r.GetLength(0));
        Assert.Equal(1, r.GetLength(1));
        Assert.Equal(0, r[1, 0]);
    }

    [Fact]
    public void Vb6Array_HonoursTheLowerBound()
    {
        var a = new VB6Array<string>(1, 3);
        Assert.Equal(1, a.LBound);
        Assert.Equal(3, a.UBound);
        Assert.Equal("", a[1]);
        a[3] = "c";
        Assert.Equal("c", a[3]);
        Assert.Throws<IndexOutOfRangeException>(() => a[0]);
        Assert.Equal(3, VbRuntime.UBound(a));
        a.ReDim(1, 5, true);
        Assert.Equal("c", a[3]);
        Assert.Equal(5, a.UBound);
        a.Erase(false);
        Assert.Equal(0, a.Length);
    }

    [Fact]
    public void Vb6Array_ElementsOfUdtsAreAssignedInPlace()
    {
        var a = new VB6Array<Rec>(1, 2);
        a[2].Name = "abc";
        Assert.Equal("abc", a[2].Name);
        Assert.Equal("   ", a[1].Name);
    }

    [Theory]
    [InlineData("ab", 4, "ab  ")]
    [InlineData("abcdef", 4, "abcd")]
    [InlineData(null, 2, "  ")]
    public void FixedLen_PadsOrTruncates(string? value, int len, string expected) => Assert.Equal(expected, VbRuntime.FixedLen(value!, len));

    [Fact]
    public void MidStmt_ReplacesInPlaceWithoutChangingLength()
    {
        var s = "abcdef";
        VbRuntime.MidStmt(ref s, 2, 3, "XYZW");
        Assert.Equal("aXYZef", s);
        VbRuntime.MidStmt(ref s, 5, "12345");
        Assert.Equal("aXYZ12", s);
        Assert.Throws<ArgumentException>(() => VbRuntime.MidStmt(ref s, 7, "x"));
    }

    [Fact]
    public void TextCompare_IgnoresCase() => Assert.Equal(0, VbRuntime.TextCompare("ABC", "abc"));
}
