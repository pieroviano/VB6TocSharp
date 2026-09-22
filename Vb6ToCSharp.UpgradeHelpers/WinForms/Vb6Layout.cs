using System;
using System.Windows.Forms;

namespace Vb6ToCSharp.UpgradeHelpers.WinForms;

/// <summary>VB6 Left/Top/Width/Height/Move/ScaleWidth/ScaleHeight in twips over WinForms pixels.</summary>
public static class Vb6Layout
{
    public static double GetLeft(Control c) => Twips.FromPixelsX(c.Left);
    public static void SetLeft(Control c, double twips) => c.Left = Twips.ToPixelsX(twips);
    public static double GetTop(Control c) => Twips.FromPixelsY(c.Top);
    public static void SetTop(Control c, double twips) => c.Top = Twips.ToPixelsY(twips);
    public static double GetWidth(Control c) => Twips.FromPixelsX(c.Width);
    public static void SetWidth(Control c, double twips) => c.Width = Twips.ToPixelsX(twips);
    public static double GetHeight(Control c) => Twips.FromPixelsY(c.Height);
    public static void SetHeight(Control c, double twips) => c.Height = Twips.ToPixelsY(twips);

    /// <summary>VB6 <c>Move left, [top], [width], [height]</c>; omitted values are kept.</summary>
    public static void Move(Control c, double left, double? top = null, double? width = null, double? height = null)
    {
        if (c == null) throw new ArgumentNullException(nameof(c));
        var spec = BoundsSpecified.X;
        if (top.HasValue) spec |= BoundsSpecified.Y;
        if (width.HasValue) spec |= BoundsSpecified.Width;
        if (height.HasValue) spec |= BoundsSpecified.Height;
        c.SetBounds(Twips.ToPixelsX(left),
            top.HasValue ? Twips.ToPixelsY(top.Value) : c.Top,
            width.HasValue ? Twips.ToPixelsX(width.Value) : c.Width,
            height.HasValue ? Twips.ToPixelsY(height.Value) : c.Height,
            spec);
    }

    /// <summary>Client-area width in twips (VB6 ScaleWidth with ScaleMode twips).</summary>
    public static double GetScaleWidth(Control c) => Twips.FromPixelsX(c.ClientSize.Width);

    /// <summary>Client-area height in twips (VB6 ScaleHeight with ScaleMode twips).</summary>
    public static double GetScaleHeight(Control c) => Twips.FromPixelsY(c.ClientSize.Height);
}
