using System.Windows;
using System.Windows.Media;
using Vb6ToCSharp.UpgradeHelpers.Wpf.Helpers;
using Vb6ToCSharp.UpgradeHelpers.Tests.Infrastructure;
using Vb6ToCSharp.UpgradeHelpers.Internal;

namespace Vb6ToCSharp.UpgradeHelpers.Tests.Wpf;

public class WpfColorTests
{
    [Fact]
    public void SystemColor_MapsToLiveSystemBrush_AndBack()
    {
        Sta.Run(() =>
        {
            var ole = unchecked((int)0x8000000F);
            Assert.Same(SystemColors.ControlBrush, UpgradeHelpers.Wpf.Helpers.Vb6Color.ToBrush(ole));
            Assert.Equal(ole, UpgradeHelpers.Wpf.Helpers.Vb6Color.FromBrush(SystemColors.ControlBrush));
            Assert.Equal(SystemColors.WindowTextColor, UpgradeHelpers.Wpf.Helpers.Vb6Color.ToColor(unchecked((int)0x80000008)));
        });
    }

    [Fact]
    public void EveryMappedSystemIndex_RoundTripsThroughBrush()
    {
        Sta.Run(() =>
        {
            foreach (var index in SystemColorTable.ByIndex.Keys)
            {
                var ole = unchecked((int)0x80000000) | index;
                Assert.Equal(ole, UpgradeHelpers.Wpf.Helpers.Vb6Color.FromBrush(UpgradeHelpers.Wpf.Helpers.Vb6Color.ToBrush(ole)));
            }
        });
    }

    [Fact]
    public void Rgb_RoundTrips()
    {
        Sta.Run(() =>
        {
            var brush = (SolidColorBrush)UpgradeHelpers.Wpf.Helpers.Vb6Color.ToBrush(0x00336699);
            Assert.Equal(Color.FromRgb(0x99, 0x66, 0x33), brush.Color);
            Assert.True(brush.IsFrozen);
            Assert.Equal(0x00336699, UpgradeHelpers.Wpf.Helpers.Vb6Color.FromBrush(brush));
            Assert.Equal(0x0000FF, UpgradeHelpers.Wpf.Helpers.Vb6Color.FromColor(Colors.Red));
            Assert.Equal(0, UpgradeHelpers.Wpf.Helpers.Vb6Color.FromBrush(null!));
            Assert.Equal(0, UpgradeHelpers.Wpf.Helpers.Vb6Color.FromBrush(new LinearGradientBrush()));
        });
    }
}