using System.Drawing;
using Vb6ToCSharp.UpgradeHelpers.Internal;
using Vb6ToCSharp.UpgradeHelpers.Model;

namespace Vb6ToCSharp.UpgradeHelpers.Tests.Model;

public class Vb6ColorTests
{
    [Theory]
    [InlineData(0x000000FF, 255, 0, 0)]
    [InlineData(0x0000FF00, 0, 255, 0)]
    [InlineData(0x00FF0000, 0, 0, 255)]
    [InlineData(0x00123456, 0x56, 0x34, 0x12)]
    public void ToColor_Bgr(int ole, int r, int g, int b)
    {
        var c = Vb6Color.ToColor(ole);
        Assert.Equal((255, r, g, b), ((int)c.A, (int)c.R, (int)c.G, (int)c.B));
        Assert.Equal(ole, Vb6Color.FromColor(c));
    }

    [Fact]
    public void ToColor_SystemColor_IsKnownColor()
    {
        var c = Vb6Color.ToColor(unchecked((int)0x8000000F));
        Assert.True(c.IsSystemColor);
        Assert.Equal(KnownColor.Control, c.ToKnownColor());
        Assert.Equal(KnownColor.WindowText, Vb6Color.ToColor(unchecked((int)0x80000008)).ToKnownColor());
        Assert.Equal(KnownColor.MenuBar, Vb6Color.ToColor(unchecked((int)0x8000001E)).ToKnownColor());
    }

    [Fact]
    public void SystemColors_RoundTrip_ForEveryIndex()
    {
        foreach (var index in SystemColorTable.ByIndex.Keys)
        {
            var ole = unchecked((int)0x80000000) | index;
            Assert.Equal(ole, Vb6Color.FromColor(Vb6Color.ToColor(ole)));
        }
    }

    [Fact]
    public void FromColor_ButtonAliases_MapToSharedIndex()
    {
        Assert.Equal(unchecked((int)0x8000000F), Vb6Color.FromColor(SystemColors.ButtonFace));
        Assert.Equal(unchecked((int)0x80000010), Vb6Color.FromColor(SystemColors.ButtonShadow));
        Assert.Equal(unchecked((int)0x80000014), Vb6Color.FromColor(SystemColors.ButtonHighlight));
    }

    [Fact]
    public void ToColor_UnmappedSystemIndex_UsesGetSysColor()
    {
        var c = Vb6Color.ToColor(unchecked((int)0x80000019)); // COLOR 25: not a KnownColor
        Assert.False(c.IsEmpty);
        Assert.Equal(255, c.A);
    }

    [Fact]
    public void FromColor_NamedNonSystemColor_IsBgr()
    {
        Assert.Equal(0x0000FF, Vb6Color.FromColor(Color.Red));
        Assert.Equal(0xFFFFFF, Vb6Color.FromColor(Color.White));
    }

    [Fact]
    public void ToColor_SystemIndexWin32DoesNotDefine_IsTheControlColour()
    {
        // 25 is the one gap in the COLOR_* range; it used to go to GetSysColor, which answered
        // nothing useful either.
        Assert.False(SystemColorTable.ByIndex.ContainsKey(25));
        Assert.Equal(SystemColors.Control, Vb6Color.ToColor(unchecked((int)0x80000019)));
        Assert.Equal(SystemColors.Control, Vb6Color.ToColor(unchecked((int)0x800000FF)));
    }
}
