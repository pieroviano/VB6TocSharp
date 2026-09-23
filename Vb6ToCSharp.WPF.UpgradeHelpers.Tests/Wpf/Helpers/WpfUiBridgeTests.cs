using System.Windows;
using Vb6ToCSharp.UpgradeHelpers.Interop;
using Vb6ToCSharp.UpgradeHelpers.Tests.Fixtures;
using Vb6ToCSharp.UpgradeHelpers.Wpf.Helpers;
using Brush = System.Windows.Media.Brush;
using Colors = System.Windows.Media.Colors;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace Vb6ToCSharp.UpgradeHelpers.Tests.Wpf.Helpers;

/// <summary>VB6 Load / Unload / DoEvents and the OCX conversions on the WPF stack.</summary>
public class WpfUiBridgeTests
{
    private sealed class Target
    {
        public System.Windows.Media.Color Tint { get; set; }
        public Brush? Fill { get; set; }
    }

    /// <summary>A WPF window exists as soon as it is constructed: Load has nothing left to do.</summary>
    [Fact]
    public void Load_HandlesAWindow()
    {
        Sta.Run(() =>
        {
            var window = new Window();
            Assert.True(new WpfUiBridge().Load(window));
            Assert.False(window.IsVisible);
        });
    }

    [Fact]
    public void Unload_ClosesTheWindow()
    {
        Sta.Run(() =>
        {
            var window = new Window();
            window.Show();
            Assert.True(new WpfUiBridge().Unload(window));
            Assert.False(window.IsVisible);
        });
    }

    [Fact]
    public void ObjectsOfAnotherStack_AreNotHandled()
    {
        var bridge = new WpfUiBridge();
        Assert.False(bridge.Load(new object()));
        Assert.False(bridge.Unload(new object()));
    }

    /// <summary>WPF answers only while an Application is running, so a WinForms program keeps its own stack.</summary>
    [Fact]
    public void IsActive_NeedsAnApplication() => Assert.Equal(Application.Current != null, new WpfUiBridge().IsActive);

    [Fact]
    public void OcxProperties_OfWpfTypes_ComeFromOleColors()
    {
        Sta.Run(() =>
        {
            var t = new Target();
            OcxHelper.SetProperty(t, "Tint", 0x00FF00);
            OcxHelper.SetProperty(t, "Fill", 0x00FF00);
            Assert.Equal(Colors.Lime, t.Tint);
            Assert.Equal(Colors.Lime, ((SolidColorBrush)t.Fill!).Color);
        });
    }
}
