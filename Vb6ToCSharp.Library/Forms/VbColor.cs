using System.Globalization;

namespace Vb6ToCSharp.FormConversion;

/// <summary>VB6 OLE colors (<c>&amp;H00BBGGRR&amp;</c>, <c>&amp;H800000xx&amp;</c> = system color) as C# / XAML expressions.</summary>
public static class VbColor
{
    // Win32 COLOR_* index → System.Drawing.SystemColors / System.Windows.SystemColors member
    private static readonly string[] system =
    {
        "ScrollBar", "Desktop", "ActiveCaption", "InactiveCaption", "Menu", "Window", "WindowFrame", "MenuText",
        "WindowText", "ActiveCaptionText", "ActiveBorder", "InactiveBorder", "AppWorkspace", "Highlight", "HighlightText",
        "Control", "ControlDark", "GrayText", "ControlText", "InactiveCaptionText", "ControlLightLight", "ControlDarkDark",
        "ControlLight", "InfoText", "Info", null, "HotTrack", "GradientActiveCaption", "GradientInactiveCaption",
        "MenuHighlight", "MenuBar",
    };

    public static uint Parse(string raw) => unchecked((uint)(long)VbValue.ToNumber(raw));

    /// <summary>System color member name, or null for an RGB color.</summary>
    public static string SystemName(uint ole)
    {
        if ((ole & 0x80000000) == 0) return null;
        var i = (int)(ole & 0xFF);
        return i < system.Length ? system[i] : null;
    }

    public static (int r, int g, int b) Rgb(uint ole) => ((int)(ole & 0xFF), (int)((ole >> 8) & 0xFF), (int)((ole >> 16) & 0xFF));

    public static string WinForms(uint ole)
    {
        var s = SystemName(ole);
        if (s != null) return "System.Drawing.SystemColors." + s;
        var (r, g, b) = Rgb(ole);
        return "System.Drawing.Color.FromArgb(" + r + ", " + g + ", " + b + ")";
    }

    /// <summary>XAML brush attribute value.</summary>
    public static string Xaml(uint ole)
    {
        var s = SystemName(ole);
        if (s != null) return "{DynamicResource {x:Static SystemColors." + s + "BrushKey}}"; // same member names as WinForms
        var (r, g, b) = Rgb(ole);
        return "#" + r.ToString("X2", CultureInfo.InvariantCulture) + g.ToString("X2", CultureInfo.InvariantCulture) + b.ToString("X2", CultureInfo.InvariantCulture);
    }
}
