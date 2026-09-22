using System;
using Vb6ToCSharp.FormConversion;
using static Microsoft.VisualBasic.Constants;
using static Microsoft.VisualBasic.Conversion;
using static Microsoft.VisualBasic.Information;
using static Microsoft.VisualBasic.Interaction;
using static Microsoft.VisualBasic.Strings;
using static Vb6ToCSharp.Modules.ModConfig;
using static Vb6ToCSharp.Modules.ModConvertForm;
using static Vb6ToCSharp.Modules.ModConvertUtils;
using static Vb6ToCSharp.Modules.ModProjectFiles;
using static Vb6ToCSharp.Modules.ModRefScan;
using static Vb6ToCSharp.Modules.ModRegEx;
using static Vb6ToCSharp.Modules.ModSubTracking;
using static Vb6ToCSharp.Modules.ModSupportFiles;
using static Vb6ToCSharp.Modules.ModTextFiles;
using static Vb6ToCSharp.Modules.ModUsingEverything;
using static Vb6ToCSharp.Modules.ModUtils;
using static Vb6ToCSharp.Modules.ModVb6ToCs;
using static Vb6ToCSharp.VbExtension;


namespace Vb6ToCSharp.Modules;

public static class ModConvert
{
    // Option Explicit
    public const string withMark = "_WithVar_";
    private static int withLevel = 0;
    private static int maxWithLevel = 0;
    private static string withVars = "";
    private static string withTypes = "";
    private static string withAssign = "";
    private static string formName = "";
    private static string currentModule = "";
    private static string currSub = "";


    public static void ConvertProject(string vbpFile)
    {
        Prg(0, 1, "Preparing...");
        ScanRefs();
        CreateProjectFile(vbpFile);
        CreateProjectSupportFiles();
        ConvertFileList(FilePath(vbpFile), VbpModules(vbpFile) + vbCrLf + VbpClasses(vbpFile) + vbCrLf + VbpForms(vbpFile) + vbCrLf + VbpUserControls(vbpFile));
        Notify("Complete.");
    }

    public static bool ConvertFileList(string path, string list, string sep = vbCrLf)
    {
        var convertFileList = true;
        var n = 0;

        var v = StrCnt(list, sep) + 1;
        Prg(0, v, n + "/" + v + "...");
        foreach (var iterL in Split(list, sep))
        {
            dynamic l = iterL;
            n = n + 1;
            if (l == "")
            {
                goto NextItem;
            }

            if (l == "modFunctionList.bas")
            {
                goto NextItem;
            }

            convertFileList = ConvertFile(path + l) & convertFileList; // was never set: always False

            NextItem:;
            Prg(n);
            DoEvents();
        }
        Prg();
        return convertFileList;
    }

    public static bool ConvertFile(string someFile, bool uiOnly = false)
    {
        var convertFile = false;
        if (!IsInStr(someFile, "\\"))
        {
            someFile = VbpPath + someFile;
        }
        currentModule = "";
        switch (LCase(FileExt(someFile)))
        {
            case ".bas":
                convertFile = ConvertModule(someFile);
                break;
            case ".cls":
                convertFile = ConvertClass(someFile);
                break;
            case ".frm":
            case ".ctl":
                formName = FileBaseName(someFile);
                convertFile = ConvertForm(someFile, uiOnly);
                break;
            default:
                Notify("UNKNOWN VB TYPE: " + someFile);
                return convertFile;
        }
        formName = ""; // the result used to be forced to True, hiding failed conversions
        return convertFile;
    }

    public static bool ConvertForm(string frmFile, bool uiOnly = false)
    {
        var convertForm = false;

        if (!FileExists(frmFile))
        {
            Notify("File not found in ConvertForm: " + frmFile);
            return convertForm;

        }

        var s = ReadEntireFile(frmFile);
        var fName = ModuleName(s);
        currentModule = fName;
        var ui = Ui;
        var f = fName + (ui == UiTarget.WinForms ? ".cs" : ".xaml.cs");
        if (IsConverted(f, frmFile))
        {
            Console.WriteLine("Form Already Converted: " + f);
            return convertForm;

        }

        var model = FrmParser.Parse(s);
        model.Path = frmFile;
        var isUserControl = model.IsUserControl || model.Root?.Type == "VB.PropertyPage";
        var ctx = new FormContext(model, ui, ProjectInfo());
        var ns = AssemblyName() + (isUserControl ? ".UserControls" : ".Forms");
        FormContext.Current = ctx;
        try
        {
            EmitResult designer;
            if (ui == UiTarget.WinForms)
            {
                designer = WinFormsEmitter.Generate(ctx, ns);
                convertForm = WriteOut(fName + ".Designer.cs", designer.Designer, frmFile);
                if (designer.Resources.Count > 0) ResxWriter.Write(OutputFolder(frmFile) + fName + ".resx", designer.Resources);
            }
            else
            {
                designer = WpfEmitter.Generate(ctx, AssemblyName());
                convertForm = WriteOut(fName + ".xaml", designer.Designer, frmFile);
                ResxWriter.WriteFiles(OutputFolder(), designer.Resources);
            }
            if (uiOnly)
            {
                return convertForm;

            }

            var j = CodeSectionLoc(s);
            var preamble = Left(s, j - 1);
            var code = Mid(s, j);
            j = CodeSectionGlobalEndLoc(code);
            var globals = ConvertGlobals(Left(code, j));
            InitLocalFuncs(FormControls(fName, preamble) + ScanRefsFileToString(frmFile));
            var functions = ConvertCodeSegment(Mid(code, j));

            var x = "";
            x = x + UsingEverything(fName) + vbCrLf;
            x = x + vbCrLf;
            x = x + "namespace " + ns + vbCrLf;
            x = x + "{" + vbCrLf;
            x = x + FormClassHeader(ctx, designer) + vbCrLf;
            x = x + vbCrLf;
            x = x + globals + vbCrLf + vbCrLf + functions;
            x = x + vbCrLf + "}";
            x = x + vbCrLf + "}";

            x = DeWs(x);

            convertForm = WriteOut(f, x, frmFile); // was never set: always False
            return convertForm;
        }
        finally
        {
            FormContext.Current = null;
        }
    }

    private static VbpInfo projectInfo;

    /// <summary>Facts of the configured .vbp (re-read when the project changes).</summary>
    public static VbpInfo ProjectInfo()
    {
        if (projectInfo == null || !string.Equals(projectInfo.Path, VbpFile, StringComparison.OrdinalIgnoreCase)) projectInfo = VbpInfo.Load(VbpFile);
        return projectInfo;
    }

    /// <summary>Class declaration, VB6 default instance and constructor of a converted form / user control.</summary>
    public static string FormClassHeader(FormContext ctx, EmitResult designer)
    {
        var fName = ctx.ClassName;
        var file = ctx.File;
        var winForms = ctx.Ui == UiTarget.WinForms;
        var isUserControl = file.IsUserControl || file.Root?.Type == "VB.PropertyPage";
        var baseType = isUserControl ? (winForms ? "System.Windows.Forms.UserControl" : "System.Windows.Controls.UserControl") : winForms ? "System.Windows.Forms.Form" : "Window";
        var n = vbCrLf;
        var x = "public partial class " + fName + " : " + baseType + " {" + n;
        if (!isUserControl)
        {
            var alive = winForms ? "_instance == null || _instance.IsDisposed" : "_instance == null";
            x = x + "  private static " + fName + " _instance;" + n;
            x = x + "  /// <summary>VB6 default instance: recreated after the form is unloaded.</summary>" + n;
            x = x + "  public static " + fName + " instance { set { _instance = null; } get { if (" + alive + ") _instance = new " + fName + "(); return _instance; } }" + n;
            x = x + "  public static void LoadForm() { if (" + alive + ") { var f = instance; " + (winForms ? "f.CreateControl(); " : "") + "} }" + n;
            x = x + "  public static void UnloadForm() { if (_instance != null) _instance.Close(); _instance = null; }" + n;
            if (!winForms)
            {
                x = x + "  public static void Load() { LoadForm(); }" + n;
                x = x + "  public static void Unload() { UnloadForm(); }" + n;
            }
        }
        x = x + "  public " + fName + "() {" + n;
        x = x + "    InitializeComponent();" + n;
        if (winForms) x = x + "    InitializeComponentExtras();" + n;
        if (designer.ConstructorCode != "") x = x + "    " + Replace(designer.ConstructorCode, n, n + "    ") + n;
        if (!isUserControl) x = x + "    " + (winForms ? "FormClosed" : "Closed") + " += (s, e) => { if (_instance == this) _instance = null; };" + n;
        if (file.IsMdiChild)
        {
            var mdi = ctx.Project?.MdiFormName ?? "";
            if (winForms && mdi != "") x = x + "    MdiParent = " + mdi + ".instance;" + n;
            else x = x + "    // TODO: VB6 MDI child" + (mdi != "" ? " of " + mdi : "") + (winForms ? "" : " (WPF has no MDI)") + n;
        }
        var init = ctx.RootPrefix + "_Initialize";
        if (ctx.Handlers.Contains(init)) x = x + "    " + ctx.ExactHandler(init) + "();" + n;
        x = x + "  }" + n;
        if (isUserControl)
        {
            var pb = "Vb6ToCSharp.UpgradeHelpers.PropertyBag";
            if (!ctx.Handlers.Contains(ctx.RootPrefix + "_InitProperties")) x = x + "  public void InitProperties() { }" + n;
            if (!ctx.Handlers.Contains(ctx.RootPrefix + "_ReadProperties")) x = x + "  public void ReadProperties(" + pb + " bag) { }" + n;
            if (!ctx.Handlers.Contains(ctx.RootPrefix + "_WriteProperties")) x = x + "  public void WriteProperties(" + pb + " bag) { }" + n;
        }
        if (designer.CodeMembers != "") x = x + designer.CodeMembers;
        return x;
    }

    public static bool ConvertModule(string basFile)
    {
        var convertModule = false;

        if (!FileExists(basFile))
        {
            Notify("File not found in ConvertModule: " + basFile);
            return convertModule;

        }
        var s = ReadEntireFile(basFile);
        var fName = ModuleName(s);
        currentModule = fName;
        var f = fName + ".cs";
        if (IsConverted(f, basFile))
        {
            Console.WriteLine("Module Already Converted: " + f);
            return convertModule;

        }

        fName = ModuleName(s);
        var code = Mid(s, CodeSectionLoc(s));

        var j = CodeSectionGlobalEndLoc(code);
        var globals = ConvertGlobals(Left(code, j - 1), true);
        var functions = ConvertCodeSegment(Mid(code, j), true);

        var x = "";
        x = x + UsingEverything(fName) + vbCrLf;
        x = x + vbCrLf;
        x = x + "public static class " + fName + " {" + vbCrLf;
        x = x + NlTrim(globals + vbCrLf + vbCrLf + functions);
        x = x + vbCrLf + "}";

        x = DeWs(x);

        convertModule = WriteOut(f, x, basFile); // was never set: always False
        return convertModule;
    }

    public static bool ConvertClass(string clsFile)
    {
        var convertClass = false;

        if (!FileExists(clsFile))
        {
            Notify("File not found in ConvertModule: " + clsFile);
            return convertClass;

        }
        var s = ReadEntireFile(clsFile);
        var fName = ModuleName(s);
        currentModule = fName;
        var f = fName + ".cs";
        if (IsConverted(f, clsFile))
        {
            Console.WriteLine("Class Already Converted: " + f);
            return convertClass;

        }

        var code = Mid(s, CodeSectionLoc(s));

        var j = CodeSectionGlobalEndLoc(code);
        var globals = ConvertGlobals(Left(code, j - 1));
        var functions = ConvertCodeSegment(Mid(code, j));

        var x = "";
        x = x + UsingEverything(fName) + vbCrLf;
        x = x + vbCrLf;
        x = x + "public class " + fName + " {" + vbCrLf;
        x = x + globals + vbCrLf + vbCrLf + functions;
        x = x + vbCrLf + "}";

        x = DeWs(x);

        f = fName + ".cs";
        convertClass = WriteOut(f, x, clsFile); // was never set: always False
        return convertClass;
    }

    public static string GetMultiLineSpace(string prv, string nxt)
    {
        var getMultiLineSpace = " ";
        var pC = Right(prv, 1);
        var nC = Left(nxt, 1);
        if (nC == "(")
        {
            getMultiLineSpace = "";
        }
        return getMultiLineSpace;
    }

    public static string SanitizeCode(string str)
    {
        const string namedParamSrc = ":=";
        const string namedParamTok = "###NAMED-PARAMETER###";
        var sp = new string[0];

        var f = "";


        var r = "";
        var n = vbCrLf;
        sp = Split(str, vbCrLf);
        var building = "";


        foreach (var iterL in sp)
        {
            var l = iterL;
            //If IsInStr(L, "POEDIFolder") Then Stop
            //If IsInStr(L, "Set objSourceArNo = New_CDbTypeAhead") Then Stop
            if (Right(l, 1) == "_")
            {
                var c = Trim(Left(l, Len(l) - 1));
                building = building + GetMultiLineSpace(building, c) + c;
                goto NextLine;
            }
            if (building != "")
            {
                l = building + GetMultiLineSpace(building, Trim(l)) + Trim(l);
                building = "";
            }

            //    If IsInStr(L, "'") Then Stop
            l = DeComment(l);
            l = DeString(l);

            //If IsInStr(L, "CustRec <> 0") Then Stop
            var finishSplitIf = false;
            if (TLeft(l, 3) == "If " && Right(RTrim(l), 5) != " Then")
            {
                finishSplitIf = true;
                f = NextBy(l, " Then ") + " Then";
                r = r + n + f;
                l = Mid(l, Len(f) + 2);
                if (NextBy(l, " Else ", 2) != "")
                {
                    r = r + ModConvert.SanitizeCode(NextBy(l, " Else ", 1));
                    r = r + n + "Else";
                    l = NextBy(l, "Else ", 2);
                }
            }

            if (NextBy(l, ":") != l)
            {
                if (RegExTest(Trim(l), "^[a-zA-Z_][a-zA-Z_0-9]*:$"))
                { // Goto Label
                    r = r + n + ReComment(l);
                }
                else
                {
                    do
                    {
                        l = Replace(l, namedParamSrc, namedParamTok);
                        f = NextBy(l, ":");
                        f = Replace(f, namedParamTok, namedParamSrc);
                        r = r + n + ReComment(f, true);
                        l = Replace(l, namedParamTok, namedParamSrc);
                        if (f == l)
                        {
                            break;
                        }
                        l = Trim(Mid(l, Len(f) + 2));
                        r = r + ModConvert.SanitizeCode(l);

                    } while (false); // VB "Loop While" (was mistranslated as Loop Until)
                }
            }
            else
            {
                r = r + n + ReComment(l, true);
            }

            if (finishSplitIf)
            {
                r = r + n + "End If";
            }
            NextLine:;
        }

        var sanitizeCode = r;
        return sanitizeCode;
    }

    public static string ConvertCodeSegment(string s, bool asModule = false)
    {
        var f = "";
        var T = 0;
        var e = 0;
        var k = "";

        var pre = "";

        var r = "";


        ClearProperties();

        InitDeString();
        //WriteFile "C:\Users\benja\Desktop\code.txt", S, True
        s = SanitizeCode(s);
        //WriteFile "C:\Users\benja\Desktop\sani.txt", S, True
        do
        {
            var p = "(Public |Private |)(Friend |)(Function |Sub |Property Get |Property Let |Property Set )" + patToken + "[ ]*\\(";
            var n = -1;
            do
            {
                n = n + 1;
                f = RegExNMatch(s, p, n);
                T = RegExNPos(s, p, n);
            } while (!IsInCode(s, T) && f != ""); // VB "Loop While" (was mistranslated as Loop Until)
            if (f == "")
            {
                break;
            }

            if (IsInStr(f, " Function "))
            {
                k = "End Function";
            }
            else if (IsInStr(f, " Sub "))
            {
                k = "End Sub";
            }
            else if (IsInStr(f, " Property "))
            {
                k = "End Property";
            }
            n = -1;
            do
            {
                n = n + 1;
                e = RegExNPos(Mid(s, T), k, n) + Len(k) + T;
            } while (!IsInCode(s, e) && e != 0); // VB "Loop While" (was mistranslated as Loop Until)

            if (T > 1)
            {
                pre = NlTrim(Left(s, T - 1));
            }
            else
            {
                pre = "";
            }
            while (!(Mid(s, e, 1) == vbCr || Mid(s, e, 1) == vbLf || Mid(s, e, 1) == ""))
            {
                e = e + 1;
            }
            var body = NlTrim(Mid(s, T, e - T));

            s = NlTrim(Mid(s, e + 1));

            r = r + CommentBlock(pre) + ConvertSub(body, asModule) + vbCrLf;
        } while (true); // VB "Loop While" (was mistranslated as Loop Until)

        r = ReadOutProperties(asModule) + vbCrLf2 + r;

        r = ReString(r, true);

        var convertCodeSegment = r;
        return convertCodeSegment;
    }

    public static string CommentBlock(string str)
    {
        var commentBlock = "";

        if (NlTrim(str) == "")
        {
            return commentBlock;

        }
        var s = "";
        s = s + "/*" + vbCrLf;
        s = s + Replace(str, "*/", "* /") + vbCrLf;
        s = s + "*/" + vbCrLf;
        commentBlock = s;
        return commentBlock;
    }

    public static string ConvertDeclare(string s, int ind, bool isGlobal = false, bool asModule = false)
    {
        var sp = new string[0];

        var asPrivate = false;

        var pType = "";

        var isArr = false;
        var aMax = 0;
        var aMin = 0;

        var res = "";

        var ss = s;

        if (TLeft(s, 7) == "Public ")
        {
            s = TMid(s, 8);
        }
        if (TLeft(s, 4) == "Dim ")
        {
            s = Mid(Trim(s), 5);
            asPrivate = true;
        }
        if (TLeft(s, 8) == "Private ")
        {
            s = TMid(s, 9);
            asPrivate = true;
        }

        //  If IsInStr(S, "aMin") Then Stop
        sp = Split(s, ",");
        foreach (var iterL in sp)
        {
            var l = iterL;
            l = Trim(l);
            if (LMatch(l, "WithEvents "))
            {
                l = Trim(TMid(l, 12));
                res = res + "// TODO: WithEvents not supported on " + RegExNMatch(l, patToken) + vbCrLf;
            }
            var pName = RegExNMatch(l, patToken);
            l = Trim(TMid(l, Len(pName) + 1));
            if (isGlobal)
            {
                res = res + IIf(asPrivate, "private ", "public ");
            }
            if (asModule)
            {
                res = res + "static ";
            }
            if (TLeft(l, 1) == "(")
            {
                isArr = true;
                var arraySpec = NextBy(Mid(l, 2), ")");
                if (arraySpec == "")
                {
                    aMin = -1;
                    aMax = -1;
                    l = Trim(TMid(l, 3));
                }
                else
                {
                    l = Trim(TMid(l, Len(arraySpec) + 3));
                    aMin = 0;
                    aMax = ValI(SplitWord(arraySpec));
                    arraySpec = Trim(TMid(arraySpec, Len(aMax) + 1));
                    if (TLeft(arraySpec, 3) == "To ")
                    {
                        aMin = aMax;
                        aMax = ValI(TMid(arraySpec, 4));
                    }
                }
            }

            var asNew = false;
            if (SplitWord(l, 1) == "As")
            {
                pType = SplitWord(l, 2);
                if (pType == "New")
                {
                    pType = SplitWord(l, 3);
                    asNew = true;
                }
            }
            else
            {
                pType = "Variant";
            }

            if (!isArr)
            {
                res = res + SSpace(ind) + ConvertDataType(pType) + " " + pName;
                res = res + " = ";
                if (asNew)
                {
                    res = res + "new ";
                    res = res + ConvertDataType(pType);
                    res = res + "()";
                }
                else
                {
                    res = res + ConvertDefaultDefault(pType);
                }
                res = res + ";" + vbCrLf;
            }
            else
            {
                var aTodo = IIf(aMin == 0, "", " // TODO - Specified Minimum Array Boundary Not Supported: " + ss);
                if (!IsNumeric(aMax))
                {
                    res = res + SSpace(ind) + "List<" + ConvertDataType(pType) + "> " + pName + " = new List<" + ConvertDataType(pType) + "> (new " + ConvertDataType(pType) + "[(" + aMax + " + 1)]);  // TODO: Confirm Array Size By Token" + aTodo + vbCrLf;
                }
                else if (Val(aMax) == -1)
                {
                    res = res + SSpace(ind) + "List<" + ConvertDataType(pType) + "> " + pName + " = new List<" + ConvertDataType(pType) + "> {};" + aTodo + vbCrLf;
                }
                else
                {
                    res = res + SSpace(ind) + "List<" + ConvertDataType(pType) + "> " + pName + " = new List<" + ConvertDataType(pType) + "> (new " + ConvertDataType(pType) + "[" + (Val(aMax) + 1) + "]);" + aTodo + vbCrLf;
                }
            }

            SubParamDecl(pName, pType, IIf(isArr, "" + aMax, ""), false, false);
        }

        var convertDeclare = res;
        return convertDeclare;
    }

    public static string ConvertApiDef(string s)
    {
        //Private Declare Function CreateFile Lib "kernel32" Alias "CreateFileA" (ByVal lpFileName As String, ByVal dwDesiredAccess As Long, ByVal dwShareMode As Long, ByVal lpSecurityAttributes As Long, ByVal dwCreationDisposition As Long, ByVal dwFlagsAndAttributes As Long, ByVal hTemplateFile As Long) As Long
        //[DllImport("User32.dll")]
        //public static extern int MessageBox(int h, string m, string c, int type);
        var isPrivate = false;
        var isSub = false;

        var aLib = "";

        var aAlias = "";

        var aReturn = "";

        var has = false;

        if (TLeft(s, 8) == "Private ")
        {
            s = TMid(s, 9);
            isPrivate = true;
        }
        if (TLeft(s, 7) == "Public ")
        {
            s = TMid(s, 8);
        }
        if (TLeft(s, 8) == "Declare ")
        {
            s = TMid(s, 9);
        }
        if (TLeft(s, 4) == "Sub ")
        {
            s = TMid(s, 5);
            isSub = true;
        }
        if (TLeft(s, 9) == "Function ")
        {
            s = TMid(s, 10);
        }
        var aName = RegExNMatch(s, patToken);
        s = Trim(TMid(s, Len(aName) + 1));
        if (TLeft(s, 4) == "Lib ")
        {
            s = Trim(TMid(s, 5));
            aLib = SplitWord(s, 1);
            s = Trim(TMid(s, Len(aLib) + 1));
            aLib = ReString(aLib);
            if (Left(aLib, 1) == "\"")
            {
                aLib = Mid(aLib, 2);
            }
            if (Right(aLib, 1) == "\"")
            {
                aLib = Left(aLib, Len(aLib) - 1);
            }
            if (LCase(Right(aLib, 4)) != ".dll")
            {
                aLib = aLib + ".dll";
            }
            aLib = LCase(aLib);
        }
        if (TLeft(s, 6) == "Alias ")
        {
            s = Trim(TMid(s, 7));
            aAlias = SplitWord(s, 1);
            s = Trim(TMid(s, Len(aAlias) + 1));
            aAlias = ReString(aAlias);
            if (Left(aAlias, 1) == "\"")
            {
                aAlias = Mid(aAlias, 2);
            }
            if (Right(aAlias, 1) == "\"")
            {
                aAlias = Left(aAlias, Len(aAlias) - 1);
            }
        }
        if (TLeft(s, 1) == "(")
        {
            s = TMid(s, 2);
        }
        var aArgs = NextBy(s, ")");
        s = Trim(TMid(s, Len(aArgs) + 2));
        if (TLeft(s, 3) == "As ")
        {
            s = Trim(TMid(s, 4));
            aReturn = SplitWord(s, 1);
            s = Trim(TMid(s, Len(aReturn) + 1));
        }
        else
        {
            aReturn = "Variant";
        }

        s = "";
        s = s + "[DllImport(\"" + aLib + "\"" + IIf(aAlias == "", "", ", EntryPoint = \"" + aAlias + "\"") + ")] ";
        s = s + IIf(isPrivate, "private ", "public ");
        s = s + "static extern ";
        s = s + IIf(isSub, "void ", ConvertDataType(aReturn)) + " ";
        s = s + aName;
        s = s + "(";
        do
        {
            if (aArgs == "")
            {
                break;
            }
            var tArg = Trim(NextBy(aArgs, ","));
            aArgs = TMid(aArgs, Len(tArg) + 2);
            s = s + IIf(has, ", ", "") + ConvertParameter(tArg, true);
            has = true;
        } while (true); // VB "Loop While" (was mistranslated as Loop Until)
        s = s + ");";


        var convertApiDef = s;
        return convertApiDef;
    }

    public static string ConvertConstant(string s, bool isGlobal = true)
    {
        var convertConstant = "";
        var cType = "";
        var cValue = "";
        var isPrivate = false;

        if (TLeft(s, 7) == "Public ")
        {
            s = Mid(Trim(s), 8);
        }
        if (TLeft(s, 7) == "Global ")
        {
            s = Mid(Trim(s), 8);
        }
        if (TLeft(s, 8) == "Private ")
        {
            s = Mid(Trim(s), 9);
            isPrivate = true;
        }
        if (TLeft(s, 6) == "Const ")
        {
            s = Mid(Trim(s), 7);
        }
        var cName = SplitWord(s, 1);
        s = Trim(Mid(Trim(s), Len(cName) + 1));
        if (TLeft(s, 3) == "As ")
        {
            s = Trim(Mid(Trim(s), 3));
            cType = SplitWord(s, 1);
            s = Trim(TMid(s, Len(cType) + 1));
        }
        else
        {
            cType = "Variant";
        }

        if (Left(s, 1) == "=")
        {
            s = Trim(Mid(s, 2));
            cValue = ConvertValue(s);
        }
        else
        {
            cValue = ConvertDefaultDefault(cType);
        }

        var dataType = ConvertDataType(cType);
        if (dataType == "dynamic")
        { // c# can't handle constants of type 'dynamic' when type can be inferred.
            if (LMatch(cValue, deStringTokenBase))
            {
                dataType = "string";
            }
            else if (IsNumeric(cValue))
            {
                if (IsInStr(cValue, "."))
                {
                    dataType = "decimal";
                }
                else
                {
                    dataType = "int";
                }
            }
        }

        if (cType == "Date")
        {
            convertConstant = IIf(isGlobal, IIf(isPrivate, "private ", "public "), "") + "static readonly " + dataType + " " + cName + " = " + cValue + ";";
        }
        else
        {
            convertConstant = IIf(isGlobal, IIf(isPrivate, "private ", "public "), "") + "const " + dataType + " " + cName + " = " + cValue + ";";
        }
        return convertConstant;
    }

    public static string ConvertEvent(string s)
    {
        var tArgs = "";

        if (TLeft(s, 7) == "Public ")
        {
            s = Mid(Trim(s), 8);
        }
        if (TLeft(s, 8) == "Private ")
        {
            s = Mid(Trim(s), 9);
        }
        if (TLeft(s, 6) == "Event ")
        {
            s = Mid(Trim(s), 7);
        }
        var cName = RegExNMatch(s, patToken);
        var cArgs = Trim(Mid(Trim(s), Len(cName) + 1));
        if (Left(cArgs, 1) == "(")
        {
            cArgs = Mid(cArgs, 2);
        }
        if (Right(cArgs, 1) == ")")
        {
            cArgs = Left(cArgs, Len(cArgs) - 1);
        }

        var n = 0;
        do
        {
            n = n + 1;
            var a = NextBy(cArgs, ",", n);
            if (a == "")
            {
                break;
            }
            tArgs = tArgs + IIf(n == 1, "", ", ");
            tArgs = tArgs + ConvertParameter(a, true);
        } while (true); // VB "Loop While" (was mistranslated as Loop Until)

        var o = vbCrLf;
        var m = "";
        var r = "";
        r = r + m + "public delegate void " + cName + "Handler(" + tArgs + ");";
        r = r + o + "public event " + cName + "Handler event" + cName + ";";

        var convertEvent = r;
        return convertEvent;
    }

    public static string ConvertEnum(string s)
    {
        var has = false;

        if (TLeft(s, 7) == "Public ")
        {
            s = TMid(s, 8);
        }
        if (TLeft(s, 8) == "Private ")
        {
            s = TMid(s, 9);
        }
        if (TLeft(s, 5) == "Enum ")
        {
            s = TMid(s, 6);
        }
        var eName = RegExNMatch(s, patToken, 0);
        s = NlTrim(TMid(s, Len(eName) + 1));

        var res = "public enum " + eName + " {";

        while (TLeft(s, 8) != "End Enum" && s != "")
        {
            eName = RegExNMatch(s, patToken, 0);
            res = res + IIf(has, ",", "") + vbCrLf + SSpace(spIndent) + eName;
            has = true;

            s = NlTrim(TMid(s, Len(eName) + 1));
            if (TLeft(s, 1) == "=")
            {
                s = NlTrim(Mid(s, 3));
                if (Left(s, 1) == "&")
                {
                    eName = ConvertElement(RegExNMatch(s, "&H[0-9A-F]+"));
                }
                else
                {
                    eName = RegExNMatch(s, "[0-9]*", 0);
                }
                res = res + " = " + eName;
                s = NlTrim(TMid(s, Len(eName) + 1));
            }
        }
        res = res + vbCrLf + "}";

        var convertEnum = res;
        return convertEnum;
    }

    public static string ConvertType(string s)
    {
        var isPrivate = false;
        var eType = "";

        var n = "";

        if (TLeft(s, 7) == "Public ")
        {
            s = TMid(s, 8);
        }
        if (TLeft(s, 8) == "Private ")
        {
            s = TMid(s, 9);
            isPrivate = true;
        }
        if (TLeft(s, 5) == "Type ")
        {
            s = TMid(s, 6);
        }
        var eName = RegExNMatch(s, patToken, 0);
        s = NlTrim(TMid(s, Len(eName) + 1));

        //If IsInStr(eName, "OSVERSIONINFO") Then Stop
        var res = IIf(isPrivate, "private ", "public ") + "class " + eName + " {";

        while (TLeft(s, 8) != "End Type" && s != "")
        {
            eName = RegExNMatch(s, patToken, 0);
            s = NlTrim(TMid(s, Len(eName) + 1));
            var eArr = "";
            if (LMatch(s, "("))
            {
                n = NextBy(Mid(s, 2), ")");
                s = NlTrim(Mid(s, Len(n) + 3));
                n = ConvertValue(n);
                eArr = "[" + n + "]";
            }

            if (TLeft(s, 3) == "As ")
            {
                s = NlTrim(Mid(s, 4));
                eType = RegExNMatch(s, patToken, 0);
                s = NlTrim(TMid(s, Len(eType) + 1));
            }
            else
            {
                eType = "Variant";
            }
            res = res + vbCrLf + " public " + ConvertDataType(eType) + IIf(eArr == "", "", "[]") + " " + eName;
            if (eArr == "")
            {
                res = res + " = " + ConvertDefaultDefault(eType);
            }
            else
            {
                res = res + " = new " + ConvertDataType(eType) + eArr;
            }
            res = res + ";";
            if (TLMatch(s, "* "))
            {
                s = Mid(LTrim(s), 3);
                n = RegExNMatch(s, "[0-9]+", 0);
                s = NlTrim(Mid(LTrim(s), Len(n) + 1));
                res = res + " //TODO: Fixed Length Strings Not Supported: * " + n;
            }

        }
        res = res + vbCrLf + "}";

        var convertType = res;
        return convertType;
    }

    public static string ConvertParameter(string s, bool neverUnused = false)
    {
        var isOptional = false;

        var asOut = false;

        var pType = "";
        var pDef = "";


        s = Trim(s);
        if (TLeft(s, 9) == "Optional ")
        {
            isOptional = true;
            s = Mid(s, 10);
        }
        var isByRef = true;
        if (TLeft(s, 6) == "ByVal ")
        {
            isByRef = false;
            s = Mid(s, 7);
        }
        if (TLeft(s, 6) == "ByRef ")
        {
            isByRef = true;
            s = Mid(s, 7);
        }
        var pName = SplitWord(s, 1);
        if (isByRef && SubParam(pName).assignedBeforeUsed)
        {
            asOut = true;
        }
        s = Trim(Mid(s, Len(pName) + 1));
        if (TLeft(s, 2) == "As")
        {
            s = TMid(s, 4);
            pType = SplitWord(s, 1, "=");
            s = Trim(Mid(s, Len(pType) + 1));
        }
        else
        {
            pType = "Variant";
        }
        if (Left(s, 1) == "=")
        {
            pDef = ConvertValue(Trim(Mid(Trim(s), 2)));
            s = "";
        }
        else
        {
            pDef = ConvertDefaultDefault(pType);
        }

        var res = "";
        if (isByRef)
        {
            res = res + IIf(asOut, "out ", "ref ");
        }
        res = res + ConvertDataType(pType) + " ";
        if (IsInStr(pName, "()"))
        {
            res = res + "[] ";
            pName = Replace(pName, "()", "");
        }
        var name = pName;
        if (!neverUnused)
        {
            if (!SubParam(pName).used && !(SubParam(pName).param && SubParam(pName).assigned))
            {
                name = name + "_UNUSED";
            }
        }
        res = res + name;
        if (isOptional && !isByRef)
        {
            res = res + "= " + pDef;
        }

        SubParamDecl(pName, pType, "False", true, false); // VB6 passed False to the String asArray parameter
        var convertParameter = Trim(res);
        return convertParameter;
    }

    public static string ConvertPrototype(string ss, ref string returnVariable, bool asModule, ref string asName)
    {
        const string retToken = "#RET#";

        var retType = "";


        var s = ss;

        var res = "";
        returnVariable = "";
        var isSub = false;
        if (LMatch(s, "Public "))
        {
            res = res + "public ";
            s = Mid(s, 8);
        }
        if (LMatch(s, "Private "))
        {
            res = res + "private ";
            s = Mid(s, 9);
        }
        if (LMatch(s, "Friend "))
        {
            s = Mid(s, 8);
        }
        if (asModule)
        {
            res = res + "static ";
        }
        if (LMatch(s, "Sub "))
        {
            res = res + "void ";
            s = Mid(s, 5);
            isSub = true;
        }
        if (LMatch(s, "Function "))
        {
            res = res + retToken + " ";
            s = Mid(s, 10);
        }

        var fName = Trim(SplitWord(Trim(s), 1, "("));
        asName = fName;

        s = Trim(TMid(s, Len(fName) + 2));
        if (Left(s, 1) == "(")
        {
            s = Trim(TMid(s, 2));
        }
        var fArgs = Trim(NextBy(s, ")"));
        s = Mid(s, Len(fArgs) + 2);
        while (Right(fArgs, 1) == "(")
        {
            fArgs = fArgs + ") ";

            var tMore = Trim(NextBy(s, ")"));
            fArgs = fArgs + tMore;
            s = Mid(s, Len(tMore) + 2);
        }
        if (Left(s, 1) == ")")
        {
            s = Trim(TMid(s, 2));
        }

        if (!isSub)
        {
            if (TLeft(s, 2) == "As")
            {
                retType = Trim(Mid(Trim(s), 3));
            }
            else
            {
                retType = "Variant";
            }
            if (Right(retType, 1) == ")" && Right(retType, 2) != "()")
            {
                retType = Left(retType, Len(retType) - 1);
            }
            res = Replace(res, retToken, ConvertDataType(retType));
        }

        res = res + fName;
        res = res + "(";
        var hArgs = false;
        do
        {
            if (Trim(fArgs) == "")
            {
                break;
            }
            var tArg = NextBy(fArgs, ",");
            fArgs = LTrim(Mid(fArgs, Len(tArg) + 2));

            res = res + IIf(hArgs, ", ", "");
            if (LMatch(tArg, "ParamArray"))
            {
                res = res + "params ";
                tArg = "ByVal " + Trim(Mid(tArg, 12));
            }
            res = res + ConvertParameter(tArg);
            hArgs = true;
        } while (!(Len(fArgs) == 0));

        res = res + ") {";
        if (retType != "")
        {
            returnVariable = fName;
            res = res + vbCrLf + SSpace(spIndent) + ConvertDataType(retType) + " " + returnVariable + " = " + ConvertDefaultDefault(retType) + ";";
            SubParamDecl(returnVariable, retType, "False", false, true); // VB6 passed False to the String asArray parameter
        }

        res = EventAdapters.Build(asName, res) + res; // .NET-signature adapter forwarding to the VB6-signature handler
        var convertPrototype = Trim(res);
        return convertPrototype;
    }

    public static string ConvertCondition(string s)
    {
        var convertCondition = "(" + s + ")";
        return convertCondition;
    }

    public static string ConvertElement(string s)
    {
        var convertElement = "";
        //Debug.Print "ConvertElement: " & S
        //If IsInStr(S, "frmSetup") Then Stop
        //If IsInStr(S, "chkShowBalance.Value") Then Stop
        //If IsInStr(S, "optTelephone") Then Stop

        var complete = false;

        s = Trim(s);
        if (s == "")
        {
            return convertElement;

        }

        //If IsInStr(S, "Debug.Print") Then Stop
        if (Left(Trim(s), 2) == "&H")
        {
            convertElement = "0x" + Mid(Trim(s), 3);
            return convertElement;

        }

        if (IsNumeric(Trim(s)))
        {
            convertElement = Val(s).ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (IsInStr(s, "."))
            {
                convertElement = convertElement + "m";
            }
            return convertElement;

        }

        var vMax = 0;

        while (RegExTest(s, "#[0-9]+/[0-9]+/[0-9]+#"))
        {
            var dStr = RegExNMatch(s, "#[0-9]+/[0-9]+/[0-9]+#", 0);
            s = Replace(s, dStr, "DateValue(\"" + Mid(dStr, 2, Len(dStr) - 2) + "\")");
            vMax = vMax + 1;
            if (vMax > 10)
            {
                break;
            }
        }

        //If IsInStr(S, "RS!") Then Stop
        //If IsInStr(S, ".SetValueDisplay Row") Then Stop
        //If IsInStr(S, "cmdSaleTotals.Move") Then Stop
        //If IsInStr(S, "2830") Then Stop
        //If IsInStr(S, "True") Then Stop
        //If IsInStr(S, ":=") Then Stop
        //If IsInStr(S, "GetRecordNotFound") Then Stop
        //If IsInStr(S, "Nonretro_14day") Then Stop
        //If IsInStr(S, "Git") Then Stop
        //If IsInStr(S, "GitFolder") Then Stop
        //If IsInStr(S, "Array") Then Stop

        s = RegExReplace(s, patNotToken + patToken + "!" + patToken + patNotToken, "$1$2(\"$3\")$4"); // RS!Field -> RS("Field")
        s = RegExReplace(s, "^" + patToken + "!" + patToken + patNotToken, "$1(\"$2\")$3"); // RS!Field -> RS("Field")

        s = RegExReplace(s, "([^a-zA-Z0-9_.])NullDate([^a-zA-Z0-9_.])", "$1NullDate()$2");

        s = ConvertVb6Specific(s, out complete);
        if (complete)
        {
            convertElement = s;
            return convertElement;

        }

        if (RegExTest(Trim(s), "^" + patToken + "$"))
        {
            //    If S = "SqFt" Then Stop
            if (IsFuncRef(Trim(s)) && s != currSub)
            {
                convertElement = Trim(s) + "()";
                return convertElement;

            }
            else if (IsPrivateFuncRef(currentModule, Trim(s)) && s != currSub)
            {
                convertElement = Trim(s) + "()";
                return convertElement;

            }
            else if (IsEnumRef(Trim(s)))
            {
                convertElement = EnumRefRepl(Trim(s));
                return convertElement;

            }
        }

        if (RegExTest(Trim(s), "^" + patTokenDot + "$") && StrCnt(s, ".") == 1)
        {
            //    If S = "SqFt" Then Stop

            var first = SplitWord(s, 1, ".");
            var second = SplitWord(s, 2, ".");
            if (IsModuleRef(first) && IsFuncRef(second))
            {
                if (IsFuncRef(Trim(second)) && s != currSub)
                {
                    convertElement = Trim(s) + "()";
                    return convertElement;

                }
                else if (IsEnumRef(Trim(s)))
                {
                    convertElement = EnumRefRepl(Trim(s));
                    return convertElement;

                }
            }
        }

        //If IsInStr(S, "Not optTagIncoming") Then Stop
        if (IsControlRef(Trim(s), formName))
        {
            //If IsInStr(S, "optTagIncoming") Then Stop
            s = FormControlRepl(s, formName);
        }
        else if (LMatch(Trim(s), "Not ") && IsControlRef(Mid(Trim(s), 5), formName))
        {
            s = "!(" + FormControlRepl(Mid(Trim(s), 5), formName) + ")";
        }

        if (IsFormRef(Trim(s)))
        {
            convertElement = FormRefRepl(Trim(s));
            return convertElement;

        }


        var firstToken = RegExNMatch(s, patTokenDot, 0);
        var firstWord = SplitWord(s, 1);
        if (firstWord == "Not")
        {
            s = "!" + ConvertValue(Mid(s, 5));
            firstWord = SplitWord(Mid(s, 2));
        }
        if (s == firstWord)
        {
            convertElement = s;
            goto ManageFunctions;
        }
        if (s == firstToken)
        {
            convertElement = s + "()";
            goto ManageFunctions;
        }

        if (firstToken == firstWord && !IsOperator(SplitWord(s, 2)))
        { // Sub without parenthesis
            convertElement = firstWord + "(" + SplitWord(s, 2, " ", true, true) + ")";
        }
        else
        {
            convertElement = s;
        }

        ManageFunctions:;
        //If IsInStr(ConvertElement, "New_CDbTypeAhead") Then Stop
        if (RegExTest(convertElement, "(\\!)?[a-zA-Z0-9_.]+[ ]*\\(.*\\)$"))
        {
            if ((Left(convertElement, 1) == "!"))
            {
                convertElement = "!" + ConvertFunctionCall(Mid(convertElement, 2));
            }
            else
            {
                convertElement = ConvertFunctionCall(convertElement);
            }
        }

        if (IsInStr(convertElement, ":="))
        {
            var ts = SplitWord(convertElement, 1, ":=");
            ts = ts + ": ";
            ts = ts + ModConvert.ConvertElement(SplitWord(convertElement, 2, ":=", true, true));
            convertElement = ts;
        }

        convertElement = Replace(convertElement, " & ", " + ");
        convertElement = Replace(convertElement, " = ", " == ");
        convertElement = Replace(convertElement, "<>", "!=");
        convertElement = Replace(convertElement, " Not ", " !");
        convertElement = Replace(convertElement, "(Not ", "(!");
        convertElement = Replace(convertElement, " Or ", " || ");
        convertElement = Replace(convertElement, " And ", " && ");
        convertElement = Replace(convertElement, " Mod ", " % ");
        convertElement = Replace(convertElement, "Err.", "Err().");
        convertElement = Replace(convertElement, "Debug.Print", "Console.WriteLine");

        convertElement = Replace(convertElement, "NullDate", "NullDate");
        while (IsInStr(convertElement, ", ,"))
        {
            convertElement = Replace(convertElement, ", ,", ", _,");
        }
        convertElement = Replace(convertElement, "(,", "(_,");

        //If IsInStr(ConvertElement, "&H") And Right(ConvertElement, 1) = "&" Then Stop
        //If IsInStr(ConvertElement, "1/1/2001") Then Stop

        convertElement = RegExReplace(convertElement, "([0-9])#", "$1");

        if (Left(convertElement, 2) == "&H")
        {
            convertElement = "0x" + Mid(convertElement, 3);
            if (Right(convertElement, 1) == "&")
            {
                convertElement = Left(convertElement, Len(convertElement) - 1);
            }
        }

        if (withLevel > 0)
        {
            var T = Stack(ref withVars, "##REM##", true);
            convertElement = Trim(RegExReplace(convertElement, "([ (])(\\.)" + patToken, "$1" + T + "$2$3"));
            if (Left(convertElement, 1) == ".")
            {
                convertElement = T + convertElement;
            }
        }
        return convertElement;
    }

    public static string ConvertFunctionCall(string fCall)
    {
        //Debug.Print "ConvertFunctionCall: " & fCall
        var tb = "";
        var name = RegExNMatch(fCall, "^[a-zA-Z0-9_.]*");
        tb = tb + name;

        var ts = Mid(fCall, Len(name) + 2);
        ts = Left(ts, Len(ts) - 1);

        var vP = SubParam(name);
        if (ConvertDataType(vP.asType) == "Recordset")
        {
            tb = tb + ".Fields[";
            tb = tb + ConvertValue(ts);
            tb = tb + "].Value";
        }
        else if (vP.asArray != "")
        {
            tb = tb + "[";
            tb = tb + ConvertValue(ts);
            tb = tb + "]";
            //    TB = Replace(TB, ", ", "][")
        }
        else
        {
            var n = NextByPCt(ts, ",");
            tb = tb + "(";
            for (var I = 1; I <= n; I++)
            {
                if (I != 1)
                {
                    tb = tb + ", ";
                }
                var tv = NextByP(ts, ",", I);
                if (IsFuncRef(name))
                {
                    if (Trim(tv) == "")
                    {
                        tb = tb + ConvertElement(FuncRefArgDefault(name, I));
                    }
                    else
                    {
                        if (FuncRefArgByRef(name, I))
                        {
                            tb = tb + "ref ";
                        }
                        tb = tb + ConvertValue(tv);
                    }
                }
                else
                {
                    tb = tb + ConvertValue(tv);
                }
            }
            tb = tb + ")";
        }
        var convertFunctionCall = tb;
        return convertFunctionCall;
    }

    public static string ConvertValue(string s)
    {
        var convertValue = "";
        var op = "";
        var opN = "";

        var o = "";
        s = Trim(s);
        if (s == "")
        {
            return convertValue;

        }

        //If IsInStr(S, "GetMaxFieldValue") Then Stop
        //If IsInStr(S, "DBAccessGeneral") Then Stop
        //If IsInStr(S, "tallable") Then Stop
        //If Left(S, 3) = "RS(" Then Stop
        //If Left(S, 6) = "DBName" Then Stop
        //If Left(S, 6) = "fName" Then Stop

        SubParamUsedList(TokenList(s));

        if (RegExTest(s, "^-[a-zA-Z0-9_]"))
        {
            convertValue = "-" + ModConvert.ConvertValue(Mid(s, 2));
            return convertValue;

        }

        while (true)
        {
            var f = NextByOp(s, 1, ref op);
            if (f == "")
            {
                break;
            }
            switch (Trim(op))
            {
                case "\\":
                    opN = "/";
                    break;
                case "=":
                    opN = " == ";
                    break;
                case "<>":
                    opN = " != ";
                    break;
                case "&":
                    opN = " + ";
                    break;
                case "Mod":
                    opN = " % ";
                    break;
                case "Is":
                    opN = " == ";
                    break;
                case "Like":
                    opN = " == ";
                    break;
                case "And":
                    opN = " && ";
                    break;
                case "Or":
                    opN = " || ";
                    break;
                default:
                    opN = op;
                    break;
            }


            if (Left(f, 1) == "(" && Right(f, 1) == ")")
            {
                o = o + "(" + ModConvert.ConvertValue(Mid(f, 2, Len(f) - 2)) + ")" + opN;
            }
            else
            {
                o = o + ConvertElement(f) + opN;
            }

            if (op == "")
            {
                break;
            }
            s = Mid(s, Len(f) + Len(op) + 1);
            if (s == "" || op == "")
            {
                break;
            }
        }
        convertValue = o;
        return convertValue;
    }

    public static string ConvertGlobals(string str, bool asModule = false)
    {
        var s = new string[0];


        var res = "";
        var building = "";
        str = Replace(str, vbLf, "");
        s = Split(str, vbCr);
        var n = 0;
        //  Prg 0, UBound(S) - LBound(S) + 1, "Globals..."
        InitDeString();
        foreach (var iterL in s)
        {
            var l = iterL;
            l = DeComment(l);
            l = DeString(l);
            var o = "";
            if (building != "")
            {
                building = building + vbCrLf + l;
                if (TLeft(l, 8) == "End Type")
                {
                    o = ConvertType(building);
                    building = "";
                }
                else if (TLeft(l, 8) == "End Enum")
                {
                    o = ConvertEnum(building);
                    building = "";
                }
            }
            else if (l == "Option *")
            {
                o = "// " + l;
            }
            else if (RegExTest(l, "^(Public |Private |)Declare "))
            {
                o = ConvertApiDef(l);
            }
            else if (RegExTest(l, "^(Global |Public |Private |)Const "))
            {
                o = ConvertConstant(l, true);
            }
            else if (RegExTest(l, "^(Public |Private |)Event "))
            {
                o = ConvertEvent(l);
            }
            else if (RegExTest(l, "^(Public |Private |)Enum "))
            {
                building = l;
            }
            else if (RegExTest(LTrim(l), "^(Public |Private |)Type "))
            {
                building = l;
            }
            else if (TLeft(l, 8) == "Private " || TLeft(l, 7) == "Public " || TLeft(l, 4) == "Dim ")
            {
                o = ConvertDeclare(l, 0, true, asModule);
            }

            o = ReComment(o);
            res = res + ReComment(o) + IIf(o == "" || Right(o, 2) == vbCrLf, "", vbCrLf);
            n = n + 1;
            //    Prg N
            //    If N Mod 10000 = 0 Then Stop
        }
        //  Prg

        res = ReString(res, true);
        var convertGlobals = res;
        return convertGlobals;
    }

    public static string ConvertCodeLine(string s)
    {
        var convertCodeLine = "";
        var b = "";


        //If IsInStr(S, "dbClose") Then Stop
        //If IsInStr(S, "Nothing") Then Stop
        //If IsInStr(S, "Close ") Then Stop
        //If IsInStr(S, "& functionType & fieldInfo &") Then Stop
        //If IsInStr(S, " & vbCrLf2 & Res)") Then Stop
        //If IsInStr(S, "Res = CompareSI(SI1, SI2)") Then Stop
        //If IsInStr(S, "frmPrintPreviewDocument") Then Stop
        //If IsInStr(S, "NewAudit.Name1") Then Stop
        //If IsInStr(S, "optDelivered") Then Stop
        //If IsInStr(S, " Is Nothing Then") Then Stop
        //If IsInStr(S, "SqFt, SqYd") Then Stop
        //If IsInStr(S, "optTagIncoming") Then Stop
        //If IsInStr(S, "Kill modAshleyItemAlign") Then Stop
        //If IsInStr(S, "PRFolder") Then Stop
        //If IsInStr(S, "Array()") Then Stop
        //If IsInStr(S, "App.Path") Then Stop

        if (Trim(s) == "")
        {
            convertCodeLine = "";
            return convertCodeLine;

        }
        var complete = false;

        s = ConvertVb6Specific(s, out complete);
        if (complete)
        {
            convertCodeLine = s;
            return convertCodeLine;

        }

        if (RegExTest(Trim(s), "^[a-zA-Z0-9_.()]+ \\= ") || RegExTest(Trim(s), "^Set [a-zA-Z0-9_.()]+ \\= "))
        {
            // Assignment
            var T = InStr(s, "=");
            var a = Trim(Left(s, T - 1));
            if (TLeft(a, 4) == "Set ")
            {
                a = Trim(Mid(a, 5));
            }
            SubParamAssign(RegExNMatch(a, patToken));
            if (RegExTest(a, "^" + patToken + "\\(\"[^\"]+\"\\)"))
            {
                var p = RegExNMatch(a, "^" + patToken);
                var v = SubParam(p);
                if (v.name == p)
                {
                    SubParamAssign(p);
                    switch (v.asType)
                    {
                        case "Recordset":
                            convertCodeLine = RegExReplace(a, "^" + patToken + "(\\(\")([^\"]+)(\"\\))", "$1.Fields[\"$3\"].Value");
                            break;
                        default:
                            if (Left(a, 1) == ".")
                            {
                                a = Stack(ref withVars, "##REM##", true) + a;
                            }
                            convertCodeLine = a;
                            break;
                    }
                }
            }
            else
            {
                if (Left(a, 1) == ".")
                {
                    a = Stack(ref withVars, "##REM##", true) + a;
                }
                convertCodeLine = a;
            }

            var tAWord = SplitWord(a, 1, ".");
            if (IsFormRef(tAWord))
            {
                a = Replace(a, tAWord, tAWord + ".instance", 1, 1);
            }

            convertCodeLine = ConvertValue(convertCodeLine) + " = ";

            b = ConvertValue(Trim(Mid(s, T + 1)));
            convertCodeLine = convertCodeLine + b;
        }
        else
        {
            //Debug.Print S
            //If IsInStr(S, "Call ") Then Stop
            if (LMatch(LTrim(s), "Call "))
            {
                s = Mid(LTrim(s), 6);
            }

            var firstWord = SplitWord(Trim(s));
            var rest = SplitWord(Trim(s), 2, " ", true, true);
            if (rest == "")
            {
                convertCodeLine = s + IIf(Right(s, 1) != ")", "()", "");
                convertCodeLine = ConvertElement(convertCodeLine);
            }
            else if (firstWord == "RaiseEvent")
            {
                convertCodeLine = ConvertValue(s);
            }
            else if (firstWord == "Debug.Print")
            {
                convertCodeLine = "Console.WriteLine(" + ConvertValue(rest) + ")";
            }
            else if (StrQCnt(firstWord, "(") == 0)
            {
                convertCodeLine = "";
                convertCodeLine = convertCodeLine + firstWord + "(";
                var n = 0;
                do
                {
                    n = n + 1;
                    b = NextByP(rest, ", ", n);
                    if (b == "")
                    {
                        break;
                    }
                    convertCodeLine = convertCodeLine + IIf(n == 1, "", ", ") + ConvertValue(b);
                } while (true); // VB "Loop While" (was mistranslated as Loop Until)
                convertCodeLine = convertCodeLine + ")";
                //      ConvertCodeLine = ConvertElement(ConvertCodeLine)
            }
            else
            {
                convertCodeLine = ConvertValue(s);
            }
            if (withLevel > 0 & Left(Trim(convertCodeLine), 1) == ".")
            {
                convertCodeLine = Stack(ref withVars, "##REM##", true) + Trim(convertCodeLine);
            }
        }

        //  If IsInStr(ConvertCodeLine, ",,,,,,,") Then Stop

        convertCodeLine = convertCodeLine + ";";
        //Debug.Print ConvertCodeLine
        return convertCodeLine;
    }

    public static string PostConvertCodeLine(string str)
    {
        var s = str;

        //  If IsInStr(S, "optPoNo") Then Stop
        if (IsInStr(s, "0 &"))
        {
            s = Replace(s, "0 &", "0");
        }
        if (IsInStr(s, ".instance.instance"))
        {
            s = Replace(s, ".instance.instance", ".instance");
        }
        if (IsInStr(s, ".IsChecked)"))
        {
            s = Replace(s, ".IsChecked)", ".IsChecked == true)", 1);
        }
        if (IsInStr(s, ".IsChecked &"))
        {
            s = Replace(s, ".IsChecked", ".IsChecked == true", 1);
        }
        if (IsInStr(s, ".IsChecked |"))
        {
            s = Replace(s, ".IsChecked", ".IsChecked == true", 1);
        }
        if (IsInStr(s, ".IsChecked,"))
        {
            s = Replace(s, ".IsChecked", ".IsChecked == true", 1);
        }
        if (IsInStr(s, ".IsChecked == 1,"))
        {
            s = Replace(s, ".IsChecked == 1", ".IsChecked == true", 1);
        }
        if (IsInStr(s, ".IsChecked == 0,"))
        {
            s = Replace(s, ".IsChecked == 1", ".IsChecked == false", 1);
        }

        if (IsInStr(s, ".Visibility = true"))
        {
            s = Replace(s, ".Visibility = true", ".setVisible(true)");
        }
        if (IsInStr(s, ".Visibility = false"))
        {
            s = Replace(s, ".Visibility = false", ".setVisible(false)");
        }

        if (IsInStr(s, ".Print("))
        {
            if (IsInStr(s, ";);"))
            {
                s = Replace(s, ";);", ");");
                s = Replace(s, "Print(", "PrintNNL(");
            }
            s = Replace(s, "; ", ", ");
        }
        if (IsInStr(s, ".Line(("))
        {
            s = Replace(s, ") - (", ", ");
            s = Replace(s, "Line((", "Line(");
            s = Replace(s, "));", ");");
        }

        s = Replace(s, "vbRetryCancel +", "vbRetryCancel |");
        s = Replace(s, "vbOkOnly +", "vbOkOnly |");
        s = Replace(s, "vbOkCancel +", "vbOkCancel |");
        s = Replace(s, "vbExclamation +", "vbExclamation |");
        s = Replace(s, "vbYesNo +", "vbYesNo |");
        s = Replace(s, "vbQuestion +", "vbQuestion |");
        s = Replace(s, "vbOKCancel +", "vbOKCancel |");
        s = Replace(s, "+ vbExclamation", "| vbExclamation");

        var postConvertCodeLine = s;
        return postConvertCodeLine;
    }

    public static string ConvertSub(string str, bool asModule = false, vbTriState scanFirst = vbTriState.vbUseDefault)
    {
        var convertSub = "";

        var s = new string[0];
        var T = "";
        var u = "";
        var v = "";

        var inCase = 0;

        var returnVariable = "";


        //  If IsInStr(Str, "Dim oFTP As New FTP") Then Stop
        //  If IsInStr(Str, "cHolding") Then Stop
        //If IsInStr(Str, "IsIDE") Then Stop


        switch (scanFirst)
        {
            case vbTriState.vbUseDefault:
                var oStr = str;
                ModConvert.ConvertSub(oStr, asModule, vbTriState.vbTrue);
                //                          If IsInStr(Str, "StoreStockToolTipText") Then Stop
                convertSub = ModConvert.ConvertSub(oStr, asModule, vbTriState.vbFalse);
                return convertSub;
            case vbTriState.vbTrue:
                SubBegin();
                break;
            case vbTriState.vbFalse:
                SubBegin(true);
                break;
        }


        var res = "";
        str = Replace(str, vbLf, "");
        s = Split(str, vbCr);
        var ind = 0;

        //If IsInStr(Str, " WinCDSDataPath(") Then Stop
        //If IsInStr(Str, " RunShellExecute(") Then Stop
        //If IsInStr(Str, " ValidateSI(") Then Stop
        foreach (var iterL in s)
        {
            var l = iterL;
            //If IsInStr(L, "OrdVoid") Then Stop
            //If IsInStr(L, "MsgBox") Then Stop
            //If IsInStr(L, "And Not IsDoddsLtd Then") Then Stop
            l = DeComment(l);
            l = DeString(l);
            var o = "";


            //If IsInStr(L, "1/1/2001") Then Stop
            //If ScanFirst = vbFalse Then Stop
            //If IsInStr(L, "Public Function GetFileAutonumber") Then Stop
            //If IsInStr(L, "GetCustomerBalance") Then Stop
            //If IsInStr(L, "IsIDE") Then Stop

            var pp = "^(Public |Private |)(Friend |)(Function |Sub )" + patToken + "[ ]*\\(";
            var pq = "^(Public |Private )(Property )(Get |Let |Set )" + patToken + "[ ]*\\(";
            if (RegExNMatch(l, pp) != "")
            {
                //      CurrSub = nextBy(L, "(", 1)
                //      If (LMatch(CurrSub, "Public ")) Then CurrSub = Mid(CurrSub, 8)
                //      If (LMatch(CurrSub, "Private ")) Then CurrSub = Mid(CurrSub, 9)
                //      If (LMatch(CurrSub, "Friend ")) Then CurrSub = Mid(CurrSub, 8)
                //      If (LMatch(CurrSub, "Function ")) Then CurrSub = Mid(CurrSub, 10)
                //      If (LMatch(CurrSub, "Sub ")) Then CurrSub = Mid(CurrSub, 5)
                //If IsInStr(L, "Public Function IsIn") Then Stop
                o = o + SSpace(ind) + ConvertPrototype(l, ref returnVariable, asModule, ref currSub);
                ind = ind + spIndent;
            }
            else if (RegExNMatch(l, pq) != "")
            {
                //      If IsInStr(L, "edi888_Admin888_Src") Then Stop
                AddProperty(str);
                return convertSub;// repacked later...  not added here.

            }
            else if (TLMatch(l, "End Sub") || TLMatch(l, "End Function"))
            {
                if (returnVariable != "")
                {
                    o = o + SSpace(ind) + "return " + returnVariable + ";" + vbCrLf;
                }
                ind = ind - spIndent;
                o = o + SSpace(ind) + "}";
            }
            else if (TLMatch(l, "Exit Function") || TLMatch(l, "Exit Sub"))
            {
                if (returnVariable != "")
                {
                    o = o + SSpace(ind) + "return " + returnVariable + ";" + vbCrLf;
                }
                else
                {
                    o = o + "return;" + vbCrLf;
                }
            }
            else if (TLMatch(l, "GoTo "))
            {
                o = o + "goto " + SplitWord(Trim(l), 2) + ";";
            }
            else if (RegExTest(Trim(l), "^[a-zA-Z_][a-zA-Z_0-9]*:$"))
            { // Goto Label
                o = o + l + ";"; // c# requires a trailing ; on goto labels without trailing statements.  Likely a C# bug/oversight, but it's there.
            }
            else if (TLeft(l, 3) == "Dim")
            {
                o = ConvertDeclare(l, ind);
            }
            else if (TLeft(l, 5) == "Const")
            {
                o = SSpace(ind) + ConvertConstant(l, false);
            }
            else if (TLeft(l, 3) == "If ")
            { // Code sanitization prevents all single-line ifs.
                //If IsInStr(L, "optDelivered") Then Stop
                //If IsInStr(L, "PRFolder") Then Stop
                T = Mid(Trim(l), 4, Len(Trim(l)) - 8);
                o = SSpace(ind) + "if (" + ConvertValue(T) + ") {";
                ind = ind + spIndent;
            }
            else if (TLeft(l, 7) == "ElseIf ")
            {
                T = TMid(l, 8);
                if (Right(Trim(l), 5) == " Then")
                {
                    T = Left(T, Len(T) - 5);
                }
                o = SSpace(ind - spIndent) + "} else if (" + ConvertValue(T) + ") {";
            }
            else if (TLeft(l, 5) == "Else")
            {
                o = SSpace(ind - spIndent) + "} else {";
            }
            else if (TLeft(l, 6) == "End If")
            {
                ind = ind - spIndent;
                o = SSpace(ind) + "}";
            }
            else if (TLeft(l, 12) == "Select Case ")
            {
                o = o + SSpace(ind) + "switch(" + ConvertValue(TMid(l, 13)) + ") {";
                ind = ind + spIndent;
            }
            else if (TLeft(l, 10) == "End Select")
            {
                if (inCase > 0)
                {
                    ind = ind - spIndent;
                    inCase = inCase - 1;
                }
                ind = ind - spIndent;
                o = o + "break;" + vbCrLf;
                o = o + "}";
            }
            else if (TLeft(l, 9) == "Case Else")
            {
                if (inCase > 0)
                {
                    o = o + SSpace(ind) + "break;" + vbCrLf;
                    ind = ind - spIndent;
                    inCase = inCase - 1;
                }
                o = o + SSpace(ind) + "default:";
                inCase = inCase + 1;
                ind = ind + spIndent;
            }
            else if (TLeft(l, 5) == "Case ")
            {
                T = Mid(res, InStrRev(res, "switch("));
                if (RegExTest(T, "case [^:]+:"))
                {
                    o = o + SSpace(ind) + "break;" + vbCrLf;
                    ind = ind - spIndent;
                    inCase = inCase - 1;
                }
                T = TMid(l, 6);
                if (TLeft(T, 5) == "Like " || TLeft(T, 3) == "Is " || T == "* = *")
                {
                    o = o + "// TODO: Cannot convert case: " + T + vbCrLf;
                    o = o + SSpace(ind) + "case 0: ";
                }
                else if (NextBy(T, ",", 2) != "")
                {
                    o = o + SSpace(ind);
                    do
                    {
                        u = NextBy(T, ", ");
                        if (u == "")
                        {
                            break;
                        }
                        T = Trim(Mid(T, Len(u) + Len(", ") + 1)); // skip the delimiter too (only the first value was emitted)
                        o = o + "case " + ConvertValue(u) + ": ";
                    } while (true); // VB "Loop While" (was mistranslated as Loop Until)
                }
                else if (T == "* To *")
                {
                    o = o + "// CONVERSION: Case was " + T + vbCrLf;
                    o = o + SSpace(ind);
                    var cN = ValI(SplitWord(T, 1, " To "));
                    var cm = ValI(SplitWord(T, 2, " To "));
                    for (var k = cN; k <= cm; k++)
                    {
                        o = o + "case " + k + ": ";
                    }
                }
                else
                {
                    //          O = O & sSpace(Ind) & "case " & ConvertValue(T) & ":"
                    o = o + Space(ind);
                    foreach (var iterLl in Split(T, ","))
                    {
                        dynamic ll = iterLl;
                        o = o + "case " + ConvertValue(Trim(ll)) + ": "; // was ConvertValue(T): the whole list per item
                    }
                }
                inCase = inCase + 1;
                ind = ind + spIndent;
            }
            else if (Trim(l) == "Do")
            {
                o = o + SSpace(ind) + "do {";
                ind = ind + spIndent;
            }
            else if (TLeft(l, 9) == "Do While ")
            {
                o = o + SSpace(ind) + "while(" + ConvertValue(TMid(l, 10)) + ") {";
                ind = ind + spIndent;
            }
            else if (TLeft(l, 9) == "Do Until ")
            {
                o = o + SSpace(ind) + "while(!(" + ConvertValue(TMid(l, 10)) + ")) {";
                ind = ind + spIndent;
            }
            else if (TLeft(l, 9) == "For Each ")
            {
                l = TMid(l, 10);

                var iterVar = SplitWord(l, 1, " In ");
                o = o + SSpace(ind) + "foreach(var iter" + iterVar + " in " + SplitWord(l, 2, " In ") + ") {" + vbCrLf + iterVar + " = iter" + iterVar + ";";
                ind = ind + spIndent;
            }
            else if (TLeft(l, 4) == "For ")
            {
                l = TMid(l, 5);
                var forKey = SplitWord(l, 1, "=");
                l = SplitWord(l, 2, "=", true, true);
                var forStr = SplitWord(l, 1, " To ");
                var forEnd = SplitWord(l, 2, " To ");
                var forStep = SplitWord(forEnd, 2, " Step ");
                if (forStep != "")
                {
                    forEnd = SplitWord(forEnd, 1, " Step "); // "Step" used to end up inside the end expression
                }
                var fk = ConvertElement(forKey);
                var fe = ConvertElement(forEnd);
                string forCond;
                string forIncr;
                // VB For ... To is inclusive of the end value (was "<"); a negative Step counts down
                if (forStep == "")
                {
                    forCond = fk + "<=" + fe;
                    forIncr = fk + "++";
                }
                else if (Microsoft.VisualBasic.Information.IsNumeric(forStep))
                {
                    forCond = fk + (Val(forStep) < 0 ? ">=" : "<=") + fe;
                    forIncr = fk + " += " + ConvertValue(forStep);
                }
                else
                {
                    var fs = ConvertValue(forStep);
                    forCond = "(" + fs + " >= 0 ? " + fk + " <= " + fe + " : " + fk + " >= " + fe + ")";
                    forIncr = fk + " += " + fs;
                }
                o = o + SSpace(ind) + "for(" + fk + "=" + ConvertElement(forStr) + "; " + forCond + "; " + forIncr + ") {";
                ind = ind + spIndent;
            }
            else if (TLeft(l, 11) == "Loop While ")
            {
                ind = ind - spIndent;
                o = o + SSpace(ind) + "} while(" + ConvertValue(TMid(l, 12)) + ");"; // was negated like Loop Until
            }
            else if (TLeft(l, 11) == "Loop Until ")
            {
                ind = ind - spIndent;
                o = o + SSpace(ind) + "} while(!(" + ConvertValue(TMid(l, 12)) + "));";
            }
            else if (TLeft(l, 5) == "Loop")
            {
                ind = ind - spIndent;
                o = o + SSpace(ind) + "}";
            }
            else if (TLeft(l, 8) == "Exit For" || TLeft(l, 7) == "Exit Do" || TLeft(l, 10) == "Exit While")
            {
                o = o + SSpace(ind) + "break;";
            }
            else if (TLeft(l, 5) == "Next")
            {
                ind = ind - spIndent;
                o = SSpace(ind) + "}";
            }
            else if (TLeft(l, 5) == "With ")
            {
                withLevel = withLevel + 1;

                T = ConvertValue(TMid(l, 6));
                u = ConvertDataType(SubParam(T).asType);
                v = withMark + (SubParam(T).name != "" ? T : Random().ToString());
                if (u == "")
                {
                    u = defaultDataType;
                }

                Stack(ref withAssign, T);
                Stack(ref withTypes, u);
                Stack(ref withVars, v);

                o = o + SSpace(ind) + u + " " + v + ";" + vbCrLf;
                maxWithLevel = maxWithLevel + 1;
                o = o + SSpace(ind) + v + " = " + T + ";";
                ind = ind + spIndent;
            }
            else if (TLeft(l, 8) == "End With")
            {
                withLevel = withLevel - 1;
                T = Stack(ref withAssign);
                u = Stack(ref withTypes);
                v = Stack(ref withVars);
                if (SubParam(T).name != "")
                {
                    o = o + SSpace(ind) + T + " = " + v + ";";
                }
                ind = ind - spIndent;
            }
            else if (IsInStr(l, "On Error ") || IsInStr(l, "Resume "))
            {
                o = SSpace(ind) + "// TODO (not supported): " + l;
            }
            else
            {
                //If IsInStr(L, "ComputeAgeing dtpArrearControlDate") Then Stop
                //If IsInStr(L, "RaiseEvent") Then Stop
                //If IsInStr(L, "Debug.Print") Then Stop
                //If IsInStr(L, "HasGit") Then Stop
                o = SSpace(ind) + ConvertCodeLine(l);
            }

            o = ModConvert.PostConvertCodeLine(o);
            o = ModProjectSpecific.ProjectSpecificPostCodeLineConvert(o);

            o = ReComment(o);
            res = res + ReComment(o) + IIf(o == "", "", vbCrLf);
        }

        convertSub = res;
        return convertSub;
    }
}