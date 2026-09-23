using System;
using System.Windows.Forms;

namespace Vb6ToCSharp.UpgradeHelpers.WinForms.Helpers;

/// <summary>
/// VB6 scroll bar Max: the value reached at the end of the track. WinForms reaches
/// <c>Maximum - LargeChange + 1</c>, so VB <c>Max</c> ↔ <c>Maximum = Max + LargeChange - 1</c>.
/// </summary>
public static class ScrollBarHelper
{
    public static int GetMax(ScrollBar sb) => sb.Maximum - sb.LargeChange + 1;

    public static void SetMax(ScrollBar sb, int vbMax)
    {
        if (sb == null) throw new ArgumentNullException(nameof(sb));
        var largeChange = RawLargeChange(sb);
        if (vbMax < sb.Minimum) sb.Minimum = vbMax;
        sb.Maximum = vbMax + largeChange - 1;
    }

    /// <summary>Sets LargeChange keeping the VB Max.</summary>
    public static void SetLargeChange(ScrollBar sb, int value)
    {
        if (sb == null) throw new ArgumentNullException(nameof(sb));
        if (value < 1) throw new ArgumentOutOfRangeException(nameof(value), "Invalid property value");
        var vbMax = GetMax(sb);
        sb.Maximum = int.MaxValue - 1; // so the new LargeChange is not clamped by the old range
        sb.LargeChange = value;
        SetMax(sb, vbMax);
    }

    /// <summary>LargeChange as set (the getter clamps it to the current range).</summary>
    private static int RawLargeChange(ScrollBar sb)
    {
        var max = sb.Maximum;
        var value = sb.Value;
        sb.Maximum = int.MaxValue - 1;
        var raw = sb.LargeChange;
        sb.Maximum = max;
        sb.Value = Math.Min(value, max);
        return raw;
    }
}
