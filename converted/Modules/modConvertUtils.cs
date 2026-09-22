using Microsoft.VisualBasic;
using static Microsoft.VisualBasic.Constants;
using static Microsoft.VisualBasic.Interaction;
using static Microsoft.VisualBasic.Strings;
using static modUtils;
using static VBExtension;


static class modConvertUtils
{
    // Option Explicit
    private static string EOLComment = "";
    private static Collection mStrings = null;
    private static int nStringCnt = 0;
    private const string DeStringToken_Base1 = "STRING_";
    private const string DeStringToken_Base2 = "TOKEN_";
    public const string DeStringToken_Base = DeStringToken_Base1 + DeStringToken_Base2;


    public static string DeComment(string Str, bool Discard = false)
    {
        string C = "";

        var DeComment = Str;
        var A = InStr(Str, "'");
        if (A == 0)
        {
            return DeComment;

        }
        while (true)
        {
            var T = Left(Str, A - 1);
            var U = Replace(T, "\"", "");
            if ((Len(T) - Len(U)) % 2 == 0)
            {
                break;
            }
            A = InStr(A + 1, Str, "'");
            if (A == 0)
            {
                return DeComment;

            }
        }
        if (!Discard)
        {
            EOLComment = Mid(Str, A + 1);
        }
        DeComment = RTrim(Left(Str, A - 1));
        return DeComment;
    }

    public static string ReComment(string Str, bool KeepVBComments = false)
    {
        string ReComment = "";

        var Pr = IIf(KeepVBComments, "'", "//");
        if (EOLComment == "")
        {
            ReComment = Str;
            return ReComment;

        }
        var C = Pr + EOLComment;
        EOLComment = "";
        if (!IsInStr(Str, vbCrLf))
        {
            ReComment = Str + IIf(Len(Str) == 0, "", " ") + C;
        }
        else
        {
            ReComment = Replace(Str, vbCrLf, C + vbCrLf, 1, 1); // Always leave on end of first line...
        }
        if (Left(LTrim(ReComment), 2) == Pr)
        {
            ReComment = LTrim(ReComment);
        }
        return ReComment;
    }

    public static void InitDeString()
    {
        mStrings = new Collection(); ;
        nStringCnt = 0;
    }

    private static string DeStringToken(int N)
    {
        var DeStringToken = DeStringToken_Base + Format(N, "00000");
        return DeStringToken;
    }

    public static string DeString(string S)
    {
        string DeString = "";
        const string Q = "\"";

        if (mStrings == null)
        {
            InitDeString();
        }

        //If IsInStr(S, """ArCheck.chkShowB") Then Stop
        var A = InStr(S, Q);
        var C = A;
        if (A > 0)
        {
        MidQuote:;
            var B = InStr(C + 1, S, Q);
            if (B > 0)
            {
                if (Mid(S, B + 1, 1) == Q)
                {
                    C = B + 1;
                    goto MidQuote;
                }
                nStringCnt = nStringCnt + 1;
                var Token = DeStringToken(nStringCnt);
                var K = Mid(S, A, B - A + 1);
                mStrings.Add(K, Token);
                S = Left(S, A - 1) + Token + Mid(S, B + 1);
                DeString = modConvertUtils.DeString(S);
                return DeString;

            }
        }
        DeString = S;
        return DeString;
    }

    public static string ReString(string Str, bool doConvertString = false)
    {
        for (var I = 1; I <= nStringCnt; I++)
        {
            var T = DeStringToken(I);
            string V = mStrings.Item(T);
            if (V != "" && doConvertString)
            {
                if (Left(V, 1) == "\"" && Right(V, 1) == "\"")
                {
                    V = "\"" + InternalConvertString(Mid(V, 2, Len(V) - 2)) + "\"";
                }
            }
            Str = Replace(Str, T, V);
        }
        var ReString = Str;
        return ReString;
    }

    private static string InternalConvertString(string S)
    {
        S = Replace(S, "\\", "\\\\");
        S = Replace(S, "\"\"", "\\\"");
        var InternalConvertString = S;
        return InternalConvertString;
    }
}
