using System;
using System.Collections.Generic;
using Vb6ToCSharp.CodeGeneration;
using static Microsoft.VisualBasic.Strings;
using static Vb6ToCSharp.Parsing.ProjectConfigurationParser;
using static Vb6ToCSharp.Parsing.ProjectFiles;
using static Vb6ToCSharp.Modules.ModRegEx;
using static Vb6ToCSharp.Modules.ModUtils;
using static Vb6ToCSharp.Runtime.RuntimeExtension;

namespace Vb6ToCSharp.ItemConversion;

public static class Vb6ToCsConverter
{
    // Option Explicit


    public static string ConvertDefaultDefault(string dType)
    {
        var convertDefaultDefault = "";
        switch (dType)
        {
            case "Integer":
            case "Long":
            case "Single":
            case "Double":
            case "Currency":
            case "Decimal":
            case "Byte":
                convertDefaultDefault = "0";
                break;
            case "Date":
                convertDefaultDefault = "DateTime.MinValue";
                break;
            case "String":
                convertDefaultDefault = "\"\"";
                break;
            case "Boolean":
                convertDefaultDefault = "false";
                break;
            default:
                convertDefaultDefault = "null";
                break;
        }
        return convertDefaultDefault;
    }

    public static string ConvertDataType(string s)
    {
        var convertDataType = IniMap(iniSectionDataTypes, s); // project-specific types from config win
        if (convertDataType != null)
        {
            return convertDataType;
        }
        s = ProjectGroup.StripProjectQualifier(s); // Lib.CFoo: a class of this project or of a referenced one
        if (s != null && s.Length > 2 && s.EndsWith("()"))
        { // array return / parameter type: Long() -> int[]
            return ConvertDataType(s.Substring(0, s.Length - 2)) + "[]";
        }
        switch (s)
        {
            case "Object":
                convertDataType = defaultDataType;
                break;
            case "Form":
                convertDataType = "Window";
                break;
            case "String":
                convertDataType = "string";
                break;
            // VB6 widths (as VB Migration Partner): Integer is 16-bit, Long 32-bit, Double a binary double
            case "Long":
                convertDataType = "int";
                break;
            case "Integer":
                convertDataType = "short";
                break;
            case "Double":
                convertDataType = "double";
                break;
            case "Decimal":
                convertDataType = "decimal";
                break;
            case "Variant": // late-bound, as in VB6 (object would reject arithmetic and member calls)
                convertDataType = "dynamic";
                break;
            case "Byte":
                convertDataType = "byte";
                break;
            case "Single":
                convertDataType = "float";
                break;
            case "Any":
            case "IUnknown": // NewEnum returns an enumerator
                convertDataType = "object";
                break;
            case "Boolean":
                convertDataType = "bool";
                break;
            case "Currency":
                convertDataType = "decimal";
                break;
            case "VbTriState":
                convertDataType = "vbTriState";
                break;
            case "Collection":
                convertDataType = "Collection";
                break;
            case "Node":
                convertDataType = "TreeViewItem";
                break;
            case "Recordset":
                convertDataType = "Recordset";
                break;
            case "Connection":
                convertDataType = "Connection";
                break;
            case "ADODB.Error":
                convertDataType = "ADODB.Error";
                break;
            case "ADODB.EventStatusEnum":
                convertDataType = "ADODB.EventStatusEnum";
                break;
            case "Date":
                convertDataType = "DateTime";
                break;
            case "VbMsgBoxResult":
                convertDataType = s;
                break;
            case "PictureBox":
                convertDataType = s;
                break;
            case "MSCommLib.MSComm":
                convertDataType = s;

                break;
            default:
                if (IsInStr(VbpClasses(classNames: true), s) || ClassesConverter.IsProjectClass(s) || StatementsConverter.IsUdt(s))
                {
                    convertDataType = s;
                }
                else
                {
                    convertDataType = s;
                    Console.WriteLine("Unknown Data Type: " + s);
                }
                break;
        }
        return convertDataType;
    }

    public static void ControlData(string cType, out string name, out bool cont, out string def, out string features)
    {
        name = "";
        features = "";
        cont = false;
        def = "Caption";
        var mapped = IniMap(iniSectionControls, cType); // project-specific controls from config: Name[;cont;def;features]
        if (mapped != null)
        {
            var p = Split(mapped, ";");
            name = Trim(p[0]);
            cont = p.Length > 1 && (Trim(p[1]) == "1" || LCase(Trim(p[1])) == "true");
            def = p.Length > 2 && Trim(p[2]) != "" ? Trim(p[2]) : def;
            features = p.Length > 3 ? Trim(p[3]) : features;
            return;
        }
        switch (cType)
        {
            case "VB.Form":
                name = "Window";
                cont = true;
                break;
            case "VB.MDIForm":
                name = "Window";
                cont = true;
                cont = true;

                break;
            case "VB.PictureBox":
                name = "Image";
                cont = true;
                def = "Picture";
                features = "Tooltiptext";
                break;
            case "VB.Label":
                name = "Label";
                features = "";
                features = "Font,Content,Tooltiptext";
                break;
            case "VB.TextBox":
                name = "TextBox";
                def = "Text";
                features = "Font,Text,Tooltiptext";
                break;
            case "VB.Frame":
                name = "GroupBox";
                features = "Tooltiptext";
                break;
            case "VB.CommandButton":
                name = "Button";
                features = "Font,Content,Tooltiptext";
                break;
            case "VB.CheckBox":
                name = "CheckBox";
                features = "Font,Content,Tooltiptext";
                break;
            case "VB.OptionButton":
                name = "RadioButton";
                features = "Font,Content,Tooltiptext";
                break;
            case "VB.ComboBox":
                name = "ComboBox";
                def = "Text";
                features = "Font,Text,Tooltiptext";
                break;
            case "VB.ListBox":
                name = "ListBox";
                def = "Text";
                features = "Font,Tooltiptext";
                break;
            case "VB.HScrollBar":
                name = "ScrollBar";
                def = "Value";
                features = "";
                break;
            case "VB.VScrollBar":
                name = "ScrollBar";
                def = "Value";
                features = "";
                break;
            case "VB.Timer":
                name = "Timer";
                def = "Enabled";
                features = "";
                break;
            case "VB.DriveListBox":
                name = "usercontrols:DriveListBox";
                def = "Path";
                features = "";
                break;
            case "VB.DirListBox":
                name = "usercontrols:DirListBox";
                def = "Path";
                features = "";
                break;
            case "VB.FileListBox":
                name = "usercontrols:FileListBox";
                def = "Path";
                features = "";
                break;
            case "VB.Shape":
                name = "Shape";
                def = "Visible";
                features = "";
                break;
            case "VB.Line":
                name = "Line";
                def = "Visible";
                features = "";
                break;
            case "VB.Image":
                name = "Image";
                def = "Picture";
                features = "Tooltiptext";
                break;
            case "VB.Data":
                name = "Data";
                def = "DataSource";
                features = "";
                break;
            case "VB.OLE":
                name = "OLE";
                def = "OLE";
                features = "";

                break;
            case "VB.Menu":
                name = "Menu";

                // MS Windows Common Controls 6.0
                break;
            case "MSComctlLib.TabStrip":
                break;
            case "MSComctlLib.ToolBar":
                break;
            case "MSComctlLib.StatusBar":
                name = "StatusBar";
                def = "Text";
                features = "Tooltiptext";
                break;
            case "MSComctlLib.ProgressBar":
                name = "ProgressBar";
                def = "Value";
                features = "Tooltiptext";
                break;
            case "MSComctlLib.TreeView":
                name = "TreeView";
                features = "Tooltiptext";
                break;
            case "MSComctlLib.ListView":
                name = "ListView";
                features = "Tooltiptext";
                break;
            case "MSComctlLib.ImageList":
                name = "ImageList";
                features = "Tooltiptext";
                break;
            case "MSComctlLib.Slider":
                name = "Slider";
                break;
            case "MSComctlLib.ImageCombo":
                // MS Windows Common Controls-2 6.0
                //    Case "MSComCtl2.Animation":
                break;
            case "MSComCtl2.UpDown":
                name = "usercontrols:UpDown";
                break;
            case "MSComCtl2.DTPicker":
                name = "DatePicker";
                break;
            case "MSComCtl2.MonthView":
                name = "DatePicker";
                break;
            case "MSComCtl2.FlatScrollBar":
                name = "ScrollBar";

                break;
            case "MSComDlg.CommonDialog":
                name = "Label";
                break;
            case "MSFlexGridLib.MSFlexGrid":
                name = "usercontrols:FlexGrid";
                break;
            case "MSDBGrid.DBGrid":
                name = "DataGrid";
                break;
            case "TabDlg.SSTab":
                name = "TabControl";
                break;
            case "RichTextLib.RichTextBox":
                name = "TextBlock";
                break;
            case "InetCtlsObjects.Inet":
                name = "INet";
                break;
            case "MSCommLib.MSComm":
                name = "MSComm";
                break;
            case "MSWinsockLib.Winsock":
                name = "Winsock";

                break;
            case "VJCZIPLib.VjcZip":
                name = "Label";
                break;
            case "MSChart20Lib.MSChart":
                name = "Label";
                break;
            case "MapPointCtl.MappointControl":
                name = "Label";

                break;
            case "LaVolpeAlphaImg.AlphaImgCtl":
                name = "Image";
                break;
            case "GIF89LibCtl.Gif89a":
                name = "Image";

                break;
            default:
                Console.WriteLine("Unknown Control Type: " + cType);
                name = "Label";
                break;
        }
    }

    public static string ConvertVb6Specific(string s, out bool complete)
    {
        switch (Trim(s))
        {
            case "Array()":
                s = "new List<dynamic>()";
                break;
            case "App.Path":
                s = "AppDomain.CurrentDomain.BaseDirectory";
                break;
        }

        complete = false;
        var w = RegExNMatch(Trim(s), patToken);
        var r = SplitWord(Trim(s), 2, " ", true, true);
        // keyword literals only when they are the whole element ("Me.Caption" is a member access, "Date + 1" an expression)
        var whole = Trim(s) == w;
        if (LMatch(Trim(s), "Me.") && !whole)
        {
            var member = RegExNMatch(Mid(Trim(s), 4), patToken);
            if (FormContext.Current != null && member != "")
            { // a form's own property (Me.Caption -> Text / Title)
                s = "this." + ControlProperties.ConvertControlProperty("", member, "VB.Form") + Mid(Trim(s), 4 + Len(member));
            }
            else
            {
                s = "this." + Mid(Trim(s), 4);
            }
            w = "";
        }
        switch (w)
        {
            case "True" when whole:
                complete = true;
                s = "true";
                break;
            case "False" when whole:
                complete = true;
                s = "false";
                break;
            case "Me" when whole:
                complete = true;
                s = "this";
                break;
            case "Nothing" when whole:
                complete = true;
                s = "null";
                break;
            case "Empty" when whole:
                complete = true;
                s = "null";
                break;
            case "Null" when whole:
                complete = true; // VB6 Null is a database null, distinct from Empty / Nothing
                s = "DBNull.Value";
                break;
            case "vbTrue" when whole:
                complete = true;
                s = "vbTriState.vbTrue";
                break;
            case "vbFalse" when whole:
                complete = true;
                s = "vbTriState.vbFalse";
                break;
            case "vbUseDefault" when whole:
                complete = true;
                s = "vbTriState.vbUseDefault";
                break;
            case "Date" when whole:
                complete = true;
                s = "DateTime.Today";
                break;
            case "Now" when whole:
                complete = true;
                s = "DateTime.Now";
                break;
            case "Time" when whole:
                complete = true;
                s = "DateAndTime.TimeOfDay";
                break;
            case "Err" when whole:
                complete = true; // the default member of Err
                s = "Err().Number";
                break;
            case "Erl" when whole:
                complete = true;
                s = "Err().Erl";
                break;
            case "Error" when whole:
                complete = true;
                s = "Err().Description";
                break;
            case "Error" when LMatch(Trim(s), "Error("):
                s = "ErrorToString" + Mid(Trim(s), 6);
                break;
            case "FreeFile" when whole:
                complete = true;
                s = "FreeFile()";
                break;
            case "New":
                complete = true;
                s = "new " + ProjectGroup.StripProjectQualifier(r) + "()";
                break;
            case "vbAlignLeft":
                s = "AlignConstants.vbAlignLeft";
                break;
            case "vbAlignRight":
                s = "AlignConstants.vbAlignRight";
                break;
            case "vbAlignTop":
                s = "AlignConstants.vbAlignTop";
                break;
            case "vbAlignBottom":
                s = "AlignConstants.vbAlignBottom";
                break;
            case "RaiseEvent":
                complete = true;
                w = RegExNMatch(r, patToken);
                r = Trim(Mid(r, Len(w) + 1));
                var eventArgs = new List<string>();
                if (Left(r, 1) == "(" && Right(r, 1) == ")" && Trim(Mid(r, 2, Len(r) - 2)) != "")
                {
                    foreach (var a in StatementsConverter.SplitTopLevel(Mid(r, 2, Len(r) - 2)))
                    {
                        eventArgs.Add(CodeConverter.ConvertValue(a));
                    }
                }
                s = "event" + w + "?.Invoke(" + string.Join(", ", eventArgs) + ");";
                break;
        }

        if (IsInStr(s, ".Print ") && !LMatch(Trim(s), "Debug.Print "))
        {
            if (Right(s, 1) == ";")
            {
                s = Replace(s, ".Print ", ".PrintNNL ");
                s = Left(s, Len(s) - 1);
            }
            s = Replace(s, ";", ",");
        }

        var convertVb6Specific = s;
        return convertVb6Specific;
    }

    public static string ConvertVb6Syntax(string s)
    {
        var w = RegExNMatch(Trim(s), patToken);
        var r = SplitWord(Trim(s), 2, " ", true, true);
        switch (w)
        {
            case "Open":
                s = "VBOpenFile(" + Replace(SplitWord(r, 2, " As "), "#", "") + ", " + SplitWord(r, 1, " For ") + ")";
                break;
            case "Print":
                s = "VBWriteFile(" + Replace(SplitWord(r, 1, ","), "#", "") + ", " + Replace(SplitWord(r, 2, ", ", true, true), ";", ",") + ")";
                break;
            case "Input":
                s = "VBReadFile(" + Replace(SplitWord(r, 1, ","), "#", "") + ", " + Replace(SplitWord(r, 2, ", ", true, true), ";", ",") + ")";
                break;
            case "Line":
                s = "VBReadFileLine(" + Replace(SplitWord(r, 1, ","), "#", "") + ", " + Replace(SplitWord(r, 2, ", ", true, true), ";", ",") + ")";
                break;
            case "Close":
                s = "VBCloseFile(" + Replace(r, "#", "") + ")";
                break;
            case "New":
                s = "new " + ProjectGroup.StripProjectQualifier(r) + "()";
                break;
            case "RaiseEvent":
                w = RegExNMatch(r, patToken);
                r = Mid(r, Len(w) + 1);
                if (r == "")
                {
                    r = "()";
                }
                s = "event" + w + "?.Invoke" + r;
                break;
        }

        var convertVb6Syntax = s;
        return convertVb6Syntax;
    }
}