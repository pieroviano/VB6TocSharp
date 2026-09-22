using System;
using System.Collections.Generic;
using Microsoft.VisualBasic;
using static Microsoft.VisualBasic.Constants;
using static Microsoft.VisualBasic.DateAndTime;
using static Microsoft.VisualBasic.FileSystem;
using static Microsoft.VisualBasic.Information;
using static Microsoft.VisualBasic.Interaction;
using static Microsoft.VisualBasic.Strings;
using static Vb6ToCSharp.Modules.ModProjectFiles;
using static Vb6ToCSharp.Modules.ModRegEx;
using static Vb6ToCSharp.VbExtension;


namespace Vb6ToCSharp.Modules;

static class ModQuickLint
{
    // Option Explicit
    private const int idnt = 2;
    private const int maxErrors = 50;
    private const string attr = "Attribute";
    private const string q = "\"";
    private const string a = "'";
    private const string s = " ";
    private const string lintKey = "'@NO-LINT";
    private const string tyAllty = "AllTy";
    private const string tyError = "Error";
    private const string tyIndnt = "Indnt";
    private const string tyArgna = "ArgNa";
    private const string tyArgty = "ArgTy";
    private const string tyFspna = "FSPNa";
    private const string tyDepre = "Depre";
    private const string tyMigra = "Migra";
    private const string tyStyle = "Style";
    private const string tyBlank = "Blank";
    private const string tyExpli = "Expli";
    private const string tyCompa = "Compa";
    private const string tyTypec = "TypeC";
    private const string tyNotyp = "NoTyp";
    private const string tyByrfv = "ByReV";
    private const string tyPripu = "PriPu";
    private const string tyFncre = "FncRe";
    private const string tyCorre = "Corre";
    private const string tyGosub = "GoSub";
    private const string tyCstop = "CStop";
    private const string tyOpdef = "OpDef";
    private const string tyDfctl = "DfCtl";
    public static string errorPrefix = "";
    public static string errorIgnore = "";


    public static dynamic ErrorTypes()
    {
        dynamic errorTypes = new[]{ tyAllty, tyError, tyIndnt, tyArgna, tyArgty, tyFspna, tyDepre, tyMigra, tyStyle,
            tyBlank, tyExpli, tyCompa, tyTypec, tyNotyp, tyByrfv, tyPripu, tyFncre, tyCorre, tyGosub,
            tyCstop, tyOpdef, tyDfctl };
        return errorTypes;
    }

    public static string Lint(string fileName = "", bool alertUnused = true)
    {
        if (fileName == "")
        {
            fileName = "prj.vbp";
        }
        if (InStr(fileName, "\\") == 0)
        {
            fileName = AppDomain.CurrentDomain.BaseDirectory + "\\" + fileName;
        }
        var fileList = IIf(Right(fileName, 4) == ".vbp", VbpCode(fileName), fileName);

        var lint = QuickLintFiles(fileList);
        return lint;
    }

    public static string QuickLintFiles(string listUnused)
    {
        string quickLintFiles = "";
        const int lintDotsPerRow = 50;

        int x = 0;

        DateTime startTime = DateTime.MinValue;

        startTime = DateTime.Now; ;

        foreach (var iterL in Split(listUnused, vbCrLf))
        {
            dynamic l = iterL;

            string result = QuickLintFile(l);
            if (result != "")
            {
                Console.WriteLine(vbCrLf + "Done (" + DateDiff("s", startTime, DateTime.Now) +"s).  To re-run for failing file, hit enter on the line below:");
                string s = "LINT FAILED: " + l + vbCrLf + result + vbCrLf + "?Lint(\"" + l + "\")";
                quickLintFiles = s;
                return quickLintFiles;

            }
            else
            {
                Console.Write(Switch(Right(l, 3) == "frm", "o", Right(l, 3) == "cls", "x", true, "."));
            }
            x = x + 1;
            if (x >= lintDotsPerRow)
            {
                x = 0;
                Console.WriteLine();
            }
            DoEvents();
        }
        Console.WriteLine(vbCrLf + "Done (" + DateDiff("s", startTime, DateTime.Now) +"s).");
        quickLintFiles = "";
        return quickLintFiles;
    }

    public static string QuickLintFile(string file)
    {
        string quickLintFile = "";
        if (InStr(file, "\\") == 0)
        {
            file = AppDomain.CurrentDomain.BaseDirectory + "\\" + file;
        }

        var fName = Mid(file, InStrRev(file, "\\") + 1);
        var checkName = Replace(Replace(Replace(fName, ".bas", ""), ".cls", ""), ".frm", "");
        errorPrefix = Right(Space(18) + fName, 18) + " ";
        var contents = ReadEntireFile(file);
        var givenName = RegExNMatch(contents, "Attribute VB_Name = \"([^\"]+)\"", 0);
        givenName = Replace(Replace(givenName, "Attribute VB_Name = ", ""), "\"", "");
        if (checkName != givenName)
        {
            quickLintFile = "Module name [" + givenName + "] must match file name [" + fName + "].  Rename module or class to match the other";
            return quickLintFile;

        }
        quickLintFile = QuickLintContents(contents);
        return quickLintFile;
    }

    public static string QuickLintContents(string contents)
    {
        List<string> lines = new List<string> { }; // TODO - Specified Minimum Array Boundary Not Supported:   Dim Lines() As String, LL As Variant, L As String

        // TODO (not supported): On Error GoTo LintError
        errorIgnore = "";
        lines.AddRange(Split(Replace(contents, vbCr, ""), vbLf));

        bool inAttributes = false;
        bool inBody = false;


        string multiLine = "";

        int lineN = 0;

        string errors = "";
        int errorCount = 0;

        int blankLineCount = 0;

        Collection options = new Collection();


        var indent = 0;

        TestDefaultControlNames(ref errors, ref errorCount, 0, contents);


        foreach (var iterLl in lines)
        {
            var ll = iterLl; // TODO - Specified Minimum Array Boundary Not Supported:   Dim Lines() As String, LL As Variant, L As String
            if (errorCount >= maxErrors)
            {
                break;
            }

            if (Right(ll, 2) == " _")
            {
                var portion = Left(ll, Len(ll) - 2);
                if (multiLine != "")
                {
                    portion = Trim(portion);
                }
                multiLine = multiLine + portion;
                lineN = lineN + 1;
                goto NextLine;
            }
            else if (multiLine != "")
            {
                ll = multiLine + Trim(ll);
                multiLine = "";
            }

            TestBlankLines(ref errors, ref errorCount, lineN, ll, ref blankLineCount);
            TestLintControl(ll);
            var l = CleanLine(ll); // TODO - Specified Minimum Array Boundary Not Supported:   Dim Lines() As String, LL As Variant, L As String

            if (!inBody)
            {
                var isAttribute = Left(l, 10) == "Attribute ";
                if (!inAttributes && isAttribute)
                {
                    inAttributes = true;
                    goto NextLine;
                }
                else if (inAttributes && !isAttribute)
                {
                    inAttributes = false;
                    inBody = true;
                    lineN = 0;
                }
                else
                {
                    goto NextLine;
                }
            }

            lineN = lineN + 1;
            //If LineN = 15 Then Stop

            bool unindentedAlready = false;

            if (RegExTest(l, "^Option "))
            {
                options.Add("true", Replace(l, "Options ", ""));
            }
            else if (RegExTest(l, "^[ ]*(Else|ElseIf .* Then)$"))
            {
                indent = indent - idnt;
            }
            else if (RegExTest(l, "^[ ]*End Select$"))
            {
                indent = indent - idnt - idnt;
            }
            else if (RegExTest(l, "^[ ]*(End (If|Function|Sub|Property|Enum|Type)|Next( .*)?|Wend|Loop|Loop (While .*|Until .*))$"))
            {
                indent = indent - idnt;
                unindentedAlready = true;
            }
            else
            {
                unindentedAlready = false;
            }

            var lineIndent = 0;
            while (Mid(RTrim(l), lineIndent + 1, 1) == s)
            {
                lineIndent = lineIndent + 1;
            }
            TestIndent(ref errors, ref errorCount, lineN, l, lineIndent, IIf(!RegExTest(l, "^[ ]*Case "), indent, indent - idnt));

            var statements = Split(l, ": "); // TODO - Specified Minimum Array Boundary Not Supported:     Dim Statements() As String, SS As Variant, St As String
            foreach (var iterSs in statements)
            {
                var ss = iterSs; // TODO - Specified Minimum Array Boundary Not Supported:     Dim Statements() As String, SS As Variant, St As String
                var st = ss; // TODO - Specified Minimum Array Boundary Not Supported:     Dim Statements() As String, SS As Variant, St As String

                if (RegExTest(l, "^[ ]*(Else|ElseIf .* Then)$"))
                {
                    indent = indent + idnt;
                }
                else if (RegExTest(st, "^[ ]*(End (If|Function|Sub|Property)|Next|Wend|Loop|Loop .*|Enum|Type|Select)$"))
                {
                    if (!unindentedAlready)
                    {
                        indent = indent - idnt;
                    }
                }
                else if (RegExTest(st, "^[ ]*If "))
                {
                    if (!RegExTest(st, "Then "))
                    {
                        indent = indent + idnt;
                    }
                }
                else if (RegExTest(st, "^[ ]*For "))
                {
                    if (!RegExTest(st, " Next"))
                    {
                        indent = indent + idnt;
                    }
                }
                else if (RegExTest(st, "^[ ]*Next$"))
                {
                    indent = indent - idnt;
                }
                else if (RegExTest(st, "^[ ]*Next [a-zA-Z_][a-zA-Z0-9_]*$"))
                {
                    RecordError(ref errors, ref errorCount, tyStyle, lineN, "Remove variable from NEXT statement");
                    indent = indent - idnt;
                }
                else if (RegExTest(st, "^[ ]*While "))
                {
                    RecordError(ref errors, ref errorCount, tyStyle, lineN, "Use Do While/Until...Loop in place of While...Wend");
                    if (!RegExTest(st, " Wend$"))
                    {
                        indent = indent + idnt;
                    }
                }
                else if (RegExTest(st, "^[ ]*Do (While|Until)"))
                {
                    if (!RegExTest(st, ": Loop"))
                    {
                        indent = indent + idnt;
                    }
                }
                else if (RegExTest(st, "^[ ]*Loop$"))
                {
                }
                else if (RegExTest(st, "^[ ]*Do$"))
                {
                    indent = indent + idnt;
                }
                else if (RegExTest(st, "^[ ]*Loop While"))
                {
                    indent = indent - idnt;
                }
                else if (RegExTest(st, "^[ ]*Select Case "))
                {
                    indent = indent + idnt + idnt;
                }
                else if (RegExTest(st, "^[ ]*With "))
                {
                    RecordError(ref errors, ref errorCount, tyMigra, lineN, "Remove all uses of WITH.  No migration path exists.");
                }
                else if (RegExTest(st, "^[ ]*(Private |Public )?Declare (Function |Sub )"))
                {
                    // External Api
                }
                else if (RegExTest(st, "^((Private|Public|Friend) )?Function "))
                {
                    if (!RegExTest(st, ": End Function"))
                    {
                        indent = indent + idnt;
                    }
                    TestSignature(ref errors, ref errorCount, lineN, st);
                }
                else if (RegExTest(st, "^((Private|Public|Friend) )?Sub "))
                {
                    if (!RegExTest(st, ": End Sub"))
                    {
                        indent = indent + idnt;
                    }
                    TestSignature(ref errors, ref errorCount, lineN, st);
                }
                else if (RegExTest(st, "^((Private|Public|Friend) )?Property (Get|Let|Set) "))
                {
                    if (!RegExTest(st, ": End Property"))
                    {
                        indent = indent + idnt;
                    }
                    TestSignature(ref errors, ref errorCount, lineN, st);
                }
                else if (RegExTest(st, "^[ ]*(Public |Private )?(Enum |Type )"))
                {
                    indent = indent + idnt;
                }
                else if (RegExTest(st, "^[ ]*(Public |Private )?Declare "))
                {
                    indent = indent + idnt;
                }
                else if (RegExTest(st, "^[ ]*(Dim|Private|Public|Const|Global) "))
                {
                    TestDeclaration(ref errors, ref errorCount, lineN, st, false);
                }
                else
                {
                    TestCodeLine(ref errors, ref errorCount, lineN, st);
                }
            }
            NextLine:;
        }

        TestModuleOptions(ref errors, ref errorCount, options);

        var quickLintContents = errors;
        return quickLintContents;
    }

    private static string ReadEntireFile(string tFileName)
    {
        // TODO (not supported): On Error Resume Next

        var mFso = CreateObject("Scripting.FileSystemObject");
        string readEntireFile = mFso.OpenTextFile(tFileName, 1).ReadAll;

        if (FileLen(tFileName) / 10 != Len(readEntireFile) / 10)
        {
            MsgBox("ReadEntireFile was short: " + FileLen(tFileName) + " vs " + Len(readEntireFile));
        }
        return readEntireFile;
    }

    public static string CleanLine(string line)
    {
        int x = 0;

        while (true)
        {
            x = InStr(line, q);
            if (x == 0)
            {
                break;
            }

            var y = InStr(x + 1, line, q);
            while (Mid(line, y + 1, 1) == q)
            {
                y = InStr(y + 2, line, q);
            }

            if (y == 0)
            {
                break;
            }
            line = Left(line, x - 1) + new String('S',y - x + 1) + Mid(line, y + 1);
        }

        x = InStr(line, a);
        if (x > 0)
        {
            line = RTrim(Left(line, x - 1));
        }

        var cleanLine = line;
        return cleanLine;
    }

    public static void RecordError(ref string errors, ref int errorCount, string typ, int lineN, string error)
    {
        if (InStr(errorIgnore, UCase(typ)) > 0 || InStr(errorIgnore, tyAllty) > 0)
        {
            return;

        }

        if (Len(errors) != 0)
        {
            errors = errors + vbCrLf;
        }
        if (InStr(Join(ErrorTypes(), ","), typ) == 0)
        {
            errors = errors + errorPrefix + "[" + tyError + "] Line " + Right(Space(5) + lineN, 5) + ": Unknown error type in linter (add to ErrorTypes): " + typ;
        }
        errors = errors + errorPrefix + "[" + Right(Space(5) + typ, 5) + "] Line " + Right(Space(5) + lineN, 5) + ": " + error;
        errorCount = errorCount + 1;
    }

    public static bool StartsWith(string l, string find)
    {
        var startsWith = Left(l, Len(find)) == find;
        return startsWith;
    }

    public static string StripLeft(string l, string find)
    {
        string stripLeft = "";
        if (StartsWith(l, find))
        {
            stripLeft = Mid(l, Len(find) + 1);
        }
        else
        {
            stripLeft = l;
        }
        return stripLeft;
    }

    public static void TestIndent(ref string errors, ref int errorCount, int lineN, string l, int lineIndent, int expectedIndent)
    {
        if (RTrim(l) == "")
        {
            return;

        }
        if (RegExTest(l, "^On Error "))
        {
            return;

        }
        if (RegExTest(l, "^[a-zA-Z][a-zA-Z0-9]*:$"))
        {
            return;

        }

        if (lineIndent != expectedIndent)
        {
            RecordError(ref errors, ref errorCount, tyIndnt, lineN, "Incorrect Indent -- expected " + expectedIndent + ", got " + lineIndent);
        }
    }

    public static void TestBlankLines(ref string errors, ref int errorCount, int lineN, string l, ref int blankLineCount)
    {
        if (Trim(l) != "")
        {
            blankLineCount = 0;
            return;

        }
        blankLineCount = blankLineCount + 1;
        if (blankLineCount > 3)
        {
            RecordError(ref errors, ref errorCount, tyBlank, lineN, "Too many blank lines.");
        }
    }

    public static void TestLintControl(string l)
    {
        if (InStr(l, lintKey) == 0)
        {
            return;

        }

        var match = RegExNMatch(l, lintKey + "(-.....)?", 0);
        var typ = IIf(match == lintKey, tyAllty, Replace(match, lintKey + "-", ""));
        errorIgnore = errorIgnore + "," + typ;
    }

    public static void TestModuleOptions(ref string errors, ref int errorCount, Collection options)
    {
        // TODO (not supported): On Error Resume Next

        var value = options["Explicit"].ToString();
        if (value != "")
        {
            RecordError(ref errors, ref errorCount, tyExpli, 0, "Option Explicit not set on file");
        }

        value = "";
        value = options["Compare Binary"].ToString();
        value = options["Compare Database"].ToString();
        if (value != "")
        {
            RecordError(ref errors, ref errorCount, tyCompa, 0, "Use of Option Compare not recommended");
        }
    }

    public static void TestArgName(ref string errors, ref int errorCount, int lineN, string name)
    {
        var ll = Trim(name);

        if (RegExTest(ll, "^[a-z][a-z0-9_]*$"))
        {
            RecordError(ref errors, ref errorCount, tyArgna, lineN, "Identifier name declared as all lower-case: " + ll);
        }

        if (RegExTest(ll, "^[a-zA-Z_][a-zA-Z0-9_]*%$"))
        { // % Integer Dim L%
            RecordError(ref errors, ref errorCount, tyTypec, lineN, "Use of Type Character For Integer deprecated: " + ll);
        }
        else if (RegExTest(ll, "^[a-zA-Z_][a-zA-Z0-9_]*&$"))
        { // & Long  Dim M&
            RecordError(ref errors, ref errorCount, tyTypec, lineN, "Use of Type Character For Long deprecated: " + ll);
        }
        else if (RegExTest(ll, "^[a-zA-Z_][a-zA-Z0-9_]*@$"))
        { // @ Decimal Const W@ = 37.5
            RecordError(ref errors, ref errorCount, tyTypec, lineN, "Use of Type Character For Decimal deprecated: " + ll);
        }
        else if (RegExTest(ll, "^[a-zA-Z_][a-TY_TYPEC-Z0-9_]*!$"))
        { // ! Single  Dim Q!
            RecordError(ref errors, ref errorCount, tyDepre, lineN, "Use of Type Character For Single deprecated: " + ll);
        }
        else if (RegExTest(ll, "^[a-zA-Z_][a-zA-Z0-9_]*#$"))
        { // # Double  Dim X#
            RecordError(ref errors, ref errorCount, tyTypec, lineN, "Use of Type Character For Double deprecated: " + ll);
        }
        else if (RegExTest(ll, "^[a-zA-Z_][a-zA-Z0-9_]*\\$$"))
        { // $ String  Dim V$ = "Secret"
            RecordError(ref errors, ref errorCount, tyTypec, lineN, "Use of Type Character For String deprecated: " + ll);
        }
    }

    public static void TestSignatureName(ref string errors, ref int errorCount, int lineN, string name)
    {
        var ll = Trim(name);

        if (RegExTest(ll, "^[a-z][a-z0-9_]*$"))
        {
            RecordError(ref errors, ref errorCount, tyFspna, lineN, "Func/Sub/Prop name declared as all lower-case: " + ll);
        }
    }

    public static void TestDeclaration(ref string errors, ref int errorCount, int lineN, string l, bool inSignature)
    {
        l = Trim(l);
        l = StripLeft(l, "Dim ");
        l = StripLeft(l, "Private ");
        l = StripLeft(l, "Public ");
        l = StripLeft(l, "Const ");
        l = StripLeft(l, "Global ");

        foreach (var iterLl in Split(l, ", "))
        {
            dynamic ll = iterLl;
            string argType = "";
            string argDefault = "";


            bool isOptional = StartsWith(ll, "Optional ");
            ll = StripLeft(ll, "Optional ");

            bool isByVal = StartsWith(ll, "ByVal ");
            ll = StripLeft(ll, "ByVal ");

            bool isByRef = StartsWith(ll, "ByRef ");
            ll = StripLeft(ll, "ByRef ");

            bool isParamArray = StartsWith(ll, "ParamArray ");
            ll = StripLeft(ll, "ParamArray ");

            int ix = InStr(ll, " = ");
            if (ix > 0)
            {
                argDefault = Trim(Mid(ll, ix + 3));
                ll = Left(ll, ix - 1);
            }
            else
            {
                argDefault = "";
            }

            ix = InStr(ll, " As ");
            if (ix > 0)
            {
                argType = Trim(Mid(ll, ix + 4));
                ll = Left(ll, ix - 1);
            }
            else
            {
                argType = "";
            }

            //    If IsParamArray Then Stop
            if (argType == "")
            {
                RecordError(ref errors, ref errorCount, tyNotyp, lineN, "Local Parameter Missing Type: [" + ll + "]");
            }
            if (inSignature)
            {
                if (isParamArray)
                {
                    if (Right(ll, 2) != "()")
                    {
                        RecordError(ref errors, ref errorCount, tyStyle, lineN, "ParamArray variable not declared as an Array.  Add '()': " + ll);
                    }
                }
                else
                {
                    if (!isByVal && !isByRef)
                    {
                        RecordError(ref errors, ref errorCount, tyByrfv, lineN, "ByVal or ByRef not specified on parameter [" + ll + "] -- specify one or the other");
                    }
                }
                if (isOptional && argDefault == "")
                {
                    RecordError(ref errors, ref errorCount, tyOpdef, lineN, "Parameter declared OPTIONAL but no default specified. Must specify default: " + ll);
                }
            }

            TestArgName(ref errors, ref errorCount, lineN, ll);

            TestArgType(ref errors, ref errorCount, lineN, ll, argType);
        }
    }

    public static void TestArgType(ref string errors, ref int errorCount, int lineN, string name, string typ)
    {
        if (typ == "Integer")
        {
            RecordError(ref errors, ref errorCount, tyArgty, lineN, "Arg [" + name + "] is of type [" + typ + "] -- use Long");
        }
        if (typ == "Short")
        {
            RecordError(ref errors, ref errorCount, tyArgty, lineN, "Arg [" + name + "] is of type [" + typ + "] -- use Long");
        }
        if (typ == "Byte")
        {
            RecordError(ref errors, ref errorCount, tyArgty, lineN, "Arg [" + name + "] is of type [" + typ + "] -- use Long");
        }
        if (typ == "Float")
        {
            RecordError(ref errors, ref errorCount, tyArgty, lineN, "Arg [" + name + "] is of type [" + typ + "] -- use Double");
        }
    }

    public static void TestSignature(ref string errors, ref int errorCount, int lineN, string ll)
    {
        if (!RegExTest(ll, "^[ ]*(Private|Public|Friend) "))
        {
            RecordError(ref errors, ref errorCount, tyPripu, lineN, "Either Private or Public should be specified, but neither was.");
        }

        bool withReturn = false;

        var l = ll;
        l = StripLeft(l, "Private ");
        l = StripLeft(l, "Public ");
        l = StripLeft(l, "Friend ");
        l = StripLeft(l, "Sub ");
        if (StartsWith(l, "Function ") || StartsWith(l, "Property Get "))
        {
            withReturn = true;
        }
        l = StripLeft(l, "Function ");
        l = StripLeft(l, "Property ");

        int ix2 = 0;

        var ix = InStr(l, "(");
        if (ix == 0)
        {
            return;

        }
        var name = Left(l, ix - 1);
        if (RegExTest(l, "\\) As .*\\(\\)"))
        {
            ix2 = InStrRev(l, ")", Len(l) - 2);
        }
        else
        {
            ix2 = InStrRev(l, ")");
        }
        var args = Mid(l, ix + 1, ix2 - ix - 1);
        var ret = Mid(l, ix2 + 1);

        TestSignatureName(ref errors, ref errorCount, lineN, name);
        if (withReturn && ret == "")
        {
            RecordError(ref errors, ref errorCount, tyFncre, lineN, "Function Return Type Not Specified -- Specify Return Type or Variant");
        }
        TestDeclaration(ref errors, ref errorCount, lineN, args, true);
    }

    public static void TestDefaultControlNames(ref string errors, ref int errorCount, int lineNUnused, string contents)
    {
        var vTypes = new []{"CheckBox", "Command", "Option", "Frame", "Label", "TextBox", "RichTextBox", "RichTextBoxNew", "ComboBox", "ListBox", "Timer", "UpDown", "HScrollBar", "Image", "Picture", "MSFlexGrid", "DBGrid", "Line", "Shape", "DTPicker"}; // TODO - Specified Minimum Array Boundary Not Supported:   Dim vTypes() As Variant, vType As Variant

        foreach (var itervType in vTypes)
        {
            var vType = itervType; // TODO - Specified Minimum Array Boundary Not Supported:   Dim vTypes() As Variant, vType As Variant
            var matcher = "Begin [a-zA-Z0-9]*.[a-zA-Z0-9]* " + vType + "[0-9]*";
            var n = RegExCount(contents, matcher);
            for (var I = 0; I <= n - 1; I++)
            {
                var results = RegExNMatch(contents, matcher, I);
                RecordError(ref errors, ref errorCount, tyDfctl, 0, "Default control name in use on form: " + results);
            }
        }
    }

    public static void TestCodeLine(ref string errorsUnused, ref int errorCount, int lineN, string l)
    {
        var e = errorCount;
        if (RegExTest(l, "+ \"") || RegExTest(l, "\" +"))
        {
            RecordError(ref errorsUnused, ref e, tyCorre, lineN, "Possible use of + instead of & on String concatenation");
        }
        if (RegExTest(l, " Me[.]"))
        {
            RecordError(ref errorsUnused, ref e, tyCorre, lineN, "Use of 'Me.*' is not required.");
        }

        if (RegExTest(l, "\\.Enabled = [-0-9]"))
        {
            RecordError(ref errorsUnused, ref e, tyCorre, lineN, "Property [Enabled] Should Be Boolean.  Numeric found.");
        }
        if (RegExTest(l, "\\.Visible = [-0-9]"))
        {
            RecordError(ref errorsUnused, ref e, tyCorre, lineN, "Property [Visible] Should Be Boolean.  Numeric found.");
        }

        if (RegExTest(l, " Call "))
        {
            RecordError(ref errorsUnused, ref e, tyCorre, lineN, "Remove keyword 'Call'.");
        }
        if (RegExTest(l, " GoSub ") || RegExTest(l, " Return$"))
        {
            RecordError(ref errorsUnused, ref e, tyGosub, lineN, "Remove uses of 'GoSub' and 'Return'.");
        }

        if (RegExTest(l, " Stop$") || RegExTest(l, " Return$"))
        {
            RecordError(ref errorsUnused, ref e, tyCstop, lineN, "Code contains STOP statement.");
        }

        errorCount = e;
    }
}