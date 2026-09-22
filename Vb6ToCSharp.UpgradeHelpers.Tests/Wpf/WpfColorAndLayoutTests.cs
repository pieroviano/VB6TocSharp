using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Vb6ToCSharp.UpgradeHelpers.Internal;
using Vb6ToCSharp.UpgradeHelpers.Wpf;
using Vb6Color = Vb6ToCSharp.UpgradeHelpers.Wpf.Vb6Color;

namespace Vb6ToCSharp.UpgradeHelpers.Tests.Wpf;

public class WpfColorTests
{
    [Fact]
    public void SystemColor_MapsToLiveSystemBrush_AndBack()
    {
        Sta.Run(() =>
        {
            var ole = unchecked((int)0x8000000F);
            Assert.Same(SystemColors.ControlBrush, Vb6Color.ToBrush(ole));
            Assert.Equal(ole, Vb6Color.FromBrush(SystemColors.ControlBrush));
            Assert.Equal(SystemColors.WindowTextColor, Vb6Color.ToColor(unchecked((int)0x80000008)));
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
                Assert.Equal(ole, Vb6Color.FromBrush(Vb6Color.ToBrush(ole)));
            }
        });
    }

    [Fact]
    public void Rgb_RoundTrips()
    {
        Sta.Run(() =>
        {
            var brush = (SolidColorBrush)Vb6Color.ToBrush(0x00336699);
            Assert.Equal(Color.FromRgb(0x99, 0x66, 0x33), brush.Color);
            Assert.True(brush.IsFrozen);
            Assert.Equal(0x00336699, Vb6Color.FromBrush(brush));
            Assert.Equal(0x0000FF, Vb6Color.FromColor(Colors.Red));
            Assert.Equal(0, Vb6Color.FromBrush(null!));
            Assert.Equal(0, Vb6Color.FromBrush(new LinearGradientBrush()));
        });
    }
}

public class WpfLayoutTests
{
    [Fact]
    public void InCanvas_UsesCanvasCoordinates()
    {
        Sta.Run(() =>
        {
            var canvas = new Canvas();
            var b = new Button();
            canvas.Children.Add(b);
            Assert.Equal(0, Vb6Layout.GetLeft(b));
            Vb6Layout.SetLeft(b, 150);
            Vb6Layout.SetTop(b, 300);
            Assert.Equal(10, Canvas.GetLeft(b));
            Assert.Equal(20, Canvas.GetTop(b));
            Assert.Equal(150, Vb6Layout.GetLeft(b));
            Assert.Equal(300, Vb6Layout.GetTop(b));
            Assert.Equal(new Thickness(0), b.Margin);
        });
    }

    [Fact]
    public void InGrid_UsesMargin_AndMoveKeepsOmittedValues()
    {
        Sta.Run(() =>
        {
            var grid = new Grid();
            var b = new Button { Margin = new Thickness(1, 2, 3, 4), Width = 50, Height = 20 };
            grid.Children.Add(b);
            Vb6Layout.Move(b, 150);
            Assert.Equal(new Thickness(10, 2, 3, 4), b.Margin);
            Assert.Equal(50, b.Width);
            Vb6Layout.Move(b, 150, 450, 1500, 600);
            Assert.Equal(new Thickness(10, 30, 3, 4), b.Margin);
            Assert.Equal((100.0, 40.0), (b.Width, b.Height));
            Assert.Equal(1500, Vb6Layout.GetWidth(b));
            Assert.Equal(600, Vb6Layout.GetHeight(b));
        });
    }

    [Fact]
    public void Window_UsesScreenPosition_AutoSizeUsesActual()
    {
        Sta.Run(() =>
        {
            var w = new Window();
            Vb6Layout.SetLeft(w, 1500);
            Vb6Layout.SetTop(w, 750);
            Assert.Equal((100.0, 50.0), (w.Left, w.Top));
            Assert.Equal(1500, Vb6Layout.GetLeft(w));
            var auto = new Border();
            Assert.Equal(0, Vb6Layout.GetWidth(auto)); // NaN width → ActualWidth
            Assert.Equal(0, Vb6Layout.GetScaleWidth(auto));
            Assert.Equal(0, Vb6Layout.GetScaleHeight(w));
            w.Close();
        });
    }
}
