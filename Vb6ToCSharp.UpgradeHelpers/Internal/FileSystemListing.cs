using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Vb6ToCSharp.UpgradeHelpers.Internal;

/// <summary>Shared logic of the VB6 Drive/Dir/FileListBox controls (both UI stacks).</summary>
internal static class FileSystemListing
{
    private static readonly Regex DriveText = new(@"^\s*([A-Za-z]):", RegexOptions.CultureInvariant);

    /// <summary>VB6 DriveListBox items: <c>"c: [label]"</c> (label only when the drive is ready).</summary>
    internal static List<string> DriveItems()
    {
        var items = new List<string>();
        foreach (var d in DriveInfo.GetDrives())
        {
            var letter = d.Name.Substring(0, 2).ToLowerInvariant();
            string label = null;
            try
            {
                if (d.DriveType is DriveType.Fixed or DriveType.Removable && d.IsReady) label = d.VolumeLabel;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Not ready / no access: letter only.
            }
            items.Add(string.IsNullOrEmpty(label) ? letter : $"{letter} [{label}]");
        }
        return items;
    }

    /// <summary>Drive letter (lower case, e.g. <c>"c:"</c>) of VB drive text: "c", "c:", "C:\x", "c: [label]".</summary>
    internal static string DriveLetter(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var m = DriveText.Match(text);
        if (m.Success) return m.Groups[1].Value.ToLowerInvariant() + ":";
        var t = text.Trim();
        return t.Length == 1 && char.IsLetter(t[0]) ? char.ToLowerInvariant(t[0]) + ":" : null;
    }

    /// <summary>Index of the item of <paramref name="items"/> for the drive of <paramref name="text"/>; -1 if none.</summary>
    internal static int FindDrive(IList<string> items, string text)
    {
        var letter = DriveLetter(text);
        if (letter == null) return -1;
        for (var i = 0; i < items.Count; i++)
            if (items[i].StartsWith(letter, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    /// <summary>Full, existing directory for a VB6 path (accepts "c: [label]" drive text).</summary>
    internal static string ResolveDirectory(string path, string current)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new DirectoryNotFoundException("Path not found");
        var p = path.Trim();
        var bracket = p.IndexOf(" [", StringComparison.Ordinal);
        if (bracket > 0 && p.EndsWith("]", StringComparison.Ordinal)) p = p.Substring(0, bracket);
        if (p.Length == 2 && p[1] == ':') p += "\\";
        var full = Path.GetFullPath(Path.IsPathRooted(p) || current == null ? p : Path.Combine(current, p));
        if (!Directory.Exists(full)) throw new DirectoryNotFoundException("Path not found");
        return full.Length > 3 ? full.TrimEnd('\\', '/') : full;
    }

    internal static List<string> SplitPatterns(string pattern)
    {
        var parts = (pattern ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
        if (parts.Count == 0) parts.Add("*.*");
        return parts;
    }

    /// <summary>DOS wildcard match (<c>*</c>, <c>?</c>; <c>*.*</c> matches names without extension too).</summary>
    internal static bool WildcardMatch(string name, string pattern)
    {
        if (pattern is "*" or "*.*") return true;
        var sb = new StringBuilder("^");
        foreach (var ch in pattern)
        {
            sb.Append(ch switch
            {
                '*' => ".*",
                '?' => ".",
                _ => Regex.Escape(ch.ToString()),
            });
        }
        sb.Append('$');
        return Regex.IsMatch(name, sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    /// <summary>File attribute filter of the VB6 FileListBox.</summary>
    internal struct FileFilter
    {
        public bool Archive, Hidden, Normal, ReadOnly, System;

        public static FileFilter Default => new() { Archive = true, Normal = true, ReadOnly = true };

        /// <remarks>
        /// Hidden/System/ReadOnly files are excluded unless their flag is set; the remaining files are shown when
        /// they carry the archive bit and <see cref="Archive"/> is set, or have no archive bit and
        /// <see cref="Normal"/> (or the flag of one of their other attributes) is set.
        /// </remarks>
        public bool Accepts(FileAttributes a)
        {
            var hidden = (a & FileAttributes.Hidden) != 0;
            var system = (a & FileAttributes.System) != 0;
            var readOnly = (a & FileAttributes.ReadOnly) != 0;
            if (hidden && !Hidden || system && !System || readOnly && !ReadOnly) return false;
            if ((a & FileAttributes.Archive) != 0) return Archive;
            return Normal || hidden && Hidden || system && System || readOnly && ReadOnly;
        }
    }

    /// <summary>File names (no path) of <paramref name="directory"/> matching the patterns and filter, sorted.</summary>
    internal static List<string> Files(string directory, string pattern, FileFilter filter)
    {
        var patterns = SplitPatterns(pattern);
        var result = new List<string>();
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return result;
        IEnumerable<FileInfo> files;
        try
        {
            files = new DirectoryInfo(directory).EnumerateFiles().ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return result;
        }
        foreach (var f in files)
        {
            if (!patterns.Any(p => WildcardMatch(f.Name, p))) continue;
            if (filter.Accepts(f.Attributes)) result.Add(f.Name);
        }
        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    /// <summary>VB6 DirListBox model: ancestors (root first, current last) then subdirectories.</summary>
    internal sealed class DirModel
    {
        /// <summary>Full paths: <see cref="AncestorCount"/> ancestors incl. current, then subdirectories.</summary>
        public List<string> FullPaths { get; } = new();

        /// <summary>Display texts aligned with <see cref="FullPaths"/>.</summary>
        public List<string> Display { get; } = new();

        public int AncestorCount { get; private set; }

        public int SubdirectoryCount => FullPaths.Count - AncestorCount;

        public void Build(string path)
        {
            FullPaths.Clear();
            Display.Clear();
            var chain = new List<string>();
            for (var d = new DirectoryInfo(path); d != null; d = d.Parent) chain.Insert(0, d.FullName);
            foreach (var a in chain)
            {
                FullPaths.Add(a);
                Display.Add(a.Length <= 3 ? a.ToLowerInvariant() : Path.GetFileName(a.TrimEnd('\\', '/')));
            }
            AncestorCount = chain.Count;
            try
            {
                foreach (var s in new DirectoryInfo(path).EnumerateDirectories()
                             .Where(s => (s.Attributes & FileAttributes.Hidden) == 0)
                             .Select(s => s.FullName)
                             .OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
                {
                    FullPaths.Add(s);
                    Display.Add(Path.GetFileName(s));
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Unreadable directory: ancestors only.
            }
        }

        /// <summary>VB6 <c>List(index)</c>: 0.. subdirectories, -1 current, -2 parent, …; "" out of range.</summary>
        public string GetList(int index)
        {
            var pos = ToPosition(index);
            return pos >= 0 && pos < FullPaths.Count ? FullPaths[pos] : "";
        }

        /// <summary>VB index → item position.</summary>
        public int ToPosition(int index) => AncestorCount + index;

        /// <summary>Item position → VB index.</summary>
        public int ToIndex(int position) => position - AncestorCount;
    }
}
