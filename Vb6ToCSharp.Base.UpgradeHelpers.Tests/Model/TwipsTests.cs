using System.Drawing;
using Vb6ToCSharp.UpgradeHelpers.Model;

namespace Vb6ToCSharp.UpgradeHelpers.Tests.Model;

public class TwipsTests
{
    [Fact]
    public void Ratio_IsDesktopDpiBased()
    {
        using var g = Graphics.FromHwnd(IntPtr.Zero);
        Assert.Equal(1440f / g.DpiX, Twips.TwipsPerPixelX, 3);
        Assert.Equal(1440f / g.DpiY, Twips.TwipsPerPixelY, 3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(1440)]
    [InlineData(-300)]
    public void Pixels_RoundTrip(int px)
    {
        Assert.Equal(px, Twips.ToPixelsX(Twips.FromPixelsX(px)));
        Assert.Equal(px, Twips.ToPixelsY(Twips.FromPixelsY(px)));
    }

    [Fact]
    public void ToPixels_Rounds()
    {
        var tpp = Twips.TwipsPerPixelX;
        Assert.Equal(10, Twips.ToPixelsX(tpp * 10 + tpp * 0.4));
        Assert.Equal(11, Twips.ToPixelsX(tpp * 10 + tpp * 0.6));
        Assert.Equal(1440 / tpp, Twips.ToPixelsX(1440), 0);
    }
}
