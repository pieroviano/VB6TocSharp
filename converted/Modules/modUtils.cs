using System;
using System.Diagnostics;
using Microsoft.VisualBasic;
using Vb6ToCSharp.Forms;
using static Microsoft.VisualBasic.Constants;
using static Microsoft.VisualBasic.FileSystem;
using static Microsoft.VisualBasic.Information;
using static Microsoft.VisualBasic.Strings;
using static Microsoft.VisualBasic.VBMath;
using static Vb6ToCSharp.Modules.ModConfig;
using static Vb6ToCSharp.Modules.ModRegEx;
using static Vb6ToCSharp.Modules.ModTextFiles;
using static Vb6ToCSharp.VbExtension;


namespace Vb6ToCSharp.Modules;

static class ModUtils
{
    // Option Explicit
    public const string patToken = "([a-zA-Z_][a-zA-Z_0-9]*)";
    public const string patNotToken = "([^a-zA-Z_0-9])";
    public const string patTokenDot = "([a-zA-Z_.][a-zA-Z_0-9.]*)";
    public const string vbCrLf2 = vbCrLf + vbCrLf;
    public const string vbCrLf3 = vbCrLf + vbCrLf + vbCrLf;
    public const string vbCrLf4 = vbCrLf + vbCrLf + vbCrLf + vbCrLf;
    public const string strChrUcase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public const string strChrLcase = "abcdefghijklmnopqrstuvwxyz";
    public const string strChrDigit = "1234567890";


    public static bool IsNotInStr(string s, string fnd)
    {
        var isNotInStr = !IsInStr(s, fnd);
        return isNotInStr;
    }

    public static bool FileExists(string fn)
    {
        var fileExists = fn != "" && Dir(fn) != "";
        return fileExists;
    }

    public static bool DirExists(string fn)
    {
        var dirExists = fn != "" && Dir(fn, vbDirectory) != "";
        return dirExists;
    }

    public static string TFileName(string fn)
    {
        var tFileName = Mid(fn, InStrRev(fn, "\\") + 1);
        return tFileName;
    }

    public static string FileBaseName(string fn)
    {
        // a name without an extension (or a dot only in the folder part) used to throw
        var name = TFileName(fn);
        var dot = InStrRev(name, ".");
        var fileBaseName = dot == 0 ? name : Left(name, dot - 1);
        return fileBaseName;
    }

    public static string FilePath(string fn)
    {
        var filePath = Left(fn, InStrRev(fn, "\\"));
        return filePath;
    }

    public static string ChgExt(string fn, string newExt)
    {
        // only a dot in the file-name part starts an extension; otherwise the extension is appended
        var dot = InStrRev(fn, ".");
        var chgExt = (dot > InStrRev(fn, "\\") ? Left(fn, dot - 1) : fn) + newExt;
        return chgExt;
    }

    public static string TLeft(string str, int n)
    {
        var tLeft = Left(Trim(str), n);
        return tLeft;
    }

    public static string TMid(string str, int n, int m = 0)
    {
        var tMid = IIf(m == 0, Mid(Trim(str), n), Mid(Trim(str), n, m));
        return tMid;
    }

    public static int StrCnt(string src, string str)
    {
        var strCnt = (Len(src) - Len(Replace(src, str, ""))) / Len(str);
        return strCnt;
    }

    public static bool LMatch(string src, string tMatch)
    {
        var lMatch = Left(src, Len(tMatch)) == tMatch;
        return lMatch;
    }

    public static bool TLMatch(string src, string tMatch)
    {
        var tLMatch = Left(LTrim(src), Len(tMatch)) == tMatch;
        return tLMatch;
    }

    public static int Px(double twips)
    {
        // VB6: Long = Twips / 14 (floating division, banker's rounding on assignment)
        return (int)Math.Round(twips / 14, MidpointRounding.ToEven);
    }

    public static int Px(string twips)
    {
        return Px(Conversion.Val(twips));
    }

    public static string Quote(object s)
    {
        var quote = "\"" + s + "\"";
        return quote;
    }

    public static string AlignString(string s, int n)
    {
        var alignString = Left(s + Space(n), n);
        return alignString;
    }

    public static string Capitalize(string s)
    {
        var capitalize = UCase(Left(s, 1)) + Mid(s, 2);
        return capitalize;
    }

    public static string DevelopmentFolder()
    {
        var developmentFolder = AppDomain.CurrentDomain.BaseDirectory + "\\";
        return developmentFolder;
    }

    public static bool IsIde()
    {
        // VB6 detected the IDE via a Debug-only error; the translation called Debugger.Break(), which
        // outside a debugger raises the JIT-debugger prompt or kills the process.
        return Debugger.IsAttached;
    }

    /// <summary>User-facing notification (message box by default); hosts and tests can replace it.</summary>
    public static Action<string> Notify = s => Interaction.MsgBox(s);

    public static bool IsIn(string s, params dynamic[] kUnused)
    {
        var isIn = false;

        foreach (var iterL in kUnused)
        {
            var l = iterL;
            if (s == l)
            {
                isIn = true;
                return isIn;

            }
        }
        return isIn;
    }

    public static bool WriteOut(string f, string s, string o = "")
    {
        var writeOut = false;
        if (!IsConverted(f, o))
        {
            writeOut = WriteFile(OutputFolder(o) + f, s, true);
        }
        else
        {
            Console.WriteLine("Already converted: " + f);
        }
        return writeOut;
    }

    public static bool IsConverted(string f, string o = "")
    {
        var isConverted = IsInStr(Left(ReadEntireFile(OutputFolder(o) + f), 100), "### CONVERTED");
        return isConverted;
    }

    public static string FileExt(string fn, bool vLCase = true)
    {
        var fileExt = "";
        if (fn == "")
        {
            return fileExt;

        }
        var name = TFileName(fn); // a dot in the folder part is not an extension
        if (InStr(name, ".") == 0)
        {
            return fileExt;

        }
        fileExt = Mid(name, InStrRev(name, "."));
        fileExt = IIf(vLCase, LCase(fileExt), fileExt);
        return fileExt;
    }

    public static string DeQuote(string src)
    {
        if (Left(src, 1) == "\"")
        {
            src = Mid(src, 2);
        }
        if (Right(src, 1) == "\"")
        {
            src = Left(src, Len(src) - 1);
        }
        var deQuote = src;
        return deQuote;
    }

    public static string DeWs(string s)
    {
        while (IsInStr(s, " " + vbCrLf))
        {
            s = Replace(s, " " + vbCrLf, vbCrLf);
        }
        while (IsInStr(s, vbCrLf4))
        {
            s = Replace(s, vbCrLf4, vbCrLf3);
        }

        s = Replace(s, "{" + vbCrLf2, "{" + vbCrLf);
        s = RegExReplace(s, "(" + vbCrLf2 + ")([ ]*{)", vbCrLf + "$2");
        s = RegExReplace(s, "([ ]*case .*:)" + vbCrLf2, "$1" + vbCrLf);
        var deWs = s;
        return deWs;
    }

    public static string NlTrim(string str)
    {
        while (InStr(" " + vbTab + vbCr + vbLf, Left(str, 1)) != 0 & str != "")
        {
            str = Mid(str, 2);
        }
        while (InStr(" " + vbTab + vbCr + vbLf, Right(str, 1)) != 0 & str != "")
        {
            str = Mid(str, 1, Len(str) - 1);
        }
        var nlTrim = str;
        return nlTrim;
    }

    public static string SSpace(int n)
    {
        var sSpace =
            // TODO (not supported): On Error Resume Next
            Space(n);
        return sSpace;
    }

    public static string NextBy(string src, string del = "\"", int ind = 1, bool processVbCommentsUnused = false)
    {
        var nextBy = "";

        DoEvents();
        var l = InStr(src, del);
        if (l == 0)
        {
            nextBy = IIf(ind <= 1, src, "");
            return nextBy;

        }
        if (ind <= 1)
        {
            nextBy = Left(src, l - 1);
        }
        else
        {
            nextBy = ModUtils.NextBy(Mid(src, l + Len(del)), del, ind - 1);
        }
        return nextBy;
    }

    public static int StrQCnt(string src, string str)
    {
        var q = false;


        var strQCnt = 0;
        var n = Len(src);
        for (var I = 1; I <= n; I++)
        {
            var c = Mid(src, I, 1);
            if (c == "\"")
            {
                q = !q;
            }
            else
            {
                if (!q)
                {
                    if (LMatch(Mid(src, I), str))
                    {
                        strQCnt = strQCnt + 1;
                    }
                }
            }
        }
        return strQCnt;
    }

    public static int NextByPCt(string src, string del = "\"", int indUnused = 1)
    {
        var m = 0;

        var n = 0;
        do
        {
            n = n + 1;
            if (n > 1000)
            {
                break;
            }
            var f = NextByP(src, del, n);
            if (f == "")
            {
                m = m + 1;
                if (m >= 10)
                {
                    break;
                }
            }
            else
            {
                m = 0;
            }
        } while (true); // VB "Loop While True" (was mistranslated as while (!(true)))
        var nextByPCt = n - m;
        return nextByPCt;
    }

    public static string NextByP(string src, string del = "\"", int ind = 1)
    {
        var nextByP = "";
        var m = 0;

        var r = "";

        var n = 0;
        do
        {
            m = m + 1;
            if (m > 100)
            {
                break;
            }
            n = n + 1;
            var T = NextBy(src, del, n);
            r = r + IIf(Len(r) == 0, "", del) + T;
        } while (!(StrQCnt(r, "(") == StrQCnt(r, ")")));
        if (ind <= 1)
        {
            nextByP = r;
        }
        else
        {
            nextByP = ModUtils.NextByP(Mid(src, Len(r) + Len(del) + 1), del, ind - 1);
        }
        return nextByP;
    }

    public static string NextByOp(string src, int ind, ref string op)
    {
        var a = NextByP(src, " + ");
        var s = NextByP(src, " - ");
        var m = NextByP(src, " * ");
        var d = NextByP(src, " / ");
        var I = NextByP(src, " \\ ");
        var c = NextByP(src, " & ");
        var e = NextByP(src, " ^ ");

        var cNe = NextByP(src, " <> ");
        var cLt = NextByP(src, " < ");
        var cGt = NextByP(src, " > ");
        var cLe = NextByP(src, " <= ");
        var cGe = NextByP(src, " >= ");
        var cEq = NextByP(src, " = ");

        var lA = NextByP(src, " And ");
        var lO = NextByP(src, " Or ");
        var lM = NextByP(src, " Mod ");
        var ll = NextByP(src, " Like ");

        var xIs = NextByP(src, " Is ");
        var xLk = NextByP(src, " Like ");

        var p = a;
        var k = 3;
        if (Len(p) > Len(s))
        {
            p = s;
            k = 3;
        }
        if (Len(p) > Len(m))
        {
            p = m;
            k = 3;
        }
        if (Len(p) > Len(d))
        {
            p = d;
            k = 3;
        }
        if (Len(p) > Len(I))
        {
            p = I;
            k = 3;
        }
        if (Len(p) > Len(c))
        {
            p = c;
            k = 3;
        }
        if (Len(p) > Len(e))
        {
            p = e;
            k = 3;
        }

        if (Len(p) > Len(cNe))
        {
            p = cNe;
            k = 4;
        }
        if (Len(p) > Len(cLt))
        {
            p = cLt;
            k = 3;
        }
        if (Len(p) > Len(cGt))
        {
            p = cGt;
            k = 3;
        }
        if (Len(p) > Len(cLe))
        {
            p = cLe;
            k = 4;
        }
        if (Len(p) > Len(cGe))
        {
            p = cGe;
            k = 4;
        }
        if (Len(p) > Len(cEq))
        {
            p = cEq;
            k = 3;
        }

        if (Len(p) > Len(lA))
        {
            p = lA;
            k = 5;
        }
        if (Len(p) > Len(lO))
        {
            p = lO;
            k = 4;
        }
        if (Len(p) > Len(lM))
        {
            p = lM;
            k = 5;
        }
        if (Len(p) > Len(ll))
        {
            p = ll;
            k = 6;
        }

        if (Len(p) > Len(xLk))
        {
            p = xLk;
            k = 6;
        }
        if (Len(p) > Len(xIs))
        {
            p = xIs;
            k = 4;
        }

        var nextByOp = p;
        op = null;
        if (ind <= 1)
        {
            op = Mid(src, Len(p) + 1, k);
            nextByOp = p;
        }
        else
        {
            nextByOp = ModUtils.NextByOp(Trim(Mid(src, Len(p) + 3)), ind - 1, ref op);
        }
        return nextByOp;
    }

    public static string ReplaceToken(string src, string origToken, string newToken)
    {
        // lookahead so the trailing delimiter is not consumed (adjacent tokens were skipped); also match at the string edges
        var replaceToken = RegExReplace(src, "(^|[^a-zA-Z_0-9])(" + origToken + ")(?=[^a-zA-Z_0-9]|$)", "$1" + newToken);
        return replaceToken;
    }

    public static string SplitWord(string source, int n = 1, string space = " ", bool trimResult = true, bool includeRest = false)
    {
        var splitWord = "";

        //::::SplitWord
        //:::SUMMARY
        //: Return an indexed word from a string
        //:::DESCRIPTION
        //: Split()s a string based on a space (or other character) and return the word specified by the index.
        //: - Returns "" for 1 > N > Count
        //:::PARAMETERS
        //: - Source - The original source string to analyze
        //: - [N] = 1 - The index of the word to return (Default = 1)
        //: - [Space] = " " - The character to use as the "space" (defaults to %20).
        //: - [TrimResult] - Apply Trim() to the result (Default = True)
        //: - [IncludeRest] - Return the rest of the string starting at the indexed word (Default = False).
        //:::EXAMPLE
        //: - SplitWord("The Rain In Spain Falls Mostly", 4) == "Spain"
        //: - SplitWord("The Rain In Spain Falls Mostly", 4, , , True) == "Spain Falls Mostly"
        //: - SplitWord("a:b:c:d", -1, ":") === "d"
        //:::RETURN
        //:  String
        //:::SEE ALSO
        //: Split, CountWords

        n = n - 1;
        if (source == "")
        {
            return splitWord;

        }
        var s = Split(source, space);
        if (n < 0)
        {
            n = UBound(s) + n + 2;
        }
        if (n < LBound(s) || n > UBound(s))
        {
            return splitWord;

        }
        if (!includeRest)
        {
            splitWord = s[n];
        }
        else
        {
            for (var I = n; I <= UBound(s); I++)
            {
                splitWord = splitWord + IIf(Len(splitWord) > 0, space, "") + s[I];
            }
        }
        if (trimResult)
        {
            splitWord = Trim(splitWord);
        }
        return splitWord;
    }

    public static int CountWords(string source, string space = " ")
    {
        var countWords = 0;
        //::::CountWords
        //:::SUMMARY
        //: Returns the number of words in a string (determined by <Space> parameter)
        //:::DESCRIPTION
        //: Returns the count of words.
        //:::PARAMETERS
        //: - Source - The original source string to analyze
        //: - [Space] = " " - The character to use as the "space" (defaults to %20).
        //:::EXAMPLE
        //: - CountWords("The Rain In Spain Falls Mostly") == 6
        //: - CountWords("The Rain In Spain Falls Mostly", "n") == 4
        //:::RETURN
        //:  String
        //:::SEE ALSO
        //: SplitWord

        // Count actual words.  Blank spaces don't count, before, after, or in the middle.
        // Only a simple split and loop--there may be faster ways...
        foreach (var iterL in Split(source, space))
        {
            dynamic l = iterL;
            if (l != "")
            {
                countWords = countWords + 1;
            }
        }
        return countWords;
    }

    public static dynamic ArrSlice(dynamic sourceArray, int fromIndex, int toIndex)
    {
        if (!IsArray(sourceArray))
        {
            return null;
        }

        var src = (Array)sourceArray;
        fromIndex = FitRange(src.GetLowerBound(0), fromIndex, src.GetUpperBound(0));
        toIndex = FitRange(fromIndex, toIndex, src.GetUpperBound(0));

        var tempList = Array.CreateInstance(src.GetType().GetElementType(), Math.Max(0, toIndex - fromIndex + 1));
        Array.Copy(src, fromIndex, tempList, 0, tempList.Length);
        return tempList;
    }

    public static void ArrAdd(ref dynamic[] arr, dynamic item)
    {
        if (arr == null)
        {
            arr = new dynamic[] { item };
            return;
        }
        Array.Resize(ref arr, arr.Length + 1);
        arr[arr.Length - 1] = item;
    }

    public static dynamic SubArr(dynamic sourceArray, int fromIndex, int copyLength)
    {
        var subArr = ArrSlice(sourceArray, fromIndex, fromIndex + copyLength - 1);
        return subArr;
    }

    public static bool InRange(dynamic lBnd, dynamic chk, dynamic uBnd, bool includeBounds = true)
    {
        var inRange = false;
        // TODO (not supported): On Error Resume Next // because we're doing this as variants..
        if (includeBounds)
        {
            inRange = (chk >= lBnd) && (chk <= uBnd);
        }
        else
        {
            inRange = (chk > lBnd) && (chk < uBnd);
        }
        return inRange;
    }

    public static dynamic FitRange(dynamic lBnd, dynamic chk, dynamic uBnd)
    {
        dynamic fitRange = null;
        // TODO (not supported): On Error Resume Next
        if (chk < lBnd)
        {
            fitRange = lBnd;
        }
        else if (chk > uBnd)
        {
            fitRange = uBnd;
        }
        else
        {
            fitRange = chk;
        }
        return fitRange;
    }

    public static int CodeSectionLoc(string s)
    {
        var codeSectionLoc = 0;
        const string token = "Attribute VB_Name";


        var n = InStr(s, token);
        if (n == 0)
        {
            return codeSectionLoc;

        }
        do
        {
            n = InStr(n, s, vbLf) + 1;
            if (n <= 1)
            {
                return codeSectionLoc;

            }
        } while (Mid(s, n, 10) == "Attribute "); // VB "Loop While" (was mistranslated as Loop Until)

        codeSectionLoc = n;
        return codeSectionLoc;
    }

    public static int CodeSectionGlobalEndLoc(string s)
    {
        var codeSectionGlobalEndLoc = 0;
        do
        {
            codeSectionGlobalEndLoc = codeSectionGlobalEndLoc + RegExNPos(Mid(s, codeSectionGlobalEndLoc + 1), "([^a-zA-Z0-9_]Function |[^a-zA-Z0-9_]Sub |[^a-zA-Z0-9_]Property )") + 1;
            if (codeSectionGlobalEndLoc == 1)
            {
                codeSectionGlobalEndLoc = Len(s);
                return codeSectionGlobalEndLoc;

            }
        } while (codeSectionGlobalEndLoc > 8 && Mid(s, codeSectionGlobalEndLoc - 8, 8) == "Declare "); // VB "Loop While" (was mistranslated as Loop Until)
        if (codeSectionGlobalEndLoc > 7 && Mid(s, codeSectionGlobalEndLoc - 7, 7) == "Friend ")
        {
            codeSectionGlobalEndLoc = codeSectionGlobalEndLoc - 7;
        }
        if (codeSectionGlobalEndLoc > 7 && Mid(s, codeSectionGlobalEndLoc - 7, 7) == "Public ")
        {
            codeSectionGlobalEndLoc = codeSectionGlobalEndLoc - 7;
        }
        if (codeSectionGlobalEndLoc > 8 && Mid(s, codeSectionGlobalEndLoc - 8, 8) == "Private ")
        {
            codeSectionGlobalEndLoc = codeSectionGlobalEndLoc - 8;
        }
        codeSectionGlobalEndLoc = codeSectionGlobalEndLoc - 1;
        return codeSectionGlobalEndLoc;
    }

    public static bool IsOperator(string s)
    {
        var isOperator = false;
        switch (Trim(s))
        {
            // the translation had kept only "+" of the VB case list
            case "+":
            case "-":
            case "/":
            case "*":
            case "&":
            case "<>":
            case "<":
            case ">":
            case "<=":
            case ">=":
            case "=":
            case "Mod":
            case "And":
            case "Or":
            case "Xor":
                isOperator = true;
                break;
            default:
                isOperator = false;
                break;
        }
        return isOperator;
    }

    /// <summary>Progress sink (the main window by default); hosts and tests can replace it.</summary>
    public static Action<int, int, string> Progress = (val, max, cap) => MainForm.Instance.Prg(val, max, cap);

    public static void Prg(int val = -1, int max = -1, string cap = "#")
    {
        Progress(val, max, cap);
    }

    public static string CVal(ref Collection coll, string key, string def = "")
    {
        // VB relied on On Error Resume Next: a missing key yields the default
        try
        {
            return coll.Item(LCase(key));
        }
        catch (ArgumentException)
        {
            return def;
        }
    }

    public static string CValP(ref Collection coll, string key, string def = "")
    {
        var cValP = P(DeQuote(CVal(ref coll, key, def)));
        return cValP;
    }

    public static string P(string str)
    {
        str = Replace(str, "&", "&amp;");
        str = Replace(str, "<", "&lt;");
        str = Replace(str, ">", "&gt;");
        var p = str;
        return p;
    }

    public static string ModuleName(string s)
    {
        const string nameTag = "Attribute VB_Name = \"";
        var j = InStr(s, nameTag) + Len(nameTag);
        var k = InStr(j, s, "\"") - j;
        var moduleName = Mid(s, j, k);
        return moduleName;
    }

    public static bool IsInCode(string src, int nUnused)
    {
        var qu = false;

        var isInCode = false;
        for (var I = nUnused; I > 0; I--)
        {
            var c = Mid(src, I, 1);
            if (c == vbCr || c == vbLf)
            {
                isInCode = true;
                return isInCode;

            }
            else if (c == "\"")
            {
                qu = !qu;
            }
            else if (c == "'")
            {
                if (!qu)
                {
                    return isInCode;

                }
            }
        }
        isInCode = true;
        return isInCode;
    }

    public static string TokenList(string s)
    {
        var tokenList = "";

        var n = RegExCount(s, patToken);
        for (var I = 0; I <= n - 1; I++)
        {
            var T = RegExNMatch(s, patToken, I);
            tokenList = tokenList + "," + T;
        }
        return tokenList;
    }

    public static int Random(int max = 10000)
    {
        Randomize();
        var random = (int)((Rnd() * max) + 1);
        return random;
    }

    public static string Stack(ref string src, string val = "##REM##", bool peek = false)
    {
        var stack = "";
        if (val == "##REM##")
        {
            // entries are quoted: find the closing quote (skipping doubled ones), since values may contain commas
            if (Left(src, 1) == "\"")
            {
                var I = 2;
                while (I <= Len(src) && !(Mid(src, I, 1) == "\"" && Mid(src, I + 1, 1) != "\""))
                {
                    I = I + (Mid(src, I, 1) == "\"" ? 2 : 1);
                }
                stack = Left(src, I);
            }
            else
            {
                stack = NextBy(src, ",");
            }
            if (!peek)
            {
                src = Mid(src, Len(stack) + 2);
            }
            if (Left(stack, 1) == "\"")
            {
                stack = Mid(stack, 2);
                stack = Left(stack, Len(stack) - 1);
            }
            stack = Replace(stack, "\"\"", "\"");
        }
        else
        {
            src = "\"" + Replace(val, "\"", "\"\"") + "\"," + src;
            stack = val;
        }
        return stack;
    }

    public static string QuoteXml(string s)
    {
        var quoteXml = s;
        quoteXml = Replace(s, "\"", "&quot;");
        quoteXml = Quote(quoteXml);
        return quoteXml;
    }

    public static string ReduceString(string src, string allowed = "", string subst = "-", int maxLen = 0, bool bLCase = true)
    {
        //::::ReduceString
        //:::SUMMARY
        //: Reduces a string by removing non-allowed characters, optionally replacing them with a substitute.
        //:::DESCRIPTION
        //: Non-allowed characters are removed, and, if supplied, replaced with a substitute.
        //: Substitutes are trimmed from either end, and all duplicated substitutes are remvoed.
        //:
        //: After this process, the string can be given LCase (default) or truncated (not default), if desired.
        //:
        //: This is effectively a slug maker, although it is somewhat adaptable to any cleaning routine.
        //:::PARAMETERS
        //: - Src - Source string to be reduced
        //: - [Allowed] - The list of allowable characters.  Defaults to [A-Za-z0-9]*
        //: - [Subst] - If specified, the character to replace non-allowed characters with (default == "-")
        //: - [MaxLen] - If passed, truncates longer strings to this length.  Default = 0
        //: - [bLCase] - Convert string to lower case after operation.  Default = True
        //:::EXAMPLE
        //: - ReduceString("   Something To be 'slugified'!!!****") == "something-to-be-slugified"
        //:::RETURN
        //:  String - The slug generated from the source.
        //:::AUTHOR
        //: Benjamin - 2018.04.28
        //:::SEE ALSO
        //:  ArrangeString, StringNumerals, slug, CleanANI

        if (allowed == "")
        {
            allowed = strChrUcase + strChrLcase + strChrDigit;
        }
        var reduceString = "";
        var n = Len(src);
        for (var I = 1; I <= n; I++)
        {
            var c = Mid(src, I, 1);
            reduceString = reduceString + IIf(IsInStr(allowed, c), c, subst);
        }

        if (subst != "")
        {
            while (IsInStr(reduceString, subst + subst))
            {
                reduceString = Replace(reduceString, subst + subst, subst);
            }
            while (Left(reduceString, Len(subst)) == subst)
            {
                reduceString = Mid(reduceString, Len(subst) + 1);
            }
            while (Right(reduceString, Len(subst)) == subst)
            {
                reduceString = Left(reduceString, Len(reduceString) - Len(subst));
            }
        }

        if (maxLen > 0)
        {
            reduceString = Left(reduceString, maxLen);
        }
        if (bLCase)
        {
            reduceString = LCase(reduceString);
        }
        return reduceString;
    }
}