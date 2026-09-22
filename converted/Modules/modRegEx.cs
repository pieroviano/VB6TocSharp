using System.Collections.Generic;
using static Vb6ToCSharp.VbExtension;


namespace Vb6ToCSharp.Modules;

static class ModRegEx
{
    // Option Explicit
    private static dynamic mRegEx = null;


    static dynamic RegEx
    {
        get
        {
            if (mRegEx == null)
            {
                mRegEx = CreateObject("vbscript.regexp");
                mRegEx.Global = true;
            }
            var regEx = mRegEx;

            return regEx;
        }
    }


    public static bool RegExTest(string src, string find)
    {
        // TODO (not supported): On Error Resume Next
        RegEx.Pattern = find;
        bool regExTest = RegEx.test(src);
        return regExTest;
    }

    public static int RegExCount(string src, string find)
    {
        // TODO (not supported): On Error Resume Next
        RegEx.Pattern = find;
        RegEx.Global = true;
        int regExCount = RegEx.Execute(src).Count;
        return regExCount;
    }

    public static int RegExNPos(string src, string find, int n = 0)
    {
        // TODO (not supported): On Error Resume Next

        RegEx.Pattern = find;
        RegEx.Global = true;
        int regExNPos = RegEx.Execute(src).Item(n).FirstIndex + 1;
        return regExNPos;
    }

    public static string RegExNMatch(string src, string find, int n = 0)
    {
        // TODO (not supported): On Error Resume Next

        RegEx.Pattern = find;
        RegEx.Global = true;
        string regExNMatch = RegEx.Execute(src).Item(n).Value;
        return regExNMatch;
    }

    public static string RegExReplace(string src, string find, string repl)
    {
        // TODO (not supported): On Error Resume Next

        RegEx.Pattern = find;
        RegEx.Global = true;
        string regExReplace = RegEx.Replace(src, repl);
        return regExReplace;
    }

    public static dynamic RegExSplit(string szStr, string szPattern)
    {
        // TODO (not supported): On Error Resume Next

        var oRe = RegEx;
        oRe.Pattern = "^(.*)(" + szPattern + ")(.*)$";
        oRe.IgnoreCase = true;
        oRe.Global = true;
        var oAl = CreateObject("System.Collections.ArrayList");

        do
        {
            var oMatches = oRe.Execute(szStr);
            if (oMatches.Count > 0)
            {
                oAl.Add(oMatches[0].SubMatches[2]);
                szStr = oMatches[0].SubMatches[0];
            }
            else
            {
                oAl.Add(szStr);
                break;
            }
        } while (true);

        oAl.Reverse();
        var regExSplit = oAl.ToArray();
        return regExSplit;
    }

    public static int RegExSplitCount(string szStr, string szPattern)
    {
        // TODO (not supported): On Error Resume Next
        List<dynamic> T = new List<dynamic> { }; // TODO - Specified Minimum Array Boundary Not Supported:   Dim T() As Variant

        T = RegExSplit(szStr, szPattern);
        var regExSplitCount = UBound(T) - LBound(T) + 1;
        return regExSplitCount;
    }
}