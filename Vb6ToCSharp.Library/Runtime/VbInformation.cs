using System;
using System.Globalization;

namespace Vb6ToCSharp.Runtime;

/// <summary>
/// The VB6 type-inspection functions this converter's own code calls, replacing
/// <c>Microsoft.VisualBasic.Information</c>. Signatures mirror it, so <c>LBound</c>/<c>UBound</c>
/// still win over <see cref="RuntimeExtension"/>'s <c>object</c> overloads for an array argument,
/// exactly as before.
/// </summary>
public static class VbInformation
{
    public static bool IsArray(object VarName) => VarName is Array;

    public static bool IsNothing(object Expression) => Expression == null;

    public static bool IsDate(object Expression)
    {
        if (Expression is DateTime) return true;
        var s = Expression as string;
        return s != null && DateTime.TryParse(s, CultureInfo.CurrentCulture, DateTimeStyles.None, out _);
    }

    /// <summary>VB's IsNumeric: a number, or a string that reads as one (including &amp;H / &amp;O literals).</summary>
    public static bool IsNumeric(object Expression)
    {
        if (Expression == null) return false;

        var s = Expression as string;
        if (s == null)
            return Expression is byte || Expression is sbyte || Expression is short || Expression is ushort
                || Expression is int || Expression is uint || Expression is long || Expression is ulong
                || Expression is float || Expression is double || Expression is decimal
                || Expression is bool || Expression is char && char.IsDigit((char)Expression);

        s = s.Trim();
        if (s.Length == 0) return false;

        if (s.Length > 2 && s[0] == '&' && (char.ToUpperInvariant(s[1]) == 'H' || char.ToUpperInvariant(s[1]) == 'O'))
        {
            var radix = char.ToUpperInvariant(s[1]) == 'H' ? 16 : 8;
            try
            {
                Convert.ToInt64(s.Substring(2).TrimEnd('&'), radix);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // VB accepts a group separator, parentheses and an 'e' exponent, but no currency symbol and
        // no 'd' exponent - IsNumeric("1d3") is False although Val("1d3") is 1000.
        const NumberStyles styles = NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite
            | NumberStyles.AllowLeadingSign | NumberStyles.AllowTrailingSign | NumberStyles.AllowParentheses
            | NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands | NumberStyles.AllowExponent;
        return double.TryParse(s, styles, CultureInfo.CurrentCulture, out _);
    }

    public static int LBound(Array Array, int Rank = 1)
    {
        if (Array == null) throw new ArgumentNullException(nameof(Array));
        if (Rank < 1 || Rank > Array.Rank)
            throw new ArgumentException("Argument 'Rank' is not a valid value.", nameof(Rank));
        return Array.GetLowerBound(Rank - 1);
    }

    public static int UBound(Array Array, int Rank = 1)
    {
        if (Array == null) throw new ArgumentNullException(nameof(Array));
        if (Rank < 1 || Rank > Array.Rank)
            throw new ArgumentException("Argument 'Rank' is not a valid value.", nameof(Rank));
        return Array.GetUpperBound(Rank - 1);
    }
}
