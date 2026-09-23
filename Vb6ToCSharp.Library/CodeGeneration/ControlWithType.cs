using System;
using System.Collections.Generic;
using System.Linq;

namespace Vb6ToCSharp.CodeGeneration;

/// <summary>A <c>Begin Lib.Class Name ... End</c> block.</summary>
public sealed class ControlWithType
{
    private readonly Dictionary<string, ItemProperty> byName = new(StringComparer.OrdinalIgnoreCase);

    public string Type { get; }
    public string Name { get; }
    public ControlWithType Parent { get; internal set; }
    public List<ItemProperty> Properties { get; } = new();
    public List<ControlWithType> Children { get; } = new();

    public ControlWithType(string type, string name)
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

    internal void Add(ItemProperty p)
    {
        Properties.Add(p);
        byName[p.Name] = p;
    }

    public bool Has(string name) => Get(name) != null;

    /// <summary>Property by name; <c>A.B.Width</c> also finds <c>A.B.Object.Width</c> (VB6 prefixes names that clash with the extender).</summary>
    public ItemProperty Get(string name)
    {
        if (byName.TryGetValue(name, out var p)) return p;
        var dot = name.LastIndexOf('.');
        return byName.TryGetValue(dot < 0 ? "Object." + name : name.Substring(0, dot) + ".Object" + name.Substring(dot), out p) ? p : null;
    }
    public string Text(string name, string def = "") => Get(name)?.Text ?? def;
    public double Num(string name, double def = 0) => Get(name)?.Number(def) ?? def;
    public bool Bool(string name, bool def = false) => Get(name)?.Bool(def) ?? def;

    /// <summary>Properties below a BeginProperty group (<c>Panels</c> → <c>Panels.Panel1.Text</c> …), keyed by the remaining path.</summary>
    public IEnumerable<ItemProperty> Group(string prefix)
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

    public IEnumerable<ControlWithType> Descendants()
    {
        foreach (var c in Children)
        {
            yield return c;
            foreach (var d in c.Descendants()) yield return d;
        }
    }

    public override string ToString() => Type + " " + MemberName;
}