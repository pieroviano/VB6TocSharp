using System.Drawing;
using System.Windows.Media;
using Vb6ToCSharp.UpgradeHelpers.Interop;
// Fill is a WPF brush (OcxHelper converts OLE colors to it)
using Color = System.Drawing.Color;
using SystemColors = System.Drawing.SystemColors;
using Vb6ToCSharp.UpgradeHelpers.Tests.Fixtures;
using Vb6ToCSharp.UpgradeHelpers.Tests.Fixtures;

namespace Vb6ToCSharp.UpgradeHelpers.Tests.Interop;

public class OcxHelperTests
{
    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData("True", true)]
    [InlineData("false", false)]
    [InlineData("-1", true)]
    [InlineData("0", false)]
    public void SetProperty_Boolean_VbValues(object value, bool expected)
    {
        var t = new Target();
        OcxHelper.SetProperty(t, "Enabled", value);
        Assert.Equal(expected, t.Enabled);
    }

    [Fact]
    public void SetProperty_Numbers_AreConverted()
    {
        var t = new Target();
        OcxHelper.SetProperty(t, "count", "&H10");
        Assert.Equal(16, t.Count);
        OcxHelper.SetProperty(t, "Small", 7.0);
        Assert.Equal((short)7, t.Small);
        OcxHelper.SetProperty(t, "Ratio", "2.5");
        Assert.Equal(2.5, t.Ratio);
        OcxHelper.SetProperty(t, "Caption", 12);
        Assert.Equal("12", t.Caption);
        OcxHelper.SetProperty(t, "Optional", 3L);
        Assert.Equal(3, t.Optional);
        OcxHelper.SetProperty(t, "Optional", null!);
        Assert.Null(t.Optional);
    }

    [Fact]
    public void SetProperty_Enum_FromIntOrName()
    {
        var t = new Target();
        OcxHelper.SetProperty(t, "Day", 5);
        Assert.Equal(DayOfWeek.Friday, t.Day);
        OcxHelper.SetProperty(t, "Day", "monday");
        Assert.Equal(DayOfWeek.Monday, t.Day);
        OcxHelper.SetProperty(t, "Day", "2");
        Assert.Equal(DayOfWeek.Tuesday, t.Day);
    }

    [Fact]
    public void SetProperty_Color_FromOleIntOrVbLiteral()
    {
        var t = new Target();
        OcxHelper.SetProperty(t, "BackColor", 0x0000FF);
        Assert.Equal(Color.FromArgb(255, 0, 0).ToArgb(), t.BackColor.ToArgb());
        OcxHelper.SetProperty(t, "BackColor", "&H8000000F&");
        Assert.Equal(SystemColors.Control, t.BackColor);
        OcxHelper.SetProperty(t, "BackColor", Color.Green);
        Assert.Equal(Color.Green, t.BackColor);
    }

    [Fact]
    public void SetProperty_WpfBrush_FromOle()
    {
        Sta.Run(() =>
        {
            var t = new Target();
            OcxHelper.SetProperty(t, "Fill", 0x00FF00);
            Assert.Equal(Colors.Lime, ((SolidColorBrush)t.Fill!).Color);
        });
    }

    [Fact]
    public void Missing_Or_ReadOnly_Throws()
    {
        var t = new Target();
        var e = Assert.Throws<MissingMemberException>(() => OcxHelper.SetProperty(t, "Nope", 1));
        Assert.Contains("Nope", e.Message);
        Assert.Throws<MissingMemberException>(() => OcxHelper.SetProperty(t, "ReadOnlyValue", 1));
        Assert.Throws<MissingMemberException>(() => OcxHelper.GetProperty(t, "Nope"));
    }

    [Fact]
    public void GetProperty_ReturnsValue_CaseInsensitive()
    {
        var t = new Target { Caption = "x" };
        Assert.Equal("x", OcxHelper.GetProperty(t, "CAPTION"));
        Assert.Equal(42, OcxHelper.GetProperty(t, "readonlyvalue"));
    }
}