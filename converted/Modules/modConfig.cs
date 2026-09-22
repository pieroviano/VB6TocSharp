using System;
using static Microsoft.VisualBasic.Constants;
using static Microsoft.VisualBasic.FileSystem;
using static Microsoft.VisualBasic.Interaction;
using static Microsoft.VisualBasic.Strings;
using static Vb6ToCSharp.Modules.ModUtils;


namespace Vb6ToCSharp.Modules;

static class ModConfig
{
    // Option Explicit
    public const int spIndent = 2;
    public const string defaultDataType = "dynamic";
    public const string packagePrefix = "";
    private const string defVbpFile = "C:\\WinCDS.NET\\cnv\\prj.vbp";
    private const string defOutputFolder = "C:\\WinCDS.NET\\cnv\\converted\\";
    private const string defAssemblyName = "VB2CS";
    private static string mVbpFile = "";
    private static string mOutputFolder = "";
    private static string mAssemblyName = "";
    private static bool loaded = false;
    public static bool hush = false;
    public const string iniSectionSettings = "Settings";
    public const string iniKeyVbpFile = "VBPFile";
    public const string iniKeyOutputFolder = "OutputFolder";
    public const string iniKeyAssemblyName = "AssemblyName";


    public static string VbpFile
    {
        get
        {
            LoadSettings();
            if (mVbpFile == "")
            {
                mVbpFile = defVbpFile;
            }
            var vbpFile = mVbpFile;

            return vbpFile;
        }
    }
    public static string VbpPath
    {
        get
        {
            var vbpPath = FilePath(VbpFile);

            return vbpPath;
        }
    }


    public static string IniFile()
    {
        var iniFile = AppDomain.CurrentDomain.BaseDirectory + "\\VB6toCS.INI";
        return iniFile;
    }

    public static void LoadSettings(bool force = false)
    {
        if (loaded && !force)
        {
            return;

        }
        loaded = true;
        mVbpFile = ModIni.IniRead(iniSectionSettings, iniKeyVbpFile, IniFile());
        mOutputFolder = ModIni.IniRead(iniSectionSettings, iniKeyOutputFolder, IniFile());
        mAssemblyName = ModIni.IniRead(iniSectionSettings, iniKeyAssemblyName, IniFile());
    }

    public static string OutputFolder(string f = "")
    {
        LoadSettings();
        if (mOutputFolder == "")
        {
            mOutputFolder = defOutputFolder;
        }
        var outputFolder = mOutputFolder;
        if (Right(outputFolder, 1) != "\\")
        {
            outputFolder = outputFolder + "\\";
        }
        outputFolder = outputFolder + OutputSubFolder(f);
        if (Dir(outputFolder, vbDirectory) == "")
        {
            // TODO (not supported): On Error GoTo CantMakeOutputFolder
            MkDir(outputFolder);
        }
        return outputFolder;

        CantMakeOutputFolder:;
        if (!hush)
        {
            MsgBox("Failed creating folder.  Perhaps create it yourself?" + vbCrLf + outputFolder);
        }
        return outputFolder;
    }

    public static string AssemblyName()
    {
        LoadSettings();
        if (mAssemblyName == "")
        {
            mAssemblyName = defAssemblyName;
        }
        var assemblyName = mAssemblyName;
        return assemblyName;
    }

    public static string OutputSubFolder(string f)
    {
        string outputSubFolder = "";
        LoadSettings();
        switch (FileExt(f))
        {
            case ".bas":
                outputSubFolder = "Modules\\";
                break;
            case ".cls":
                outputSubFolder = "Classes\\";
                break;
            case ".frm":
                outputSubFolder = "Forms\\";
                break;
            default:
                outputSubFolder = "";
                break;
        }
        return outputSubFolder;
    }
}