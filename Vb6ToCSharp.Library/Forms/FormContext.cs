using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Vb6ToCSharp.Modules;

namespace Vb6ToCSharp.FormConversion;

/// <summary>The designer file being converted, shared by the UI emitters and the code conversion (event adapters, member rewrites).</summary>
public sealed class FormContext
{
    private static readonly Regex subRx = new(@"^[ \t]*(?:(?:Private|Public|Friend|Static)[ \t]+)*Sub[ \t]+([A-Za-z_]\w*)[ \t]*\(", RegexOptions.Multiline | RegexOptions.IgnoreCase);

    public VbFormFile File { get; }
    public UiTarget Ui { get; }
    public VbpInfo Project { get; }
    /// <summary>Names of the Subs in the code section.</summary>
    public HashSet<string> Handlers { get; }
    public HashSet<string> Arrays { get; }

    /// <summary>Form being converted (null while converting modules and classes).</summary>
    public static FormContext Current { get; set; }

    public FormContext(VbFormFile file, UiTarget ui, VbpInfo project = null)
    {
        File = file;
        Ui = ui;
        Project = project;
        Handlers = new HashSet<string>(subRx.Matches(file.Code ?? "").Cast<Match>().Select(m => m.Groups[1].Value), StringComparer.OrdinalIgnoreCase);
        Arrays = file.ControlArrays;
    }

    public string ClassName => File.Name;

    /// <summary>Handler prefix of the root (<c>Form_Load</c>, <c>MDIForm_Load</c>, <c>UserControl_Resize</c>).</summary>
    public string RootPrefix => File.Root?.Type switch
    {
        "VB.MDIForm" => "MDIForm",
        "VB.UserControl" => "UserControl",
        "VB.PropertyPage" => "PropertyPage",
        _ => "Form",
    };

    public ControlInfo Info(VbControl c) => ControlCatalog.Lookup(c.Type, Ui, Project);

    public string HandlerName(VbControl c, string vbEvent) => (c == File.Root ? RootPrefix : c.Name) + "_" + vbEvent;

    /// <summary>Control (first array element) and VB event a Sub handles, by trying every <c>_</c> split.</summary>
    public bool TryResolveHandler(string method, out VbControl control, out string vbEvent)
    {
        control = null;
        vbEvent = "";
        for (var i = method.LastIndexOf('_'); i > 0; i = method.LastIndexOf('_', i - 1))
        {
            var name = method.Substring(0, i);
            var ev = method.Substring(i + 1);
            var c = name.Equals(RootPrefix, StringComparison.OrdinalIgnoreCase) ? File.Root : File.Find(name);
            if (c != null && ev != "")
            {
                control = c;
                vbEvent = ev;
                return true;
            }
        }
        return false;
    }

    /// <summary>Mapped events that have a handler in the code, in subscription order.</summary>
    public List<(string vbEvent, EventBinding binding, string handler)> EventsOf(VbControl c)
    {
        var res = new List<(string, EventBinding, string)>();
        var info = Info(c);
        var order = c == File.Root ? EventCatalog.RootEventOrder.Concat(EventCatalog.AllEvents).Distinct() : EventCatalog.AllEvents;
        foreach (var ev in order)
        {
            var h = HandlerName(c, ev);
            if (!Handlers.Contains(h)) continue;
            var b = EventCatalog.Resolve(info, ev, Ui);
            if (b == null || b.Special != "") continue;
            res.Add((ev, b, ExactHandler(h)));
        }
        return res;
    }

    /// <summary>Handler name with the capitalization used in the code.</summary>
    public string ExactHandler(string h) => Handlers.FirstOrDefault(x => x.Equals(h, StringComparison.OrdinalIgnoreCase)) ?? h;
}

/// <summary>Builds the .NET-signature adapter that forwards an event to the converted VB6-signature handler.</summary>
public static class EventAdapters
{
    private const string H = "Vb6ToCSharp.UpgradeHelpers.";

    public sealed class Param
    {
        public string Modifier = "";
        public string Type = "";
        public string Name = "";
    }

    /// <summary>Parameters of a converted prototype (<c>private void X(ref int Index, int KeyAscii) {</c>).</summary>
    public static List<Param> ParseParams(string prototype)
    {
        var res = new List<Param>();
        var open = prototype.IndexOf('(');
        var close = prototype.LastIndexOf(')');
        if (open < 0 || close < open) return res;
        var inner = prototype.Substring(open + 1, close - open - 1);
        var depth = 0;
        var cur = new StringBuilder();
        var parts = new List<string>();
        foreach (var ch in inner)
        {
            if (ch == '<' || ch == '(' || ch == '[') depth++;
            if (ch == '>' || ch == ')' || ch == ']') depth--;
            if (ch == ',' && depth == 0)
            {
                parts.Add(cur.ToString());
                cur.Clear();
            }
            else cur.Append(ch);
        }
        if (cur.ToString().Trim() != "") parts.Add(cur.ToString());
        foreach (var raw in parts)
        {
            var s = raw.Trim();
            var eq = s.IndexOf('=');
            if (eq > 0) s = s.Substring(0, eq).Trim();
            var p = new Param();
            foreach (var m in new[] { "ref ", "out ", "params ", "in " })
            {
                if (s.StartsWith(m, StringComparison.Ordinal))
                {
                    p.Modifier = m.Trim();
                    s = s.Substring(m.Length).Trim();
                }
            }
            var sp = s.LastIndexOf(' ');
            p.Type = sp > 0 ? s.Substring(0, sp).Trim() : "dynamic";
            p.Name = sp > 0 ? s.Substring(sp + 1).Trim() : s;
            res.Add(p);
        }
        return res;
    }

    /// <summary>Adapter code for the converted VB handler <paramref name="method"/>, "" when it is not a mapped event of the current form.</summary>
    public static string Build(string method, string prototype)
    {
        var ctx = FormContext.Current;
        if (ctx == null || !ctx.TryResolveHandler(method, out var ctl, out var ev)) return "";
        var b = EventCatalog.Resolve(ctx.Info(ctl), ev, ctx.Ui);
        if (b == null)
        {
            return "// TODO: VB6 event " + ev + " of " + ctl.Type + " has no .NET mapping; " + method + " is not wired.\r\n";
        }
        if (b.Direct) return "";
        var ps = ParseParams(prototype);
        var isArray = ctl != ctx.File.Root && ctx.Arrays.Contains(ctl.Name);
        var roles = new List<ArgRole?>();
        if (isArray) roles.Add(null);
        roles.AddRange(b.Roles.Select(r => (ArgRole?)r));

        switch (b.Special)
        {
            case "ctor":
                return "";
            case "InitProperties":
                return "public void InitProperties() { " + method + "(); }\r\n";
            case "ReadProperties":
            case "WriteProperties":
                var mod = ps.Count > 0 && ps[0].Modifier != "" ? ps[0].Modifier + " " : "";
                return "public void " + b.Special + "(" + H + "PropertyBag bag) { var v0 = bag; " + method + "(" + mod + "v0); }\r\n";
        }

        var sb = new StringBuilder();
        sb.Append("private void ").Append(method).Append("(object sender, ").Append(b.Args).Append(" e) { ");
        if (b.Guard != "") sb.Append(b.Guard).Append(' ');
        var args = new List<string>();
        var outs = new StringBuilder();
        for (var i = 0; i < ps.Count; i++)
        {
            var p = ps[i];
            var v = "v" + i;
            var role = i < roles.Count ? roles[i] : ArgRole.Other;
            var expr = role == null ? ctl.Name + ".GetIndex(sender)" : In(role.Value, ctx, ctl);
            sb.Append(p.Type).Append(' ').Append(v).Append(" = ").Append(expr == null ? "default(" + p.Type + ")" : "(" + p.Type + ")(" + expr + ")").Append("; ");
            args.Add((p.Modifier is "ref" or "out" ? p.Modifier + " " : "") + v);
            if (role != null && p.Modifier is "ref" or "out") outs.Append(Out(role.Value, ctx.Ui, v));
        }
        sb.Append(method).Append('(').Append(string.Join(", ", args)).Append("); ");
        sb.Append(outs);
        sb.Append("}\r\n");
        return sb.ToString();
    }

    private static string In(ArgRole role, FormContext ctx, VbControl ctl)
    {
        var wf = ctx.Ui == UiTarget.WinForms;
        var fh = wf ? H + "WinForms.FormsHelper." : H + "Wpf.FormsHelper.";
        switch (role)
        {
            case ArgRole.KeyAscii: return wf ? "(int)e.KeyChar" : "e.Text.Length > 0 ? (int)e.Text[0] : 0";
            case ArgRole.KeyCode: return wf ? "(int)e.KeyCode" : "System.Windows.Input.KeyInterop.VirtualKeyFromKey(e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key)";
            case ArgRole.KeyShift: return wf ? fh + "ShiftFrom(e.Modifiers)" : fh + "ShiftFrom(System.Windows.Input.Keyboard.Modifiers)";
            case ArgRole.MouseShift: return wf ? fh + "ShiftFrom(System.Windows.Forms.Control.ModifierKeys)" : fh + "ShiftFrom(System.Windows.Input.Keyboard.Modifiers)";
            case ArgRole.Button: return fh + (wf ? "ButtonFrom(e.Button)" : "ButtonFrom(e)");
            case ArgRole.X:
            case ArgRole.Y:
                return Coordinate(ctx, ctl, role == ArgRole.Y);
            case ArgRole.UnloadMode: return wf ? fh + "UnloadModeFrom(e.CloseReason)" : "0";
            case ArgRole.Node: return wf ? "e.Node" : "e.NewValue as System.Windows.Controls.TreeViewItem";
            case ArgRole.Item: return wf ? "e.Item" : "e.AddedItems.Count > 0 ? e.AddedItems[0] : null";
            case ArgRole.Column: return "((System.Windows.Forms.ListView)sender).Columns[e.Column]";
            case ArgRole.ToolButton: return wf ? "e.ClickedItem" : "e.OriginalSource";
            case ArgRole.Panel: return "e.ClickedItem as System.Windows.Forms.ToolStripStatusLabel";
            case ArgRole.ItemIndex: return "e.Index";
            case ArgRole.Date: return "e.Start";
            case ArgRole.NewString: return "e.Label";
            default: return null; // Cancel, CancelEdit, PreviousTab, Other: VB default value
        }
    }

    private static string Out(ArgRole role, UiTarget ui, string v)
    {
        var wf = ui == UiTarget.WinForms;
        switch (role)
        {
            case ArgRole.KeyAscii: return wf ? "e.KeyChar = (char)" + v + "; if (" + v + " == 0) e.Handled = true; " : "if (" + v + " == 0) e.Handled = true; ";
            case ArgRole.KeyCode: return wf ? "if (" + v + " == 0) { e.Handled = true; e.SuppressKeyPress = true; } " : "if (" + v + " == 0) e.Handled = true; ";
            case ArgRole.Cancel: return "e.Cancel = System.Convert.ToBoolean(" + v + "); ";
            case ArgRole.CancelEdit: return "e.CancelEdit = System.Convert.ToBoolean(" + v + "); ";
            default: return "";
        }
    }

    /// <summary>Mouse X/Y in the VB6 scale units of the object (its own ScaleMode, else its container's).</summary>
    private static string Coordinate(FormContext ctx, VbControl ctl, bool y)
    {
        var owner = Units.HasScale(ctl) ? ctl : Units.ScaleOwner(ctl);
        var factor = Units.Factor(owner, y);
        var inv = CultureInfo.InvariantCulture;
        if (ctx.Ui == UiTarget.WinForms)
        {
            if (Math.Abs(factor - Units.TwipsPerPixel) < 1e-9) return y ? "e.Y" : "e.X";
            var twips = H + "Twips.FromPixels" + (y ? "Y(e.Y)" : "X(e.X)");
            return Math.Abs(factor - 1) < 1e-9 ? twips : twips + " / " + factor.ToString("R", inv);
        }
        var dip = "e.GetPosition((System.Windows.IInputElement)sender)." + (y ? "Y" : "X");
        var k = Units.TwipsPerPixel / factor;
        return Math.Abs(k - 1) < 1e-9 ? dip : dip + " * " + k.ToString("R", inv);
    }
}
