using System.Drawing;
using Vb6ToCSharp.UpgradeHelpers.Internal;

namespace Vb6ToCSharp.UpgradeHelpers;

/// <summary>VB6 OLE_COLOR (<c>&amp;H00BBGGRR</c>, or <c>&amp;H800000xx</c> system color) ↔ <see cref="Color"/>.</summary>
public static class Vb6Color
{
    public static Color ToColor(int oleColor)
    {
        if (SystemColorTable.IsSystem(oleColor))
        {
            var index = SystemColorTable.IndexOf(oleColor);
            return SystemColorTable.ByIndex.TryGetValue(index, out var known)
                ? Color.FromKnownColor(known)
                : SystemColorTable.FromWin32(index);
        }
        return Color.FromArgb(255, oleColor & 0xFF, (oleColor >> 8) & 0xFF, (oleColor >> 16) & 0xFF);
    }

    public static int FromColor(Color c)
    {
        if (c.IsSystemColor && SystemColorTable.ByKnownColor.TryGetValue(c.ToKnownColor(), out var index))
            return SystemColorTable.ToOle(index);
        return SystemColorTable.ToBgr(c.R, c.G, c.B);
    }
}
