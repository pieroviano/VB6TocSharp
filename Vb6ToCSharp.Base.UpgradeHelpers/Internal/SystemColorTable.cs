using System.Collections.Generic;
using System.Drawing;

namespace Vb6ToCSharp.UpgradeHelpers.Internal;

/// <summary>Win32 COLOR_* indexes (low byte of a VB6 system OLE color) ↔ <see cref="KnownColor"/>.</summary>
internal static class SystemColorTable
{
    internal const int SystemFlag = unchecked((int)0x80000000);

    internal static readonly IReadOnlyDictionary<int, KnownColor> ByIndex = new Dictionary<int, KnownColor>
    {
        [0] = KnownColor.ScrollBar,
        [1] = KnownColor.Desktop,
        [2] = KnownColor.ActiveCaption,
        [3] = KnownColor.InactiveCaption,
        [4] = KnownColor.Menu,
        [5] = KnownColor.Window,
        [6] = KnownColor.WindowFrame,
        [7] = KnownColor.MenuText,
        [8] = KnownColor.WindowText,
        [9] = KnownColor.ActiveCaptionText,
        [10] = KnownColor.ActiveBorder,
        [11] = KnownColor.InactiveBorder,
        [12] = KnownColor.AppWorkspace,
        [13] = KnownColor.Highlight,
        [14] = KnownColor.HighlightText,
        [15] = KnownColor.Control,
        [16] = KnownColor.ControlDark,
        [17] = KnownColor.GrayText,
        [18] = KnownColor.ControlText,
        [19] = KnownColor.InactiveCaptionText,
        [20] = KnownColor.ControlLightLight,
        [21] = KnownColor.ControlDarkDark,
        [22] = KnownColor.ControlLight,
        [23] = KnownColor.InfoText,
        [24] = KnownColor.Info,
        [26] = KnownColor.HotTrack,
        [27] = KnownColor.GradientActiveCaption,
        [28] = KnownColor.GradientInactiveCaption,
        [29] = KnownColor.MenuHighlight,
        [30] = KnownColor.MenuBar,
    };

    internal static readonly IReadOnlyDictionary<KnownColor, int> ByKnownColor = BuildReverse();

    private static Dictionary<KnownColor, int> BuildReverse()
    {
        var map = new Dictionary<KnownColor, int>();
        foreach (var kv in ByIndex) map[kv.Value] = kv.Key;
        // Aliases sharing a COLOR_* index.
        map[KnownColor.ButtonFace] = 15;
        map[KnownColor.ButtonShadow] = 16;
        map[KnownColor.ButtonHighlight] = 20;
        return map;
    }

    internal static bool IsSystem(int oleColor) => (oleColor & SystemFlag) != 0;

    internal static int IndexOf(int oleColor) => oleColor & 0xFF;

    internal static int ToOle(int index) => SystemFlag | (index & 0xFF);

    /// <summary>
    /// Fallback for a COLOR_* index <see cref="ByIndex"/> does not map. Every index Win32 defines is
    /// in that table, so this is reached only for one that does not exist - which VB6 painted with
    /// the control colour, as the GetSysColor this replaced also did on failure.
    /// </summary>
    internal static Color FromWin32(int index) => SystemColors.Control;

    internal static int ToBgr(byte r, byte g, byte b) => r | (g << 8) | (b << 16);

}
