using System.Collections.Generic;
using Microsoft.VisualBasic;
using static Microsoft.VisualBasic.Constants;
using static Microsoft.VisualBasic.Conversion;
using static Microsoft.VisualBasic.Information;
using static Microsoft.VisualBasic.Strings;
using static Vb6ToCSharp.Modules.ModConfig;
using static Vb6ToCSharp.Modules.ModConvertUtils;
using static Vb6ToCSharp.Modules.ModUtils;
using static Vb6ToCSharp.Modules.ModVb6ToCs;
using static Vb6ToCSharp.VbExtension;


namespace Vb6ToCSharp.Modules;

public static class ModConvertForm
{
    public static string eventStubs = "";

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

    public static string ConvertFormUi(string f, string codeSection)
    {
        var stck = new List<string>(new string[1]);

        var sp = new string[0];
        var I = 0;
        var tag = "";

        var m = "";

        var r = "";

        var prefix = "";

        Collection props = null;

        sp = Split(f, vbCrLf);

        eventStubs = "";

        for (var k = LBound(sp); k <= UBound(sp); k++)
        {
            var l = Trim(sp[k]);
            if (l == "")
            {
                goto NextLine;
            }

            if (Left(l, 10) == "Attribute " || Left(l, 8) == "VERSION ")
            {
            }
            else if (Left(l, 6) == "Begin ")
            {
                props = new Collection(); ;
                var j = 0;
                do
                {
                    j = j + 1;
                    m = Trim(sp[k + j]);
                    if (LMatch(m, "Begin ") || m == "End")
                    {
                        break;
                    }

                    if (LMatch(m, "BeginProperty "))
                    {
                        prefix = LCase(prefix + SplitWord(m, 2) + ".");
                    }
                    else if (LMatch(m, "EndProperty"))
                    {
                        prefix = Left(prefix, Len(prefix) - 1);
                        if (!IsInStr(prefix, "."))
                        {
                            prefix = "";
                        }
                        else
                        {
                            prefix = Left(prefix, InStrRev(Left(prefix, Len(prefix) - 1), "."));
                        }
                    }
                    else
                    {
                        var pK = prefix + LCase(SplitWord(m, 1, "="));
                        var pV = ConvertProperty(SplitWord(m, 2, "=", true, true));
                        // TODO (not supported): On Error Resume Next
                        props.Add(pV, pK);
                        // TODO (not supported): On Error GoTo 0
                    }
                } while (true); // VB "Loop While" (was mistranslated as Loop Until)
                k = k + j - 1;
                r = r + SSpace(I * spIndent) + StartControl(l, props, LMatch(m, "End"), codeSection, out tag) + vbCrLf;
                I = I + 1;
                while (stck.Count <= I)
                {
                    stck.Add(""); // VB had Stck(0 To 100); the translation allocated a single slot
                }
                stck[I] = tag;
            }
            else if (l == "End")
            {
                props = null;
                tag = stck[I];
                I = I - 1;
                if (tag != "")
                {
                    r = r + SSpace(I * spIndent) + EndControl(tag) + vbCrLf;
                }
            }
            NextLine:;
        }
        var convertFormUi = r;
        return convertFormUi;
    }

    private static string ConvertProperty(string s)
    {
        s = DeQuote(s);
        s = DeComment(s);
        var convertProperty = s;
        return convertProperty;
    }

    private static string StartControl(string l, Collection props, bool doEmpty, string code, out string tagType)
    {
        var startControl = "";

        var tType = "";
        var tCont = false;
        var tDef = "";
        var features = "";

        var m = "";

        var n = vbCrLf;
        tagType = "";

        var cType = SplitWord(l, 2);
        var cName = SplitWord(l, 3);
        var cIndex = CValP(ref props, "Index");
        if (cIndex != "")
        {
            cName = cName + "_" + cIndex;
        }

        ControlData(cType, out tType, out tCont, out tDef, out features);

        var s = "";
        // TODO (not supported): On Error Resume Next
        if (tType == "Line" || tType == "Shape" || tType == "Timer")
        {
            return startControl;

        }
        else if (tType == "Window")
        {
            s = s + m + "<Window x:Class=\"" + AssemblyName() + ".Forms." + cName + "\"";
            s = s + n + "    xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"";
            s = s + n + "    xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";
            s = s + n + "    xmlns:d=\"http://schemas.microsoft.com/expression/blend/2008\"";
            s = s + n + "    xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\"";
            s = s + n + "    xmlns:local=\"clr-namespace:" + AssemblyName() + ".Forms\"";
            s = s + n + "    xmlns:usercontrols=\"clr-namespace:" + AssemblyName() + ".UserControls\"";
            s = s + n + "    mc:Ignorable=\"d\"";
            s = s + n + "    Title=" + Quote(CValP(ref props, "caption"));
            s = s + m + "    Height=" + Quote(Px(Val(CValP(ref props, "clientheight", "0")) + 435));
            s = s + m + "    Width=" + Quote(Px(Val(CValP(ref props, "clientwidth", "0")) + 435));
            s = s + CheckControlEvents("Window", "Form", code);
            s = s + m + ">";
            s = s + n + " <Grid";
        }
        else if (tType == "GroupBox")
        {
            s = s + "<" + tType;
            s = s + " x:Name=\"" + cName + "\"";

            s = s + " Margin=" + Quote(Px(CValP(ref props, "left")) + "," + Px(CValP(ref props, "top")) + ",0,0");
            s = s + " Width=" + Quote(Px(CValP(ref props, "width")));
            s = s + " Height=" + Quote(Px(CValP(ref props, "height")));
            s = s + " VerticalAlignment=\"Top\"";
            s = s + " HorizontalAlignment=\"Left\"";
            s = s + " FontFamily=" + Quote(CValP(ref props, "font.name", "Calibri"));
            s = s + " FontSize=" + Quote(CValP(ref props, "font.size", "10"));

            s = s + " Header=\"" + CValP(ref props, "caption") + "\"";
            s = s + "> <Grid Margin=\"0,-15,0,0\"";
        }
        else if (tType == "Canvas")
        {
            s = s + "<" + tType;
            s = s + " x:Name=\"" + cName + "\"";

            s = s + " Margin=" + Quote(Px(CValP(ref props, "left")) + "," + Px(CValP(ref props, "top")) + ",0,0");
            s = s + " Width=" + Quote(Px(CValP(ref props, "width")));
            s = s + " Height=" + Quote(Px(CValP(ref props, "height")));
        }
        else if (tType == "Image")
        {
            s = s + "<" + tType;

            s = s + " x:Name=\"" + cName + "\"";
            s = s + " Margin=" + Quote(Px(CValP(ref props, "left")) + "," + Px(CValP(ref props, "top")) + ",0,0");
            s = s + " Width=" + Quote(Px(CValP(ref props, "width")));
            s = s + " Height=" + Quote(Px(CValP(ref props, "height")));
            s = s + " VerticalAlignment=" + Quote("Top");
            s = s + " HorizontalAlignment=" + Quote("Left");
        }
        else
        {
            s = "";
            s = s + "<" + tType;
            s = s + " x:Name=\"" + cName + "\"";
            s = s + " Margin=" + Quote(Px(CValP(ref props, "left")) + "," + Px(CValP(ref props, "top")) + ",0,0");
            s = s + " Padding=" + Quote("2,2,2,2");
            s = s + " Width=" + Quote(Px(CValP(ref props, "width")));
            s = s + " Height=" + Quote(Px(CValP(ref props, "height")));
            s = s + " VerticalAlignment=" + Quote("Top");
            s = s + " HorizontalAlignment=" + Quote("Left");

        }

        if (IsInStr(features, "Font"))
        {
            s = s + " FontFamily=" + Quote(CValP(ref props, "font.name", "Calibri"));
            s = s + " FontSize=" + Quote(CValP(ref props, "font.size", "10"));
            if (Val(CValP(ref props, "font.weight", "400")) > 400)
            {
                s = s + " FontWeight=" + Quote("Bold");
            }

        }

        if (IsInStr(features, "Content"))
        {
            s = s + " Content=" + QuoteXml(CValP(ref props, "caption") + CValP(ref props, "text"));
        }

        if (IsInStr(features, "Header"))
        {
            s = s + " Content=" + QuoteXml(CValP(ref props, "caption") + CValP(ref props, "text"));
        }

        var v = CValP(ref props, "caption") + CValP(ref props, "text");
        if (IsInStr(features, "Text") && v != "")
        {
            s = s + " Text=" + QuoteXml(v);
        }

        v = CValP(ref props, "ToolTipText");
        if (IsInStr(features, "ToolTip") && v != "")
        {
            s = s + " ToolTip=" + Quote(v);
        }

        s = s + CheckControlEvents(tType, cName, code);

        if (doEmpty)
        {
            s = s + " />";
            tagType = "";
        }
        else
        {
            s = s + ">";
            tagType = tType;
        }
        startControl = s;
        return startControl;
    }

    public static string CheckControlEvents(string controlType, string controlName, string codeSection = "")
    {
        var hasClick = true;
        var hasFocus = !IsInStr("GroupBox", controlType);
        var hasChange = IsInStr("TextBox,ListBox", controlType);
        var isWindow = controlType == "Window";

        var res = "";
        res = res + CheckEvent("MouseMove", controlName, controlType, codeSection);
        if (hasFocus)
        {
            res = res + CheckEvent("GotFocus", controlName, controlType, codeSection);
            res = res + CheckEvent("LostFocus", controlName, controlType, codeSection);
            res = res + CheckEvent("KeyDown", controlName, controlType, codeSection);
            res = res + CheckEvent("KeyUp", controlName, controlType, codeSection);
        }
        if (hasClick)
        {
            res = res + CheckEvent("Click", controlName, controlType, codeSection);
            res = res + CheckEvent("DblClick", controlName, controlType, codeSection);
        }
        if (hasChange)
        {
            res = res + CheckEvent("Change", controlName, controlType, codeSection);
        }
        if (isWindow)
        {
            res = res + CheckEvent("Load", controlName, controlType, codeSection);
            res = res + CheckEvent("Unload", controlName, controlType, codeSection);
            //    Res = Res & CheckEvent("QueryUnload", ControlName, ControlType, CodeSection)
        }

        var checkControlEvents = res;
        return checkControlEvents;
    }

    public static string CheckEvent(string eventName, string controlName, string controlType, string codeSection = "")
    {
        var checkEvent = "";

        var n = controlName + "_" + eventName;
        var search = " " + n + "(";
        var target = eventName;
        switch (eventName)
        {
            case "DblClick":
                target = "MouseDoubleClick";
                break;
            case "Change":
                if (controlType == "TextBox")
                {
                    target = "TextChanged";
                }
                break;
            case "Load":
                target = "Loaded";
                break;
            case "Unload":
                target = "Unloaded";
                break;
        }
        var l = InStr(1, codeSection, search, vbTextCompare);
        if (l > 0)
        {
            var v = Mid(codeSection, l + 1, Len(n)); // Get exact capitalization from source....
            checkEvent = " " + target + "=\"" + v + "\"";
        }
        else
        {
            checkEvent = "";
        }
        return checkEvent;
    }

    public static string EndControl(string tType)
    {
        var endControl = "";
        switch (tType)
        {
            case "Line":
                endControl = "";
                break;
            case "Window":
                endControl = " </Grid>" + vbCrLf + "</Window>";
                break;
            case "GroupBox":
                endControl = "</Grid> </GroupBox>";
                break;
            default:
                endControl = "</" + tType + ">";
                break;
        }
        return endControl;
    }

    public static bool IsEvent(string str)
    {
        var isEvent = EventStub(str) != "";
        return isEvent;
    }

    public static string EventStub(string fName)
    {
        var s = "";


        var c = SplitWord(fName, 1, "_");
        var k = SplitWord(fName, 2, "_");
        switch (k)
        {
            case "Click":
                s = "private void " + fName + "(object sender, RoutedEventArgs e) { " + fName + "(); }" + vbCrLf;
                break;
            case "Change":
                s = "private void " + c + "_Change(object sender, System.Windows.Controls.TextChangedEventArgs e) { " + fName + "(); }" + vbCrLf;
                break;
            case "QueryUnload":
                s = "private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) { int c = 0, u = 0 ;  " + fName + "(out c, ref u); e.Cancel = c != 0;  }" + vbCrLf;
                //      V = " long doCancel; long UnloadMode; " & FName & "(ref doCancel, ref UnloadMode);"
                break;
            case "Validate":
                //      V = "long doCancel; " & FName & "(ref doCancel);"
                break;
            case "KeyDown":
                break;
            case "MouseMove":
                break;
        }

        var eventStub = s;
        return eventStub;
    }
}