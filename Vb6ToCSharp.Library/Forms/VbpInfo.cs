using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Vb6ToCSharp.FormConversion;

/// <summary>Project-level facts from a .vbp the form conversion needs.</summary>
public sealed class VbpInfo
{
    public string Path { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string Startup { get; private set; } = "";
    public string Type { get; private set; } = "Exe";
    public List<OcxRef> Objects { get; } = new();
    public List<string> Forms { get; } = new();
    public List<string> UserControls { get; } = new();
    /// <summary>Conditional compilation arguments (Project Properties, VBP "CondComp"): name to VB6 value expression.</summary>
    public Dictionary<string, string> CondComp { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Type-library references ("Reference=" lines: guid, version, path, description).</summary>
    public List<string> References { get; } = new();

    public string Folder => System.IO.Path.GetDirectoryName(Path) ?? "";
    public bool StartsWithSubMain => Startup.Equals("Sub Main", StringComparison.OrdinalIgnoreCase) || Startup == "";

    public static VbpInfo Load(string path)
    {
        var res = Parse(File.Exists(path) ? File.ReadAllText(path, Encoding.Default) : "");
        res.Path = path ?? "";
        return res;
    }

    public static VbpInfo Parse(string text)
    {
        var res = new VbpInfo();
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var l = raw.Trim();
            var eq = l.IndexOf('=');
            if (eq <= 0) continue;
            var key = l.Substring(0, eq).Trim();
            var value = l.Substring(eq + 1).Trim();
            switch (key.ToLowerInvariant())
            {
                case "name": res.Name = value.Trim('"'); break;
                case "startup": res.Startup = value.Trim('"'); break;
                case "type": res.Type = value; break;
                case "object":
                    var o = OcxRef.Parse(value);
                    if (o != null && res.Objects.All(x => x.Guid != o.Guid)) res.Objects.Add(o);
                    break;
                case "form": res.Forms.Add(value.Contains(';') ? value.Split(';')[1].Trim() : value); break;
                case "reference":
                    res.References.Add(value);
                    break;
                case "condcomp":
                    foreach (var arg in value.Trim('"').Split(':'))
                    {
                        var e = arg.IndexOf('=');
                        if (e > 0) res.CondComp[arg.Substring(0, e).Trim()] = arg.Substring(e + 1).Trim();
                    }
                    break;
                case "usercontrol": res.UserControls.Add(value.Contains(';') ? value.Split(';')[1].Trim() : value); break;
            }
        }
        return res;
    }

    private List<VbFormFile> parsed;

    /// <summary>Designer files of the project's forms and user controls (parsed once).</summary>
    public List<VbFormFile> DesignerFiles()
    {
        if (parsed != null) return parsed;
        parsed = new List<VbFormFile>();
        foreach (var f in Forms.Concat(UserControls))
        {
            var p = System.IO.Path.Combine(Folder, f);
            if (File.Exists(p)) parsed.Add(FrmParser.ParseFile(p));
        }
        return parsed;
    }

    /// <summary>Name of the project's MDI form, or "".</summary>
    public string MdiFormName => DesignerFiles().FirstOrDefault(f => f.IsMdiForm)?.Name ?? "";

    /// <summary>Names of the project's user controls (a form control typed <c>Project.Name</c> is one of them).</summary>
    public HashSet<string> UserControlNames => new(DesignerFiles().Where(f => f.IsUserControl).Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
}
