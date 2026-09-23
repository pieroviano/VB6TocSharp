using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Vb6ToCSharp.CodeGeneration;
using Vb6ToCSharp.Runtime;

namespace Vb6ToCSharp.ItemConversion;

/// <summary>Builds the .NET-signature adapter that forwards an event to the converted VB6-signature handler.</summary>
public static class EventAdapters
{
    private const string H = "Vb6ToCSharp.UpgradeHelpers.";

    /// <summary>Parameters of a converted prototype (<c>private void X(ref int Index, int KeyAscii) {</c>).</summary>
    public static List<Parameter> ParseParams(string prototype)
    {
        var res = new List<Parameter>();
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
            var p = new Parameter();
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
        var isArray = ctl != ctx.ControlFile.Root && ctx.Arrays.Contains(ctl.Name);
        var roles = new List<ArgumentRole?>();
        if (isArray) roles.Add(null);
        roles.AddRange(b.Roles.Select(r => (ArgumentRole?)r));

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
            var role = i < roles.Count ? roles[i] : ArgumentRole.Other;
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

    private static string In(ArgumentRole role, FormContext ctx, ControlWithType ctl)
    {
        var wf = ctx.Ui == UiTarget.WinForms;
        var fh = wf ? H + "WinForms.FormsHelper." : H + "Wpf.FormsHelper.";
        switch (role)
        {
            case ArgumentRole.KeyAscii: return wf ? "(int)e.KeyChar" : "e.Text.Length > 0 ? (int)e.Text[0] : 0";
            case ArgumentRole.KeyCode: return wf ? "(int)e.KeyCode" : "System.Windows.Input.KeyInterop.VirtualKeyFromKey(e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key)";
            case ArgumentRole.KeyShift: return wf ? fh + "ShiftFrom(e.Modifiers)" : fh + "ShiftFrom(System.Windows.Input.Keyboard.Modifiers)";
            case ArgumentRole.MouseShift: return wf ? fh + "ShiftFrom(System.Windows.Forms.Control.ModifierKeys)" : fh + "ShiftFrom(System.Windows.Input.Keyboard.Modifiers)";
            case ArgumentRole.Button: return fh + (wf ? "ButtonFrom(e.Button)" : "ButtonFrom(e)");
            case ArgumentRole.X:
            case ArgumentRole.Y:
                return Coordinate(ctx, ctl, role == ArgumentRole.Y);
            case ArgumentRole.UnloadMode: return wf ? fh + "UnloadModeFrom(e.CloseReason)" : "0";
            case ArgumentRole.Node: return wf ? "e.Node" : "e.NewValue as System.Windows.Controls.TreeViewItem";
            case ArgumentRole.Item: return wf ? "e.Item" : "e.AddedItems.Count > 0 ? e.AddedItems[0] : null";
            case ArgumentRole.Column: return "((System.Windows.Forms.ListView)sender).Columns[e.Column]";
            case ArgumentRole.ToolButton: return wf ? "e.ClickedItem" : "e.OriginalSource";
            case ArgumentRole.Panel: return "e.ClickedItem as System.Windows.Forms.ToolStripStatusLabel";
            case ArgumentRole.ItemIndex: return "e.Index";
            case ArgumentRole.Date: return "e.Start";
            case ArgumentRole.NewString: return "e.Label";
            default: return null; // Cancel, CancelEdit, PreviousTab, Other: VB default value
        }
    }

    private static string Out(ArgumentRole role, UiTarget ui, string v)
    {
        var wf = ui == UiTarget.WinForms;
        switch (role)
        {
            case ArgumentRole.KeyAscii: return wf ? "e.KeyChar = (char)" + v + "; if (" + v + " == 0) e.Handled = true; " : "if (" + v + " == 0) e.Handled = true; ";
            case ArgumentRole.KeyCode: return wf ? "if (" + v + " == 0) { e.Handled = true; e.SuppressKeyPress = true; } " : "if (" + v + " == 0) e.Handled = true; ";
            case ArgumentRole.Cancel: return "e.Cancel = System.Convert.ToBoolean(" + v + "); ";
            case ArgumentRole.CancelEdit: return "e.CancelEdit = System.Convert.ToBoolean(" + v + "); ";
            default: return "";
        }
    }

    /// <summary>Mouse X/Y in the VB6 scale units of the object (its own ScaleMode, else its container's).</summary>
    private static string Coordinate(FormContext ctx, ControlWithType ctl, bool y)
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