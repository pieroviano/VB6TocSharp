using static Vb6ToCSharp.Runtime.VbConstants;
using static Vb6ToCSharp.Runtime.VbStrings;
using static Vb6ToCSharp.Parsing.ProjectConfigurationParser;
using static Vb6ToCSharp.Infrastructure.TextFiles;
using static Vb6ToCSharp.CodeConversion.ConversionUtility;
using static Vb6ToCSharp.Runtime.RuntimeExtension;
using Vb6ToCSharp.Runtime;


namespace Vb6ToCSharp.Parsing;

public static class ProjectFiles
{
    public static string VbpCode(string projectFileUnused = "")
    {
        var vbpCode = VbpModules() + vbCrLf + VbpForms() + vbCrLf + VbpClasses() + vbCrLf + VbpUserControls();
        return vbpCode;
    }

    public static string VbpModules(string projectFile = "")
    {
        var vbpModules = "";

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
        var vbpForms = "";
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
                // project-specific renames come from config (the hard-coded WinCDS list never matched: T keeps ".frm")
                T = IniMap(iniSectionFormRenames, T) ?? T;
                vbpForms = vbpForms + IIf(vbpForms == "", "", vbCrLf) + T;
            }
        }
        return vbpForms;
    }

    public static string VbpClasses(string projectFile = "", bool classNames = false)
    {
        var vbpClasses = "";

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
        }
        if (classNames)
        {
            vbpClasses = Replace(vbpClasses, ".cls", "");
        }
        return vbpClasses;
    }

    public static string VbpUserControls(string projectFile = "")
    {
        var vbpUserControls = "";

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
        }
        return vbpUserControls;
    }
}