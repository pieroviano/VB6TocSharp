using System;
using System.Globalization;

namespace Vb6ToCSharp.UpgradeHelpers.Internal;

/// <summary>VB-style value helpers: numeric-tolerant equality, index/number coercion, VB literals in strings.</summary>
internal static class VbCompare
{
    /// <summary>VB <c>=</c> on two values: numbers compare by value whatever their CLR type (5 = 5.0).</summary>
    internal static bool ValueEquals(object a, object b)
    {
        if (a == null || b == null) return a == null && b == null;
        if (IsNumeric(a) && IsNumeric(b))
        {
            try
            {
                return Convert.ToDecimal(a, CultureInfo.InvariantCulture) == Convert.ToDecimal(b, CultureInfo.InvariantCulture);
            }
            catch (OverflowException)
            {
                return Convert.ToDouble(a, CultureInfo.InvariantCulture).Equals(Convert.ToDouble(b, CultureInfo.InvariantCulture));
            }
        }
        return Equals(a, b);
    }

    internal static bool IsNumeric(object o) => o is byte or sbyte or short or ushort or int or uint or long or ulong
        or float or double or decimal || o is Enum;

    /// <summary>True when <paramref name="o"/> is an integral-valued number usable as an index.</summary>
    internal static bool TryGetIndex(object o, out int index)
    {
        index = 0;
        if (o == null || o is string || !IsNumeric(o)) return false;
        index = Convert.ToInt32(o, CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>Parses VB numeric text: decimal, <c>&amp;H</c> hex or <c>&amp;O</c> octal, optional type suffix.</summary>
    internal static bool TryParseLong(string s, out long value)
    {
        value = 0;
        if (s == null) return false;
        s = s.Trim().TrimEnd('&', '%', '!', '#', '@', '^');
        if (s.StartsWith("&H", StringComparison.OrdinalIgnoreCase))
        {
            if (!ulong.TryParse(s.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var hex)) return false;
            // &H80000000 in VB is a (negative) Long: keep the 32-bit pattern.
            value = hex <= uint.MaxValue ? unchecked((int)(uint)hex) : unchecked((long)hex);
            return true;
        }
        if (s.StartsWith("&O", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                value = Convert.ToInt64(s.Substring(2), 8);
                return true;
            }
            catch (Exception e) when (e is FormatException or ArgumentException or OverflowException)
            {
                return false;
            }
        }
        return long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
