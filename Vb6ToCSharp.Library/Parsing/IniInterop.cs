using System;
using System.Runtime.InteropServices;
using System.Text;
using static Microsoft.VisualBasic.Strings;

namespace Vb6ToCSharp.Parsing;

public static class IniInterop
{
    [DllImport("kernel32.dll", EntryPoint = "WritePrivateProfileStringW", CharSet = CharSet.Unicode)] private static extern int WritePrivateProfileString(string lpApplicationName, string lpKeyName, string lpString, string lpFileName);
    [DllImport("kernel32.dll", EntryPoint = "GetPrivateProfileStringW", CharSet = CharSet.Unicode)] private static extern int GetPrivateProfileString(string lpApplicationName, string lpKeyName, string lpDefault, StringBuilder lpReturnedString, int nSize, string lpFileName);
    [DllImport("kernel32.dll", EntryPoint = "GetPrivateProfileSectionNamesW", CharSet = CharSet.Unicode)] private static extern int GetPrivateProfileSectionNames(char[] lpszReturnBuffer, int nSize, string lpFileName);
    [DllImport("kernel32.dll", EntryPoint = "GetPrivateProfileSectionW", CharSet = CharSet.Unicode)] private static extern int GetPrivateProfileSection(string lpAppName, char[] lpReturnedString, int nSize, string lpFileName);


    public static bool IniWrite(string sSection, string sKeyName, string sNewString, string sIniFileName)
    {
        return WritePrivateProfileString(sSection, sKeyName, sNewString, sIniFileName) != 0;
    }

    public static string IniRead(string sSection, string sKeyName, string sIniFileName)
    {
        var sRet = new StringBuilder(255);
        GetPrivateProfileString(sSection, sKeyName, "", sRet, sRet.Capacity, sIniFileName);
        return sRet.ToString();
    }

    public static string[] IniSections(string tFileName)
    {
        var strBuffer = new char[256];
        var intLen = GetPrivateProfileSectionNames(strBuffer, strBuffer.Length, tFileName);
        while (intLen == strBuffer.Length - 2)
        {
            strBuffer = new char[strBuffer.Length * 2];
            intLen = GetPrivateProfileSectionNames(strBuffer, strBuffer.Length, tFileName);
        }
        if (intLen == 0) return new string[0];

        // buffer is a list of NUL-terminated strings; drop the trailing empty entry
        return new string(strBuffer, 0, intLen).TrimEnd('\0').Split('\0');
    }

    public static string[] IniSectionKeys(string tFileName, string section)
    {
        var strBuffer = new char[256];
        var intLen = GetPrivateProfileSection(section, strBuffer, strBuffer.Length, tFileName);
        while (intLen == strBuffer.Length - 2)
        {
            strBuffer = new char[strBuffer.Length * 2];
            intLen = GetPrivateProfileSection(section, strBuffer, strBuffer.Length, tFileName);
        }
        if (intLen == 0) return null;

        // "key=value" per NUL-terminated entry; drop the trailing empty entry
        var ret = new string(strBuffer, 0, intLen).TrimEnd('\0').Split('\0');
        for (var I = 0; I < ret.Length; I++)
        {
            var n = InStr(ret[I], "=");
            if (n > 0)
            {
                ret[I] = Left(ret[I], n - 1);
            }
            else
            {
                Console.WriteLine("modINI.INISectionKeys - No '=' character found in line.  Section=" + section + ", Line=" + ret[I] + ", file=" + tFileName);
            }
        }
        return ret;
    }

    public static string ReadIniValue(string iniPath, string key, string variable, string vDefault = "")
    {
        var readIniValue = IniRead(key, variable, iniPath);
        if (readIniValue == "")
        {
            readIniValue = vDefault;
        }
        return readIniValue;
    }

    public static string WriteIniValue(string iniPath, string putKey, string putVariable, string putValue, bool deleteOnEmptyUnused = false)
    {
        IniWrite(putKey, putVariable, putValue, iniPath);
        return IniRead(putKey, putVariable, iniPath);
    }
}