using static Vb6ToCSharp.Runtime.VbConstants;
using static Vb6ToCSharp.Runtime.VbStrings;
using static Vb6ToCSharp.Parsing.ProjectConfigurationParser;
using static Vb6ToCSharp.CodeConversion.ConversionUtility;
using static Vb6ToCSharp.Runtime.RuntimeExtension;
using Vb6ToCSharp.Runtime;


namespace Vb6ToCSharp.CodeConversion;

public static class FormConverter
{
    public static string Frm2Xml(string f)
    {
        var strings = new string[0];
        var i = 0;

        var r = "";

        strings = Split(f, vbCrLf);

        foreach (var iterL in strings)
        {
            var l = iterL;
            l = Trim(l);
            if (l == "")
            {
                goto NextLine;
            }
            if (Left(l, 10) == "Attribute " || Left(l, 8) == "VERSION ")
            {
            }
            else if (Left(l, 6) == "Begin ")
            {
                r = r + SSpace(i * spIndent) + "<item type=\"" + SplitWord(l, 2) + "\" name=\"" + SplitWord(l, 3) + "\">" + vbCrLf;
                i = i + 1;
            }
            else if (l == "End")
            {
                i = i - 1;
                r = r + SSpace(i * spIndent) + "</item>" + vbCrLf;
            }
            else
            {
                r = r + SSpace(i * spIndent) + "<prop name=\"" + SplitWord(l, 1, "=") + "\" value=\"" + SplitWord(l, 2, "=", true, true) + "\" />" + vbCrLf;
            }
            NextLine:;
        }
        var frm2Xml = r;
        return frm2Xml;
    }

    public static string FormControls(string src, string f, bool asLocal = true)
    {
        var sp = new string[0];

        var r = "";

        sp = Split(f, vbCrLf);

        foreach (var iterL in sp)
        {
            var l = iterL;
            l = Trim(l);
            if (l == "")
            {
                goto NextLine;
            }
            if (Left(l, 6) == "Begin ")
            {
                var ty = SplitWord(l, 2);
                var nm = SplitWord(l, 3);
                switch (ty)
                {
                    case "VB.Form":
                        break;
                    default:
                        var T = src + ":" + IIf(asLocal, "", src + ".") + nm + ":Control:" + ty;
                        if (Right(r, Len(T)) != T)
                        {
                            r = r + vbCrLf + T;
                        }
                        break;
                }
            }
            NextLine:;
        }
        var formControls = r;
        return formControls;
    }
}
