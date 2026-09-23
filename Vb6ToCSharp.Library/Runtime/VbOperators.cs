using System;
using Vb6ToCSharp.Runtime.Model;

namespace Vb6ToCSharp.Runtime;

/// <summary>
/// The VB6 operators that have no C# equivalent, replacing
/// <c>Microsoft.VisualBasic.CompilerServices.LikeOperator</c> for this converter's own code.
/// </summary>
public static class VbOperators
{
    /// <summary>
    /// VB6's <c>Like</c>: <c>?</c> is any one character, <c>*</c> any run of them, <c>#</c> any digit,
    /// <c>[abc]</c> one of a list (ranges with <c>-</c>), <c>[!abc]</c> one not in it.
    /// </summary>
    public static bool LikeString(string Source, string Pattern, CompareMethod CompareOption)
        => Match(Source ?? "", 0, Pattern ?? "", 0, CompareOption == CompareMethod.Text);

    private static bool Match(string s, int si, string p, int pi, bool ignoreCase)
    {
        while (pi < p.Length)
        {
            var pc = p[pi];

            if (pc == '*')
            {
                pi++;
                if (pi == p.Length) return true;
                for (var k = si; k <= s.Length; k++)
                    if (Match(s, k, p, pi, ignoreCase)) return true;
                return false;
            }

            if (si >= s.Length) return false;

            if (pc == '?')
            {
                si++;
                pi++;
                continue;
            }

            if (pc == '#')
            {
                if (s[si] < '0' || s[si] > '9') return false;
                si++;
                pi++;
                continue;
            }

            if (pc == '[')
            {
                var close = p.IndexOf(']', pi + 1);
                // VB rejects a list that is never closed rather than reading '[' as a literal.
                if (close < 0) throw new ArgumentException("Argument 'Pattern' is not a valid value.", nameof(p));
                if (!InList(p.Substring(pi + 1, close - pi - 1), s[si], ignoreCase)) return false;
                si++;
                pi = close + 1;
                continue;
            }

            if (!Same(s[si], pc, ignoreCase)) return false;
            si++;
            pi++;
        }

        return si == s.Length;
    }

    private static bool InList(string list, char c, bool ignoreCase)
    {
        var negate = list.Length > 0 && list[0] == '!';
        if (negate) list = list.Substring(1);

        var hit = false;
        for (var i = 0; i < list.Length; i++)
        {
            // a-z is a range; a '-' first or last is itself.
            if (i + 2 < list.Length && list[i + 1] == '-')
            {
                if (Between(c, list[i], list[i + 2], ignoreCase)) hit = true;
                i += 2;
                continue;
            }
            if (Same(c, list[i], ignoreCase)) hit = true;
        }

        return negate ? !hit : hit;
    }

    private static bool Between(char c, char from, char to, bool ignoreCase)
    {
        if (!ignoreCase) return c >= from && c <= to;
        var l = char.ToLowerInvariant(c);
        return l >= char.ToLowerInvariant(from) && l <= char.ToLowerInvariant(to);
    }

    private static bool Same(char a, char b, bool ignoreCase)
        => ignoreCase ? char.ToLowerInvariant(a) == char.ToLowerInvariant(b) : a == b;
}
