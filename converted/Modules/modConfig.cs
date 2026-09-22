using System;
using System.Collections.Generic;
using static Microsoft.VisualBasic.Constants;
using static Microsoft.VisualBasic.FileSystem;
using static Microsoft.VisualBasic.Interaction;
using static Microsoft.VisualBasic.Strings;
using static Vb6ToCSharp.Modules.ModUtils;


namespace Vb6ToCSharp.Modules;

public static class ModConfig
{
    // Option Explicit
    public const int spIndent = 2;
    public const string defaultDataType = "dynamic";
    public const string packagePrefix = "";
    private const string defVbpFile = ""; // no default project (was the original author's C:\WinCDS.NET\cnv\prj.vbp)
    private const string defOutputSubFolder = "converted\\"; // under the project's folder (or the exe's when no project is set)
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

    // Project-specific conversion rules (all optional, empty by default)
    public const string iniSectionFormRenames = "FormRenames";   // <vbp Form= entry>=<new name>
    public const string iniSectionDataTypes = "DataTypes";       // <VB type>=<C# type>
    public const string iniSectionControls = "Controls";         // <VB control type>=<WPF type>[;<container 0|1>;<default property>;<features>]
    public const string iniSectionPostCodeLine = "PostCodeLine"; // <n>=<rule>, see ModProjectSpecific
    private static readonly Dictionary<string, List<KeyValuePair<string, string>>> sections = new Dictionary<string, List<KeyValuePair<string, string>>>();


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
        sections.Clear();
        mVbpFile = ModIni.IniRead(iniSectionSettings, iniKeyVbpFile, IniFile());
        mOutputFolder = ModIni.IniRead(iniSectionSettings, iniKeyOutputFolder, IniFile());
        mAssemblyName = ModIni.IniRead(iniSectionSettings, iniKeyAssemblyName, IniFile());
    }

    public static string OutputFolder(string f = "")
    {
        LoadSettings();
        if (mOutputFolder == "")
        {
            mOutputFolder = (VbpFile != "" ? VbpPath : AppDomain.CurrentDomain.BaseDirectory) + defOutputSubFolder;
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
            System.IO.Directory.CreateDirectory(outputFolder); // MkDir creates one level only (Modules\ etc. under a new folder threw)
        }
        return outputFolder;
    }

    /// <summary>Key/value pairs of an INI section in file order (cached until LoadSettings(true)); comment lines skipped.</summary>
    public static List<KeyValuePair<string, string>> IniSection(string section)
    {
        LoadSettings();
        if (!sections.TryGetValue(section, out var list))
        {
            list = new List<KeyValuePair<string, string>>();
            foreach (var key in ModIni.IniSectionKeys(IniFile(), section) ?? new string[0])
            {
                var k = Trim(key);
                if (k == "" || Left(k, 1) == ";" || Left(k, 1) == "#")
                {
                    continue;
                }
                list.Add(new KeyValuePair<string, string>(k, ModIni.IniRead(section, k, IniFile())));
            }
            sections[section] = list;
        }
        return list;
    }

    /// <summary>Value for <paramref name="key"/> (case-insensitive) in an INI section, or null.</summary>
    public static string IniMap(string section, string key)
    {
        foreach (var kv in IniSection(section))
        {
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return kv.Value;
            }
        }
        return null;
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
        var outputSubFolder = "";
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