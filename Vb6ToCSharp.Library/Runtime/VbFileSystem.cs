using System;
using System.Collections.Generic;
using System.IO;
using Vb6ToCSharp.Runtime.Model;

namespace Vb6ToCSharp.Runtime;

/// <summary>
/// The VB6 file functions this converter's own code calls, replacing
/// <c>Microsoft.VisualBasic.FileSystem</c>. <see cref="Dir(string, FileAttribute)"/> keeps VB's
/// pattern matching and its stateful continuation through <see cref="Dir()"/>.
/// </summary>
public static class VbFileSystem
{
    private static readonly Queue<string> dirMatches = new Queue<string>();

    public static void ChDir(string Path)
    {
        if (string.IsNullOrEmpty(Path))
            throw new ArgumentException("Argument 'Path' cannot be empty.", nameof(Path));
        Directory.SetCurrentDirectory(Path);
    }

    public static string CurDir() => Directory.GetCurrentDirectory();

    /// <summary>The first name matching the pattern (wildcards allowed), or "" when nothing matches.</summary>
    public static string Dir(string PathName, FileAttribute Attributes = FileAttribute.Normal)
    {
        if (PathName == null) throw new ArgumentNullException(nameof(PathName));

        dirMatches.Clear();
        // VB treats an empty pattern as "every entry of the current directory".
        var spec = PathName.Length == 0 ? "*" : PathName;
        var folder = System.IO.Path.GetDirectoryName(spec);
        var pattern = System.IO.Path.GetFileName(spec);
        if (pattern.Length == 0) { folder = spec; pattern = "*"; }
        if (string.IsNullOrEmpty(folder)) folder = CurDir();

        if (!Directory.Exists(folder)) return "";

        var wantDirectories = (Attributes & FileAttribute.Directory) != 0;
        try
        {
            foreach (var entry in Directory.GetFileSystemEntries(folder, pattern))
            {
                if (Directory.Exists(entry) && !wantDirectories) continue;
                dirMatches.Enqueue(System.IO.Path.GetFileName(entry));
            }
        }
        catch (IOException)
        {
            return "";
        }

        return Dir();
    }

    /// <summary>The next name of the pattern the last <see cref="Dir(string, FileAttribute)"/> matched.</summary>
    public static string Dir() => dirMatches.Count == 0 ? "" : dirMatches.Dequeue();

    public static DateTime FileDateTime(string PathName)
    {
        if (Directory.Exists(PathName)) return Directory.GetLastWriteTime(PathName);
        if (!File.Exists(PathName)) throw new FileNotFoundException("File not found.", PathName);
        return File.GetLastWriteTime(PathName);
    }

    public static long FileLen(string PathName)
    {
        if (!File.Exists(PathName)) throw new FileNotFoundException("File not found.", PathName);
        return new FileInfo(PathName).Length;
    }

    public static void SetAttr(string PathName, FileAttribute Attributes)
    {
        if (!File.Exists(PathName) && !Directory.Exists(PathName))
            throw new FileNotFoundException("File not found.", PathName);

        var attrs = FileAttributes.Normal;
        if ((Attributes & FileAttribute.ReadOnly) != 0) attrs |= FileAttributes.ReadOnly;
        if ((Attributes & FileAttribute.Hidden) != 0) attrs |= FileAttributes.Hidden;
        if ((Attributes & FileAttribute.System) != 0) attrs |= FileAttributes.System;
        if ((Attributes & FileAttribute.Archive) != 0) attrs |= FileAttributes.Archive;
        File.SetAttributes(PathName, attrs);
    }
}
