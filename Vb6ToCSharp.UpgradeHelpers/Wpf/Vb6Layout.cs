using System;
using System.Windows;
using System.Windows.Controls;

namespace Vb6ToCSharp.UpgradeHelpers.Wpf;

/// <summary>
/// VB6 Left/Top/Width/Height/Move/ScaleWidth/ScaleHeight in twips over WPF (1 DIP = 15 twips).
/// Position is Canvas.Left/Top inside a Canvas, Window.Left/Top for windows, Margin.Left/Top otherwise.
/// </summary>
public static class Vb6Layout
{
    public const double TwipsPerDip = 15.0;

    public static double GetLeft(FrameworkElement e) => e switch
    {
        Window w => w.Left * TwipsPerDip,
        _ when e.Parent is Canvas => NaNToZero(Canvas.GetLeft(e)) * TwipsPerDip,
        _ => e.Margin.Left * TwipsPerDip,
    };

    public static void SetLeft(FrameworkElement e, double twips)
    {
        var dip = twips / TwipsPerDip;
        if (e is Window w) w.Left = dip;
        else if (e.Parent is Canvas) Canvas.SetLeft(e, dip);
        else e.Margin = new Thickness(dip, e.Margin.Top, e.Margin.Right, e.Margin.Bottom);
    }

    public static double GetTop(FrameworkElement e) => e switch
    {
        Window w => w.Top * TwipsPerDip,
        _ when e.Parent is Canvas => NaNToZero(Canvas.GetTop(e)) * TwipsPerDip,
        _ => e.Margin.Top * TwipsPerDip,
    };

    public static void SetTop(FrameworkElement e, double twips)
    {
        var dip = twips / TwipsPerDip;
        if (e is Window w) w.Top = dip;
        else if (e.Parent is Canvas) Canvas.SetTop(e, dip);
        else e.Margin = new Thickness(e.Margin.Left, dip, e.Margin.Right, e.Margin.Bottom);
    }

    /// <summary>Width in twips (explicit Width, else ActualWidth).</summary>
    public static double GetWidth(FrameworkElement e) => (double.IsNaN(e.Width) ? e.ActualWidth : e.Width) * TwipsPerDip;

    public static void SetWidth(FrameworkElement e, double twips) => e.Width = Math.Max(0, twips / TwipsPerDip);

    public static double GetHeight(FrameworkElement e) => (double.IsNaN(e.Height) ? e.ActualHeight : e.Height) * TwipsPerDip;

    public static void SetHeight(FrameworkElement e, double twips) => e.Height = Math.Max(0, twips / TwipsPerDip);

    /// <summary>VB6 <c>Move left, [top], [width], [height]</c>; omitted values are kept.</summary>
    public static void Move(FrameworkElement e, double left, double? top = null, double? width = null, double? height = null)
    {
        if (e == null) throw new ArgumentNullException(nameof(e));
        SetLeft(e, left);
        if (top.HasValue) SetTop(e, top.Value);
        if (width.HasValue) SetWidth(e, width.Value);
        if (height.HasValue) SetHeight(e, height.Value);
    }

    /// <summary>Client width in twips (a window's content, else the element's ActualWidth).</summary>
    public static double GetScaleWidth(FrameworkElement e) =>
        (e is Window { Content: FrameworkElement c } ? c.ActualWidth : e.ActualWidth) * TwipsPerDip;

    public static double GetScaleHeight(FrameworkElement e) =>
        (e is Window { Content: FrameworkElement c } ? c.ActualHeight : e.ActualHeight) * TwipsPerDip;

    private static double NaNToZero(double v) => double.IsNaN(v) ? 0 : v;
}
