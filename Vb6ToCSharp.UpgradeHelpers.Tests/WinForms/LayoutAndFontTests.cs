using System.Drawing;
using System.Windows.Forms;
using Vb6ToCSharp.UpgradeHelpers.Tests.Infrastructure;
using Vb6ToCSharp.UpgradeHelpers.WinForms;

namespace Vb6ToCSharp.UpgradeHelpers.Tests.WinForms;

public class LayoutAndFontTests
{
    private static double Tx(int px) => Twips.FromPixelsX(px);
    private static double Ty(int px) => Twips.FromPixelsY(px);

    [Fact]
    public void Get_Set_Twips()
    {
        Sta.Run(() =>
        {
            var c = new Panel();
            Vb6Layout.SetLeft(c, Tx(10));
            Vb6Layout.SetTop(c, Ty(20));
            Vb6Layout.SetWidth(c, Tx(30));
            Vb6Layout.SetHeight(c, Ty(40));
            Assert.Equal(new Rectangle(10, 20, 30, 40), c.Bounds);
            Assert.Equal(Tx(10), Vb6Layout.GetLeft(c), 3);
            Assert.Equal(Ty(20), Vb6Layout.GetTop(c), 3);
            Assert.Equal(Tx(30), Vb6Layout.GetWidth(c), 3);
            Assert.Equal(Ty(40), Vb6Layout.GetHeight(c), 3);
        });
    }

    [Fact]
    public void Move_KeepsOmittedValues()
    {
        Sta.Run(() =>
        {
            var c = new Panel { Bounds = new Rectangle(1, 2, 3, 4) };
            Vb6Layout.Move(c, Tx(50));
            Assert.Equal(new Rectangle(50, 2, 3, 4), c.Bounds);
            Vb6Layout.Move(c, Tx(5), Ty(6), Tx(70), Ty(80));
            Assert.Equal(new Rectangle(5, 6, 70, 80), c.Bounds);
            Vb6Layout.Move(c, Tx(5), null, Tx(90));
            Assert.Equal(new Rectangle(5, 6, 90, 80), c.Bounds);
        });
    }

    [Fact]
    public void ScaleSize_IsClientArea()
    {
        Sta.Run(() =>
        {
            var c = new Panel { Size = new Size(100, 60), BorderStyle = BorderStyle.FixedSingle };
            Assert.Equal(Tx(c.ClientSize.Width), Vb6Layout.GetScaleWidth(c), 3);
            Assert.Equal(Ty(c.ClientSize.Height), Vb6Layout.GetScaleHeight(c), 3);
        });
    }

    [Fact]
    public void Font_Changes_ReturnNewFonts()
    {
        using var f = new Font("Arial", 10f, FontStyle.Italic);
        using var named = Vb6Font.ChangeName(f, "Courier New");
        Assert.Equal("Courier New", named.Name);
        Assert.Equal(10f, named.SizeInPoints, 2);
        Assert.True(named.Italic);

        using var sized = Vb6Font.ChangeSize(f, 14f);
        Assert.Equal(14f, sized.SizeInPoints, 2);
        Assert.Equal("Arial", sized.Name);

        using var bold = Vb6Font.ChangeBold(f, true);
        Assert.True(bold.Bold && bold.Italic);
        using var notItalic = Vb6Font.ChangeItalic(bold, false);
        Assert.True(notItalic.Bold);
        Assert.False(notItalic.Italic);
        using var under = Vb6Font.ChangeUnderline(f, true);
        Assert.True(under.Underline);
        using var strike = Vb6Font.ChangeStrikeout(f, true);
        Assert.True(strike.Strikeout);
        Assert.Same(f, Vb6Font.ChangeItalic(f, true)); // unchanged style → same immutable font

        var fromNull = Vb6Font.ChangeBold(null!, true);
        Assert.True(fromNull.Bold);
    }
}
