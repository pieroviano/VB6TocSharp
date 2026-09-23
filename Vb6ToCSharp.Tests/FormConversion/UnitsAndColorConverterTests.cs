using Vb6ToCSharp.FormConversion;
using Vb6ToCSharp.Parsing;
using Vb6ToCSharp.Parsing.Model;
using Vb6ToCSharp.UpgradeHelpers.Model;

namespace Vb6ToCSharp.Tests.FormConversion;

public class UnitsAndColorConverterTests
{
    private static ControlWithType Ctl(string frm, string name) => FrmParser.Parse(frm).Find(name);

    private const string Twips = "Begin VB.Form f \r\n Begin VB.CommandButton b \r\n  Left = 150\r\n  Top = 300\r\n  Width = 1500\r\n  Height = 450\r\n End\r\nEnd\r\n";
    private const string Pixels = "Begin VB.Form f \r\n ScaleMode = 3\r\n Begin VB.Frame fr \r\n  Begin VB.CommandButton b \r\n   Left = 10\r\n   Width = 100\r\n  End\r\n End\r\nEnd\r\n";
    private const string User = "Begin VB.Form f \r\n ClientWidth = 3000\r\n ClientHeight = 1500\r\n ScaleMode = 0\r\n ScaleWidth = 100\r\n ScaleHeight = 50\r\n ScaleLeft = 10\r\n Begin VB.CommandButton b \r\n  Left = 20\r\n  Width = 10\r\n  Top = 5\r\n End\r\nEnd\r\n";

    [Fact]
    public void Twips_ToPixels()
    {
        var b = Ctl(Twips, "b");
        Assert.Equal(10, Units.PosPx(b, "Left"));
        Assert.Equal(20, Units.PosPx(b, "Top"));
        Assert.Equal(100, Units.SizePx(b, "Width"));
        Assert.Equal(30, Units.SizePx(b, "Height"));
    }

    [Fact]
    public void ScaleMode_InheritedThroughFrame()
    {
        var b = Ctl(Pixels, "b");
        Assert.Equal(10, Units.PosPx(b, "Left"));
        Assert.Equal(100, Units.SizePx(b, "Width"));
    }

    [Fact]
    public void ScaleMode_UserDefinedUsesScaleOriginAndClientSize()
    {
        var b = Ctl(User, "b");
        Assert.Equal(300, Units.PosTwips(b, "Left"));   // (20 - 10) * 3000/100
        Assert.Equal(300, Units.SizeTwips(b, "Width"));  // 10 * 30
        Assert.Equal(150, Units.PosTwips(b, "Top"));     // 5 * 1500/50
    }

    [Fact]
    public void SizeTwips_MissingUsesDefault() => Assert.Equal(99, Units.SizeTwips(Ctl(Twips, "b"), "Nope", 99));

    [Fact]
    public void Colors_SystemAndRgb()
    {
        Assert.Equal("System.Drawing.SystemColors.Control", ColorConverter.WinForms(ColorConverter.Parse("&H8000000F&")));
        Assert.Equal("System.Drawing.SystemColors.WindowText", ColorConverter.WinForms(ColorConverter.Parse("&H80000008&")));
        Assert.Equal("System.Drawing.Color.FromArgb(255, 128, 0)", ColorConverter.WinForms(ColorConverter.Parse("&H000080FF&")));
        Assert.Equal("#FF8000", ColorConverter.Xaml(ColorConverter.Parse("&H000080FF&")));
        Assert.Equal("{DynamicResource {x:Static SystemColors.ControlBrushKey}}", ColorConverter.Xaml(ColorConverter.Parse("&H8000000F&")));
        Assert.Null(ColorConverter.SystemName(0x000000FF));
    }
}