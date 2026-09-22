using System;
using Microsoft.VisualBasic;
using static Microsoft.VisualBasic.Constants;
using static Microsoft.VisualBasic.FileSystem;
using static Microsoft.VisualBasic.Strings;
using static Vb6ToCSharp.Modules.ModConfig;
using static Vb6ToCSharp.Modules.ModControlProperties;
using static Vb6ToCSharp.Modules.ModConvertForm;
using static Vb6ToCSharp.Modules.ModProjectFiles;
using static Vb6ToCSharp.Modules.ModRegEx;
using static Vb6ToCSharp.Modules.ModTextFiles;
using static Vb6ToCSharp.Modules.ModUtils;
using static Vb6ToCSharp.Modules.ModVb6ToCs;
using static Vb6ToCSharp.VbExtension;

namespace Vb6ToCSharp.Modules;

public static class ModRefScan
{
    private static string outRes = "";
    private static string cFuncRefName = "";
    private static string cFuncRefValue = "";
    private static Collection funcs = null;
    private static Collection localFuncs = null;


    private static string RefList(bool killRef = false)
    {
        var refList =
            // TODO (not supported): On Error Resume Next
            AppDomain.CurrentDomain.BaseDirectory + "\\refs.txt";
        if (killRef)
        {
            System.IO.File.Delete(refList);
        }
        return refList;
    }

    public static int FuncsCount(bool vLocal = false)
    {
        var funcsCount = 0;
        // TODO (not supported): On Error Resume Next
        if (vLocal)
        {
            funcsCount = localFuncs.Count;
        }
        else
        {
            funcsCount = funcs.Count;
        }
        return funcsCount;
    }

    public static int ScanRefs()
    {
        dynamic l = null;

        // TODO (not supported): On Error Resume Next
        outRes = "";
        var scanRefs = 0;
        foreach (var iterL in Split(VbpModules(VbpFile), vbCrLf))
        {
            l = iterL;
            if (l == "")
            {
                goto SkipMod;
            }
            string ll = Replace(l, ".bas", "");
            outRes = outRes + vbCrLf + ll + ":" + ll + ":Module:";
            scanRefs = scanRefs + ScanRefsFile(FilePath(VbpFile) + l);
            SkipMod:;
        }

        foreach (var iterL in Split(VbpForms(VbpFile), vbCrLf))
        {
            l = iterL;
            l = Replace(l, ".frm", "");
            if (string.IsNullOrEmpty(l)) // .NET Strings.Replace returns null (not "") for an empty input
            {
                goto SkipForm;
            }
            string T = vbCrLf + l + ":" + l + ":Form:";
            outRes = outRes + T;

            //'''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

            string s = ReadEntireFile(VbpPath + l + ".frm");
            var j = CodeSectionLoc(s);
            var preamble = Left(s, j - 1);
            string controlRefs = FormControls(l, preamble, false);
            outRes = outRes + controlRefs;
            //'''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

            scanRefs = scanRefs + 1;
            SkipForm:;
        }
        RefList(killRef: true);
        WriteFile(RefList(), outRes);
        outRes = "";
        return scanRefs;
    }

    private static int ScanRefsFile(string fn)
    {
        var l = "";

        var f = "";
        var g = "";

        var cont = false;

        var currEnum = "";

        var m = FileBaseName(fn);
        var s = ReadEntireFile(fn);
        var scanRefsFile = 0;
        foreach (var iterLl in Split(s, vbCrLf))
        {
            dynamic ll = iterLl;
            bool doCont = Right(ll, 1) == "_";
            if (!cont && !doCont)
            {
                l = Trim(ll);
                cont = false;
            }
            else if (cont && !doCont)
            {
                l = l + Trim(ll);
                cont = false;
            }
            else if (!cont && doCont)
            {
                l = Trim(Left(ll, Len(ll) - 2));
                cont = true;
                goto NextLine;
            }
            else if (cont && doCont)
            {
                l = l + Trim(Left(ll, Len(ll) - 2));
                cont = true;
                goto NextLine;
            }

            if (TLMatch(l, "Function ") || TLMatch(l, "Public Function ") || TLMatch(l, "Sub ") || TLMatch(l, "Public Sub ") || false)
            {
                f = Trim(l);
                if (Left(f, 7) == "Public ")
                {
                    f = Mid(f, 8);
                }
                f = Trim(NextBy(f, ":"));

                g = f;
                if (TLMatch(g, "Function "))
                {
                    g = Mid(g, 10);
                }
                if (TLMatch(g, "Sub "))
                {
                    g = Mid(g, 5);
                }
                g = NextBy(g, "(");

                f = m + ":" + g + ":Function:" + f;
                outRes = outRes + vbCrLf + f;
                scanRefsFile = scanRefsFile + 1;
            }
            else if (TLMatch(l, "Private Function ") || TLMatch(l, "Private Sub ") || false)
            {
                f = Trim(l);
                f = Trim(NextBy(f, ":"));

                g = f;
                if (TLMatch(g, "Private Function "))
                {
                    g = Mid(g, 17);
                }
                if (TLMatch(g, "Private Sub "))
                {
                    g = Mid(g, 12);
                }
                g = NextBy(g, "(");

                f = m + ":" + Trim(m) + "." + Trim(g) + ":Private Function:" + f;
                outRes = outRes + vbCrLf + f;
                scanRefsFile = scanRefsFile + 1;
            }
            else if (TLMatch(l, "Declare ") || TLMatch(l, "Public Decalre "))
            {
                l = LTrim(l);
                if (LMatch(l, "Public "))
                {
                    l = Mid(l, 8);
                }
                if (LMatch(l, "Declare "))
                {
                    l = Mid(l, 9);
                }
                g = SplitWord(l);

            }
            else if (TLMatch(l, "Const ") || TLMatch(l, "Public Const ") || TLMatch(l, "Global Const "))
            {
                l = LTrim(l);
                if (LMatch(l, "Public "))
                {
                    l = Mid(l, 8);
                }
                if (LMatch(l, "Global "))
                {
                    l = Mid(l, 8);
                }
                if (LMatch(l, "Const "))
                {
                    l = Mid(l, 7);
                }
                g = SplitWord(l);
            }
            else if (TLMatch(l, "Enum ") || TLMatch(l, "Public Enum "))
            {
                l = LTrim(l);
                if (LMatch(l, "Public "))
                {
                    l = Mid(l, 8);
                }
                if (LMatch(l, "Enum "))
                {
                    l = Mid(l, 5);
                }
                currEnum = Trim(l);
            }
            else if (TLMatch(l, "End Enum"))
            {
                currEnum = "";
            }
            else if (currEnum != "")
            {
                g = SplitWord(l);
                f = m + ":" + g + ":Enum:" + currEnum + "." + g;
                outRes = outRes + vbCrLf + f;
                scanRefsFile = scanRefsFile + 1;
            }
            NextLine:;
        }
        return scanRefsFile;
    }

    public static string ScanRefsFileToString(string fn)
    {
        outRes = "";
        ScanRefsFile(fn);
        var scanRefsFileToString = outRes;
        outRes = "";
        return scanRefsFileToString;
    }

    private static void InitFuncs()
    {
        if (Dir(RefList()) == "")
        {
            ScanRefs();
        }
        if (!(funcs == null))
        {
            return;

        }
        var s = ReadEntireFile(RefList());
        funcs = new Collection(); ;
        // TODO (not supported): On Error Resume Next
        foreach (var iterL in Split(s, vbCrLf))
        {
            dynamic l = iterL;
            if (!funcs.Contains(SplitWord(l, 2, ":"))) funcs.Add(l, SplitWord(l, 2, ":")); // VB: On Error Resume Next skipped duplicates
        }
        InitLocalFuncs();
    }

    public static void InitLocalFuncs(string s = "")
    {
        // TODO (not supported): On Error Resume Next

        localFuncs = new Collection(); ;
        foreach (var iterL in Split(s, vbCrLf))
        {
            dynamic l = iterL;
            if (!localFuncs.Contains(SplitWord(l, 2, ":"))) localFuncs.Add(l, SplitWord(l, 2, ":")); // VB: On Error Resume Next skipped duplicates
        }
    }

    public static string FuncRef(string fName)
    {
        var funcRef = "";
        if (fName == cFuncRefName)
        {
            funcRef = cFuncRefValue;
            return funcRef;

        }

        InitFuncs();
        // TODO (not supported): On Error Resume Next
        funcRef = funcs.Contains(fName) ? (string)funcs[fName] : "";
        if (funcRef == "" && localFuncs != null && localFuncs.Contains(fName))
        {
            funcRef = (string)localFuncs[fName];
        }

        cFuncRefName = fName;
        cFuncRefValue = funcRef;
        return funcRef;
    }

    public static string FuncRefModule(string fName)
    {
        var funcRefModule = NextBy(FuncRef(fName), ":");
        return funcRefModule;
    }

    public static string FuncRefEntity(string fName)
    {
        var funcRefEntity = NextBy(FuncRef(fName), ":", 3);
        return funcRefEntity;
    }

    public static string FuncRefDecl(string fName)
    {
        var funcRefDecl = NextBy(FuncRef(fName), ":", 4);
        return funcRefDecl;
    }

    public static bool IsFuncRef(string fName)
    {
        var isFuncRef = FuncRef(fName) != "" && FuncRefEntity(fName) == "Function";
        return isFuncRef;
    }

    public static bool IsPrivateFuncRef(string module, string fName)
    {
        var name = Trim(module) + "." + Trim(fName);
        var isPrivateFuncRef = FuncRef(name) != "" && FuncRefEntity(name) == "Private Function";
        return isPrivateFuncRef;
    }

    public static bool IsEnumRef(string fName)
    {
        var isEnumRef = FuncRef(fName) != "" && FuncRefEntity(fName) == "Enum";
        return isEnumRef;
    }

    public static bool IsFormRef(string fName)
    {
        var T = SplitWord(fName, 1, ".");
        var isFormRef = FuncRef(T) != "" && FuncRefEntity(T) == "Form";
        return isFormRef;
    }

    public static bool IsModuleRef(string fName)
    {
        var T = SplitWord(fName, 1, ".");
        var isModuleRef = FuncRef(T) != "" && FuncRefEntity(T) == "Module";
        return isModuleRef;
    }

    public static bool IsControlRef(string src, string formName = "")
    {
        var isControlRef = false;

        var tok = RegExNMatch(src, patToken);
        var tok2 = RegExNMatch(src, patToken, 1);
        var name = tok + "." + tok2;
        var fTok = formName + "." + tok;
        if (FuncRef(name) != "" && FuncRefEntity(name) == "Control" || FuncRef(fTok) != "" && FuncRefEntity(fTok) == "Control")
        {
            isControlRef = true;
        }
        return isControlRef;
    }

    public static string FuncRefDeclTyp(string fName)
    {
        var funcRefDeclTyp = SplitWord(FuncRefDecl(fName), 1);
        return funcRefDeclTyp;
    }

    public static string FuncRefDeclRet(string fName)
    {
        var funcRefDeclRet = FuncRefDecl(fName);
        funcRefDeclRet = Trim(Mid(funcRefDeclRet, InStrRev(funcRefDeclRet, " ")));
        if (Right(funcRefDeclRet, 1) == ")" && Right(funcRefDeclRet, 2) != "()")
        {
            funcRefDeclRet = "";
        }
        return funcRefDeclRet;
    }

    public static string FuncRefDeclArgs(string fName)
    {
        var funcRefDeclArgs =
            // TODO (not supported): On Error Resume Next
            FuncRefDecl(fName);
        funcRefDeclArgs = Mid(funcRefDeclArgs, InStr(funcRefDeclArgs, "(") + 1);
        funcRefDeclArgs = Left(funcRefDeclArgs, InStrRev(funcRefDeclArgs, ")") - 1);
        funcRefDeclArgs = Trim(funcRefDeclArgs);
        return funcRefDeclArgs;
    }

    public static string FuncRefDeclArgN(string fName, int n)
    {
        var f = FuncRefDeclArgs(fName);
        var funcRefDeclArgN = NextBy(f, ", ", n);
        return funcRefDeclArgN;
    }

    public static int FuncRefDeclArgCnt(string fName)
    {
        var f = FuncRefDeclArgs(fName);
        var funcRefDeclArgCnt = 0;
        do
        {
            var k = NextBy(f, ", ", funcRefDeclArgCnt + 1);
            if (k == "")
            {
                return funcRefDeclArgCnt;
            }
            funcRefDeclArgCnt = funcRefDeclArgCnt + 1;
        } while (true); 
    }

    public static string FuncRefArgType(string fName, int n)
    {
        var funcRefArgType = FuncRefDeclArgN(fName, n);
        if (funcRefArgType == "")
        {
            return funcRefArgType;
        }
        funcRefArgType = SplitWord(funcRefArgType, 2, " As ");
        return funcRefArgType;
    }

    public static bool FuncRefArgByRef(string fName, int n)
    {
        var funcRefArgByRef = !IsInStr(FuncRefDeclArgN(fName, n), "ByVal ");
        return funcRefArgByRef;
    }

    public static bool FuncRefArgOptional(string fName, int n)
    {
        var funcRefArgOptional = IsInStr(FuncRefDeclArgN(fName, n), "Optional ");
        return funcRefArgOptional;
    }

    public static string FuncRefArgDefault(string fName, int n)
    {
        var funcRefArgDefault = "";

        if (!FuncRefArgOptional(fName, n))
        {
            return funcRefArgDefault;

        }
        funcRefArgDefault = SplitWord(FuncRefDeclArgN(fName, n), 2, " = ", true, true);
        if (funcRefArgDefault == "")
        {
            funcRefArgDefault = ConvertDefaultDefault(FuncRefArgType(fName, n));
        }
        return funcRefArgDefault;
    }

    public static string EnumRefRepl(string eName)
    {
        var enumRefRepl = FuncRefDecl(eName);
        return enumRefRepl;
    }

    public static string FormRefRepl(string fName)
    {
        var T = SplitWord(fName, 1, ".");
        var u = FuncRefModule(T) + ".instance";
        var formRefRepl = Replace(fName, T, u);
        return formRefRepl;
    }

    public static string FormControlRepl(string src, string formName = "")
    {
        var formControlRepl = "";

        var f = "";
        var v = "";

        var tok = RegExNMatch(src, patToken);
        var tok2 = RegExNMatch(src, patToken, 1);
        var tok3 = RegExNMatch(src, patToken, 2);

        //If IsInStr(Tok, "BillOSale") Then Stop
        //If IsInStr(Src, "SetFocus") Then Stop
        if (!IsFormRef(tok))
        {
            f = tok;
            v = ConvertControlProperty(f, tok2, FuncRefDecl(formName + "." + tok));
            if (tok2 != "")
            {
                formControlRepl = Replace(src, tok2, v);
            }
            else
            {
                formControlRepl = src + "." + v;
            }
        }
        else
        {
            f = tok + "." + tok2;
            v = ConvertControlProperty(f, tok3, FuncRefDecl(tok + "." + tok2));
            if (tok3 != "")
            {
                formControlRepl = Replace(src, tok3, v);
            }
            else
            {
                formControlRepl = src + "." + v;
            }
        }
        return formControlRepl;
    }
}