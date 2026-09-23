using System;
using System.Globalization;

namespace Vb6ToCSharp.Parsing;

/// <summary>VB6 literal parsing (designer values).</summary>
public static class StringConvert
{
    public static double ToNumber(string raw, double def = 0)
    {
        var s = (raw ?? "").Trim().Trim('"');
        if (s == "") return def;
        if (s.StartsWith("&H", StringComparison.OrdinalIgnoreCase))
        {
            var hex = s.Substring(2).TrimEnd('&', '%');
            return long.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var h) ? (int)(uint)h : def;
        }
        if (string.Equals(s, "True", StringComparison.OrdinalIgnoreCase)) return -1;
        if (string.Equals(s, "False", StringComparison.OrdinalIgnoreCase)) return 0;
        return double.TryParse(s.TrimEnd('&', '%', '!', '#', '@'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : def;
    }

    public static bool ToBool(string raw, bool def = false)
    {
        var s = (raw ?? "").Trim();
        if (s == "") return def;
        if (string.Equals(s, "True", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(s, "False", StringComparison.OrdinalIgnoreCase)) return false;
        return ToNumber(s, def ? -1 : 0) != 0;
    }
}
