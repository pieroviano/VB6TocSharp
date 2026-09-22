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
    /// <summary>Exit Property: a getter turns it into "return &lt;property&gt;;" (see ReadOutProperties).</summary>
    public const string ExitPropertyMark = "return /*Exit Property*/;";
    private static int withLevel = 0;
    private static int maxWithLevel = 0;
    private static string withVars = "";
    private static string withTypes = "";
    private static string withAssign = "";
    private static string formName = "";
    private static string currentModule = "";
    private static string currSub = "";


    /// <summary>Converts a project (.vbp), or every project of a group (.vbg) into a solution.</summary>
    public static void ConvertProject(string vbpFile)
    {
        if (ModProjectGroup.IsGroupFile(vbpFile))
        {
            Notify("Complete: " + ModProjectGroup.ConvertGroup(vbpFile));
            return;
        }
        ConvertSingleProject(vbpFile);
        Notify("Complete.");
    }

    /// <summary>Scan, project and support files, every file of the .vbp, migration report.</summary>
    internal static void ConvertSingleProject(string vbpFile)
    {
        Prg(0, 1, "Preparing...");
        ModConvertStatements.ResetProjectCaches(); // sources may have changed since the last run
        ScanRefs();
        CreateProjectFile(vbpFile);
        CreateProjectSupportFiles();
        ConvertFileList(FilePath(vbpFile), VbpModules(vbpFile) + vbCrLf + VbpClasses(vbpFile) + vbCrLf + VbpForms(vbpFile) + vbCrLf + VbpUserControls(vbpFile));
        ModMigrationReport.Write(); // what is left to review, per category and file
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

        var s = ModConvertPragmas.PreProcess(ReadEntireFile(frmFile));
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
            var ppHeader = ModConvertStatements.BeginFile(s, ProjectInfo().CondComp); // #Const -> #define
            var globals = ConvertGlobals(Left(code, j));
            InitLocalFuncs(FormControls(fName, preamble) + ScanRefsFileToString(frmFile));
            var functions = ConvertCodeSegment(Mid(code, j));

            var x = "";
            x = x + ppHeader + UsingEverything(fName) + vbCrLf;
            x = x + vbCrLf;
            x = x + "namespace " + ns + vbCrLf;
            x = x + "{" + vbCrLf;
            x = x + FormClassHeader(ctx, designer) + vbCrLf;
            x = x + vbCrLf;
            x = x + globals + vbCrLf + vbCrLf + functions;
            x = x + vbCrLf + "}";
            x = x + vbCrLf + "}";

            x = DeWs(x);

            convertForm = WriteOut(f, ModConvertPragmas.PostProcess(x), frmFile); // was never set: always False
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
        var x = ModProjectGroup.TypeModifier(ModProjectGroup.IsExposed(file)) + " partial class " + fName + " : " + baseType + " {" + n;
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
        var s = ModConvertPragmas.PreProcess(ReadEntireFile(basFile));
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
        var ppHeader = ModConvertStatements.BeginFile(s, ProjectInfo().CondComp); // #Const -> #define
        var globals = ConvertGlobals(Left(code, j - 1), true);
        var functions = ConvertCodeSegment(Mid(code, j), true);

        var x = "";
        x = x + ppHeader + UsingEverything(fName) + vbCrLf;
        x = x + vbCrLf;
        x = x + ModProjectGroup.TypeModifier(false) + " static class " + fName + " {" + vbCrLf; // a standard module is never seen by other projects
        x = x + NlTrim(globals + vbCrLf + vbCrLf + functions);
        x = x + vbCrLf + "}";

        x = DeWs(x);

        convertModule = WriteOut(f, ModConvertPragmas.PostProcess(x), basFile); // was never set: always False
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
        var s = ModConvertPragmas.PreProcess(ReadEntireFile(clsFile));
        var fName = ModuleName(s);
        currentModule = fName;
        var f = fName + ".cs";
        if (IsConverted(f, clsFile))
        {
            Console.WriteLine("Class Already Converted: " + f);
            return convertClass;

        }

        var x = UsingEverything(fName) + vbCrLf + vbCrLf + ConvertClassSource(s, ProjectInfo().CondComp);
        var header = ModConvertStatements.FileHeader;
        x = DeWs(header + x);

        f = fName + ".cs";
        convertClass = WriteOut(f, ModConvertPragmas.PostProcess(x), clsFile); // was never set: always False
        return convertClass;
    }

    /// <summary>
    /// A class module (.cls) as C#: the class (constructor from Class_Initialize, IDisposable from Class_Terminate, Implements,
    /// default member, NewEnum, default instance) or, for a VB6 "interface class" with empty procedures, an interface.
    /// </summary>
    public static string ConvertClassSource(string s, System.Collections.Generic.IDictionary<string, string> projectConstants = null)
    {
        var fName = ModuleName(s);
        currentModule = fName;
        ModConvertStatements.FileHeader = ModConvertStatements.BeginFile(s, projectConstants); // #Const -> #define
        var model = ModConvertClasses.ClassModel.Scan(fName, s);
        ModConvertClasses.Register(model);
        if (ModConvertClasses.IsInterface(fName))
        {
            var iface = ModConvertClasses.InterfaceDeclaration(model);
            if (iface != null)
            {
                return iface;
            }
        }
        ModConvertClasses.Current = model;
        try
        {
            var code = Mid(s, CodeSectionLoc(s));
            var j = CodeSectionGlobalEndLoc(code);
            var globals = ConvertGlobals(Left(code, j - 1));
            var functions = ConvertCodeSegment(Mid(code, j));

            var x = "";
            x = x + ModConvertClasses.ClassDeclaration(model) + vbCrLf;
            x = x + ModConvertClasses.ClassMembers(model);
            x = x + globals + vbCrLf + vbCrLf + functions;
            x = x + vbCrLf + "}";
            return x;
        }
        finally
        {
            ModConvertClasses.Current = null;
        }
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

    /// <param name="midLine">the text follows a ':' or a single-line If on the same line: it holds no labels or line numbers</param>
    public static string SanitizeCode(string str, bool midLine = false)
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
            if (l == "_" || Right(l, 2) == " _" || Right(l, 2) == "\t_")
            { // line continuation needs whitespace before "_" (identifiers may end with "_")
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
            l = ModConvertStatements.EscapeKeywords(DeString(l)); // VB6 names that are C# keywords get a "_"

            // line number: "10 x = 1" -> "L10:" + "x = 1"
            var lineNo = System.Text.RegularExpressions.Regex.Match(l, "^\\s*([0-9]+):?(\\s+(.*))?$");
            if (lineNo.Success && !midLine)
            {
                var lineLabel = ModConvertStatements.LabelName(lineNo.Groups[1].Value) + ":";
                l = lineNo.Groups[3].Value;
                if (Trim(l) == "")
                {
                    r = r + n + ReComment(lineLabel, true);
                    goto NextLine;
                }
                r = r + n + lineLabel;
            }

            //If IsInStr(L, "CustRec <> 0") Then Stop
            if (TLeft(l, 3) == "If " && Right(RTrim(l), 5) != " Then" && IsInStr(l, " Then "))
            { // single-line If: each branch is sanitized again (it may hold ':' lists or another single-line If)
                f = NextBy(l, " Then ") + " Then";
                r = r + n + ReComment(f, true);
                SplitSingleLineIf(Mid(l, Len(f) + 2), out var thenPart, out var elsePart);
                r = r + ModConvert.SanitizeCode(SingleLineIfBranch(thenPart), true);
                if (elsePart != null)
                {
                    r = r + n + "Else";
                    r = r + ModConvert.SanitizeCode(SingleLineIfBranch(elsePart), true);
                }
                r = r + n + "End If";
                goto NextLine;
            }

            // a time literal (#10:30:00#) is not a statement separator
            var timeLiterals = new System.Collections.Generic.List<string>();
            l = System.Text.RegularExpressions.Regex.Replace(l, ModConvertStatements.TimeLiteralPattern, m =>
            {
                timeLiterals.Add(m.Value);
                return "###TIME-LITERAL-" + (timeLiterals.Count - 1) + "###";
            });
            string RestoreTime(string x) => System.Text.RegularExpressions.Regex.Replace(x, "###TIME-LITERAL-([0-9]+)###", m => timeLiterals[int.Parse(m.Groups[1].Value)]);

            if (NextBy(l, ":") != l)
            {
                if (RegExTest(Trim(l), "^[a-zA-Z_][a-zA-Z_0-9]*:$"))
                { // Goto Label
                    r = r + n + ReComment(l);
                }
                else
                {
                    l = Replace(l, namedParamSrc, namedParamTok);
                    f = NextBy(l, ":");
                    // "Label: statement" - VB6 keeps labels in the first column
                    var isLabel = !midLine && RegExTest(f, "^[a-zA-Z_][a-zA-Z_0-9]*$") && !IsReservedStatementWord(f);
                    f = Replace(f, namedParamTok, namedParamSrc);
                    r = r + n + ReComment(RestoreTime(f) + (isLabel ? ":" : ""), true);
                    l = Replace(l, namedParamTok, namedParamSrc);
                    if (f != l)
                    {
                        l = Trim(Mid(l, Len(f) + 2));
                        r = r + ModConvert.SanitizeCode(RestoreTime(l), true);
                    }
                }
            }
            else
            {
                r = r + n + ReComment(RestoreTime(l), true);
            }
            NextLine:;
        }

        var sanitizeCode = r;
        return sanitizeCode;
    }

    /// <summary>VB6 words that end a statement list and therefore can never be a line label.</summary>
    private static bool IsReservedStatementWord(string w)
    {
        switch (w)
        {
            case "Else":
            case "End":
            case "Loop":
            case "Next":
            case "Wend":
            case "Do":
            case "Stop":
            case "Return":
            case "Resume":
            case "Exit":
                return true;
            default:
                return false;
        }
    }

    /// <summary>Splits "a Else b" of a single-line If; an Else belongs to the innermost nested single-line If.</summary>
    public static void SplitSingleLineIf(string rest, out string thenPart, out string elsePart)
    {
        var words = Split(rest, " ");
        var depth = 0;
        for (var i = 0; i < words.Length; i++)
        {
            var w = words[i].TrimEnd(':');
            if (w == "If")
            {
                depth++;
            }
            else if (w == "Else")
            {
                if (depth == 0)
                {
                    thenPart = string.Join(" ", words, 0, i);
                    elsePart = string.Join(" ", words, i + 1, words.Length - i - 1);
                    return;
                }
                depth--;
            }
        }
        thenPart = rest;
        elsePart = null;
    }

    /// <summary>"If x Then 100" jumps to line 100.</summary>
    private static string SingleLineIfBranch(string s)
    {
        s = Trim(s);
        return RegExTest(s, "^[0-9]+$") ? "GoTo " + s : s;
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
            var p = ProcModifiers + "(Function |Sub |Property Get |Property Let |Property Set )" + patToken + "[$%&!#@]?[ ]*\\(";
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

            k = "End " + Trim(RegExNMatch(f, "(Function|Sub|Property) ")); // the first keyword (f may have no modifier)
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
        r = r + CommentBlock(NlTrim(s)); // after the last procedure: comments, a closing #End If

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
        // conditional compilation around procedures stays live; everything else is kept as a comment
        var s = "";
        var run = "";
        foreach (var l in Split(Replace(str, vbLf, ""), vbCr))
        {
            if (ModConvertStatements.IsDirective(l))
            {
                if (NlTrim(run) != "") s = s + "/*" + vbCrLf + Replace(NlTrim(run), "*/", "* /") + vbCrLf + "*/" + vbCrLf;
                run = "";
                s = s + ModConvertStatements.ConvertDirective(l) + vbCrLf;
            }
            else
            {
                run = run + l + vbCrLf;
            }
        }
        if (NlTrim(run) != "") s = s + "/*" + vbCrLf + Replace(NlTrim(run), "*/", "* /") + vbCrLf + "*/" + vbCrLf;
        commentBlock = s;
        return commentBlock;
    }

    /// <summary>Optional modifiers of a procedure declaration: [Public|Private|Friend] [Static].</summary>
    public const string ProcModifiers = "(Public |Private |Friend |)(Friend |)(Static |)";

    public static string ConvertDeclare(string s, int ind, bool isGlobal = false, bool asModule = false)
    {
        var asPrivate = false;

        var pType = "";

        var isArr = false;

        var res = "";

        var ss = s;

        if (TLeft(s, 7) == "Public ")
        {
            s = TMid(s, 8);
        }
        if (TLeft(s, 7) == "Global ")
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
        foreach (var iterL in ModConvertStatements.SplitTopLevel(s))
        {
            var l = iterL;
            l = Trim(l);
            if (l == "")
            {
                continue;
            }
            var withEvents = false;
            if (LMatch(l, "WithEvents "))
            { // a property that (un)subscribes the module's handlers when the object changes
                l = Trim(TMid(l, 12));
                withEvents = true;
            }
            var pName = RegExNMatch(l, patToken);
            l = Trim(TMid(l, Len(pName) + 1));
            var suffixType = Len(l) > 0 ? ModConvertStatements.SuffixType(l[0]) : "";
            if (suffixType != "")
            { // Dim s$ / Dim n%(10)
                l = Trim(Mid(l, 2));
            }
            var declStart = Len(res);
            if (isGlobal)
            {
                res = res + IIf(asPrivate, "private ", "public ");
            }
            if (asModule)
            {
                res = res + "static ";
            }
            isArr = false;
            var dims = new System.Collections.Generic.List<string>();
            if (TLeft(l, 1) == "(")
            {
                isArr = true;
                var close = ModConvertStatements.MatchParen(l, 0);
                var arraySpec = close < 0 ? "" : Mid(l, 2, close - 1);
                l = close < 0 ? "" : Trim(Mid(l, close + 2));
                if (Trim(arraySpec) != "")
                {
                    dims = ModConvertStatements.SplitTopLevel(arraySpec);
                }
            }

            var asNew = false;
            var fixedLen = "";
            if (SplitWord(l, 1) == "As")
            {
                pType = SplitWord(l, 2);
                if (pType == "New")
                {
                    pType = SplitWord(l, 3);
                    asNew = true;
                }
                if (LMatch(SplitWord(l, 3), "*"))
                { // String * n
                    fixedLen = ConvertValue(Trim(Mid(l, InStr(l, "*") + 1)));
                }
            }
            else
            {
                pType = suffixType != "" ? suffixType : ModConvertStatements.ImplicitType(pName);
            }

            pType = ModConvertPragmas.TypeOverride(pName) ?? pType; // pragma SetType
            var cType = ConvertDataType(pType);
            var asArray = "";
            var vb6Array = false;
            var mods = Mid(res, declStart + 1); // access modifiers already emitted for this variable
            var before = Left(res, declStart);
            if (!isArr && fixedLen != "" && isGlobal)
            { // a field keeps its length through a property (VB6 pads or truncates every assignment)
                res = before + SSpace(ind) + Replace(mods, "public ", "private ") + "string _" + pName + " = new string(' ', " + fixedLen + ");" + vbCrLf
                      + SSpace(ind) + mods + "string " + pName + " { get => _" + pName + "; set => _" + pName + " = FixedLen(value, " + fixedLen + "); }" + vbCrLf;
            }
            else if (!isArr && fixedLen != "")
            { // a local: assignments are padded / truncated (see ConvertCodeLine)
                res = res + SSpace(ind) + "string " + pName + " = new string(' ', " + fixedLen + ");" + vbCrLf;
            }
            else if (!isArr && withEvents && isGlobal)
            {
                res = before + ModConvertClasses.WithEventsProperty(mods, cType, pType, pName, ind);
            }
            else if (!isArr && asNew && isGlobal && ModConvertStatements.AutoNew)
            { // VB6 auto-instancing: created on first use, and again after Set x = Nothing
                res = before + SSpace(ind) + Replace(mods, "public ", "private ") + cType + " _" + pName + ";" + vbCrLf
                      + SSpace(ind) + mods + cType + " " + pName + " { get => _" + pName + " ?? (_" + pName + " = new " + cType + "()); set => _" + pName + " = value; }" + vbCrLf;
            }
            else if (!isArr)
            {
                res = res + SSpace(ind) + cType + " " + pName;
                res = res + " = ";
                if (asNew)
                {
                    res = res + "new ";
                    res = res + cType;
                    res = res + "()";
                }
                else
                {
                    res = res + ModConvertStatements.DefaultValue(pType);
                }
                res = res + ";" + vbCrLf;
            }
            else
            {
                var aTodo = "";
                var lbs = new System.Collections.Generic.List<string>();
                var ubs = new System.Collections.Generic.List<string>();
                var counts = new System.Collections.Generic.List<string>();
                foreach (var d in dims)
                {
                    counts.Add(ModConvertStatements.DimBounds(d, out var lb, out var ub));
                    lbs.Add(lb);
                    ubs.Add(ub);
                    if (lb != "0" && dims.Count > 1)
                    {
                        aTodo = " // TODO - Specified Minimum Array Boundary Not Supported: " + ss;
                    }
                }
                var lower = dims.Count == 0 ? ModConvertStatements.OptionBase.ToString() : lbs[0];
                vb6Array = dims.Count <= 1 && lower != "0" && !ModConvertStatements.ForceZeroBounds;
                if (dims.Count == 0)
                { // dynamic array: sized by ReDim
                    asArray = "-1";
                    res = res + SSpace(ind) + (vb6Array ? "VB6Array<" + cType + "> " + pName + " = new VB6Array<" + cType + ">(" + lower + ");" : cType + "[] " + pName + " = null;") + vbCrLf;
                }
                else if (vb6Array)
                {
                    asArray = counts[0];
                    res = res + SSpace(ind) + "VB6Array<" + cType + "> " + pName + " = new VB6Array<" + cType + ">(" + lbs[0] + ", " + ubs[0] + ");" + vbCrLf;
                }
                else
                {
                    asArray = string.Join(", ", counts);
                    var rank = dims.Count == 1 ? "[]" : "[" + new string(',', dims.Count - 1) + "]";
                    var alloc = ModConvertStatements.IsPlainValueType(cType) || dims.Count > 2 ? "new " + cType + "[" + asArray + "]" : "NewArray<" + cType + ">(" + asArray + ")";
                    res = res + SSpace(ind) + cType + rank + " " + pName + " = " + alloc + ";" + aTodo + vbCrLf;
                }
            }

            if (isGlobal)
            {
                ModuleVarDecl(pName, pType, asArray);
            }
            else
            {
                SubParamDecl(pName, pType, asArray, false, false);
            }
            var declared = SubParam(pName);
            if (declared.name == pName)
            {
                declared.vb6Array = vb6Array;
                declared.fixedLen = fixedLen;
            }
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
        if (TLeft(s, 8) == "PtrSafe ")
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
        var cName = RegExNMatch(Trim(s), "^" + patToken + "[$%&!#@]?");
        s = Trim(Mid(Trim(s), Len(cName) + 1));
        var suffixType = ModConvertStatements.StripSuffix(ref cName);
        if (TLeft(s, 3) == "As ")
        {
            s = Trim(Mid(Trim(s), 3));
            cType = SplitWord(s, 1);
            s = Trim(TMid(s, Len(cType) + 1));
        }
        else
        {
            cType = suffixType != "" ? suffixType : "Variant";
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
        var inferred = true;
        if (dataType == "dynamic" || dataType == "object")
        { // c# can't handle constants of type 'dynamic' (or object) when type can be inferred.
            if (RegExTest(cValue, "^" + deStringTokenBase + "[0-9]+$"))
            {
                dataType = "string";
            }
            else if (RegExTest(cValue, "^-?[0-9]+[.][0-9]+$"))
            {
                dataType = "double";
            }
            else if (RegExTest(cValue, "^-?([0-9]+|0x[0-9A-Fa-f]+)$"))
            {
                dataType = "int";
            }
            else if (cValue == "true" || cValue == "false")
            {
                dataType = "bool";
            }
            else
            {
                inferred = false; // an expression: its type is only known to the compiler
            }
        }

        if (!inferred)
        {
            convertConstant = isGlobal ? IIf(isPrivate, "private ", "public ") + "static readonly dynamic " + cName + " = " + cValue + ";" : "var " + cName + " = " + cValue + ";";
        }
        else if (cType == "Date")
        {
            convertConstant = isGlobal ? IIf(isPrivate, "private ", "public ") + "static readonly " + dataType + " " + cName + " = " + cValue + ";" : dataType + " " + cName + " = " + cValue + ";";
        }
        else
        {
            convertConstant = IIf(isGlobal, IIf(isPrivate, "private ", "public "), "") + "const " + dataType + " " + cName + " = " + cValue + ";";
        }
        return convertConstant;
    }

    /// <summary>A Const statement, which may declare several constants ("Const A = 1, B = 2").</summary>
    public static string ConvertConstants(string s, bool isGlobal = true, int ind = 0)
    {
        var m = System.Text.RegularExpressions.Regex.Match(Trim(s), "^((?:Public |Private |Global )?Const )(.*)$");
        if (!m.Success)
        {
            return SSpace(ind) + ConvertConstant(s, isGlobal) + vbCrLf;
        }
        var r = "";
        foreach (var item in ModConvertStatements.SplitTopLevel(m.Groups[2].Value))
        {
            if (item != "")
            {
                r = r + SSpace(ind) + ConvertConstant(m.Groups[1].Value + item, isGlobal) + vbCrLf;
            }
        }
        return r;
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
        var lines = Split(Replace(s, vbLf, ""), vbCr);
        var eName = RegExNMatch(lines[0], patToken, 0);
        var res = "public enum " + eName + " {";

        // one member per line: Name [= value]; [Bracketed Name] members get a valid identifier
        for (var i = 1; i < lines.Length; i++)
        {
            var t = Trim(lines[i]);
            if (t == "" || LMatch(t, "End Enum"))
            {
                continue;
            }
            if (ModConvertStatements.IsDirective(t))
            {
                res = res + vbCrLf + ModConvertStatements.ConvertDirective(t);
                continue;
            }
            string member;
            if (Left(t, 1) == "[" && InStr(t, "]") > 0)
            {
                member = System.Text.RegularExpressions.Regex.Replace(Mid(t, 2, InStr(t, "]") - 2), "[^A-Za-z0-9_]", "_");
                t = Trim(Mid(t, InStr(t, "]") + 1));
            }
            else
            {
                member = RegExNMatch(t, patToken, 0);
                t = Trim(Mid(t, Len(member) + 1));
            }
            res = res + vbCrLf + SSpace(spIndent) + member;
            if (Left(t, 1) == "=")
            {
                res = res + " = " + ConvertValue(Trim(Mid(t, 2)));
            }
            res = res + ","; // a trailing comma is valid C#, and keeps members apart across #if branches
        }
        res = res + vbCrLf + "}";

        var convertEnum = res;
        return convertEnum;
    }

    /// <summary>
    /// Type ... End Type to a struct (VB6 UDTs are values: assignment copies them). Strings, fixed-length strings, fixed
    /// arrays and nested UDTs are set up by Initialize (IVbStruct); fixed members carry MarshalAs for API calls.
    /// </summary>
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
        ModConvertStatements.RegisterUdt(eName);
        ModConvertStatements.RegisterUdtFields(eName, s);

        //If IsInStr(eName, "OSVERSIONINFO") Then Stop
        var res = "[StructLayout(LayoutKind.Sequential)]" + vbCrLf + IIf(isPrivate, "private ", "public ") + "struct " + eName + " : IVbStruct {";
        var init = "";

        while (TLeft(s, 8) != "End Type" && s != "")
        {
            if (TLeft(s, 1) == "#")
            { // conditional compilation around members
                var eol = InStr(s, vbCr);
                var directive = ModConvertStatements.ConvertDirective(eol > 0 ? Left(s, eol - 1) : s);
                res = res + vbCrLf + directive;
                init = init + vbCrLf + directive;
                s = eol > 0 ? NlTrim(Mid(s, eol)) : "";
                continue;
            }
            eName = RegExNMatch(s, patToken, 0);
            s = NlTrim(TMid(s, Len(eName) + 1));
            var counts = new System.Collections.Generic.List<string>();
            var dynamicArray = false;
            var boundTodo = "";
            if (LMatch(s, "("))
            {
                n = NextBy(Mid(s, 2), ")");
                s = NlTrim(Mid(s, Len(n) + 3));
                dynamicArray = Trim(n) == "";
                if (!dynamicArray)
                {
                    foreach (var d in ModConvertStatements.SplitTopLevel(n))
                    {
                        counts.Add(ModConvertStatements.DimBounds(d, out var lb, out _)); // VB6 bounds are inclusive
                        if (lb != "0")
                        {
                            boundTodo = " // TODO: VB6 lower bound " + lb + " (element indexes are not shifted)";
                        }
                    }
                }
            }

            if (TLeft(s, 3) == "As ")
            {
                s = NlTrim(Mid(s, 4));
                eType = RegExNMatch(s, patToken + "(\\." + patToken + ")?", 0);
                s = NlTrim(TMid(s, Len(eType) + 1));
            }
            else
            {
                eType = ModConvertStatements.ImplicitType(eName);
            }
            var fixedLen = "";
            if (TLMatch(s, "*"))
            { // String * n
                s = LTrim(Mid(LTrim(s), 2));
                fixedLen = RegExNMatch(s, "^[a-zA-Z0-9_]+", 0);
                s = NlTrim(Mid(LTrim(s), Len(fixedLen) + 1));
                fixedLen = ConvertValue(fixedLen);
            }

            var cType = ConvertDataType(eType);
            var isArray = dynamicArray || counts.Count > 0;
            var rank = !isArray ? "" : "[" + new string(',', Math.Max(counts.Count, 1) - 1) + "]";
            var marshal = "";
            if (fixedLen != "" && !isArray)
            {
                marshal = "[MarshalAs(UnmanagedType.ByValTStr, SizeConst = " + fixedLen + ")] ";
                init = init + vbCrLf + "  " + eName + " = FixedLen(\"\", " + fixedLen + ");";
            }
            else if (counts.Count > 0)
            {
                if (counts.Count == 1 && RegExTest(counts[0], "^[0-9]+$"))
                {
                    marshal = "[MarshalAs(UnmanagedType.ByValArray, SizeConst = " + counts[0] + ")] ";
                }
                init = init + vbCrLf + "  " + eName + " = NewArray<" + cType + ">(" + string.Join(", ", counts) + ");";
            }
            else if (!isArray && eType == "String")
            {
                init = init + vbCrLf + "  " + eName + " = \"\";";
            }
            else if (!isArray && ModConvertStatements.IsUdt(eType))
            {
                init = init + vbCrLf + "  " + eName + " = NewStruct<" + cType + ">();";
            }
            res = res + vbCrLf + " " + marshal + "public " + cType + rank + " " + eName + ";" + boundTodo;
        }
        res = res + vbCrLf + " public void Initialize() {" + init + vbCrLf + " }";
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
        s = Trim(Mid(s, Len(pName) + 1));
        var isArrayParam = IsInStr(pName, "()");
        pName = Replace(pName, "()", "");
        var suffixType = ModConvertStatements.StripSuffix(ref pName); // ByVal s$
        if (isArrayParam)
        {
            pName = pName + "()";
        }
        if (isByRef && SubParam(Replace(pName, "()", "")).assignedBeforeUsed)
        {
            asOut = true;
        }
        if (TLeft(s, 3) == "As ")
        {
            s = TMid(s, 4);
            pType = Trim(SplitWord(s, 1, "="));
            s = Trim(Mid(s, InStr(s, pType) + Len(pType)));
        }
        else
        {
            pType = suffixType != "" ? suffixType : ModConvertStatements.ImplicitType(pName);
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

        SubParamDecl(pName, pType, isArrayParam ? "-1" : "", true, false); // was "False": every parameter looked like an array
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
        { // visible to the project only
            res = res + "internal ";
            s = Mid(s, 8);
        }
        if (LMatch(s, "Static "))
        { // all locals static: handled by ConvertSub
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
        s = Trim(TMid(s, Len(fName) + 2));
        var suffixType = ModConvertStatements.StripSuffix(ref fName); // Function Name$(...)
        asName = fName;

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
                retType = suffixType != "" ? suffixType : ModConvertStatements.ImplicitType(fName);
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
            SubParamDecl(returnVariable, retType, Right(retType, 2) == "()" ? "-1" : "", false, true); // was "False": F(x) inside F became F[x]
        }

        var iface = ModConvertClasses.ImplementedInterface(asName);
        if (iface != null)
        { // Implements: IFoo_Bar is the explicit implementation of IFoo.Bar
            res = System.Text.RegularExpressions.Regex.Replace(res, "^(public |private |internal )*(static )?", "");
            var member = Mid(asName, Len(iface) + 2);
            var at = InStr(res, " " + asName + "(");
            res = Left(res, at) + iface + "." + member + Mid(res, at + Len(asName) + 1);
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
        var radix = ModConvertStatements.ConvertRadixLiteral(s);
        if (radix != null)
        {
            convertElement = radix;
            return convertElement;

        }

        s = ModConvertStatements.StripTypeSuffixes(s);
        var element = System.Text.RegularExpressions.Regex.Match(Trim(s), "^([a-zA-Z_][a-zA-Z_0-9]*)\\(");
        if (element.Success && SubParam(element.Groups[1].Value).asArray != "")
        { // an array element with a member: list(i).Name -> list[i].Name
            var close = ModConvertStatements.MatchParen(Trim(s), element.Length - 1);
            if (close > 0 && close < Len(Trim(s)) - 1 && Trim(s)[close + 1] == '.')
            {
                convertElement = element.Groups[1].Value + "[" + ConvertValue(Mid(Trim(s), element.Length + 1, close - element.Length)) + "]" + Mid(Trim(s), close + 2);
                return convertElement;
            }
        }
        if (LMatch(s, "AddressOf "))
        { // a procedure passed as a callback: the method group converts to the delegate type
            convertElement = Trim(Mid(s, 11));
            return convertElement;
        }

        if (IsNumeric(Trim(s)) && !RegExTest(Trim(s), "^&"))
        {
            convertElement = Val(s).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            if (IsInStr(s, ".") && !IsInStr(convertElement, ".") && !IsInStr(convertElement, "E"))
            { // a VB6 literal with a decimal point is a Double
                convertElement = convertElement + ".0";
            }
            return convertElement;

        }

        // VB6 date literals are always month/day/year
        var dateLit = ModConvertStatements.ConvertDateLiterals(s);
        if (dateLit != s && RegExTest(Trim(s), "^#[^#]+#$"))
        {
            convertElement = dateLit;
            return convertElement;
        }
        s = dateLit;

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
            if (ModConvertClasses.IsMethod(SubParam(first).asType, second))
            { // obj.Method without parentheses calls it (in C# it is a method group, which converts to a delegate)
                convertElement = Trim(s) + "()";
                return convertElement;
            }
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
        if (IsInStr(s, ".") && ModConvertClasses.IsPredeclared(SplitWord(Trim(s), 1, ".")) && ModConvertClasses.Current?.Name != SplitWord(Trim(s), 1, "."))
        { // a VB_PredeclaredId class used by name: its default instance
            s = Replace(Trim(s), SplitWord(Trim(s), 1, "."), SplitWord(Trim(s), 1, ".") + ".instance", 1, 1);
        }
        if (System.Text.RegularExpressions.Regex.IsMatch(Trim(s), "^[a-zA-Z_][a-zA-Z_0-9]*\\.(\\[_NewEnum\\]|_NewEnum)$"))
        { // the enumerator of a collection (NewEnum)
            convertElement = "((System.Collections.IEnumerable)" + SplitWord(Trim(s), 1, ".") + ").GetEnumerator()";
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

        convertElement = System.Text.RegularExpressions.Regex.Replace(convertElement, "&[HhOo][0-9A-Fa-f]+&?(?![A-Za-z0-9_])", m => ModConvertStatements.ConvertRadixLiteral(m.Value) ?? m.Value);

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
        if (name == "")
        { // not a call (e.g. a cast expression)
            return fCall;
        }
        tb = tb + (ModConvertStatements.ConversionFunction(name) ?? ModConvertStatements.RuntimeFunction(name) ?? name);

        var ts = Mid(fCall, Len(name) + 2);
        ts = Left(ts, Len(ts) - 1);

        var vP = SubParam(name);
        if (ConvertDataType(vP.asType) == "Recordset")
        {
            tb = tb + ".Fields[";
            tb = tb + ConvertValue(ts);
            tb = tb + "].Value";
        }
        else if (vP.asArray != "" || ModConvertClasses.HasIndexedDefault(vP.asType) || ModConvertStatements.IsUdtArrayField(name))
        { // an array element, or the default member of a Collection / class: obj(i) -> obj[i]
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
                            tb = tb + "ref " + ConvertValue(tv);
                        }
                        else
                        { // a VB6 argument is converted to the parameter type (Integer parameter, Long argument)
                            tb = tb + ModConvertStatements.ImplicitConversion(Trim(SplitWord(FuncRefArgType(name, I), 1, "=")), tv, ConvertValue(tv));
                        }
                    }
                }
                else
                {
                    tb = tb + ConvertValue(tv);
                }
            }
            tb = tb + ModConvertStatements.CompareArgument(name, n) + ")";
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
        s = ModConvertStatements.GroupNot(s);

        if (RegExTest(s, "^-[a-zA-Z0-9_]"))
        {
            convertValue = "-" + ModConvert.ConvertValue(Mid(s, 2));
            return convertValue;

        }

        // operands and the (trimmed) VB operators between them
        var raws = new System.Collections.Generic.List<string>();
        var parts = new System.Collections.Generic.List<string>();
        var ops = new System.Collections.Generic.List<string>();
        while (true)
        {
            var f = NextByOp(s, 1, ref op);
            if (f == "")
            {
                break;
            }

            raws.Add(Trim(f));
            if (Left(f, 1) == "(" && Right(f, 1) == ")" && ModConvertStatements.MatchParen(f, 0) == Len(f) - 1)
            {
                parts.Add("(" + ModConvert.ConvertValue(Mid(f, 2, Len(f) - 2)) + ")");
            }
            else if (TLMatch(f, "TypeOf "))
            {
                parts.Add(ModConvert.ConvertValue(Trim(Mid(Trim(f), 8))));
            }
            else
            {
                parts.Add(ConvertElement(f));
            }

            if (op == "" || op == null)
            {
                break;
            }
            ops.Add(Trim(op));
            s = Mid(s, Len(f) + Len(op) + 1);
            if (s == "")
            {
                break;
            }
        }
        while (ops.Count >= parts.Count && ops.Count > 0)
        {
            ops.RemoveAt(ops.Count - 1);
        }

        // operators that become calls or patterns bind their two neighbouring operands
        for (var pass = 0; pass < 2; pass++)
        {
            for (var i = 0; i < ops.Count; i++)
            {
                string folded = null;
                switch (ops[i])
                {
                    case "^" when pass == 0:
                        folded = "Pow(" + parts[i] + ", " + parts[i + 1] + ")";
                        break;
                    case "Like" when pass == 1: // Option Compare decides the case sensitivity
                        folded = "LikeOperator.LikeString(" + parts[i] + ", " + parts[i + 1] + ", CompareMethod." + (ModConvertStatements.OptionCompareText ? "Text" : "Binary") + ")";
                        break;
                    case "Is" when pass == 1 && TLMatch(raws[i], "TypeOf "):
                        folded = parts[i] + " is " + ConvertDataType(raws[i + 1]);
                        break;
                    case "Imp" when pass == 1:
                        folded = "(!(" + parts[i] + ") || (" + parts[i + 1] + "))";
                        break;
                }
                if (folded == null)
                {
                    continue;
                }
                parts[i] = folded;
                raws[i] = folded;
                parts.RemoveAt(i + 1);
                raws.RemoveAt(i + 1);
                ops.RemoveAt(i);
                i--;
            }
        }

        ModConvertStatements.PromoteCurrency(raws, parts);
        for (var i = 0; i < ops.Count; i++)
        {
            if (ops[i] == "/" && (i == 0 || ops[i - 1] != "/"))
            { // VB6 "/" always divides as Double; C# would divide integers as integers
                parts[i] = ModConvertStatements.AsDouble(raws[i], parts[i]);
            }
        }

        ModConvertStatements.FoldStringComparisons(raws, parts, ops);

        o = parts.Count > 0 ? parts[0] : "";
        for (var i = 0; i < ops.Count; i++)
        {
            // And/Or/Xor on an integer operand are bitwise in VB6
            // comparisons bind tighter than And / Or: a side holding one is Boolean, whatever its neighbouring operand
            var bitwise = !ComparisonBetweenLogicals(ops, i)
                          && (IsIntegerLiteral(raws[i]) || IsIntegerLiteral(raws[i + 1]) || ModConvertStatements.IsIntegralOperand(raws[i]) || ModConvertStatements.IsIntegralOperand(raws[i + 1]));
            switch (ops[i])
            {
                case "\\":
                    opN = " / ";
                    break;
                case "=":
                case "Is":
                case "Eqv":
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
                case "And":
                    opN = bitwise ? " & " : " && ";
                    break;
                case "Or":
                    opN = bitwise ? " | " : " || ";
                    break;
                case "Xor":
                    opN = " ^ ";
                    break;
                default:
                    opN = " " + ops[i] + " ";
                    break;
            }
            o = o + opN + parts[i + 1];
        }
        convertValue = o;
        return convertValue;
    }

    /// <summary>A comparison among the operators on either side of ops[i], up to the neighbouring logical operators.</summary>
    private static bool ComparisonBetweenLogicals(System.Collections.Generic.List<string> ops, int i)
    {
        bool IsLogical(string op) => op == "And" || op == "Or" || op == "Xor" || op == "Eqv" || op == "Imp";
        bool IsComparison(string op) => op == "=" || op == "<>" || op == "<" || op == ">" || op == "<=" || op == ">=" || op == "Is" || op == "Like";
        for (var k = i - 1; k >= 0 && !IsLogical(ops[k]); k--)
        {
            if (IsComparison(ops[k])) return true;
        }
        for (var k = i + 1; k < ops.Count && !IsLogical(ops[k]); k++)
        {
            if (IsComparison(ops[k])) return true;
        }
        return false;
    }

    /// <summary>An integer literal operand (decimal, &amp;H or &amp;O, optional type character).</summary>
    private static bool IsIntegerLiteral(string s)
    {
        s = Trim(s);
        return RegExTest(s, "^-?[0-9]+[%&]?$") || RegExTest(s, "^&[HhOo][0-9A-Fa-f]+[&%]?$");
    }

    public static string ConvertGlobals(string str, bool asModule = false)
    {
        var s = new string[0];


        var res = "";
        var building = "";
        str = Replace(str, vbLf, "");
        s = Split(str, vbCr);
        var n = 0;
        string x;
        //  Prg 0, UBound(S) - LBound(S) + 1, "Globals..."
        InitDeString();
        foreach (var iterL in s)
        {
            var l = iterL;
            l = DeComment(l);
            l = ModConvertStatements.EscapeKeywords(DeString(l)); // VB6 names that are C# keywords get a "_"
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
            else if ((x = ModConvertStatements.ApplyModuleOption(l)) != null)
            { // Option Base / Option Compare / DefType change how the rest of the file converts
                o = x;
            }
            else if (LMatch(l, "Option ") || LMatch(l, "Attribute ") || LMatch(l, "Rem "))
            {
                o = "// " + l;
            }
            else if (ModConvertStatements.IsDirective(l))
            {
                o = ModConvertStatements.ConvertDirective(l);
            }
            else if (LMatch(l, "Implements "))
            {
                o = ModConvertClasses.Current != null ? "// VB6 " + l + " (explicit interface implementation)" : "// TODO: VB6 " + l + " (only class modules implement interfaces)";
            }
            else if (RegExTest(l, "^(Public |Private |)Declare "))
            {
                o = ConvertApiDef(l);
            }
            else if (RegExTest(l, "^(Global |Public |Private |)Const "))
            {
                o = TrimCrLf(ConvertConstants(l, true));
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
            else if (TLeft(l, 8) == "Private " || TLeft(l, 7) == "Public " || TLeft(l, 7) == "Global " || TLeft(l, 4) == "Dim ")
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

    /// <summary>1-based position of the "=" of an assignment statement ("[Set ]target = value"), or 0.</summary>
    /// <remarks>The target may index with several arguments ("grid(i, j) = x"): only spaces outside parentheses end it.</remarks>
    public static int AssignmentPos(string s)
    {
        var t = Trim(s);
        var offset = Len(s) - Len(LTrim(s));
        if (LMatch(t, "Set "))
        {
            t = Mid(t, 5);
            offset = offset + 4;
        }
        if (!RegExTest(t, "^[a-zA-Z_.]"))
        {
            return 0;
        }
        var depth = 0;
        for (var i = 0; i < t.Length; i++)
        {
            var c = t[i];
            if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth--;
            }
            else if (depth == 0 && c == ' ')
            {
                return string.CompareOrdinal(t, i, " = ", 0, 3) == 0 ? offset + i + 2 : 0;
            }
            else if (depth == 0 && !(char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '!'))
            {
                return 0;
            }
        }
        return 0;
    }

    private static string TrimCrLf(string s)
    {
        while (Right(s, 2) == vbCrLf)
        {
            s = Left(s, Len(s) - 2);
        }
        return s;
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

        var T = AssignmentPos(s);
        if (T > 0)
        {
            // Assignment
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

            var lhs = ConvertValue(convertCodeLine);
            convertCodeLine = lhs + " = ";

            var rhs = Trim(Mid(s, T + 1));
            b = ConvertValue(rhs);
            // the target's VB6 type decides the conversion VB6 applies implicitly (a variable, or an element of an array)
            var target = SubParam(RegExNMatch(a, "^" + patToken));
            if (target.name != "" && RegExTest(a, "^" + patToken + "(\\(.*\\))?$") && (!IsInStr(a, "(") || target.asArray != ""))
            {
                b = target.fixedLen != "" && !IsInStr(a, "(") ? "FixedLen(" + b + ", " + target.fixedLen + ")" : ModConvertStatements.ImplicitConversion(target.asType, rhs, b);
            }
            convertCodeLine = convertCodeLine + b;
            if (rhs == "Nothing" && TLeft(s, 4) == "Set " && ModConvertPragmas.AutoDispose != "No"
                && (ModConvertPragmas.AutoDispose == "Force" || ModConvertClasses.HasTerminate(target.asType)))
            { // pragma AutoDispose: releasing the reference disposes the object (VB6 Class_Terminate)
                convertCodeLine = "(" + lhs + " as IDisposable)?.Dispose(); " + convertCodeLine;
            }
            var setter = System.Text.RegularExpressions.Regex.Match(lhs, "^(.*?)([a-zA-Z_][a-zA-Z_0-9]*)\\((.*)\\)$");
            if (setter.Success && ModConvertClasses.IsParameterizedSetter(setter.Groups[2].Value) && SubParam(setter.Groups[2].Value).asArray == "")
            { // a property with parameters: obj.Name(i) = v -> obj.set_Name(i, v)
                convertCodeLine = setter.Groups[1].Value + "set_" + setter.Groups[2].Value + "(" + setter.Groups[3].Value + ", " + b + ")";
            }
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
                convertCodeLine = ModConvertStatements.ConvertDebugPrint(rest);
            }
            else if (firstWord == "Debug.Assert")
            {
                convertCodeLine = "System.Diagnostics.Debug.Assert(" + ConvertValue(rest) + ")";
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
                    var arg = IsFuncRef(firstWord) && !FuncRefArgByRef(firstWord, n)
                        ? ModConvertStatements.ImplicitConversion(Trim(SplitWord(FuncRefArgType(firstWord, n), 1, "=")), b, ConvertValue(b))
                        : ConvertValue(b);
                    convertCodeLine = convertCodeLine + IIf(n == 1, "", ", ") + arg;
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

    /// <summary>A name VB6 resolves without a local declaration: procedure, enum, form, module, control, project global, property.</summary>
    private static bool IsKnownName(string name)
    {
        return name == "" || name == "Me" || name == currSub || IsFuncRef(name) || IsEnumRef(name) || IsFormRef(name) || IsModuleRef(name)
               || IsControlRef(name, formName) || ModConvertStatements.IsProjectGlobal(name) || IsPropertyName(name);
    }

    /// <summary>A loop or Select Case open in the procedure being converted.</summary>
    private sealed class Breakable
    {
        public string Kind = "";
        public string Label = "";
    }

    /// <summary>Lines that end the innermost open block (or one of its sections).</summary>
    private static bool IsBlockCloser(string t)
    {
        return t == "Else" || LMatch(t, "ElseIf ") || t == "End If" || t == "Case Else" || LMatch(t, "Case ") || t == "End Select"
               || t == "Loop" || LMatch(t, "Loop ") || t == "Next" || LMatch(t, "Next ") || t == "Wend" || t == "End With"
               || t == "End Sub" || t == "End Function" || t == "End Property";
    }

    /// <summary>Statements an On Error GoTo handler protects (declarations are hoisted, labels and error statements stay outside).</summary>
    private static bool IsProtectable(string t)
    {
        return !IsBlockCloser(t) && !RegExTest(t, "^[a-zA-Z_][a-zA-Z_0-9]*:$") && !LMatch(t, "Dim ") && !LMatch(t, "Static ") && !LMatch(t, "Const ")
               && !LMatch(t, "On Error ") && !LMatch(t, "On Local Error ") && !LMatch(t, "Attribute ") && !ModConvertStatements.IsDirective(t);
    }

    public static string ConvertSub(string str, bool asModule = false, vbTriState scanFirst = vbTriState.vbUseDefault)
    {
        var convertSub = "";

        var s = new string[0];
        var T = "";
        var u = "";
        var v = "";

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

        var decls = ""; // VB6 locals are procedure-scoped and initialized on entry: declarations are hoisted
        var declPos = 0;
        var fields = ""; // Static locals keep their value between calls: they become fields
        var statics = new System.Collections.Generic.Dictionary<string, string>();
        var staticProc = false;
        var hadPrototype = false;
        var errs = new ModConvertStatements.ErrorScope();
        var selects = new System.Collections.Generic.Stack<bool>(); // per open Select Case: a case section is open
        // open loops and switches, innermost last: a C# break leaves only the innermost one
        var breakables = new System.Collections.Generic.List<Breakable>();
        var exitLabels = 0;
        var ppBranches = new System.Collections.Generic.Stack<int[]>(); // per open #If: indent, selects, breakables at the #If

        void OpenBreakable(string kind)
        {
            breakables.Add(new Breakable { Kind = kind });
        }

        // closes the innermost block; a loop left with goto gets its exit label after the closing brace
        string CloseBreakable()
        {
            if (breakables.Count == 0)
            {
                return "";
            }
            var b = breakables[breakables.Count - 1];
            breakables.RemoveAt(breakables.Count - 1);
            return b.Label == "" ? "" : vbCrLf + SSpace(ind) + b.Label + ":;";
        }

        string ExitLoop(string kind)
        {
            for (var k = breakables.Count - 1; k >= 0; k--)
            {
                if (breakables[k].Kind != kind)
                {
                    continue;
                }
                if (k == breakables.Count - 1)
                {
                    return "break;";
                }
                if (breakables[k].Label == "")
                {
                    exitLabels = exitLabels + 1;
                    breakables[k].Label = "exit" + kind + exitLabels;
                }
                return "goto " + breakables[k].Label + ";";
            }
            return "break;";
        }

        // GoSub routines and resumable handlers become local functions; line numbers feed Erl
        var plan = ModConvertStatements.ProcedurePlan.Scan(s);
        errs.Erl = plan.HasLineNumbers && plan.HasErrorHandling;
        if (errs.Erl)
        {
            decls = decls + SSpace(spIndent) + "int vbErl = 0; // VB6 Erl: the last line number passed" + vbCrLf;
        }
        if (scanFirst == vbTriState.vbFalse && !ModConvertStatements.OptionExplicit)
        { // no Option Explicit: names assigned without a declaration are implicit locals
            foreach (var name in UnknownAssigned())
            {
                if (IsKnownName(name))
                {
                    continue;
                }
                var implicitType = ModConvertStatements.ImplicitType(name);
                decls = decls + SSpace(spIndent) + ConvertDataType(implicitType) + " " + name + " = " + ModConvertStatements.DefaultValue(implicitType) + "; // VB6 implicit declaration" + vbCrLf;
            }
        }
        string route = null; // label of the GoSub routine / resumable handler being collected
        var routeBody = "";
        var routeSavedInd = 0;
        var routeSavedMode = ModConvertStatements.ErrorScope.Modes.None;
        var routeSavedResumable = false;
        var routes = new System.Collections.Generic.List<string[]>();
        var retries = 0;

        void StartRoute(string label)
        {
            route = label;
            routeBody = "";
            routeSavedInd = ind;
            routeSavedMode = errs.Mode;
            routeSavedResumable = errs.Resumable;
            errs.Mode = ModConvertStatements.ErrorScope.Modes.None; // a handler runs unprotected
            errs.Resumable = false;
            ind = (hadPrototype ? spIndent : 0) + spIndent;
        }

        void EndRoute()
        {
            if (route == null)
            {
                return;
            }
            routes.Add(new[] { route, routeBody });
            route = null;
            ind = routeSavedInd;
            errs.Mode = routeSavedMode;
            errs.Resumable = routeSavedResumable;
        }

        string LocalFunctions()
        {
            var fi = SSpace(hadPrototype ? spIndent : 0);
            var r = "";
            foreach (var fn in routes)
            {
                var handler = plan.Handlers.ContainsKey(fn[0]);
                r = r + fi + (handler ? "int " + ModConvertStatements.HandlerFunction(fn[0]) : "void " + fn[0]) + "() {" + vbCrLf + fn[1]
                    + (handler ? fi + SSpace(spIndent) + "return -1; // end of the handler: leave the procedure" + vbCrLf : "") + fi + "}" + vbCrLf;
            }
            routes.Clear();
            return r;
        }

        var pp = "^" + ProcModifiers + "(Function |Sub )" + patToken + "[$%&!#@]?[ ]*\\(";
        var pq = "^" + ProcModifiers + "(Property )(Get |Let |Set )" + patToken + "[$%&!#@]?[ ]*\\(";

        // pragmas inside the procedure act from their line to the procedure end
        var savedForceZero = ModConvertStatements.ForceZeroBounds;
        var savedAutoNew = ModConvertStatements.AutoNew;
        var outputOff = false;
        var parseOff = false;
        string replaceNext = null;

        void Emit(string text)
        {
            if (route != null)
            {
                routeBody = routeBody + text + IIf(text == "", "", vbCrLf);
            }
            else
            {
                res = res + text + IIf(text == "", "", vbCrLf);
            }
        }

        //If IsInStr(Str, " WinCDSDataPath(") Then Stop
        //If IsInStr(Str, " RunShellExecute(") Then Stop
        //If IsInStr(Str, " ValidateSI(") Then Stop
        foreach (var iterL in s)
        {
            var l = iterL;
            //If IsInStr(L, "OrdVoid") Then Stop
            //If IsInStr(L, "MsgBox") Then Stop
            //If IsInStr(L, "And Not IsDoddsLtd Then") Then Stop
            var pragma = ModConvertPragmas.Parse(l);
            if (pragma != null)
            { // VB Migration Partner pragma
                switch (pragma.Name)
                {
                    case "OutputMode":
                        outputOff = pragma.Args == "Off";
                        break;
                    case "ParseMode":
                        parseOff = pragma.Args == "Off";
                        break;
                    case "InsertStatement":
                        Emit(SSpace(ind) + pragma.Args);
                        break;
                    case "ReplaceStatement":
                        replaceNext = pragma.Args;
                        break;
                    case "Note":
                        Emit(SSpace(ind) + "// NOTE: " + pragma.Args);
                        break;
                    default:
                        if (!ModConvertPragmas.ApplySetting(pragma))
                        {
                            Emit(SSpace(ind) + "// TODO: VB Migration Partner pragma not supported: " + pragma.Name + " " + pragma.Args);
                        }
                        break;
                }
                continue;
            }
            var boundary = RegExNMatch(Trim(l), pp) != "" || RegExTest(Trim(l), "^End (Sub|Function|Property)$");
            if (outputOff && !boundary)
            { // OutputMode Off: left out of the C# code
                continue;
            }
            if (parseOff && !boundary)
            { // ParseMode Off: kept as VB6 in a comment
                Emit(SSpace(ind) + "// VB6: " + Trim(l));
                continue;
            }
            if (replaceNext != null && !boundary && Trim(DeComment(l, true)) != "")
            { // ReplaceStatement: the next statement is the given code
                Emit(SSpace(ind) + replaceNext);
                replaceNext = null;
                continue;
            }
            l = DeComment(l);
            l = ModConvertStatements.EscapeKeywords(DeString(l)); // VB6 names that are C# keywords get a "_"
            var o = "";
            var pre = "";
            var wrap = false; // a simple statement: guarded under On Error Resume Next
            var isProto = RegExNMatch(l, pp) != "";
            var isDecl = TLMatch(l, "Dim ") || TLMatch(l, "Static ") || TLMatch(l, "Const ");
            if (!isProto && !isDecl)
            {
                l = ModConvertStatements.StripTypeSuffixes(l);
            }
            if (TLMatch(l, "Let "))
            {
                l = TMid(l, 5);
            }
            var t = Trim(l);

            //If IsInStr(L, "1/1/2001") Then Stop
            //If ScanFirst = vbFalse Then Stop
            //If IsInStr(L, "Public Function GetFileAutonumber") Then Stop
            //If IsInStr(L, "GetCustomerBalance") Then Stop
            //If IsInStr(L, "IsIDE") Then Stop

            // On Error GoTo: the handler protects the statements through a try block, which can only span one C# block
            if (t != "" && !isProto)
            {
                if (errs.TryOpen && IsBlockCloser(t) && ind <= errs.TryInd + spIndent)
                {
                    pre = pre + errs.CloseTry(ref ind);
                }
                else if (errs.Mode == ModConvertStatements.ErrorScope.Modes.GoTo && !errs.Resumable && !errs.TryOpen && IsProtectable(t))
                {
                    pre = pre + errs.OpenTry(ref ind);
                }
            }

            string x;
            if (t == "")
            {
                o = "";
            }
            else if (ModConvertStatements.IsDirective(t))
            {
                // only one branch is compiled: each starts from the block nesting the #If saw
                if (LMatch(t, "#If "))
                {
                    ppBranches.Push(new[] { ind, selects.Count, breakables.Count });
                }
                else if ((LMatch(t, "#ElseIf ") || t == "#Else") && ppBranches.Count > 0)
                {
                    var at = ppBranches.Peek();
                    ind = at[0];
                    while (selects.Count > at[1])
                    {
                        selects.Pop();
                    }
                    if (breakables.Count > at[2])
                    {
                        breakables.RemoveRange(at[2], breakables.Count - at[2]);
                    }
                }
                else if (LMatch(t, "#End") && ppBranches.Count > 0)
                {
                    ppBranches.Pop();
                }
                o = SSpace(ind) + ModConvertStatements.ConvertDirective(t);
            }
            else if (isProto)
            {
                //      CurrSub = nextBy(L, "(", 1)
                //If IsInStr(L, "Public Function IsIn") Then Stop
                staticProc = RegExTest(l, "^(Public |Private |Friend |)(Friend |)Static ");
                hadPrototype = true;
                o = o + SSpace(ind) + ConvertPrototype(l, ref returnVariable, asModule, ref currSub);
                ind = ind + spIndent;
            }
            else if (RegExNMatch(l, pq) != "")
            {
                //      If IsInStr(L, "edi888_Admin888_Src") Then Stop
                AddProperty(str);
                return convertSub;// repacked later...  not added here.

            }
            else if (t == "End Sub" || t == "End Function")
            {
                EndRoute();
                pre = pre + errs.CloseTry(ref ind);
                if (returnVariable != "")
                {
                    o = o + SSpace(ind) + "return " + returnVariable + ";" + vbCrLf;
                }
                o = o + LocalFunctions();
                ind = ind - spIndent;
                o = o + SSpace(ind) + "}";
            }
            else if (route != null && (t == "Exit Function" || t == "Exit Sub" || t == "Exit Property"))
            { // leaving the procedure from a handler: the caller of the handler returns
                o = SSpace(ind) + (plan.Handlers.ContainsKey(route) ? "return -1;" : "return; // TODO: VB6 " + t + " inside a GoSub routine leaves only the routine here");
            }
            else if (route != null && plan.Handlers.ContainsKey(route) && (t == "Resume" || LMatch(t, "Resume ") || LMatch(t, "GoTo ")))
            { // the handler tells the failed statement how to go on
                var target = Trim(Mid(t, InStr(t + " ", " ") + 1));
                var code = target == "Next" ? 0 : target == "" || target == "0" ? 1 : plan.Handlers[route].ResumeLabels.IndexOf(ModConvertStatements.LabelName(target)) + 2;
                o = SSpace(ind) + (code >= 0 ? "return " + code + "; // VB6 " + t : "// TODO: VB6 " + t);
            }
            else if (route != null && plan.GoSubTargets.Contains(route) && t == "Return")
            {
                o = SSpace(ind) + "return;";
            }
            else if (LMatch(t, "GoSub ") && plan.GoSubTargets.Contains(ModConvertStatements.LabelName(Mid(t, 7))))
            {
                o = SSpace(ind) + ModConvertStatements.LabelName(Mid(t, 7)) + "();";
            }
            else if (t == "Exit Function" || t == "Exit Sub")
            {
                if (returnVariable != "")
                {
                    o = o + SSpace(ind) + "return " + returnVariable + ";";
                }
                else
                {
                    o = o + SSpace(ind) + "return;";
                }
            }
            else if (t == "Exit Property")
            {
                o = SSpace(ind) + ExitPropertyMark;
            }
            else if (RegExTest(t, "^[a-zA-Z_][a-zA-Z_0-9]*:$"))
            { // Goto Label: kept outside the protected block, so the handler (and Resume label) can jump to it
                var labelName = Left(t, Len(t) - 1);
                if (route != null && (plan.IsRouted(labelName) || plan.JumpTargets.Contains(labelName)))
                {
                    EndRoute();
                }
                if (errs.TryOpen && ind <= errs.TryInd + spIndent)
                {
                    pre = pre + errs.CloseTry(ref ind);
                }
                if (plan.IsRouted(labelName))
                { // the routine / handler code is collected into a local function
                    res = res + pre;
                    pre = "";
                    StartRoute(labelName);
                    continue;
                }
                if (errs.Mode == ModConvertStatements.ErrorScope.Modes.GoTo && labelName == errs.Handler)
                {
                    errs.Mode = ModConvertStatements.ErrorScope.Modes.None; // the handler itself runs unprotected
                }
                o = o + t + ";"; // c# requires a trailing ; on goto labels without trailing statements.  Likely a C# bug/oversight, but it's there.
                if (errs.Erl && RegExTest(labelName, "^L[0-9]+$"))
                {
                    o = o + " vbErl = " + Mid(labelName, 2) + ";";
                }
            }
            else if (LMatch(t, "GoTo "))
            {
                o = SSpace(ind) + "goto " + ModConvertStatements.LabelName(Mid(t, 6)) + ";";
            }
            else if (LMatch(t, "On Error ") || LMatch(t, "On Local Error "))
            {
                o = ModConvertStatements.ConvertOnError(t, errs, ref ind);
                errs.Resumable = errs.Mode == ModConvertStatements.ErrorScope.Modes.GoTo && plan.Handlers.ContainsKey(errs.Handler);
            }
            else if (LMatch(t, "On ") && (x = ModConvertStatements.ConvertOnGoTo(t, ind)) != null)
            {
                o = x;
            }
            else if (t == "Resume" || LMatch(t, "Resume "))
            {
                o = ModConvertStatements.ConvertResume(t, ind);
            }
            else if (LMatch(t, "Static ") && hadPrototype || LMatch(t, "Dim ") && staticProc && hadPrototype)
            {
                // a field named after the procedure; references in the body are renamed
                var rest = Trim(Mid(t, InStr(t, " ") + 1));
                var field = ConvertDeclare("Private " + rest, 0, true, asModule);
                foreach (var item in ModConvertStatements.SplitTopLevel(rest))
                {
                    var name = RegExNMatch(item, "^" + patToken);
                    if (name == "")
                    {
                        continue;
                    }
                    statics[name] = currSub + "_" + name;
                    field = System.Text.RegularExpressions.Regex.Replace(field, "(?<![A-Za-z0-9_.])" + name + "(?![A-Za-z0-9_])", currSub + "_" + name);
                }
                fields = fields + field;
            }
            else if (LMatch(t, "Dim ") || LMatch(t, "Static "))
            {
                decls = decls + (LMatch(t, "Static ") ? SSpace(spIndent) + "// TODO: VB6 Static local (value not kept between calls)" + vbCrLf : "")
                        + ConvertDeclare("Dim " + Trim(Mid(t, InStr(t, " ") + 1)), spIndent);
            }
            else if (LMatch(t, "Const "))
            {
                decls = decls + ConvertConstants(t, false, spIndent);
            }
            else if (TLeft(l, 3) == "If ")
            { // Code sanitization prevents all single-line ifs.
                //If IsInStr(L, "optDelivered") Then Stop
                //If IsInStr(L, "PRFolder") Then Stop
                T = Mid(t, 4, Len(t) - 8);
                o = SSpace(ind) + "if (" + ModConvertStatements.ConditionValue(T) + ") {";
                ind = ind + spIndent;
            }
            else if (TLeft(l, 7) == "ElseIf ")
            {
                T = TMid(l, 8);
                if (Right(t, 5) == " Then")
                {
                    T = Left(T, Len(T) - 5);
                }
                o = SSpace(ind - spIndent) + "} else if (" + ModConvertStatements.ConditionValue(T) + ") {";
            }
            else if (t == "Else")
            {
                o = SSpace(ind - spIndent) + "} else {";
            }
            else if (t == "End If")
            {
                ind = ind - spIndent;
                o = SSpace(ind) + "}";
            }
            else if (LMatch(t, "Select Case "))
            {
                o = o + SSpace(ind) + "switch (" + ConvertValue(Mid(t, 13)) + ") {";
                selects.Push(false);
                OpenBreakable("Select");
                ind = ind + spIndent;
            }
            else if (t == "End Select")
            {
                if (selects.Count > 0 && selects.Pop())
                {
                    o = o + SSpace(ind) + "break;" + vbCrLf;
                    ind = ind - spIndent;
                }
                ind = ind - spIndent;
                o = o + SSpace(ind) + "}" + CloseBreakable();
            }
            else if (t == "Case Else" || LMatch(t, "Case "))
            {
                if (selects.Count > 0 && selects.Peek())
                { // C# sections cannot fall through
                    o = o + SSpace(ind) + "break;" + vbCrLf;
                    ind = ind - spIndent;
                }
                if (selects.Count > 0)
                {
                    selects.Pop();
                    selects.Push(true);
                }
                o = o + SSpace(ind) + (t == "Case Else" ? "default:" : ModConvertStatements.ConvertCaseLabels(Mid(t, 6)));
                ind = ind + spIndent;
            }
            else if (t == "Do")
            {
                o = o + SSpace(ind) + "do {";
                OpenBreakable("Do");
                ind = ind + spIndent;
            }
            else if (LMatch(t, "Do While "))
            {
                o = o + SSpace(ind) + "while(" + ModConvertStatements.ConditionValue(Mid(t, 10)) + ") {";
                OpenBreakable("Do");
                ind = ind + spIndent;
            }
            else if (LMatch(t, "Do Until "))
            {
                o = o + SSpace(ind) + "while(!(" + ModConvertStatements.ConditionValue(Mid(t, 10)) + ")) {";
                OpenBreakable("Do");
                ind = ind + spIndent;
            }
            else if (LMatch(t, "While "))
            {
                o = o + SSpace(ind) + "while(" + ModConvertStatements.ConditionValue(Mid(t, 7)) + ") {";
                OpenBreakable("While");
                ind = ind + spIndent;
            }
            else if (t == "Wend")
            {
                ind = ind - spIndent;
                o = o + SSpace(ind) + "}" + CloseBreakable();
            }
            else if (LMatch(t, "For Each "))
            {
                l = Mid(t, 10);

                var iterVar = SplitWord(l, 1, " In ");
                o = o + SSpace(ind) + "foreach(var iter" + iterVar + " in " + ConvertValue(SplitWord(l, 2, " In ", true, true)) + ") {" + vbCrLf + SSpace(ind + spIndent) + iterVar + " = iter" + iterVar + ";";
                SubParamAssign(iterVar);
                OpenBreakable("For");
                ind = ind + spIndent;
            }
            else if (LMatch(t, "For "))
            {
                l = Mid(t, 5);
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
                var fe = ConvertValue(forEnd);
                SubParamAssign(Trim(forKey));
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
                o = o + SSpace(ind) + "for(" + fk + "=" + ConvertValue(forStr) + "; " + forCond + "; " + forIncr + ") {";
                OpenBreakable("For");
                ind = ind + spIndent;
            }
            else if (LMatch(t, "Loop While "))
            {
                ind = ind - spIndent;
                o = o + SSpace(ind) + "} while(" + ModConvertStatements.ConditionValue(Mid(t, 12)) + ");" + CloseBreakable(); // was negated like Loop Until
            }
            else if (LMatch(t, "Loop Until "))
            {
                ind = ind - spIndent;
                o = o + SSpace(ind) + "} while(!(" + ModConvertStatements.ConditionValue(Mid(t, 12)) + "));" + CloseBreakable();
            }
            else if (t == "Loop")
            {
                ind = ind - spIndent;
                o = o + SSpace(ind) + "}" + CloseBreakable();
            }
            else if (t == "Exit For" || t == "Exit Do" || t == "Exit While")
            {
                o = o + SSpace(ind) + ExitLoop(Mid(t, 6));
            }
            else if (t == "Next" || LMatch(t, "Next "))
            { // "Next j, i" closes two loops
                var loops = t == "Next" ? 1 : ModConvertStatements.SplitTopLevel(Mid(t, 6)).Count;
                for (var k = 0; k < loops; k++)
                {
                    ind = ind - spIndent;
                    o = o + (k == 0 ? "" : vbCrLf) + SSpace(ind) + "}" + CloseBreakable();
                }
            }
            else if (LMatch(t, "With "))
            {
                withLevel = withLevel + 1;

                T = ConvertValue(Mid(t, 6));
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
            else if (t == "End With")
            {
                withLevel = withLevel - 1;
                T = Stack(ref withAssign);
                u = Stack(ref withTypes);
                v = Stack(ref withVars);
                ind = ind - spIndent;
                if (SubParam(T).name != "")
                {
                    o = o + SSpace(ind) + T + " = " + v + ";";
                }
            }
            else if ((x = ModConvertStatements.ConvertSimpleStatement(t, ind)) != null)
            {
                o = x;
                wrap = LMatch(t, "Error ") || LMatch(t, "Date") || LMatch(t, "Time");
            }
            else if (LMatch(t, "ReDim "))
            {
                o = ModConvertStatements.ConvertReDim(t, ind);
                wrap = true;
            }
            else if (LMatch(t, "Erase "))
            {
                o = ModConvertStatements.ConvertErase(t, ind);
                wrap = true;
            }
            else if (t == "Rem" || LMatch(t, "Rem "))
            {
                o = SSpace(ind) + "// " + Trim(Mid(t, 4));
            }
            else if ((x = ModConvertStatements.ConvertMidStatement(t, ind) ?? ModConvertStatements.ConvertLRSet(t, ind) ?? ModConvertStatements.ConvertFileStatement(t, ind)
                          ?? ModConvertStatements.ConvertGraphicsStatement(t, ind)) != null)
            {
                o = x;
                wrap = true;
            }
            else
            {
                //If IsInStr(L, "ComputeAgeing dtpArrearControlDate") Then Stop
                //If IsInStr(L, "RaiseEvent") Then Stop
                //If IsInStr(L, "Debug.Print") Then Stop
                //If IsInStr(L, "HasGit") Then Stop
                o = SSpace(ind) + ConvertCodeLine(l);
                wrap = true;
            }

            if (wrap && errs.Mode == ModConvertStatements.ErrorScope.Modes.ResumeNext && Trim(o) != "")
            {
                o = ModConvertStatements.WrapResumeNext(o, ind, errs);
            }
            else if (wrap && errs.Mode == ModConvertStatements.ErrorScope.Modes.GoTo && errs.Resumable && Trim(o) != "")
            {
                var leave = returnVariable != "" ? "return " + returnVariable + ";" : hadPrototype ? "return;" : ExitPropertyMark;
                o = ModConvertStatements.WrapResumable(o, ind, errs, plan, leave, ref retries);
            }
            o = pre + o;
            foreach (var st in statics)
            {
                o = System.Text.RegularExpressions.Regex.Replace(o, "(?<![A-Za-z0-9_.])" + st.Key + "(?![A-Za-z0-9_])", st.Value);
            }

            o = ModConvert.PostConvertCodeLine(o);
            o = ModProjectSpecific.ProjectSpecificPostCodeLineConvert(o);

            o = ReComment(o);
            Emit(ReComment(o));
            if (isProto)
            {
                declPos = Len(res);
            }
        }
        EndRoute();
        res = res + LocalFunctions(); // a property body has no End Sub
        ModConvertStatements.ForceZeroBounds = savedForceZero;
        ModConvertStatements.AutoNew = savedAutoNew;

        if (decls != "")
        {
            res = Left(res, declPos) + decls + Mid(res, declPos + 1);
        }
        convertSub = fields + res;
        return convertSub;
    }
}
