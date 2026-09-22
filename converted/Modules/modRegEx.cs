using System.Collections.Generic;
using static Microsoft.VisualBasic.Information;
using static Microsoft.VisualBasic.Interaction;
using static VBExtension;


static class modRegEx
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
            var RegEx = mRegEx;

            return RegEx;
        }
    }


    public static bool RegExTest(string Src, string Find)
    {
        // TODO (not supported): On Error Resume Next
        RegEx.Pattern = Find;
        bool RegExTest = RegEx.test(Src);
        return RegExTest;
    }

    public static int RegExCount(string Src, string Find)
    {
        // TODO (not supported): On Error Resume Next
        RegEx.Pattern = Find;
        RegEx.Global = true;
        int RegExCount = RegEx.Execute(Src).Count;
        return RegExCount;
    }

    public static int RegExNPos(string Src, string Find, int N = 0)
    {
        // TODO (not supported): On Error Resume Next
        dynamic RegM = null;
        string tempStr = "";
        string tempStr2 = "";

        RegEx.Pattern = Find;
        RegEx.Global = true;
        int RegExNPos = RegEx.Execute(Src).Item(N).FirstIndex + 1;
        return RegExNPos;
    }

    public static string RegExNMatch(string Src, string Find, int N = 0)
    {
        // TODO (not supported): On Error Resume Next
        dynamic RegM = null;
        string tempStr = "";
        string tempStr2 = "";

        RegEx.Pattern = Find;
        RegEx.Global = true;
        string RegExNMatch = RegEx.Execute(Src).Item(N).Value;
        return RegExNMatch;
    }

    public static string RegExReplace(string Src, string Find, string Repl)
    {
        // TODO (not supported): On Error Resume Next
        dynamic RegM = null;
        string tempStr = "";
        string tempStr2 = "";

        RegEx.Pattern = Find;
        RegEx.Global = true;
        string RegExReplace = RegEx.Replace(Src, Repl);
        return RegExReplace;
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
        var RegExSplit = oAl.ToArray();
        return RegExSplit;
    }

    public static int RegExSplitCount(string szStr, string szPattern)
    {
        // TODO (not supported): On Error Resume Next
        List<dynamic> T = new List<dynamic> { }; // TODO - Specified Minimum Array Boundary Not Supported:   Dim T() As Variant

        T = RegExSplit(szStr, szPattern);
        var RegExSplitCount = UBound(T) - LBound(T) + 1;
        return RegExSplitCount;
    }
}
