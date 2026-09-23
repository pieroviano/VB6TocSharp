using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Vb6ToCSharp.ItemConversion;

/// <summary>A VB6 project group (.vbg): its projects, the startup project first.</summary>
public sealed class ProjectGroupInfo
{
    public string Path { get; private set; } = "";
    public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);

    /// <summary>Full paths of the group's .vbp files: the startup project first, then in file order.</summary>
    public List<string> Projects { get; } = new();

    public static ProjectGroupInfo Load(string path)
    {
        var full = System.IO.Path.GetFullPath(path);
        var res = Parse(File.Exists(full) ? File.ReadAllText(full, Encoding.Default) : "", System.IO.Path.GetDirectoryName(full) ?? "");
        res.Path = full;
        return res;
    }

    /// <summary>Reads <c>StartupProject=</c> / <c>Project=</c> lines; paths are relative to <paramref name="folder"/>.</summary>
    public static ProjectGroupInfo Parse(string text, string folder)
    {
        var res = new ProjectGroupInfo();
        string startup = null;
        foreach (var raw in (text ?? "").Replace("\r\n", "\n").Split('\n'))
        {
            var l = raw.Trim();
            var eq = l.IndexOf('=');
            if (eq <= 0) continue;
            var key = l.Substring(0, eq).Trim();
            var value = l.Substring(eq + 1).Trim().Trim('"');
            if (value == "") continue;
            var isStartup = key.Equals("StartupProject", StringComparison.OrdinalIgnoreCase);
            if (!isStartup && !key.Equals("Project", StringComparison.OrdinalIgnoreCase)) continue;
            var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(folder, value));
            if (isStartup) startup = path;
            if (!res.Projects.Contains(path, StringComparer.OrdinalIgnoreCase)) res.Projects.Add(path);
        }
        if (startup != null)
        {
            res.Projects.RemoveAll(p => p.Equals(startup, StringComparison.OrdinalIgnoreCase));
            res.Projects.Insert(0, startup);
        }
        return res;
    }
}