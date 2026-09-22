using System;
using static Microsoft.VisualBasic.Constants;
using static Microsoft.VisualBasic.Conversion;
using static Microsoft.VisualBasic.Strings;
using static Vb6ToCSharp.Modules.ModConfig;
using static Vb6ToCSharp.Modules.ModProjectFiles;
using static Vb6ToCSharp.Modules.ModRegEx;
using static Vb6ToCSharp.Modules.ModSubTracking;
using static Vb6ToCSharp.Modules.ModUtils;
using static Vb6ToCSharp.VbExtension;


namespace Vb6ToCSharp.Modules;

static class ModVb6ToCs
{
    // Option Explicit


    public static string ConvertDefaultDefault(string dType)
    {
        string convertDefaultDefault = "";
        switch (dType)
        {
            case "Integer":
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
        string convertDataType = IniMap(iniSectionDataTypes, s); // project-specific types from config win
        if (convertDataType != null)
        {
            return convertDataType;
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
            case "String()":
                convertDataType = "List<string>";
                break;
            case "Long":
                convertDataType = "int";
                break;
            case "Integer":
                convertDataType = "int";
                break;
            case "Double":
                convertDataType = "decimal";
                break;
            case "Variant":
                convertDataType = "object";
                break;
            case "Byte":
                convertDataType = "byte";
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
                if (IsInStr(VbpClasses(classNames: true), s))
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
        switch (w)
        {
            case "True":
                complete = true;
                s = "true";
                break;
            case "False":
                complete = true;
                s = "false";
                break;
            case "Me":
                complete = true;
                s = "this";
                break;
            case "Nothing":
                complete = true;
                s = "null";
                break;
            case "vbTrue":
                complete = true;
                s = "vbTriState.vbTrue";
                break;
            case "vbFalse":
                complete = true;
                s = "vbTriState.vbFalse";
                break;
            case "vbUseDefault":
                complete = true;
                s = "vbTriState.vbUseDefault";
                break;
            case "Date":
                complete = true;
                s = "DateTime.Today;";
                break;
            case "Now":
                complete = true;
                s = "DateTime.Now;";
                break;
            case "Kill":
                s = "File.Delete(" + r + ");";
                break;
            case "FreeFile":
                s = "FreeFile();";
                break;
            case "Open":
                s = "VBOpenFile(" + Replace(SplitWord(r, 2, " As "), "#", "") + ", " + SplitWord(r, 1, " For ") + ");";
                break;
            case "Print":
                s = "VBWriteFile(" + Replace(SplitWord(r, 1, ","), "#", "") + ", " + Replace(SplitWord(r, 2, ", ", true, true), ";", ",") + ");";
                break;
            case "Close":
                s = "VBCloseFile(" + Replace(r, "#", "") + ");";
                break;
            case "New":
                complete = true;
                s = "new " + r + "();";
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
                r = Mid(r, Len(w) + 1);
                if (r == "")
                {
                    r = "()";
                }
                s = "event" + w + "?.Invoke" + r + ";";
                break;
            case "ReDim":
                complete = true;
                bool redimPres = false;

                if (TLMatch(r, "Preserve "))
                {
                    r = Trim(TMid(r, 10));
                    redimPres = true;
                }

                var redimVar = RegExNMatch(r, patToken);
                var redimTyp = ConvertDataType(SubParam(redimVar).asType);
                r = Trim(Replace(r, redimVar, ""));
                if (TLeft(r, 1) == "(")
                {
                    r = Mid(Trim(r), 2);
                }
                var redimMax = Val(NextBy(r, ")")).ToString();
                var redimTmp = redimVar + "_" + Random() + "_tmp";
                var redimIter = "redim_iter_" + Random();
                s = "";
                s = s + "List<" + redimTyp + "> " + redimTmp + " = new List<" + redimTyp + ">();" + vbCrLf;

                s = s + "for (int " + redimIter + "=0;i<" + redimMax + ";" + redimIter + "++) {";
                if (redimPres)
                {
                    s = s + redimVar + ".Add(" + redimIter + "<" + redimVar + ".Count ? " + redimVar + "(" + redimIter + ") : " + ConvertDefaultDefault(SubParam(redimVar).asType) + ");";
                }
                else
                {
                    s = s + redimVar + ".Add(" + ConvertDefaultDefault(SubParam(redimVar).asType) + ");";
                }
                s = s + "}";
                break;
        }

        if (IsInStr(s, ".Print "))
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
                s = "new " + r + "()";
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