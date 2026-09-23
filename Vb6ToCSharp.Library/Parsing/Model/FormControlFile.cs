using System;
using System.Collections.Generic;
using System.Linq;

namespace Vb6ToCSharp.Parsing.Model;

/// <summary>A parsed .frm / .ctl / .dob file: designer tree, attributes and code.</summary>
public sealed class FormControlFile
{
    public string Path { get; set; }
    public List<OcxRef> Objects { get; } = new();
    public ControlWithType Root { get; set; }
    public Dictionary<string, string> Attributes { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string Code { get; set; } = "";

    public string Name => Attributes.TryGetValue("VB_Name", out var n) ? n : Root?.Name ?? "";
    public bool IsMdiForm => Root?.Type == "VB.MDIForm";
    public bool IsUserControl => Root?.Type == "VB.UserControl";
    public bool IsMdiChild => Root != null && Root.Bool("MDIChild");

    public IEnumerable<ControlWithType> AllControls() => Root == null ? Enumerable.Empty<ControlWithType>() : Root.Descendants();

    /// <summary>Names of control arrays (controls declared with an Index).</summary>
    public HashSet<string> ControlArrays => new(AllControls().Where(c => c.IsArrayElement).Select(c => c.Name), StringComparer.OrdinalIgnoreCase);

    /// <summary>First control named <paramref name="name"/> (any array element), or null.</summary>
    public ControlWithType Find(string name) =>
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