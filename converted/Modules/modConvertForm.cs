using Microsoft.VisualBasic;
using System.Collections.Generic;
using static Microsoft.VisualBasic.Constants;
using static Microsoft.VisualBasic.Conversion;
using static Microsoft.VisualBasic.Information;
using static Microsoft.VisualBasic.Interaction;
using static Microsoft.VisualBasic.Strings;
using static modConfig;
using static modConvertUtils;
using static modUtils;
using static modVB6ToCS;
using static VBExtension;


static class modConvertForm
{
    // Option Explicit
    private static string EventStubs = "";


    public static string Frm2Xml(string F)
    {
        string[] Sp = new string[0];
        int I = 0;

        string R = "";

        Sp = Split(F, vbCrLf);

        foreach (var iterL in Sp)
        {
            var L = iterL;
            L = Trim(L);
            if (L == "")
            {
                goto NextLine;
            }
            if (Left(L, 10) == "Attribute " || Left(L, 8) == "VERSION ")
            {
            }
            else if (Left(L, 6) == "Begin ")
            {
                R = R + sSpace(I * SpIndent) + "<item type=\"" + SplitWord(L, 2) + "\" name=\"" + SplitWord(L, 3) + "\">" + vbCrLf;
                I = I + 1;
            }
            else if (L == "End")
            {
                I = I - 1;
                R = R + sSpace(I * SpIndent) + "</item>" + vbCrLf;
            }
            else
            {
                R = R + sSpace(I * SpIndent) + "<prop name=\"" + SplitWord(L, 1, "=") + "\" value=\"" + SplitWord(L, 2, "=", true, true) + "\" />" + vbCrLf;
            }
        NextLine:;
        }
        var Frm2Xml = R;
        return Frm2Xml;
    }

    public static string FormControls(string Src, string F, bool asLocal = true)
    {
        string[] Sp = new string[0];
        int I = 0;

        string R = "";

        Sp = Split(F, vbCrLf);

        foreach (var iterL in Sp)
        {
            var L = iterL;
            L = Trim(L);
            if (L == "")
            {
                goto NextLine;
            }
            if (Left(L, 6) == "Begin ")
            {
                var Ty = SplitWord(L, 2);
                var Nm = SplitWord(L, 3);
                switch (Ty)
                {
                    case "VB.Form":
                        break;
                    default:
                        var T = Src + ":" + IIf(asLocal, "", Src + ".") + Nm + ":Control:" + Ty;
                        if (Right(R, Len(T)) != T)
                        {
                            R = R + vbCrLf + T;
                        }
                        break;
                }
            }
        NextLine:;
        }
        var FormControls = R;
        return FormControls;
    }

    public static string ConvertFormUi(string F, string CodeSection)
    {
        List<string> Stck = new List<string>(new string[1]);

        string[] Sp = new string[0];
        int I = 0;
        string Tag = "";

        string M = "";

        string R = "";

        string Prefix = "";

        Collection Props = null;

        Sp = Split(F, vbCrLf);

        EventStubs = "";

        for (var K = LBound(Sp); K <= UBound(Sp); K++)
        {
            var L = Trim(Sp[K]);
            if (L == "")
            {
                goto NextLine;
            }

            if (Left(L, 10) == "Attribute " || Left(L, 8) == "VERSION ")
            {
            }
            else if (Left(L, 6) == "Begin ")
            {
                Props = new Collection(); ;
                var J = 0;
                do
                {
                    J = J + 1;
                    M = Trim(Sp[K + J]);
                    if (LMatch(M, "Begin ") || M == "End")
                    {
                        break;
                    }

                    if (LMatch(M, "BeginProperty "))
                    {
                        Prefix = LCase(Prefix + SplitWord(M, 2) + ".");
                    }
                    else if (LMatch(M, "EndProperty"))
                    {
                        Prefix = Left(Prefix, Len(Prefix) - 1);
                        if (!IsInStr(Prefix, "."))
                        {
                            Prefix = "";
                        }
                        else
                        {
                            Prefix = Left(Prefix, InStrRev(Left(Prefix, Len(Prefix) - 1), "."));
                        }
                    }
                    else
                    {
                        var pK = Prefix + LCase(SplitWord(M, 1, "="));
                        var pV = ConvertProperty(SplitWord(M, 2, "=", true, true));
                        // TODO (not supported): On Error Resume Next
                        Props.Add(pV, pK);
                        // TODO (not supported): On Error GoTo 0
                    }
                } while (!(true));
                K = K + J - 1;
                R = R + sSpace(I * SpIndent) + StartControl(L, Props, LMatch(M, "End"), CodeSection, out Tag) + vbCrLf;
                I = I + 1;
                Stck[I] = Tag;
            }
            else if (L == "End")
            {
                Props = null;
                Tag = Stck[I];
                I = I - 1;
                if (Tag != "")
                {
                    R = R + sSpace(I * SpIndent) + EndControl(Tag) + vbCrLf;
                }
            }
        NextLine:;
        }
        var ConvertFormUi = R;
        return ConvertFormUi;
    }

    private static string ConvertProperty(string S)
    {
        S = deQuote(S);
        S = DeComment(S);
        var ConvertProperty = S;
        return ConvertProperty;
    }

    private static string StartControl(string L, Collection Props, bool DoEmpty, string Code, out string TagType)
    {
        string StartControl = "";

        string tType = "";
        bool tCont = false;
        string tDef = "";
        string Features = "";

        string M = "";

        var N = vbCrLf;
        TagType = "";

        var cType = SplitWord(L, 2);
        var cName = SplitWord(L, 3);
        var cIndex = cValP(ref Props, "Index");
        if (cIndex != "")
        {
            cName = cName + "_" + cIndex;
        }

        ControlData(cType, out tType, out tCont, out tDef, out Features);

        var S = "";
        // TODO (not supported): On Error Resume Next
        if (tType == "Line" || tType == "Shape" || tType == "Timer")
        {
            return StartControl;

        }
        else if (tType == "Window")
        {
            S = S + M + "<Window x:Class=\"" + AssemblyName() + ".Forms." + cName + "\"";
            S = S + N + "    xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"";
            S = S + N + "    xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";
            S = S + N + "    xmlns:d=\"http://schemas.microsoft.com/expression/blend/2008\"";
            S = S + N + "    xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\"";
            S = S + N + "    xmlns:local=\"clr-namespace:" + AssemblyName() + ".Forms\"";
            S = S + N + "    xmlns:usercontrols=\"clr-namespace:" + AssemblyName() + ".UserControls\"";
            S = S + N + "    mc:Ignorable=\"d\"";
            S = S + N + "    Title=" + Quote(cValP(ref Props, "caption"));
            S = S + M + "    Height=" + Quote(Px(Val(cValP(ref Props, "clientheight", "0")) + 435));
            S = S + M + "    Width=" + Quote(Px(Val(cValP(ref Props, "clientwidth", "0")) + 435));
            S = S + CheckControlEvents("Window", "Form", Code);
            S = S + M + ">";
            S = S + N + " <Grid";
        }
        else if (tType == "GroupBox")
        {
            S = S + "<" + tType;
            S = S + " x:Name=\"" + cName + "\"";

            S = S + " Margin=" + Quote(Px(cValP(ref Props, "left")) + "," + Px(cValP(ref Props, "top")) + ",0,0");
            S = S + " Width=" + Quote(Px(cValP(ref Props, "width")));
            S = S + " Height=" + Quote(Px(cValP(ref Props, "height")));
            S = S + " VerticalAlignment=\"Top\"";
            S = S + " HorizontalAlignment=\"Left\"";
            S = S + " FontFamily=" + Quote(cValP(ref Props, "font.name", "Calibri"));
            S = S + " FontSize=" + Quote(cValP(ref Props, "font.size", "10"));

            S = S + " Header=\"" + cValP(ref Props, "caption") + "\"";
            S = S + "> <Grid Margin=\"0,-15,0,0\"";
        }
        else if (tType == "Canvas")
        {
            S = S + "<" + tType;
            S = S + " x:Name=\"" + cName + "\"";

            S = S + " Margin=" + Quote(Px(cValP(ref Props, "left")) + "," + Px(cValP(ref Props, "top")) + ",0,0");
            S = S + " Width=" + Quote(Px(cValP(ref Props, "width")));
            S = S + " Height=" + Quote(Px(cValP(ref Props, "height")));
        }
        else if (tType == "Image")
        {
            S = S + "<" + tType;

            S = S + " x:Name=\"" + cName + "\"";
            S = S + " Margin=" + Quote(Px(cValP(ref Props, "left")) + "," + Px(cValP(ref Props, "top")) + ",0,0");
            S = S + " Width=" + Quote(Px(cValP(ref Props, "width")));
            S = S + " Height=" + Quote(Px(cValP(ref Props, "height")));
            S = S + " VerticalAlignment=" + Quote("Top");
            S = S + " HorizontalAlignment=" + Quote("Left");
        }
        else
        {
            S = "";
            S = S + "<" + tType;
            S = S + " x:Name=\"" + cName + "\"";
            S = S + " Margin=" + Quote(Px(cValP(ref Props, "left")) + "," + Px(cValP(ref Props, "top")) + ",0,0");
            S = S + " Padding=" + Quote("2,2,2,2");
            S = S + " Width=" + Quote(Px(cValP(ref Props, "width")));
            S = S + " Height=" + Quote(Px(cValP(ref Props, "height")));
            S = S + " VerticalAlignment=" + Quote("Top");
            S = S + " HorizontalAlignment=" + Quote("Left");

        }

        if (IsInStr(Features, "Font"))
        {
            S = S + " FontFamily=" + Quote(cValP(ref Props, "font.name", "Calibri"));
            S = S + " FontSize=" + Quote(cValP(ref Props, "font.size", "10"));
            if (Val(cValP(ref Props, "font.weight", "400")) > 400)
            {
                S = S + " FontWeight=" + Quote("Bold");
            }

        }

        if (IsInStr(Features, "Content"))
        {
            S = S + " Content=" + QuoteXML(cValP(ref Props, "caption") + cValP(ref Props, "text"));
        }

        if (IsInStr(Features, "Header"))
        {
            S = S + " Content=" + QuoteXML(cValP(ref Props, "caption") + cValP(ref Props, "text"));
        }

        var V = cValP(ref Props, "caption") + cValP(ref Props, "text");
        if (IsInStr(Features, "Text") && V != "")
        {
            S = S + " Text=" + QuoteXML(V);
        }

        V = cValP(ref Props, "ToolTipText");
        if (IsInStr(Features, "ToolTip") && V != "")
        {
            S = S + " ToolTip=" + Quote(V);
        }

        S = S + CheckControlEvents(tType, cName, Code);

        if (DoEmpty)
        {
            S = S + " />";
            TagType = "";
        }
        else
        {
            S = S + ">";
            TagType = tType;
        }
        StartControl = S;
        return StartControl;
    }

    public static string CheckControlEvents(string ControlType, string ControlName, string CodeSection = "")
    {
        var HasClick = true;
        var HasFocus = !IsInStr("GroupBox", ControlType);
        var HasChange = IsInStr("TextBox,ListBox", ControlType);
        var IsWindow = ControlType == "Window";

        var Res = "";
        Res = Res + CheckEvent("MouseMove", ControlName, ControlType, CodeSection);
        if (HasFocus)
        {
            Res = Res + CheckEvent("GotFocus", ControlName, ControlType, CodeSection);
            Res = Res + CheckEvent("LostFocus", ControlName, ControlType, CodeSection);
            Res = Res + CheckEvent("KeyDown", ControlName, ControlType, CodeSection);
            Res = Res + CheckEvent("KeyUp", ControlName, ControlType, CodeSection);
        }
        if (HasClick)
        {
            Res = Res + CheckEvent("Click", ControlName, ControlType, CodeSection);
            Res = Res + CheckEvent("DblClick", ControlName, ControlType, CodeSection);
        }
        if (HasChange)
        {
            Res = Res + CheckEvent("Change", ControlName, ControlType, CodeSection);
        }
        if (IsWindow)
        {
            Res = Res + CheckEvent("Load", ControlName, ControlType, CodeSection);
            Res = Res + CheckEvent("Unload", ControlName, ControlType, CodeSection);
            //    Res = Res & CheckEvent("QueryUnload", ControlName, ControlType, CodeSection)
        }

        var CheckControlEvents = Res;
        return CheckControlEvents;
    }

    public static string CheckEvent(string EventName, string ControlName, string ControlType, string CodeSection = "")
    {
        string CheckEvent = "";

        var N = ControlName + "_" + EventName;
        var Search = " " + N + "(";
        var Target = EventName;
        switch (EventName)
        {
            case "DblClick":
                Target = "MouseDoubleClick";
                break;
            case "Change":
                if (ControlType == "TextBox")
                {
                    Target = "TextChanged";
                }
                break;
            case "Load":
                Target = "Loaded";
                break;
            case "Unload":
                Target = "Unloaded";
                break;
        }
        var L = InStr(1, CodeSection, Search, vbTextCompare);
        if (L > 0)
        {
            var V = Mid(CodeSection, L + 1, Len(N)); // Get exact capitalization from source....
            CheckEvent = " " + Target + "=\"" + V + "\"";
        }
        else
        {
            CheckEvent = "";
        }
        return CheckEvent;
    }

    public static string EndControl(string tType)
    {
        string EndControl = "";
        switch (tType)
        {
            case "Line":
                EndControl = "";
                break;
            case "Window":
                EndControl = " </Grid>" + vbCrLf + "</Window>";
                break;
            case "GroupBox":
                EndControl = "</Grid> </GroupBox>";
                break;
            default:
                EndControl = "</" + tType + ">";
                break;
        }
        return EndControl;
    }

    public static bool IsEvent(string Str)
    {
        var IsEvent = EventStub(Str) != "";
        return IsEvent;
    }

    public static string EventStub(string fName)
    {
        string S = "";


        var C = SplitWord(fName, 1, "_");
        var K = SplitWord(fName, 2, "_");
        switch (K)
        {
            case "Click":
                S = "private void " + fName + "(object sender, RoutedEventArgs e) { " + fName + "(); }" + vbCrLf;
                break;
            case "Change":
                S = "private void " + C + "_Change(object sender, System.Windows.Controls.TextChangedEventArgs e) { " + fName + "(); }" + vbCrLf;
                break;
            case "QueryUnload":
                S = "private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) { int c = 0, u = 0 ;  " + fName + "(out c, ref u); e.Cancel = c != 0;  }" + vbCrLf;
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

        var EventStub = S;
        return EventStub;
    }
}
