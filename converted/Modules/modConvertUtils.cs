using Microsoft.VisualBasic;
using static Microsoft.VisualBasic.Constants;
using static Microsoft.VisualBasic.Strings;
using static Vb6ToCSharp.VbExtension;


namespace Vb6ToCSharp.Modules;

static class ModConvertUtils
{
    // Option Explicit
    private static string eolComment = "";
    private static Collection mStrings = null;
    private static int nStringCnt = 0;
    private const string deStringTokenBase1 = "STRING_";
    private const string deStringTokenBase2 = "TOKEN_";
    public const string deStringTokenBase = deStringTokenBase1 + deStringTokenBase2;


    public static string DeComment(string str, bool discard = false)
    {
        var deComment = str;
        var a = InStr(str, "'");
        if (a == 0)
        {
            return deComment;

        }
        while (true)
        {
            var T = Left(str, a - 1);
            var u = Replace(T, "\"", "");
            if ((Len(T) - Len(u)) % 2 == 0)
            {
                break;
            }
            a = InStr(a + 1, str, "'");
            if (a == 0)
            {
                return deComment;

            }
        }
        if (!discard)
        {
            eolComment = Mid(str, a + 1);
        }
        deComment = RTrim(Left(str, a - 1));
        return deComment;
    }

    public static string ReComment(string str, bool keepVbComments = false)
    {
        var reComment = "";

        var pr = IIf(keepVbComments, "'", "//");
        if (eolComment == "")
        {
            reComment = str;
            return reComment;

        }
        var c = pr + eolComment;
        eolComment = "";
        if (!IsInStr(str, vbCrLf))
        {
            reComment = str + IIf(Len(str) == 0, "", " ") + c;
        }
        else
        {
            reComment = Replace(str, vbCrLf, c + vbCrLf, 1, 1); // Always leave on end of first line...
        }
        if (Left(LTrim(reComment), 2) == pr)
        {
            reComment = LTrim(reComment);
        }
        return reComment;
    }

    public static void InitDeString()
    {
        mStrings = new Collection(); ;
        nStringCnt = 0;
    }

    private static string DeStringToken(int n)
    {
        var deStringToken = deStringTokenBase + Format(n, "00000");
        return deStringToken;
    }

    public static string DeString(string s)
    {
        var deString = "";
        const string q = "\"";

        if (mStrings == null)
        {
            InitDeString();
        }

        //If IsInStr(S, """ArCheck.chkShowB") Then Stop
        var a = InStr(s, q);
        var c = a;
        if (a > 0)
        {
            MidQuote:;
            var b = InStr(c + 1, s, q);
            if (b > 0)
            {
                if (Mid(s, b + 1, 1) == q)
                {
                    c = b + 1;
                    goto MidQuote;
                }
                nStringCnt = nStringCnt + 1;
                var token = DeStringToken(nStringCnt);
                var k = Mid(s, a, b - a + 1);
                mStrings.Add(k, token);
                s = Left(s, a - 1) + token + Mid(s, b + 1);
                deString = ModConvertUtils.DeString(s);
                return deString;

            }
        }
        deString = s;
        return deString;
    }

    public static string ReString(string str, bool doConvertString = false)
    {
        for (var I = 1; I <= nStringCnt; I++)
        {
            var T = DeStringToken(I);
            string v = mStrings.Item(T);
            if (v != "" && doConvertString)
            {
                if (Left(v, 1) == "\"" && Right(v, 1) == "\"")
                {
                    v = "\"" + InternalConvertString(Mid(v, 2, Len(v) - 2)) + "\"";
                }
            }
            str = Replace(str, T, v);
        }
        var reString = str;
        return reString;
    }

    private static string InternalConvertString(string s)
    {
        s = Replace(s, "\\", "\\\\");
        s = Replace(s, "\"\"", "\\\"");
        var internalConvertString = s;
        return internalConvertString;
    }
}