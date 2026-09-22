using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Vb6ToCSharp.FormConversion;

/// <summary>A reference to data stored in the form's .frx file (<c>"frmX.frx":01A2</c>; <c>$"..."</c> marks a string).</summary>
public sealed class FrxRef
{
    public string File { get; }
    public int Offset { get; }
    public bool IsString { get; }

    public FrxRef(string file, int offset, bool isString)
    {
        File = file;
        Offset = offset;
        IsString = isString;
    }
}

/// <summary>One <c>Name = Value</c> line of a .frm/.ctl designer section; nested BeginProperty groups give dotted names.</summary>
public sealed class VbProperty
{
    public string Name { get; }
    /// <summary>Value text as written, trailing <c>'comment</c> removed.</summary>
    public string Raw { get; }

    public VbProperty(string name, string raw)
    {
        Name = name;
        Raw = raw;
    }

    public bool IsQuoted => Raw.Length >= 2 && Raw[0] == '"' && Raw[Raw.Length - 1] == '"';

    /// <summary>Value with quotes removed and doubled quotes collapsed.</summary>
    public string Text => IsQuoted ? Raw.Substring(1, Raw.Length - 2).Replace("\"\"", "\"") : Raw;

    public FrxRef Frx
    {
        get
        {
            var s = Raw;
            var isString = s.StartsWith("$", StringComparison.Ordinal);
            if (isString) s = s.Substring(1);
            if (!s.StartsWith("\"", StringComparison.Ordinal)) return null;
            var close = s.IndexOf('"', 1);
            if (close < 0 || close + 1 >= s.Length || s[close + 1] != ':') return null;
            var file = s.Substring(1, close - 1);
            var off = s.Substring(close + 2).Trim();
            return int.TryParse(off, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var offset)
                ? new FrxRef(file, offset, isString)
                : null;
        }
    }

    public double Number(double def = 0) => VbValue.ToNumber(Raw, def);
    public bool Bool(bool def = false) => VbValue.ToBool(Raw, def);
}

/// <summary>A <c>Begin Lib.Class Name ... End</c> block.</summary>
public sealed class VbControl
{
    private readonly Dictionary<string, VbProperty> byName = new(StringComparer.OrdinalIgnoreCase);

    public string Type { get; }
    public string Name { get; }
    public VbControl Parent { get; internal set; }
    public List<VbProperty> Properties { get; } = new();
    public List<VbControl> Children { get; } = new();

    public VbControl(string type, string name)
    {
        Type = type;
        Name = name;
    }

    /// <summary>Type library part of the type (<c>VB</c>, <c>MSComctlLib</c>, the project name for a UserControl).</summary>
    public string Library => Type.Contains('.') ? Type.Substring(0, Type.IndexOf('.')) : "";
    public string ClassName => Type.Contains('.') ? Type.Substring(Type.IndexOf('.') + 1) : Type;

    /// <summary>Control array index, or null.</summary>
    public int? Index => Has("Index") ? (int)Num("Index") : null;
    public bool IsArrayElement => Index.HasValue;
    /// <summary>Unique .NET member name: <c>Name</c>, or <c>_Name_Index</c> for array elements (VBUC style; <c>Name</c> is the array).</summary>
    public string MemberName => Index.HasValue ? "_" + Name + "_" + Index.Value : Name;

    internal void Add(VbProperty p)
    {
        Properties.Add(p);
        byName[p.Name] = p;
    }

    public bool Has(string name) => Get(name) != null;

    /// <summary>Property by name; <c>A.B.Width</c> also finds <c>A.B.Object.Width</c> (VB6 prefixes names that clash with the extender).</summary>
    public VbProperty Get(string name)
    {
        if (byName.TryGetValue(name, out var p)) return p;
        var dot = name.LastIndexOf('.');
        return byName.TryGetValue(dot < 0 ? "Object." + name : name.Substring(0, dot) + ".Object" + name.Substring(dot), out p) ? p : null;
    }
    public string Text(string name, string def = "") => Get(name)?.Text ?? def;
    public double Num(string name, double def = 0) => Get(name)?.Number(def) ?? def;
    public bool Bool(string name, bool def = false) => Get(name)?.Bool(def) ?? def;

    /// <summary>Properties below a BeginProperty group (<c>Panels</c> → <c>Panels.Panel1.Text</c> …), keyed by the remaining path.</summary>
    public IEnumerable<VbProperty> Group(string prefix)
    {
        var p = prefix + ".";
        return Properties.Where(x => x.Name.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Distinct first-level sub-group names of a group, in file order (<c>Panel1</c>, <c>Panel2</c>…).</summary>
    public List<string> SubGroups(string prefix)
    {
        var res = new List<string>();
        foreach (var x in Group(prefix))
        {
            var rest = x.Name.Substring(prefix.Length + 1);
            var dot = rest.IndexOf('.');
            if (dot < 0) continue;
            var g = rest.Substring(0, dot);
            if (!res.Contains(g, StringComparer.OrdinalIgnoreCase)) res.Add(g);
        }
        return res;
    }

    public IEnumerable<VbControl> Descendants()
    {
        foreach (var c in Children)
        {
            yield return c;
            foreach (var d in c.Descendants()) yield return d;
        }
    }

    public override string ToString() => Type + " " + MemberName;
}

/// <summary>An <c>Object={guid}#ver#lcid; file.ocx</c> reference.</summary>
public sealed class OcxRef
{
    public string Guid { get; }
    public string Version { get; }
    public string Lcid { get; }
    public string File { get; }

    public OcxRef(string guid, string version, string lcid, string file)
    {
        Guid = guid;
        Version = version;
        Lcid = lcid;
        File = file;
    }

    public int VersionMajor => int.TryParse(Version.Split('.')[0], out var v) ? v : 1;
    public int VersionMinor => Version.Contains('.') && int.TryParse(Version.Split('.')[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v) ? v : 0;

    /// <summary>Parses the value part of <c>Object=</c> (.vbp) or <c>Object = "..."; "..."</c> (.frm).</summary>
    public static OcxRef Parse(string value)
    {
        var parts = value.Split(';');
        var id = parts[0].Trim().Trim('"');
        var file = parts.Length > 1 ? parts[1].Trim().Trim('"') : "";
        var bits = id.Split('#');
        if (bits.Length < 1 || !bits[0].StartsWith("{", StringComparison.Ordinal)) return null;
        return new OcxRef(bits[0].ToUpperInvariant(), bits.Length > 1 ? bits[1] : "1.0", bits.Length > 2 ? bits[2] : "0", file);
    }
}

/// <summary>A parsed .frm / .ctl / .dob file: designer tree, attributes and code.</summary>
public sealed class VbFormFile
{
    public string Path { get; set; }
    public List<OcxRef> Objects { get; } = new();
    public VbControl Root { get; set; }
    public Dictionary<string, string> Attributes { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string Code { get; set; } = "";

    public string Name => Attributes.TryGetValue("VB_Name", out var n) ? n : Root?.Name ?? "";
    public bool IsMdiForm => Root?.Type == "VB.MDIForm";
    public bool IsUserControl => Root?.Type == "VB.UserControl";
    public bool IsMdiChild => Root != null && Root.Bool("MDIChild");

    public IEnumerable<VbControl> AllControls() => Root == null ? Enumerable.Empty<VbControl>() : Root.Descendants();

    /// <summary>Names of control arrays (controls declared with an Index).</summary>
    public HashSet<string> ControlArrays => new(AllControls().Where(c => c.IsArrayElement).Select(c => c.Name), StringComparer.OrdinalIgnoreCase);

    /// <summary>First control named <paramref name="name"/> (any array element), or null.</summary>
    public VbControl Find(string name) =>
        string.Equals(Root?.Name, name, StringComparison.OrdinalIgnoreCase)
            ? Root
            : AllControls().FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

    public string FrxPath(FrxRef r)
    {
        if (r == null) return null;
        var dir = System.IO.Path.GetDirectoryName(Path ?? "") ?? "";
        return System.IO.Path.Combine(dir, r.File);
    }
}

/// <summary>Parses the designer section of .frm/.ctl files.</summary>
public static class FrmParser
{
    public static VbFormFile ParseFile(string path)
    {
        var f = Parse(File.ReadAllText(path, Encoding.Default));
        f.Path = path;
        return f;
    }

    public static VbFormFile Parse(string text)
    {
        var res = new VbFormFile();
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var stack = new Stack<VbControl>();
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
                var c = new VbControl(Word(l, 1), Word(l, 2));
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
            stack.Peek().Add(new VbProperty(name, StripComment(l.Substring(eq + 1).Trim())));
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
                if (eq > 0) res.Attributes[body.Substring(0, eq).Trim()] = new VbProperty("", body.Substring(eq + 1).Trim()).Text;
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

/// <summary>VB6 literal parsing (designer values).</summary>
public static class VbValue
{
    public static double ToNumber(string raw, double def = 0)
    {
        var s = (raw ?? "").Trim().Trim('"');
        if (s == "") return def;
        if (s.StartsWith("&H", StringComparison.OrdinalIgnoreCase))
        {
            var hex = s.Substring(2).TrimEnd('&', '%');
            return long.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var h) ? (int)(uint)h : def;
        }
        if (string.Equals(s, "True", StringComparison.OrdinalIgnoreCase)) return -1;
        if (string.Equals(s, "False", StringComparison.OrdinalIgnoreCase)) return 0;
        return double.TryParse(s.TrimEnd('&', '%', '!', '#', '@'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : def;
    }

    public static bool ToBool(string raw, bool def = false)
    {
        var s = (raw ?? "").Trim();
        if (s == "") return def;
        if (string.Equals(s, "True", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(s, "False", StringComparison.OrdinalIgnoreCase)) return false;
        return ToNumber(s, def ? -1 : 0) != 0;
    }
}
