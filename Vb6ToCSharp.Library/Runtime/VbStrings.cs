using System;
using System.Collections.Generic;
using System.Text;
using Vb6ToCSharp.Runtime.Model;

namespace Vb6ToCSharp.Runtime;

/// <summary>
/// The VB6 string functions this converter's own (machine-converted) code calls, in plain C#.
/// Every member mirrors <c>Microsoft.VisualBasic.Strings</c> signature for signature — same
/// parameter types, same optional tail — for two reasons: a <c>using static</c> of this class is a
/// drop-in replacement, and overload resolution against the shims in
/// <see cref="RuntimeExtension"/> (which declare e.g. <c>Replace</c> with three parameters and so
/// win over the six-parameter version) stays exactly as it was.
/// Behaviour is preserved too: 1-based positions, clamping instead of throwing on over-long
/// lengths, null read as the empty string, and the argument exceptions VB raises.
/// </summary>
public static class VbStrings
{
    /// <summary>VB trims these two, and neither tabs nor the other white space <c>string.Trim()</c> would.</summary>
    private static readonly char[] VbSpaces = { ' ', '\u3000' };

    public static int Asc(char String) => AnsiCode(String);

    public static int Asc(string String)
    {
        if (string.IsNullOrEmpty(String))
            throw new ArgumentException("Length of argument 'String' must be greater than zero.", nameof(String));
        return AnsiCode(String[0]);
    }

    public static int AscW(char String) => String;

    public static int AscW(string String)
    {
        if (string.IsNullOrEmpty(String))
            throw new ArgumentException("Length of argument 'String' must be greater than zero.", nameof(String));
        return String[0];
    }

    public static char Chr(int CharCode)
    {
        if (CharCode >= 0 && CharCode <= 127) return (char)CharCode;
        if (CharCode < 0 || CharCode > 255)
            throw new ArgumentException("Argument 'CharCode' must be in the range 0..255.", nameof(CharCode));
        // 128..255 are the ANSI code page, as in VB.
        return Encoding.Default.GetChars(new[] { (byte)CharCode })[0];
    }

    public static char ChrW(int CharCode)
    {
        if (CharCode < -32768 || CharCode > 65535)
            throw new ArgumentException("Argument 'CharCode' is not a valid value.", nameof(CharCode));
        return (char)(CharCode < 0 ? CharCode + 65536 : CharCode);
    }

    public static string[] Filter(string[] Source, string Match, bool Include = true,
        CompareMethod Compare = CompareMethod.Binary)
    {
        if (Source == null) throw new ArgumentException("Argument 'Source' cannot be Nothing.", nameof(Source));
        if (string.IsNullOrEmpty(Match)) return null;

        var cmp = Comparison(Compare);
        var res = new List<string>();
        foreach (var s in Source)
            if ((s != null && s.IndexOf(Match, cmp) >= 0) == Include) res.Add(s);
        return res.ToArray();
    }

    public static int InStr(string String1, string String2, CompareMethod Compare = CompareMethod.Binary)
        => InStr(1, String1, String2, Compare);

    public static int InStr(int Start, string String1, string String2, CompareMethod Compare = CompareMethod.Binary)
    {
        if (Start < 1)
            throw new ArgumentException("Argument 'Start' must be greater than or equal to 1.", nameof(Start));
        if (string.IsNullOrEmpty(String1) || Start > String1.Length) return 0;
        if (string.IsNullOrEmpty(String2)) return Start;
        return String1.IndexOf(String2, Start - 1, Comparison(Compare)) + 1;
    }

    public static int InStrRev(string StringCheck, string StringMatch, int Start = -1,
        CompareMethod Compare = CompareMethod.Binary)
    {
        if (Start == 0 || Start < -1)
            throw new ArgumentException("Argument 'Start' is not a valid value.", nameof(Start));
        if (StringCheck == null) return 0;

        var from = Start == -1 ? StringCheck.Length : Start;
        if (string.IsNullOrEmpty(StringMatch)) return from;
        if (StringCheck.Length == 0 || from > StringCheck.Length) return 0;

        // VB searches the first `from` characters, so the match has to end inside them:
        // InStrRev("abcabc", "bc", 5) is 2, not 5.
        var window = from >= StringCheck.Length ? StringCheck : StringCheck.Substring(0, from);
        return window.LastIndexOf(StringMatch, Comparison(Compare)) + 1;
    }

    public static string Join(string[] SourceArray, string Delimiter = " ")
    {
        if (SourceArray == null)
            throw new ArgumentException("Argument 'SourceArray' cannot be Nothing.", nameof(SourceArray));
        // VB answers Nothing for an empty array, not "" — callers coalesce it.
        if (SourceArray.Length == 0) return null;
        return string.Join(Delimiter, SourceArray);
    }

    public static string LCase(string Value) => Value == null ? "" : Value.ToLower(System.Globalization.CultureInfo.CurrentCulture);

    public static char LCase(char Value) => char.ToLower(Value, System.Globalization.CultureInfo.CurrentCulture);

    public static string Left(string str, int Length)
    {
        if (Length < 0)
            throw new ArgumentException("Argument 'Length' must be greater than or equal to zero.", nameof(Length));
        if (string.IsNullOrEmpty(str)) return "";
        return Length >= str.Length ? str : str.Substring(0, Length);
    }

    public static int Len(string Expression) => Expression == null ? 0 : Expression.Length;

    public static string LSet(string Source, int Length)
    {
        if (Length < 0)
            throw new ArgumentException("Argument 'Length' must be greater than or equal to zero.", nameof(Length));
        if (Source == null) Source = "";
        return Source.Length >= Length ? Source.Substring(0, Length) : Source.PadRight(Length);
    }

    public static string RSet(string Source, int Length)
    {
        if (Length < 0)
            throw new ArgumentException("Argument 'Length' must be greater than or equal to zero.", nameof(Length));
        if (Source == null) Source = "";
        return Source.Length >= Length ? Source.Substring(0, Length) : Source.PadLeft(Length);
    }

    public static string LTrim(string str) => str == null ? "" : str.TrimStart(VbSpaces);

    public static string RTrim(string str) => str == null ? "" : str.TrimEnd(VbSpaces);

    public static string Trim(string str) => str == null ? "" : str.Trim(VbSpaces);

    public static string Mid(string str, int Start) => Mid(str, Start, int.MaxValue);

    public static string Mid(string str, int Start, int Length)
    {
        if (Start < 1)
            throw new ArgumentException("Argument 'Start' must be greater than zero.", nameof(Start));
        if (Length < 0)
            throw new ArgumentException("Argument 'Length' must be greater than or equal to zero.", nameof(Length));
        if (string.IsNullOrEmpty(str) || Start > str.Length || Length == 0) return "";

        var from = Start - 1;
        return Length >= str.Length - from ? str.Substring(from) : str.Substring(from, Length);
    }

    public static string Replace(string Expression, string Find, string Replacement, int Start = 1, int Count = -1,
        CompareMethod Compare = CompareMethod.Binary)
    {
        if (Start < 1)
            throw new ArgumentException("Argument 'Start' must be greater than or equal to 1.", nameof(Start));
        if (Count < -1)
            throw new ArgumentException("Argument 'Count' must be greater than or equal to -1.", nameof(Count));
        // VB answers Nothing - not "" - for an empty Expression; RuntimeExtension.Replace coalesces it.
        if (Expression == null || Start > Expression.Length) return null;

        // VB returns the string from Start onwards, not the whole of it.
        var s = Start == 1 ? Expression : Expression.Substring(Start - 1);
        if (string.IsNullOrEmpty(Find) || Count == 0) return s;
        if (Replacement == null) Replacement = "";

        var cmp = Comparison(Compare);
        var res = new StringBuilder();
        var i = 0;
        var left = Count;
        while (left != 0 && i < s.Length)
        {
            var at = s.IndexOf(Find, i, cmp);
            if (at < 0) break;
            res.Append(s, i, at - i).Append(Replacement);
            i = at + Find.Length;
            if (left > 0) left--;
        }
        return res.Append(s, i, s.Length - i).ToString();
    }

    public static string Right(string str, int Length)
    {
        if (Length < 0)
            throw new ArgumentException("Argument 'Length' must be greater than or equal to zero.", nameof(Length));
        if (string.IsNullOrEmpty(str)) return "";
        return Length >= str.Length ? str : str.Substring(str.Length - Length);
    }

    public static string Space(int Number)
    {
        if (Number < 0)
            throw new ArgumentException("Argument 'Number' must be greater than or equal to zero.", nameof(Number));
        return new string(' ', Number);
    }

    public static string[] Split(string Expression, string Delimiter = " ", int Limit = -1,
        CompareMethod Compare = CompareMethod.Binary)
    {
        if (string.IsNullOrEmpty(Expression)) return new[] { "" };
        if (string.IsNullOrEmpty(Delimiter) || Limit == 1) return new[] { Expression };

        var cmp = Comparison(Compare);
        var parts = new List<string>();
        var i = 0;
        while (true)
        {
            if (Limit > 0 && parts.Count == Limit - 1)
            {
                parts.Add(Expression.Substring(i));
                break;
            }

            var at = Expression.IndexOf(Delimiter, i, cmp);
            if (at < 0)
            {
                parts.Add(Expression.Substring(i));
                break;
            }

            parts.Add(Expression.Substring(i, at - i));
            i = at + Delimiter.Length;
        }
        return parts.ToArray();
    }

    public static int StrComp(string String1, string String2, CompareMethod Compare = CompareMethod.Binary)
    {
        var r = Compare == CompareMethod.Text
            ? string.Compare(String1, String2, StringComparison.CurrentCultureIgnoreCase)
            : string.CompareOrdinal(String1, String2);
        return r < 0 ? -1 : r > 0 ? 1 : 0;
    }

    public static string StrDup(int Number, string Character)
    {
        if (Number < 0)
            throw new ArgumentException("Argument 'Number' must be greater than or equal to zero.", nameof(Number));
        if (string.IsNullOrEmpty(Character))
            throw new ArgumentException("Length of argument 'Character' must be greater than zero.", nameof(Character));
        return new string(Character[0], Number);
    }

    public static string StrDup(int Number, char Character)
    {
        if (Number < 0)
            throw new ArgumentException("Argument 'Number' must be greater than or equal to zero.", nameof(Number));
        return new string(Character, Number);
    }

    public static string UCase(string Value) => Value == null ? "" : Value.ToUpper(System.Globalization.CultureInfo.CurrentCulture);

    public static char UCase(char Value) => char.ToUpper(Value, System.Globalization.CultureInfo.CurrentCulture);

    internal static StringComparison Comparison(CompareMethod Compare)
        => Compare == CompareMethod.Text ? StringComparison.CurrentCultureIgnoreCase : StringComparison.Ordinal;

    private static int AnsiCode(char c)
    {
        if (c < 128) return c;
        var bytes = Encoding.Default.GetBytes(new[] { c });
        return bytes.Length == 1 ? bytes[0] : (bytes[0] << 8) | bytes[1];
    }
}
