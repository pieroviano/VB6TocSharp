using Vb6ToCSharp.UpgradeHelpers.Tests.Fixtures;
using Vb6ToCSharp.UpgradeHelpers.Arrays;
using Vb6ToCSharp.UpgradeHelpers;

namespace Vb6ToCSharp.UpgradeHelpers.Tests.Arrays;

/// <summary>VB6 arrays with a lower bound other than zero (Dim a(1 To 10), Option Base 1).</summary>
public class VB6ArrayTests
{
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
}
