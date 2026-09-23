using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Vb6ToCSharp.CodeGeneration;

namespace Vb6ToCSharp.Parsing;

/// <summary>Parses the designer section of .frm/.ctl files.</summary>
public static class FrmParser
{
    public static FormControlFile ParseFile(string path)
    {
        var f = Parse(File.ReadAllText(path, Encoding.Default));
        f.Path = path;
        return f;
    }

    public static FormControlFile Parse(string text)
    {
        var res = new FormControlFile();
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var stack = new Stack<ControlWithType>();
        var groups = new List<string>();
        var i = 0;
        for (; i < lines.Length; i++)
        {
            var l = lines[i].Trim();
            if (l == "") continue;
            if (stack.Count == 0)
            {
                if (l.StartsWith("Object", StringComparison.OrdinalIgnoreCase) && l.Contains('='))
                {
                    var o = OcxRef.Parse(l.Substring(l.IndexOf('=') + 1));
                    if (o != null) res.Objects.Add(o);
                    continue;
                }
                if (l.StartsWith("Attribute ", StringComparison.Ordinal)) break;
            }
            if (l.StartsWith("BeginProperty ", StringComparison.OrdinalIgnoreCase))
            {
                groups.Add(Word(l, 1));
                continue;
            }
            if (l.StartsWith("EndProperty", StringComparison.OrdinalIgnoreCase))
            {
                if (groups.Count > 0) groups.RemoveAt(groups.Count - 1);
                continue;
            }
            if (l.StartsWith("Begin ", StringComparison.Ordinal))
            {
                var c = new ControlWithType(Word(l, 1), Word(l, 2));
                if (stack.Count > 0)
                {
                    c.Parent = stack.Peek();
                    stack.Peek().Children.Add(c);
                }
                else
                {
                    res.Root = c;
                }
                stack.Push(c);
                groups.Clear();
                continue;
            }
            if (l == "End")
            {
                if (stack.Count > 0) stack.Pop();
                groups.Clear();
                if (stack.Count == 0 && res.Root != null)
                {
                    i++;
                    break;
                }
                continue;
            }
            if (stack.Count == 0) continue; // VERSION line etc.
            var eq = l.IndexOf('=');
            if (eq <= 0) continue;
            var name = l.Substring(0, eq).Trim();
            if (groups.Count > 0) name = string.Join(".", groups) + "." + name;
            stack.Peek().Add(new ItemProperty(name, StripComment(l.Substring(eq + 1).Trim())));
        }

        var code = new StringBuilder();
        var inAttributes = true;
        for (; i < lines.Length; i++)
        {
            var l = lines[i];
            if (inAttributes && l.StartsWith("Attribute ", StringComparison.Ordinal))
            {
                var body = l.Substring(10);
                var eq = body.IndexOf('=');
                if (eq > 0) res.Attributes[body.Substring(0, eq).Trim()] = new ItemProperty("", body.Substring(eq + 1).Trim()).Text;
                continue;
            }
            inAttributes = false;
            code.Append(l).Append("\r\n");
        }
        res.Code = code.ToString();
        return res;
    }

    private static string Word(string l, int n)
    {
        var parts = l.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > n ? parts[n] : "";
    }

    /// <summary>Drops a trailing <c>'comment</c> that is outside quotes.</summary>
    internal static string StripComment(string v)
    {
        var inQ = false;
        for (var k = 0; k < v.Length; k++)
        {
            if (v[k] == '"') inQ = !inQ;
            else if (v[k] == '\'' && !inQ) return v.Substring(0, k).TrimEnd();
        }
        return v;
    }
}