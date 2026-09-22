using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using Vb6ToCSharp.UpgradeHelpers.Internal;
using Brush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using SystemColors = System.Windows.SystemColors;

namespace Vb6ToCSharp.UpgradeHelpers.Wpf;

/// <summary>VB6 OLE_COLOR ↔ WPF colors/brushes; system colors map to <see cref="SystemColors"/> (live) brushes.</summary>
public static class Vb6Color
{
    // COLOR_* index → SystemColors.<KnownColor name>Brush / Color (WPF uses the same names as KnownColor).
    private static readonly Dictionary<int, (PropertyInfo Brush, PropertyInfo Color)> SystemProps =
        SystemColorTable.ByIndex.ToDictionary(
            kv => kv.Key,
            kv => (typeof(SystemColors).GetProperty(kv.Value + "Brush", BindingFlags.Public | BindingFlags.Static),
                typeof(SystemColors).GetProperty(kv.Value + "Color", BindingFlags.Public | BindingFlags.Static)));

    public static Brush ToBrush(int oleColor)
    {
        if (SystemColorTable.IsSystem(oleColor) && SystemProps.TryGetValue(SystemColorTable.IndexOf(oleColor), out var p) && p.Brush != null)
            return (Brush)p.Brush.GetValue(null, null);
        var brush = new SolidColorBrush(ToColor(oleColor));
        brush.Freeze();
        return brush;
    }

    /// <summary>OLE color of a brush: system brushes → <c>&amp;H800000xx</c>, solid brushes → BGR, others → 0.</summary>
    public static int FromBrush(Brush b)
    {
        if (b == null) return 0;
        foreach (var kv in SystemProps)
            if (kv.Value.Brush != null && ReferenceEquals(kv.Value.Brush.GetValue(null, null), b))
                return SystemColorTable.ToOle(kv.Key);
        return b is SolidColorBrush s ? FromColor(s.Color) : 0;
    }

    public static MediaColor ToColor(int oleColor)
    {
        if (SystemColorTable.IsSystem(oleColor))
        {
            var index = SystemColorTable.IndexOf(oleColor);
            if (SystemProps.TryGetValue(index, out var p) && p.Color != null) return (MediaColor)p.Color.GetValue(null, null);
            var c = SystemColorTable.FromWin32(index);
            return MediaColor.FromRgb(c.R, c.G, c.B);
        }
        return MediaColor.FromRgb((byte)(oleColor & 0xFF), (byte)((oleColor >> 8) & 0xFF), (byte)((oleColor >> 16) & 0xFF));
    }

    /// <summary>BGR value of <paramref name="c"/> (alpha ignored; WPF colors carry no system-color identity).</summary>
    public static int FromColor(MediaColor c) => SystemColorTable.ToBgr(c.R, c.G, c.B);
}
