using System;
using static Microsoft.VisualBasic.Strings;
using static Vb6ToCSharp.Parsing.ProjectConfigurationParser;
using static Vb6ToCSharp.Infrastructure.RegularExpressions;
using static Vb6ToCSharp.CodeConversion.ConversionUtility;
using static Vb6ToCSharp.Runtime.RuntimeExtension;
using Vb6ToCSharp.Runtime;


namespace Vb6ToCSharp.CodeConversion;

public static class ProjectSpecificConverter
{
    // Option Explicit


    public static string ProjectSpecificPostCodeLineConvert(string str)
    {
        var s = str;

        // Generic post-conversion fixes
        if (IsInStr(s, "MousePointer = vbNormal"))
        {
            s = Replace(s, "MousePointer = vbNormal", "MousePointer = vbDefault");
        }

        // We use decimal, not double
        if (IsInStr(s, "Val("))
        {
            s = Replace(s, "Val( ", "ValD(");
        }

        // Bad pattern combination
        if (RegExTest(s, "\\(!" + patToken + " == null\\)"))
        {
            s = Replace(s, "!", "", 1);
            s = Replace(s, "==", "!=", 1);
        }

        if (IsInStr(s, ".hwnd"))
        {
            s = Replace(s, ".hwnd", ".hWnd()");
        }
        s = Replace(s, "VbMsgBoxResult", "MsgBoxResult");

        // Project-specific rules from the INI [PostCodeLine] section (the original author's WinCDS rules used
        // to be hard-coded here; see Vb6ToCSharp.sample.ini)
        foreach (var rule in IniSection(iniSectionPostCodeLine))
        {
            s = ApplyPostCodeLineRule(s, rule.Value);
        }

        var projectSpecificPostCodeLineConvert = s;
        return projectSpecificPostCodeLineConvert;
    }

    /// <summary>
    /// Applies one rule, fields separated by '|':
    /// replace|find|repl · ifcontains|trigger|find|repl · regex|pattern|repl · blankif|trigger
    /// </summary>
    internal static string ApplyPostCodeLineRule(string s, string rule)
    {
        var f = Split(rule, "|");
        var kind = f.Length > 0 ? LCase(Trim(f[0])) : "";
        string F(int i) => i < f.Length ? f[i] : "";
        switch (kind)
        {
            case "replace":
                return F(1) == "" ? s : Replace(s, F(1), F(2));
            case "ifcontains":
                return F(1) != "" && F(2) != "" && IsInStr(s, F(1)) ? Replace(s, F(2), F(3)) : s;
            case "regex":
                return F(1) == "" ? s : RegExReplace(s, F(1), F(2));
            case "blankif":
                return F(1) != "" && IsInStr(s, F(1)) ? "" : s;
            default:
                Console.WriteLine("Unknown [" + iniSectionPostCodeLine + "] rule: " + rule);
                return s;
        }
    }
}