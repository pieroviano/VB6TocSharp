using System.Drawing;
using System.Windows.Forms;

namespace Vb6ToCSharp.UpgradeHelpers.WinForms;

/// <summary>VB6 <c>Font.Name/Size/Bold/…</c> assignments: returns a new immutable <see cref="Font"/>.</summary>
public static class Vb6Font
{
    public static Font ChangeName(Font f, string name)
    {
        f ??= Control.DefaultFont;
        return new Font(name, f.SizeInPoints, f.Style, GraphicsUnit.Point, f.GdiCharSet, f.GdiVerticalFont);
    }

    /// <summary>New size in points (VB6 FontSize unit).</summary>
    public static Font ChangeSize(Font f, float size)
    {
        f ??= Control.DefaultFont;
        return new Font(f.FontFamily, size, f.Style, GraphicsUnit.Point, f.GdiCharSet, f.GdiVerticalFont);
    }

    public static Font ChangeBold(Font f, bool v) => ChangeStyle(f, FontStyle.Bold, v);
    public static Font ChangeItalic(Font f, bool v) => ChangeStyle(f, FontStyle.Italic, v);
    public static Font ChangeUnderline(Font f, bool v) => ChangeStyle(f, FontStyle.Underline, v);
    public static Font ChangeStrikeout(Font f, bool v) => ChangeStyle(f, FontStyle.Strikeout, v);

    private static Font ChangeStyle(Font f, FontStyle flag, bool on)
    {
        f ??= Control.DefaultFont;
        var style = on ? f.Style | flag : f.Style & ~flag;
        return style == f.Style ? f : new Font(f, style);
    }
}
