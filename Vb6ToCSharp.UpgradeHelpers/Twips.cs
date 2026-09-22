using System;
using System.Drawing;

namespace Vb6ToCSharp.UpgradeHelpers;

/// <summary>VB6 twips ↔ screen pixels conversion (1440 twips per inch, desktop DPI read once).</summary>
public static class Twips
{
    private static readonly Lazy<(float X, float Y)> Ratio = new(ReadRatio);

    /// <summary>Twips per horizontal pixel (1440 / DPI; 15 at 96 DPI).</summary>
    public static float TwipsPerPixelX => Ratio.Value.X;

    /// <summary>Twips per vertical pixel (1440 / DPI; 15 at 96 DPI).</summary>
    public static float TwipsPerPixelY => Ratio.Value.Y;

    public static int ToPixelsX(double twips) => (int)Math.Round(twips / TwipsPerPixelX, MidpointRounding.AwayFromZero);

    public static int ToPixelsY(double twips) => (int)Math.Round(twips / TwipsPerPixelY, MidpointRounding.AwayFromZero);

    public static double FromPixelsX(double px) => px * TwipsPerPixelX;

    public static double FromPixelsY(double px) => px * TwipsPerPixelY;

    private static (float, float) ReadRatio()
    {
        try
        {
            using var g = Graphics.FromHwnd(IntPtr.Zero);
            return (g.DpiX > 0 ? 1440f / g.DpiX : 15f, g.DpiY > 0 ? 1440f / g.DpiY : 15f);
        }
        catch (Exception)
        {
            return (15f, 15f); // no desktop (service / headless): 96 DPI
        }
    }
}
