using System;
using System.Globalization;

namespace Vb6ToCSharp.Runtime;

/// <summary>
/// The VB6 conversion functions this converter's own code calls, replacing
/// <c>Microsoft.VisualBasic.Conversion</c>. Same signatures, same behaviour — including
/// <see cref="Val(string)"/> reading as much of a number as it can and ignoring white space
/// anywhere inside it, and <see cref="Str(object)"/> putting a space where the sign would be.
/// </summary>
public static class VbConversion
{
    /// <summary>VB's Val: the leading number of the string, 0 when there is none. Never throws on junk.</summary>
    public static double Val(string InputStr)
    {
        if (string.IsNullOrEmpty(InputStr)) return 0;

        // VB strips white space everywhere, so "1 2" reads as 12.
        var s = Compact(InputStr);
        if (s.Length == 0) return 0;

        if (s[0] == '&' && s.Length > 1)
        {
            var base_ = char.ToUpperInvariant(s[1]);
            if (base_ == 'H') return RadixValue(s, 2, 16);
            if (base_ == 'O') return RadixValue(s, 2, 8);
        }

        var end = NumberEnd(s);
        if (end == 0) return 0;
        // The scanner only ever accepts an invariant number, so this cannot fail.
        return double.Parse(s.Substring(0, end).Replace("d", "e").Replace("D", "E"),
            NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    public static double Val(object Expression) => Expression == null ? 0 : Val(Expression.ToString());

    public static int Val(char Expression) => char.IsDigit(Expression) ? Expression - '0' : 0;

    /// <summary>VB's Str: the number as text, with a leading space instead of a sign when it is not negative.</summary>
    public static string Str(object Number)
    {
        if (Number == null) throw new ArgumentNullException(nameof(Number));
        var d = Convert.ToDouble(Number, CultureInfo.InvariantCulture);
        var s = d.ToString("R", CultureInfo.InvariantCulture);
        return s.StartsWith("-", StringComparison.Ordinal) ? s : " " + s;
    }

    public static string Hex(int Number) => Number.ToString("X", CultureInfo.InvariantCulture);

    public static string Hex(long Number) => Number.ToString("X", CultureInfo.InvariantCulture);

    public static string Oct(int Number) => Convert.ToString(Number, 8);

    public static string Oct(long Number) => Convert.ToString(Number, 8);

    public static double Fix(double Number) => Number >= 0 ? Math.Floor(Number) : Math.Ceiling(Number);

    public static int Fix(int Number) => Number;

    public static double Int(double Number) => Math.Floor(Number);

    public static int Int(int Number) => Number;

    private static string Compact(string s)
    {
        var keep = new char[s.Length];
        var n = 0;
        foreach (var c in s)
            if (c != ' ' && c != '\t' && c != '\n' && c != '\r' && c != '\f' && c != '\v' && c != '　')
                keep[n++] = c;
        return new string(keep, 0, n);
    }

    /// <summary>Length of the leading invariant number in <paramref name="s"/>, 0 when there is none.</summary>
    private static int NumberEnd(string s)
    {
        var i = 0;
        if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;

        var digits = 0;
        while (i < s.Length && char.IsDigit(s[i])) { i++; digits++; }
        if (i < s.Length && s[i] == '.')
        {
            i++;
            while (i < s.Length && char.IsDigit(s[i])) { i++; digits++; }
        }
        if (digits == 0) return 0;

        var beforeExponent = i;
        if (i < s.Length && (s[i] == 'e' || s[i] == 'E' || s[i] == 'd' || s[i] == 'D'))
        {
            i++;
            if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;
            var exponentDigits = 0;
            while (i < s.Length && char.IsDigit(s[i])) { i++; exponentDigits++; }
            if (exponentDigits == 0) return beforeExponent; // a trailing 'e' is not part of the number
        }
        return i;
    }

    private static double RadixValue(string s, int from, int radix)
    {
        var digits = "";
        for (var i = from; i < s.Length; i++)
        {
            var c = char.ToUpperInvariant(s[i]);
            var v = c >= '0' && c <= '9' ? c - '0' : c >= 'A' && c <= 'F' ? c - 'A' + 10 : -1;
            if (v < 0 || v >= radix) break;
            digits += c;
        }
        return digits.Length == 0 ? 0 : (double)Convert.ToInt64(digits, radix);
    }
}
