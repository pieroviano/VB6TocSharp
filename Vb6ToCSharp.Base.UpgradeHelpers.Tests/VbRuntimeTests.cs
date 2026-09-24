using Vb6ToCSharp.UpgradeHelpers.Tests.Fixtures;
using Vb6ToCSharp.UpgradeHelpers.Arrays;
using Vb6ToCSharp.UpgradeHelpers;

namespace Vb6ToCSharp.UpgradeHelpers.Tests;

/// <summary>Vb6ToCSharp.UpgradeHelpers runtime used by converted code: arrays, UDTs, fixed-length strings.</summary>
public class VbRuntimeTests
{
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

    [Fact]
    public void ComInvoke_CallsTheMemberByName()
    {
        Assert.Equal("a|b", VbRuntime.ComInvoke(new LateBound(), "Join", "a", "b"));
    }

    [Fact]
    public void ComInvoke_PassesATypeLibraryConstantAsItsNumber()
    {
        // an ADO constant reaches COM as the Long VB6 sees, not as the value type the converted project declares
        Assert.Equal(7, VbRuntime.ComInvoke(new LateBound(), "Number", new Const7()));
    }

    [Fact]
    public void ComInvoke_LeavesAnOmittedArgumentUnsupplied()
    {
        Assert.Equal("x-", VbRuntime.ComInvoke(new LateBound(), "Optional2", "x", VbRuntime.Missing));
    }

    [Fact]
    public void ComInvoke_RefusesNoTarget() => Assert.Throws<ArgumentNullException>(() => VbRuntime.ComInvoke(null, "Join"));

    private sealed class Const7 : Vb6ToCSharp.UpgradeHelpers.Interop.IVbLibraryConstant
    {
        public int Value => 7;
    }

    private sealed class LateBound
    {
        public string Join(string a, string b) => a + "|" + b;
        public int Number(int n) => n;
        public string Optional2(string a, string b = "-") => a + b;
    }
}
