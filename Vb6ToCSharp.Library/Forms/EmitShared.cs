using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Vb6ToCSharp.FormConversion;

/// <summary>A binary resource extracted from the .frx.</summary>
public sealed class FormResource
{
    public string Name { get; set; } = "";
    public byte[] Data { get; set; } = new byte[0];
    public FrxBlobKind Kind { get; set; }
}

/// <summary>Output of a UI emitter.</summary>
public sealed class EmitResult
{
    /// <summary>Designer.cs (WinForms) or XAML (WPF).</summary>
    public string Designer { get; set; } = "";
    /// <summary>Members added to the code file (WPF: non-visual fields, extras method).</summary>
    public string CodeMembers { get; set; } = "";
    /// <summary>Statements run in the constructor right after InitializeComponent.</summary>
    public string ConstructorCode { get; set; } = "";
    public List<FormResource> Resources { get; } = new();
    /// <summary>Type libraries of controls hosted through AxHost.</summary>
    public HashSet<string> HostedLibraries { get; } = new(StringComparer.OrdinalIgnoreCase);
    public bool UsesPowerPacks { get; set; }
    public bool UsesWindowsFormsHost { get; set; }
}

internal static class Emit
{
    public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>C# string literal.</summary>
    public static string Lit(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (var ch in s ?? "")
        {
            switch (ch)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\r': sb.Append("\\r"); break;
                case '\n': sb.Append("\\n"); break;
                case '\t': sb.Append("\\t"); break;
                case '\0': sb.Append("\\0"); break;
                default:
                    if (ch < 32) sb.Append("\\u").Append(((int)ch).ToString("X4", Inv));
                    else sb.Append(ch);
                    break;
            }
        }
        return sb.Append('"').ToString();
    }

    /// <summary>XAML attribute value (quoted, escaped).</summary>
    public static string Xml(string s) =>
        "\"" + (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("\r", "&#13;").Replace("\n", "&#10;") + "\"";

    public static string B(bool v) => v ? "true" : "false";
    public static string F(double v) => ((float)v).ToString("R", Inv) + "F";
    public static string N(double v) => v.ToString("R", Inv);

    /// <summary>Resolved string property: literal text or <c>$"x.frx":off</c> string data.</summary>
    public static string TextOf(VbFormFile file, VbControl c, string prop)
    {
        var p = c.Get(prop);
        if (p == null) return null;
        var frx = p.Frx;
        if (frx == null) return p.Text;
        var r = FrxReader.Open(file.FrxPath(frx));
        return r == null ? "" : r.ReadString(frx.Offset);
    }

    public static List<string> ListOf(VbFormFile file, VbControl c, string prop)
    {
        var frx = c.Get(prop)?.Frx;
        var r = frx == null ? null : FrxReader.Open(file.FrxPath(frx));
        return r == null ? new List<string>() : r.ReadList(frx.Offset);
    }

    public static List<int> ItemDataOf(VbFormFile file, VbControl c, string prop)
    {
        var frx = c.Get(prop)?.Frx;
        var r = frx == null ? null : FrxReader.Open(file.FrxPath(frx));
        return r == null ? new List<int>() : r.ReadItemData(frx.Offset);
    }

    /// <summary>Picture-type property data, or null.</summary>
    public static FormResource Picture(VbFormFile file, VbControl c, string prop, string name)
    {
        var frx = c.Get(prop)?.Frx;
        var r = frx == null ? null : FrxReader.Open(file.FrxPath(frx));
        if (r == null) return null;
        var data = r.ReadBlob(frx.Offset);
        if (data.Length == 0) return null;
        return new FormResource { Name = name, Data = data, Kind = FrxReader.Sniff(data) };
    }

    /// <summary>VB6 menu shortcut (<c>^S</c>, <c>+{F1}</c>, <c>%{BKSP}</c>) as <c>Keys</c> flags, or "".</summary>
    public static string Shortcut(string vb, string keysType)
    {
        if (string.IsNullOrEmpty(vb)) return "";
        var mods = new List<string>();
        var i = 0;
        while (i < vb.Length && "^+%".IndexOf(vb[i]) >= 0)
        {
            mods.Add(vb[i] == '^' ? "Control" : vb[i] == '+' ? "Shift" : "Alt");
            i++;
        }
        var k = vb.Substring(i).Trim('{', '}').ToUpperInvariant();
        string key;
        if (k.Length == 1 && char.IsLetter(k[0])) key = k;
        else if (k.Length == 1 && char.IsDigit(k[0])) key = "D" + k;
        else if (k.StartsWith("F", StringComparison.Ordinal) && int.TryParse(k.Substring(1), out var fn) && fn is >= 1 and <= 24) key = "F" + fn;
        else
        {
            key = k switch
            {
                "DEL" or "DELETE" => "Delete",
                "INSERT" or "INS" => "Insert",
                "BKSP" or "BACKSPACE" or "BS" => "Back",
                _ => "",
            };
        }
        if (key == "") return "";
        return string.Join(" | ", mods.Select(m => keysType + "." + m).Concat(new[] { keysType + "." + key }));
    }

    public static readonly string[] WinFormsCursors =
    {
        "Default", "Arrow", "Cross", "IBeam", "Default", "SizeAll", "SizeNESW", "SizeNS", "SizeNWSE", "SizeWE", "UpArrow",
        "WaitCursor", "No", "AppStarting", "Help", "SizeAll",
    };

    public static readonly string[] WpfCursors =
    {
        "Arrow", "Arrow", "Cross", "IBeam", "Arrow", "SizeAll", "SizeNESW", "SizeNS", "SizeNWSE", "SizeWE", "UpArrow",
        "Wait", "No", "AppStarting", "Help", "SizeAll",
    };

    /// <summary>Properties that belong to the VB extender (not persisted by a UserControl/ActiveX itself).</summary>
    public static bool IsExtenderProperty(string name) =>
        name.StartsWith("_", StringComparison.Ordinal) || name.Contains(".") ||
        new[] { "Left", "Top", "Width", "Height", "TabIndex", "TabStop", "Visible", "Enabled", "ToolTipText", "Index", "Tag",
                "HelpContextID", "Name", "DragMode", "DragIcon", "WhatsThisHelpID", "CausesValidation", "Align", "Container",
                "OLEDragMode", "OLEDropMode" }.Contains(name, StringComparer.OrdinalIgnoreCase);

    /// <summary>A designer-persisted value as a C# literal for reflection-based assignment.</summary>
    public static string ValueLiteral(VbProperty p)
    {
        if (p.IsQuoted) return Lit(p.Text);
        var raw = p.Raw.Trim();
        if (raw.StartsWith("&H", StringComparison.OrdinalIgnoreCase)) return N(VbValue.ToNumber(raw));
        if (raw.Equals("True", StringComparison.OrdinalIgnoreCase)) return "true";
        if (raw.Equals("False", StringComparison.OrdinalIgnoreCase)) return "false";
        if (double.TryParse(raw, NumberStyles.Float, Inv, out var d)) return raw.Contains(".") || raw.Contains("E") ? N(d) : ((long)d).ToString(Inv);
        return Lit(p.Text);
    }
}
