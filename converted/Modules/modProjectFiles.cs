using static Microsoft.VisualBasic.Constants;
using static Microsoft.VisualBasic.Strings;
using static Vb6ToCSharp.Modules.ModConfig;
using static Vb6ToCSharp.Modules.ModTextFiles;
using static Vb6ToCSharp.Modules.ModUtils;
using static Vb6ToCSharp.VbExtension;


namespace Vb6ToCSharp.Modules;

static class ModProjectFiles
{
    // Option Explicit


    public static string VbpCode(string projectFileUnused = "")
    {
        var vbpCode = VbpModules() + vbCrLf + VbpForms() + vbCrLf + VbpClasses() + vbCrLf + VbpUserControls();
        return vbpCode;
    }

    public static string VbpModules(string projectFile = "")
    {
        string vbpModules = "";

        const string c = "Module=";
        if (projectFile == "")
        {
            projectFile = VbpFile;
        }
        var s = ReadEntireFile(projectFile);
        foreach (var iterL in Split(s, vbCrLf))
        {
            dynamic l = iterL;
            if (Left(l, Len(c)) == c)
            {
                string T = Mid(l, Len(c) + 1);
                if (IsInStr(T, ";"))
                {
                    T = SplitWord(T, 2, ";");
                }
                //If IsInStr(LCase(T), "subclass") Then Stop
                if (LCase(T) == "modlistsubclass.bas")
                {
                    goto NextItem;
                }
                vbpModules = vbpModules + IIf(vbpModules == "", "", vbCrLf) + T;
            }
            NextItem:;
        }
        return vbpModules;
    }

    public static string VbpForms(string projectFile = "")
    {
        string vbpForms = "";
        const bool withExt = true;

        const string c = "Form=";
        if (projectFile == "")
        {
            projectFile = VbpFile;
        }
        var s = ReadEntireFile(projectFile);
        foreach (var iterL in Split(s, vbCrLf))
        {
            dynamic l = iterL;
            if (Left(l, Len(c)) == c)
            {
                string T = Mid(l, Len(c) + 1);
                if (IsInStr(T, ";"))
                {
                    T = SplitWord(T, 1, ";");
                }
                if (!withExt && Right(T, 4) == ".frm")
                {
                    T = Left(T, Len(T) - 4);
                }
                switch (LCase(T))
                {
                    case "faxtest":
                        T = "FaxPO";
                        break;
                    case "frmpos":
                        T = "frmCashRegister";
                        break;
                    case "frmposquantity":
                        T = "frmCashRegisterQuantity";
                        break;
                    case "calendarinst":
                        T = "CalendarInstr";
                        break;
                    case "frmedi":
                        T = "frmAshleyEDIItemAlign";
                        break;
                    case "frmpracticefiles":
                        T = "PracticeFiles";
                        break;
                    case "txttextselect":
                        T = "frmSelectText";
                        break;
                }
                vbpForms = vbpForms + IIf(vbpForms == "", "", vbCrLf) + T;
            }
            NextItem:;
        }
        return vbpForms;
    }

    public static string VbpClasses(string projectFile = "", bool classNames = false)
    {
        string vbpClasses = "";

        const string c = "Class=";
        if (projectFile == "")
        {
            projectFile = VbpFile;
        }
        var s = ReadEntireFile(projectFile);
        foreach (var iterL in Split(s, vbCrLf))
        {
            dynamic l = iterL;
            if (Left(l, Len(c)) == c)
            {
                string T = Mid(l, Len(c) + 1);
                if (IsInStr(T, ";"))
                {
                    T = SplitWord(T, 2, ";");
                }
                vbpClasses = vbpClasses + IIf(vbpClasses == "", "", vbCrLf) + T;
            }
            NextItem:;
        }
        if (classNames)
        {
            vbpClasses = Replace(vbpClasses, ".cls", "");
        }
        return vbpClasses;
    }

    public static string VbpUserControls(string projectFile = "")
    {
        string vbpUserControls = "";

        const string c = "UserControl=";
        if (projectFile == "")
        {
            projectFile = VbpFile;
        }
        var s = ReadEntireFile(projectFile);
        foreach (var iterL in Split(s, vbCrLf))
        {
            dynamic l = iterL;
            if (Left(l, Len(c)) == c)
            {
                string T = Mid(l, Len(c) + 1);
                if (IsInStr(T, ";"))
                {
                    T = SplitWord(T, 2, ";");
                }
                vbpUserControls = vbpUserControls + IIf(vbpUserControls == "", "", vbCrLf) + T;
            }
            NextItem:;
        }
        return vbpUserControls;
    }
}