using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Vb6ToCSharp.Infrastructure;

/// <summary>
/// Reads and writes .ini files the way Win32's profile API did, in plain C#: section and key names
/// are case-insensitive, the first match wins (a repeated section or key after it is dead), values
/// are trimmed and lose one surrounding pair of double quotes, <c>;</c> starts a comment (<c>#</c>
/// does not), and a write edits its own line and leaves the rest of the file alone.
/// One deliberate difference from the API it replaces: a value is no longer cut at 255 characters.
/// </summary>
public static class IniInterop
{
    public static bool IniWrite(string sSection, string sKeyName, string sNewString, string sIniFileName)
    {
        if (string.IsNullOrEmpty(sSection) || string.IsNullOrEmpty(sIniFileName)) return false;

        try
        {
            var file = Load(sIniFileName);
            var at = SectionAt(file.Lines, sSection);

            // A null key removes the whole section, a null value removes the key - as in Win32.
            if (sKeyName == null)
            {
                if (at < 0) return true;
                file.Lines.RemoveRange(at, SectionLength(file.Lines, at));
                Save(sIniFileName, file);
                return true;
            }

            if (at < 0)
            {
                if (sNewString == null) return true;
                if (file.Lines.Count > 0 && file.Lines[file.Lines.Count - 1].Trim().Length == 0)
                    file.Lines.RemoveAt(file.Lines.Count - 1);
                file.Lines.Add("[" + sSection + "]");
                file.Lines.Add(sKeyName + "=" + sNewString);
                Save(sIniFileName, file);
                return true;
            }

            var end = at + SectionLength(file.Lines, at);
            for (var i = at + 1; i < end; i++)
            {
                if (!SplitEntry(file.Lines[i], out var key, out _) || !SameName(key, sKeyName)) continue;

                if (sNewString == null) file.Lines.RemoveAt(i);
                // Keep the key exactly as the file spells it and replace only the value.
                else file.Lines[i] = file.Lines[i].Substring(0, file.Lines[i].IndexOf('=')) + "=" + sNewString;
                Save(sIniFileName, file);
                return true;
            }

            if (sNewString == null) return true;
            // A new key goes at the end of its own section, not of the file.
            var insert = end;
            while (insert > at + 1 && file.Lines[insert - 1].Trim().Length == 0) insert--;
            file.Lines.Insert(insert, sKeyName + "=" + sNewString);
            Save(sIniFileName, file);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static string IniRead(string sSection, string sKeyName, string sIniFileName)
    {
        foreach (var line in SectionLines(sIniFileName, sSection))
        {
            if (!SplitEntry(line, out var key, out var value) || !SameName(key, sKeyName)) continue;
            return CleanValue(value);
        }
        return "";
    }

    public static string[] IniSections(string tFileName)
    {
        var res = new List<string>();
        foreach (var line in Load(tFileName).Lines)
            if (SectionName(line, out var name)) res.Add(name);
        return res.ToArray();
    }

    public static string[] IniSectionKeys(string tFileName, string section)
    {
        var lines = SectionLines(tFileName, section);
        if (lines.Count == 0) return null;

        var res = new List<string>();
        foreach (var line in lines)
        {
            if (SplitEntry(line, out var key, out _))
            {
                res.Add(key);
                continue;
            }
            Console.WriteLine("modINI.INISectionKeys - No '=' character found in line.  Section=" + section
                + ", Line=" + line.Trim() + ", file=" + tFileName);
            res.Add(line.Trim());
        }
        return res.Count == 0 ? null : res.ToArray();
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

    // ---------------------------------------------------------------- the file

    private sealed class IniText
    {
        public List<string> Lines = new List<string>();
        public Encoding Encoding = DefaultEncoding;
        public string NewLine = "\r\n";
    }

    /// <summary>Win32's ...W functions read a file without a BOM as ANSI, so that is the default here too.</summary>
    private static Encoding DefaultEncoding => Encoding.Default;

    private static IniText Load(string path)
    {
        var file = new IniText();
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return file;

        string text;
        using (var reader = new StreamReader(path, DefaultEncoding, true))
        {
            text = reader.ReadToEnd();
            file.Encoding = reader.CurrentEncoding;
        }

        // Keep the line ending the file already uses, so an edit does not rewrite every line.
        if (text.IndexOf("\r\n", StringComparison.Ordinal) < 0 && text.IndexOf('\n') >= 0) file.NewLine = "\n";
        file.Lines.AddRange(text.Split('\n'));
        for (var i = 0; i < file.Lines.Count; i++) file.Lines[i] = file.Lines[i].TrimEnd('\r');
        // Split leaves a trailing empty entry for a file that ends with a newline.
        if (file.Lines.Count > 0 && file.Lines[file.Lines.Count - 1].Length == 0) file.Lines.RemoveAt(file.Lines.Count - 1);
        return file;
    }

    private static void Save(string path, IniText file)
    {
        var sb = new StringBuilder();
        foreach (var line in file.Lines) sb.Append(line).Append(file.NewLine);
        File.WriteAllText(path, sb.ToString(), file.Encoding);
    }

    // ---------------------------------------------------------------- lines

    private static List<string> SectionLines(string path, string section)
    {
        var res = new List<string>();
        if (string.IsNullOrEmpty(section)) return res;

        var lines = Load(path).Lines;
        var at = SectionAt(lines, section);
        if (at < 0) return res;

        var end = at + SectionLength(lines, at);
        for (var i = at + 1; i < end; i++)
        {
            var line = lines[i];
            if (line.Trim().Length == 0 || IsComment(line)) continue;
            res.Add(line);
        }
        return res;
    }

    /// <summary>Index of the first header naming <paramref name="section"/>, or -1.</summary>
    private static int SectionAt(List<string> lines, string section)
    {
        for (var i = 0; i < lines.Count; i++)
            if (SectionName(lines[i], out var name) && SameName(name, section)) return i;
        return -1;
    }

    /// <summary>How many lines the section at <paramref name="at"/> spans, header included.</summary>
    private static int SectionLength(List<string> lines, int at)
    {
        var i = at + 1;
        while (i < lines.Count && !SectionName(lines[i], out _)) i++;
        return i - at;
    }

    private static bool SectionName(string line, out string name)
    {
        name = null;
        var t = line == null ? "" : line.Trim();
        if (t.Length < 2 || t[0] != '[') return false;
        var close = t.IndexOf(']');
        if (close < 0) return false;
        name = t.Substring(1, close - 1).Trim();
        return true;
    }

    /// <summary>Win32 treats <c>;</c> as a comment; <c>#</c> is an ordinary line.</summary>
    private static bool IsComment(string line) => line.TrimStart().StartsWith(";", StringComparison.Ordinal);

    private static bool SplitEntry(string line, out string key, out string value)
    {
        key = null;
        value = null;
        var at = line == null ? -1 : line.IndexOf('=');
        if (at < 0) return false;
        key = line.Substring(0, at).Trim();
        value = line.Substring(at + 1);
        return true;
    }

    /// <summary>Trim the value, then drop one surrounding pair of double quotes, as Win32 does.</summary>
    private static string CleanValue(string raw)
    {
        var v = raw.Trim(' ', '\t');
        if (v.Length >= 2 && v[0] == '"' && v[v.Length - 1] == '"') v = v.Substring(1, v.Length - 2);
        return v;
    }

    private static bool SameName(string a, string b)
        => string.Equals(a == null ? "" : a.Trim(), b == null ? "" : b.Trim(), StringComparison.OrdinalIgnoreCase);
}
