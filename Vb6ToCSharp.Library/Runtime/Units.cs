using System;
using System.Globalization;
using Vb6ToCSharp.CodeGeneration;

namespace Vb6ToCSharp.Runtime;

/// <summary>VB6 coordinate conversion: container ScaleMode units → twips → pixels (design-time 96 DPI).</summary>
public static class Units
{
    /// <summary>Twips per pixel at 96 DPI (VB6 <c>Screen.TwipsPerPixelX</c>); also twips per WPF device-independent unit.</summary>
    public const double TwipsPerPixel = 15;

    public static int ToPixels(double twips) => (int)Math.Round(twips / TwipsPerPixel, MidpointRounding.AwayFromZero);

    /// <summary>Nearest ancestor that owns a coordinate system (Form, MDIForm, UserControl, PictureBox).</summary>
    public static ControlWithType ScaleOwner(ControlWithType c)
    {
        for (var p = c?.Parent; p != null; p = p.Parent)
        {
            if (HasScale(p)) return p;
        }
        return null;
    }

    public static bool HasScale(ControlWithType c) => c.Type is "VB.Form" or "VB.MDIForm" or "VB.UserControl" or "VB.PictureBox" or "VB.PropertyPage";

    /// <summary>Twips per scale unit on the X / Y axis of <paramref name="owner"/>.</summary>
    public static double Factor(ControlWithType owner, bool y)
    {
        if (owner == null) return 1;
        var mode = (int)owner.Num("ScaleMode", 1);
        switch (mode)
        {
            case 0:
                var scale = owner.Num(y ? "ScaleHeight" : "ScaleWidth", 0);
                var client = ClientTwips(owner, y);
                return scale != 0 && client > 0 ? client / scale : 1;
            case 2: return 20;
            case 3: return TwipsPerPixel;
            case 4: return y ? 240 : 120;
            case 5: return 1440;
            case 6: return 1440 / 25.4;
            case 7: return 1440 / 2.54;
            default: return 1;
        }
    }

    /// <summary>Client size of a scale owner in twips.</summary>
    public static double ClientTwips(ControlWithType owner, bool y)
    {
        if (owner.Has(y ? "ClientHeight" : "ClientWidth")) return owner.Num(y ? "ClientHeight" : "ClientWidth");
        return SizeTwips(owner, y ? "Height" : "Width");
    }

    /// <summary>A position property (Left/Top/X1…) of <paramref name="c"/> in twips, relative to its container.</summary>
    public static double PosTwips(ControlWithType c, string prop)
    {
        var y = IsY(prop);
        var owner = ScaleOwner(c);
        var v = c.Num(prop);
        if (owner != null && (int)owner.Num("ScaleMode", 1) == 0) v -= owner.Num(y ? "ScaleTop" : "ScaleLeft");
        return v * Factor(owner, y);
    }

    /// <summary>A size property (Width/Height) of <paramref name="c"/> in twips.</summary>
    public static double SizeTwips(ControlWithType c, string prop, double def = 0)
    {
        if (!c.Has(prop)) return def;
        return c.Num(prop) * Factor(ScaleOwner(c), IsY(prop));
    }

    public static int PosPx(ControlWithType c, string prop) => ToPixels(PosTwips(c, prop));
    public static int SizePx(ControlWithType c, string prop, double defTwips = 0) => ToPixels(SizeTwips(c, prop, defTwips));

    private static bool IsY(string prop) =>
        prop.Equals("Top", StringComparison.OrdinalIgnoreCase) || prop.Equals("Height", StringComparison.OrdinalIgnoreCase) ||
        prop.Equals("Y1", StringComparison.OrdinalIgnoreCase) || prop.Equals("Y2", StringComparison.OrdinalIgnoreCase) ||
        prop.Equals("ClientTop", StringComparison.OrdinalIgnoreCase) || prop.Equals("ClientHeight", StringComparison.OrdinalIgnoreCase);

    public static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
}
