using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vb6ToCSharp.FormConversion.Model;
using Vb6ToCSharp.Infrastructure;
using Vb6ToCSharp.Parsing;
using Vb6ToCSharp.Parsing.Model;
using Vb6ToCSharp.Runtime;
using static Vb6ToCSharp.FormConversion.Emit;

namespace Vb6ToCSharp.FormConversion;

/// <summary>Emits a WinForms <c>X.Designer.cs</c> (+ resources) for a VB6 form / user control, VBUC-style.</summary>
public sealed class WinFormsEmitter
{
    private const string W = "System.Windows.Forms.";
    private const string D = "System.Drawing.";
    private const string PP = ControlCatalog.PowerPacks;
    private const string HW = ControlCatalog.HelpersWinForms;
    private const string HS = ControlCatalog.HelpersWinFormsSupport;

    private readonly FormContext ctx;
    private readonly FormControlFile controlFile;
    private readonly EmitResult result = new();
    private readonly List<string> fields = new();
    private readonly List<string> create = new();
    private readonly List<string> beginInit = new();
    private readonly List<string> suspend = new();
    private readonly List<string> body = new();
    private readonly List<string> resume = new();
    private readonly List<string> endInit = new();
    private readonly List<string> extras = new();
    private readonly List<string> ocx = new();
    private readonly HashSet<string> declaredArrays = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> arrayTypes = new(StringComparer.OrdinalIgnoreCase);
    private bool toolTip;
    private int shapeContainers;

    public WinFormsEmitter(FormContext ctx)
    {
        this.ctx = ctx;
        controlFile = ctx.ControlFile;
    }

    public static EmitResult Generate(FormContext ctx, string ns) => new WinFormsEmitter(ctx).Run(ns);

    private string Ref(ControlWithType c) => c == controlFile.Root ? "this" : "this." + c.MemberName;

    /// <summary>Concrete .NET type, refined by properties (borderless Frame → Panel, checked ListBox, oval Shape…).</summary>
    public static string EffectiveType(ControlWithType c, ControlInfo info)
    {
        var t = info.WinForms;
        switch (c.Type)
        {
            case "VB.Frame": return c.Num("BorderStyle", 1) == 0 ? W + "Panel" : t;
            case "VB.ListBox": return c.Num("Style") == 1 ? W + "CheckedListBox" : t;
            case "VB.Shape": return c.Num("Shape") is 2 or 3 ? PP + "OvalShape" : PP + "RectangleShape";
            case "VB.Menu": return c.Text("Caption") == "-" && !c.IsArrayElement ? W + "ToolStripSeparator" : t;
        }
        if (c.ClassName == "FlatScrollBar") return c.Num("Orientation", 1) == 0 ? W + "VScrollBar" : W + "HScrollBar";
        return t;
    }

    private EmitResult Run(string ns)
    {
        var root = controlFile.Root;
        var rootInfo = ctx.Info(root);
        toolTip = controlFile.AllControls().Any(c => c.Has("ToolTipText") && c.Type != "VB.Menu");

        foreach (var c in root.Children) Visit(c);
        RootProperties(root, rootInfo);
        AddChildren(root, "this");
        Menus(root);
        Wire(root);
        if (toolTip)
        {
            fields.Insert(0, "public " + W + "ToolTip ToolTipMain;");
            create.Insert(0, "this.ToolTipMain = new " + W + "ToolTip(this.components);");
        }

        var sb = new StringBuilder();
        sb.Append("namespace ").Append(ns).Append("\r\n{\r\n");
        sb.Append("    partial class ").Append(ctx.ClassName).Append("\r\n    {\r\n");
        sb.Append("        private System.ComponentModel.IContainer components = null;\r\n\r\n");
        sb.Append("        protected override void Dispose(bool disposing)\r\n        {\r\n");
        sb.Append("            if (disposing && (components != null))\r\n            {\r\n                components.Dispose();\r\n            }\r\n");
        sb.Append("            base.Dispose(disposing);\r\n        }\r\n\r\n");
        sb.Append("        #region Windows Form Designer generated code\r\n\r\n");
        sb.Append("        private void InitializeComponent()\r\n        {\r\n");
        sb.Append("            this.components = new System.ComponentModel.Container();\r\n");
        if (result.Resources.Count > 0) sb.Append("            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(").Append(ctx.ClassName).Append("));\r\n");
        foreach (var l in create.Concat(beginInit).Concat(suspend).Concat(body).Concat(endInit).Concat(resume)) sb.Append("            ").Append(l).Append("\r\n");
        sb.Append("        }\r\n\r\n        #endregion\r\n\r\n");
        sb.Append("        /// <summary>Set-up the designer cannot serialize (control arrays, collections, ItemData…).</summary>\r\n");
        sb.Append("        private void InitializeComponentExtras()\r\n        {\r\n");
        foreach (var l in extras) sb.Append("            ").Append(l).Append("\r\n");
        sb.Append("        }\r\n");
        if (ocx.Count > 0)
        {
            sb.Append("\r\n        /// <summary>ActiveX properties from the .frm (the controls must exist first).</summary>\r\n");
            sb.Append("        protected override void OnLoad(System.EventArgs e)\r\n        {\r\n");
            foreach (var l in ocx) sb.Append("            ").Append(l).Append("\r\n");
            sb.Append("            base.OnLoad(e);\r\n        }\r\n");
        }
        sb.Append("\r\n");
        foreach (var f in fields) sb.Append("        ").Append(f).Append("\r\n");
        sb.Append("    }\r\n}\r\n");
        result.Designer = sb.ToString();
        return result;
    }

    private void Declare(ControlWithType c, string type)
    {
        fields.Add("public " + type + " " + c.MemberName + ";");
        if (c.IsArrayElement && declaredArrays.Add(c.Name))
        {
            var t = arrayTypes.TryGetValue(c.Name, out var at) ? at : type;
            fields.Add("public " + HW + "ControlArray<" + t + "> " + c.Name + ";");
            create.Insert(0, "this." + c.Name + " = new " + HW + "ControlArray<" + t + ">(" + Lit(c.Name) + ");");
        }
        if (c.IsArrayElement) extras.Add("this." + c.Name + ".SetIndex(" + Ref(c) + ", " + c.Index + ");");
    }

    private void Section(string name) => body.Add("// " + name);

    private void Visit(ControlWithType c)
    {
        var info = ctx.Info(c);
        var type = EffectiveType(c, info);
        var me = Ref(c);
        switch (info.Kind)
        {
            case ControlKind.Menu:
                return; // Menus() builds the MenuStrip
            case ControlKind.Line:
            case ControlKind.Shape:
                result.UsesPowerPacks = true;
                Declare(c, type);
                create.Add(me + " = new " + type + "();");
                Section(c.MemberName);
                ShapeProperties(c, me);
                Wire(c);
                return;
            case ControlKind.NonVisual:
                Declare(c, type);
                create.Add(me + " = new " + type + (c.Type is "VB.Timer" || c.ClassName == "ImageList" ? "(this.components)" : "()") + ";");
                if (c.ClassName == "CommonDialog") create.Add("this.components.Add(" + me + ");");
                Section(c.MemberName);
                NonVisualProperties(c, me);
                Wire(c);
                return;
        }

        Declare(c, type);
        create.Add(me + " = new " + type + "();");
        if (info.Kind == ControlKind.Hosted)
        {
            result.HostedLibraries.Add(info.Library);
            beginInit.Add("((System.ComponentModel.ISupportInitialize)(" + me + ")).BeginInit();");
            endInit.Add("((System.ComponentModel.ISupportInitialize)(" + me + ")).EndInit();");
        }
        if (info.IsContainer || c.Children.Count > 0)
        {
            suspend.Add(me + ".SuspendLayout();");
            resume.Insert(0, me + ".ResumeLayout(false);");
        }
        foreach (var ch in c.Children) Visit(ch);

        Section(c.MemberName);
        CommonProperties(c, info, type, me);
        TypeProperties(c, info, type, me);
        if (info.Kind == ControlKind.TabbedContainer) TabPages(c, me);
        else AddChildren(c, me);
        Wire(c);
    }

    private void AddChildren(ControlWithType container, string me)
    {
        var visual = container.Children.Where(ch => ctx.Info(ch).Kind is not (ControlKind.Menu or ControlKind.NonVisual or ControlKind.Line or ControlKind.Shape)).ToList();
        visual.Reverse(); // VB lists back-to-front; WinForms Controls[0] is the top-most
        foreach (var ch in visual) body.Add(me + ".Controls.Add(" + Ref(ch) + ");");
        Shapes(container, me);
    }

    private void Shapes(ControlWithType container, string me)
    {
        var shapes = container.Children.Where(ch => ctx.Info(ch).Kind is ControlKind.Line or ControlKind.Shape).ToList();
        if (shapes.Count == 0) return;
        var sc = "ShapeContainer" + (++shapeContainers);
        fields.Add("public " + PP + "ShapeContainer " + sc + ";");
        create.Add("this." + sc + " = new " + PP + "ShapeContainer();");
        body.Add("// " + sc);
        body.Add("this." + sc + ".Dock = " + W + "DockStyle.Fill;");
        body.Add("this." + sc + ".Name = " + Lit(sc) + ";");
        body.Add("this." + sc + ".Shapes.AddRange(new " + PP + "Shape[] { " + string.Join(", ", shapes.AsEnumerable().Reverse().Select(Ref)) + " });");
        body.Add(me + ".Controls.Add(this." + sc + ");"); // added last: drawn below the windowed controls, like VB6
    }

    private void Wire(ControlWithType c)
    {
        var info = ctx.Info(c);
        foreach (var (ev, b, handler) in ctx.EventsOf(c))
        {
            if (b.Direct)
            {
                extras.Add(Ref(c) + "." + b.NetEvent + " += this." + handler + ";");
                continue;
            }
            if (info.Kind == ControlKind.Hosted && EventHasArgs(c, ev)) continue;
            var sub = b.NetEvent;
            if (c.IsArrayElement)
            {
                if (c.Index != controlFile.AllControls().Where(x => x.Name == c.Name).Min(x => x.Index)) continue; // once per array
                extras.Add("this." + c.Name + ".Wire(c => c." + sub + " += new " + b.Delegate + "(this." + handler + "));");
            }
            else
            {
                body.Add(Ref(c) + "." + sub + " += new " + b.Delegate + "(this." + handler + ");");
            }
        }
    }

    private bool EventHasArgs(ControlWithType c, string ev)
    {
        var rx = new System.Text.RegularExpressions.Regex(@"Sub\s+" + System.Text.RegularExpressions.Regex.Escape(ctx.HandlerName(c, ev)) + @"\s*\(\s*\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return !rx.IsMatch(controlFile.Code);
    }

    private void Add(string me, string prop, string value) => body.Add(me + "." + prop + " = " + value + ";");

    private static string ResourceExpr(string name, string cast) => "((" + cast + ")(resources.GetObject(" + Lit(name) + ")))";

    /// <summary>Image resource assignment for a picture property; unknown formats become a TODO.</summary>
    private void Image(ControlWithType c, string me, string vbProp, string netProp, bool icon = false)
    {
        var name = (c == controlFile.Root ? "$this" : c.MemberName) + "." + netProp;
        var res = Picture(controlFile, c, vbProp, name);
        if (res == null) return;
        if (res.Kind is FrxBlobKind.Unknown or FrxBlobKind.Cursor or FrxBlobKind.Wmf or FrxBlobKind.Emf || (icon && res.Kind != FrxBlobKind.Icon))
        {
            body.Add("// TODO: " + c.MemberName + "." + vbProp + " (" + res.Kind + " data in the .frx) not converted");
            return;
        }
        result.Resources.Add(res);
        Add(me, netProp, ResourceExpr(name, icon ? D + "Icon" : D + "Image"));
    }

    private void RootProperties(ControlWithType root, ControlInfo info)
    {
        const string me = "this";
        Section(ctx.ClassName);
        var uc = controlFile.IsUserControl || root.Type == "VB.PropertyPage";
        Add(me, "AutoScaleDimensions", "new " + D + "SizeF(96F, 96F)");
        Add(me, "AutoScaleMode", W + "AutoScaleMode.Dpi");
        var w = Units.ToPixels(root.Num("ClientWidth", root.Num("Width")));
        var h = Units.ToPixels(root.Num("ClientHeight", root.Num("Height")));
        Add(me, uc ? "Size" : "ClientSize", "new " + D + "Size(" + w + ", " + h + ")");
        if (root.Has("BackColor")) Add(me, "BackColor", ColorConverter.WinForms(ColorConverter.Parse(root.Get("BackColor").Raw)));
        if (root.Has("ForeColor")) Add(me, "ForeColor", ColorConverter.WinForms(ColorConverter.Parse(root.Get("ForeColor").Raw)));
        FontProperty(root, me);
        CursorProperty(root, me);
        Add(me, "Name", Lit(ctx.ClassName));
        if (root.Has("Enabled") && !root.Bool("Enabled", true)) Add(me, "Enabled", "false");
        if (!uc)
        {
            Add(me, "Text", Lit(root.Text("Caption")));
            var bs = (int)root.Num("BorderStyle", 2);
            var fbs = bs switch { 0 => "None", 1 => "FixedSingle", 3 => "FixedDialog", 4 => "FixedToolWindow", 5 => "SizableToolWindow", _ => "Sizable" };
            if (fbs != "Sizable") Add(me, "FormBorderStyle", W + "FormBorderStyle." + fbs);
            if (root.Has("ControlBox") && !root.Bool("ControlBox", true)) Add(me, "ControlBox", "false");
            // VB6 hides Min/Max buttons of fixed dialogs unless set explicitly
            var fixedDialog = bs is 1 or 3 or 4;
            if (!root.Bool("MinButton", !fixedDialog)) Add(me, "MinimizeBox", "false");
            if (!root.Bool("MaxButton", !fixedDialog)) Add(me, "MaximizeBox", "false");
            if (root.Has("ShowInTaskbar") && !root.Bool("ShowInTaskbar", true)) Add(me, "ShowInTaskbar", "false");
            if (root.Bool("KeyPreview")) Add(me, "KeyPreview", "true");
            if (root.Bool("WhatsThisButton")) Add(me, "HelpButton", "true");
            var ws = (int)root.Num("WindowState");
            if (ws == 1) Add(me, "WindowState", W + "FormWindowState.Minimized");
            if (ws == 2) Add(me, "WindowState", W + "FormWindowState.Maximized");
            var sp = (int)root.Num("StartUpPosition", 3);
            switch (sp)
            {
                case 0:
                    Add(me, "StartPosition", W + "FormStartPosition.Manual");
                    Add(me, "Location", "new " + D + "Point(" + Units.ToPixels(root.Num("ClientLeft")) + ", " + Units.ToPixels(root.Num("ClientTop")) + ")");
                    break;
                case 1: Add(me, "StartPosition", W + "FormStartPosition.CenterParent"); break;
                case 2: Add(me, "StartPosition", W + "FormStartPosition.CenterScreen"); break;
                default: Add(me, "StartPosition", W + "FormStartPosition.WindowsDefaultLocation"); break;
            }
            if (controlFile.IsMdiForm) Add(me, "IsMdiContainer", "true");
            Image(root, me, "Icon", "Icon", true);
            Image(root, me, "Picture", "BackgroundImage");
            foreach (var c in controlFile.AllControls().Where(x => x.Type == "VB.CommandButton"))
            {
                if (c.Bool("Default")) Add(me, "AcceptButton", Ref(c));
                if (c.Bool("Cancel")) Add(me, "CancelButton", Ref(c));
            }
        }
        else
        {
            Image(root, me, "Picture", "BackgroundImage");
        }
    }

    private void FontProperty(ControlWithType c, string me)
    {
        string name;
        double size;
        bool bold, italic, underline, strike;
        int charset;
        if (c.Has("Font.Name"))
        {
            name = c.Text("Font.Name");
            size = c.Num("Font.Size", 8.25);
            bold = c.Num("Font.Weight", 400) >= 600 || c.Bool("Font.Bold");
            italic = c.Bool("Font.Italic");
            underline = c.Bool("Font.Underline");
            strike = c.Bool("Font.Strikethrough");
            charset = (int)c.Num("Font.Charset");
        }
        else if (c.Has("FontName"))
        {
            name = c.Text("FontName");
            size = c.Num("FontSize", 8.25);
            bold = c.Bool("FontBold");
            italic = c.Bool("FontItalic");
            underline = c.Bool("FontUnderline");
            strike = c.Bool("FontStrikethru");
            charset = 0;
        }
        else return;
        var styles = new List<string>();
        if (bold) styles.Add(D + "FontStyle.Bold");
        if (italic) styles.Add(D + "FontStyle.Italic");
        if (underline) styles.Add(D + "FontStyle.Underline");
        if (strike) styles.Add(D + "FontStyle.Strikeout");
        var style = styles.Count == 0 ? D + "FontStyle.Regular" : "(" + D + "FontStyle)(" + string.Join(" | ", styles) + ")";
        Add(me, "Font", "new " + D + "Font(" + Lit(name) + ", " + F(size) + ", " + style + ", " + D + "GraphicsUnit.Point, ((byte)(" + charset + ")))");
    }

    private void CursorProperty(ControlWithType c, string me)
    {
        var mp = (int)c.Num("MousePointer");
        if (mp == 0) return;
        if (mp == 99 || mp >= WinFormsCursors.Length)
        {
            body.Add("// TODO: " + c.MemberName + ".MouseIcon (custom cursor) not converted");
            return;
        }
        Add(me, "Cursor", W + "Cursors." + WinFormsCursors[mp]);
    }

    /// <summary>Location in the container's client area (SSTab pages: the hidden-tab offset and tab strip are removed).</summary>
    private (int x, int y) Location(ControlWithType c)
    {
        var x = Units.PosTwips(c, "Left");
        var y = Units.PosTwips(c, "Top");
        if (c.Parent?.Type != "TabDlg.SSTab") return (Units.ToPixels(x), Units.ToPixels(y));
        if (x < -60000) x += 75000; // VB6 parks the controls of hidden tabs 75000 twips to the left
        y -= c.Parent.Num("TabHeight", 353);
        return (Math.Max(0, Units.ToPixels(x)), Math.Max(0, Units.ToPixels(y)));
    }

    private void CommonProperties(ControlWithType c, ControlInfo info, string type, string me)
    {
        var (x, y) = Location(c);
        var align = (int)c.Num("Align", c.ClassName == "StatusBar" ? 2 : c.ClassName == "Toolbar" ? 1 : 0);
        if (align != 0) Add(me, "Dock", W + "DockStyle." + (align switch { 1 => "Top", 2 => "Bottom", 3 => "Left", _ => "Right" }));
        if (align == 0 || c.ClassName is not ("StatusBar" or "Toolbar"))
        {
            Add(me, "Location", "new " + D + "Point(" + x + ", " + y + ")");
            Add(me, "Size", "new " + D + "Size(" + Units.SizePx(c, "Width") + ", " + Units.SizePx(c, "Height") + ")");
        }
        Add(me, "Name", Lit(c.MemberName));
        if (c.Has("TabIndex")) Add(me, "TabIndex", ((int)c.Num("TabIndex")).ToString(Inv));
        if (c.Has("TabStop") && !c.Bool("TabStop", true)) Add(me, "TabStop", "false");
        if (c.Has("Enabled") && !c.Bool("Enabled", true)) Add(me, "Enabled", "false");
        if (c.Has("Visible") && !c.Bool("Visible", true)) Add(me, "Visible", "false");
        if (c.Has("BackColor")) Add(me, "BackColor", ColorConverter.WinForms(ColorConverter.Parse(c.Get("BackColor").Raw)));
        if (c.Has("ForeColor")) Add(me, "ForeColor", ColorConverter.WinForms(ColorConverter.Parse(c.Get("ForeColor").Raw)));
        if (c.Has("RightToLeft") && c.Bool("RightToLeft")) Add(me, "RightToLeft", W + "RightToLeft.Yes");
        if (c.Has("Tag")) Add(me, "Tag", Lit(c.Text("Tag")));
        FontProperty(c, me);
        CursorProperty(c, me);
        if (toolTip && c.Has("ToolTipText")) body.Add("this.ToolTipMain.SetToolTip(" + me + ", " + Lit(TextOf(controlFile, c, "ToolTipText")) + ");");
        if (info.Kind == ControlKind.Placeholder)
        {
            body.Add("// TODO: VB6 control " + c.Type + " '" + c.Name + "' has no .NET equivalent (placeholder Panel)");
            Add(me, "BorderStyle", W + "BorderStyle.FixedSingle");
        }
    }

    private void Caption(ControlWithType c, string me, string prop = "Text")
    {
        var t = TextOf(controlFile, c, "Caption");
        if (t != null) Add(me, prop, Lit(t));
    }

    private void Items(ControlWithType c, string me)
    {
        var list = ListOf(controlFile, c, "List");
        if (list.Count > 0) body.Add(me + ".Items.AddRange(new object[] { " + string.Join(", ", list.Select(Lit)) + " });");
        var data = ItemDataOf(controlFile, c, "ItemData");
        for (var i = 0; i < data.Count && i < list.Count; i++)
        {
            if (data[i] != 0) extras.Add(HS + "ListHelper.SetItemData(" + me + ", " + i + ", " + data[i] + ");");
        }
    }

    private void UseVisualStyle(ControlWithType c, string me) => Add(me, "UseVisualStyleBackColor", B(!c.Has("BackColor")));

    private void ScrollRange(ControlWithType c, string me, bool vbMax = true)
    {
        var min = (int)c.Num("Min", 0);
        var max = (int)c.Num("Max", 32767);
        var small = (int)c.Num("SmallChange", 1);
        var large = (int)c.Num("LargeChange", 1);
        if (min > max) body.Add("// TODO: " + c.MemberName + " has Min > Max (reversed VB6 scroll bar); .NET needs Minimum <= Maximum");
        Add(me, "Minimum", Math.Min(min, max).ToString(Inv));
        // VB6 Max is reachable; .NET's reachable maximum is Maximum - LargeChange + 1
        Add(me, "Maximum", (vbMax ? Math.Max(min, max) + large - 1 : Math.Max(min, max)).ToString(Inv));
        Add(me, "SmallChange", small.ToString(Inv));
        Add(me, "LargeChange", large.ToString(Inv));
        if (c.Has("Value")) Add(me, "Value", ((int)c.Num("Value")).ToString(Inv));
    }

    private void TypeProperties(ControlWithType c, ControlInfo info, string type, string me)
    {
        switch (c.Type)
        {
            case "VB.Label":
                Caption(c, me);
                if (c.Bool("AutoSize")) Add(me, "AutoSize", "true");
                var la = (int)c.Num("Alignment");
                if (la != 0) Add(me, "TextAlign", D + "ContentAlignment." + (la == 1 ? "TopRight" : "TopCenter"));
                if (c.Num("BorderStyle") == 1) Add(me, "BorderStyle", W + "BorderStyle.FixedSingle");
                if (c.Has("BackStyle") && c.Num("BackStyle") == 0) Add(me, "BackColor", D + "Color.Transparent");
                if (c.Has("UseMnemonic") && !c.Bool("UseMnemonic", true)) Add(me, "UseMnemonic", "false");
                return;
            case "VB.TextBox":
                var text = TextOf(controlFile, c, "Text");
                if (c.Bool("MultiLine")) Add(me, "Multiline", "true");
                var sb = (int)c.Num("ScrollBars");
                if (sb != 0) Add(me, "ScrollBars", W + "ScrollBars." + (sb == 1 ? "Horizontal" : sb == 2 ? "Vertical" : "Both"));
                if (sb is 1 or 3) Add(me, "WordWrap", "false");
                if (c.Num("MaxLength") > 0) Add(me, "MaxLength", ((int)c.Num("MaxLength")).ToString(Inv));
                var pw = c.Text("PasswordChar");
                if (pw != "") Add(me, "PasswordChar", "'" + (pw[0] == '\'' ? "\\'" : pw[0] == '\\' ? "\\\\" : pw.Substring(0, 1)) + "'");
                if (c.Bool("Locked"))
                {
                    Add(me, "ReadOnly", "true");
                    if (!c.Has("BackColor")) Add(me, "BackColor", D + "SystemColors.Window"); // VB6 keeps the normal background
                }
                var ta = (int)c.Num("Alignment");
                if (ta != 0) Add(me, "TextAlign", W + "HorizontalAlignment." + (ta == 1 ? "Right" : "Center"));
                var tbs = (int)c.Num("BorderStyle", 1);
                if (tbs == 0) Add(me, "BorderStyle", W + "BorderStyle.None");
                else if (c.Num("Appearance", 1) == 0) Add(me, "BorderStyle", W + "BorderStyle.FixedSingle");
                if (c.Has("HideSelection") && !c.Bool("HideSelection", true)) Add(me, "HideSelection", "false");
                if (text != null) Add(me, "Text", Lit(text));
                return;
            case "VB.CommandButton":
                Caption(c, me);
                UseVisualStyle(c, me);
                if (c.Num("Style") == 1) Image(c, me, "Picture", "Image");
                return;
            case "VB.CheckBox":
                Caption(c, me);
                UseVisualStyle(c, me);
                var v = (int)c.Num("Value");
                if (v != 0) Add(me, "CheckState", W + "CheckState." + (v == 1 ? "Checked" : "Indeterminate"));
                if (c.Num("Alignment") == 1) Add(me, "CheckAlign", D + "ContentAlignment.MiddleRight");
                if (c.Num("Style") == 1)
                {
                    Add(me, "Appearance", W + "Appearance.Button");
                    Image(c, me, "Picture", "Image");
                }
                return;
            case "VB.OptionButton":
                Caption(c, me);
                UseVisualStyle(c, me);
                if (c.Bool("Value")) Add(me, "Checked", "true");
                if (c.Num("Alignment") == 1) Add(me, "CheckAlign", D + "ContentAlignment.MiddleRight");
                if (c.Num("Style") == 1)
                {
                    Add(me, "Appearance", W + "Appearance.Button");
                    Image(c, me, "Picture", "Image");
                }
                return;
            case "VB.Frame":
                if (type.EndsWith("GroupBox", StringComparison.Ordinal)) Caption(c, me);
                return;
            case "VB.ComboBox":
                var st = (int)c.Num("Style");
                Add(me, "DropDownStyle", W + "ComboBoxStyle." + (st == 1 ? "Simple" : st == 2 ? "DropDownList" : "DropDown"));
                if (c.Bool("Sorted")) Add(me, "Sorted", "true");
                if (c.Has("IntegralHeight") && !c.Bool("IntegralHeight", true)) Add(me, "IntegralHeight", "false");
                Items(c, me);
                if (st != 2 && c.Has("Text")) Add(me, "Text", Lit(TextOf(controlFile, c, "Text")));
                return;
            case "VB.ListBox":
                var ms = (int)c.Num("MultiSelect");
                if (ms != 0) Add(me, "SelectionMode", W + "SelectionMode." + (ms == 1 ? "MultiSimple" : "MultiExtended"));
                if (c.Bool("Sorted")) Add(me, "Sorted", "true");
                if (c.Num("Columns") > 0) Add(me, "MultiColumn", "true");
                if (c.Has("IntegralHeight") && !c.Bool("IntegralHeight", true)) Add(me, "IntegralHeight", "false");
                Add(me, "FormattingEnabled", "true");
                Items(c, me);
                return;
            case "VB.PictureBox":
                Image(c, me, "Picture", "Image");
                if (c.Bool("AutoSize")) Add(me, "SizeMode", W + "PictureBoxSizeMode.AutoSize");
                var pbs = (int)c.Num("BorderStyle", 1);
                if (pbs == 1) Add(me, "BorderStyle", W + "BorderStyle." + (c.Num("Appearance", 1) == 0 ? "FixedSingle" : "Fixed3D"));
                Add(me, "TabStop", B(c.Bool("TabStop", true)));
                return;
            case "VB.Image":
                Image(c, me, "Picture", "Image");
                Add(me, "SizeMode", W + "PictureBoxSizeMode." + (c.Bool("Stretch") ? "StretchImage" : "Normal"));
                if (c.Num("BorderStyle") == 1) Add(me, "BorderStyle", W + "BorderStyle.FixedSingle");
                if (!c.Has("BackColor")) Add(me, "BackColor", D + "Color.Transparent");
                return;
            case "VB.HScrollBar":
            case "VB.VScrollBar":
                ScrollRange(c, me);
                return;
            case "VB.DriveListBox":
                return;
            case "VB.DirListBox":
                return;
            case "VB.FileListBox":
                if (c.Has("Pattern")) Add(me, "Pattern", Lit(c.Text("Pattern")));
                foreach (var a in new[] { "Archive", "Hidden", "Normal", "ReadOnly", "System" })
                {
                    if (c.Has(a)) Add(me, a, B(c.Bool(a)));
                }
                return;
            case "VB.UserControl":
                return;
        }

        switch (info.Kind)
        {
            case ControlKind.Hosted:
                OcxProperties(c, me);
                return;
            case ControlKind.ProjectUserControl:
                var persisted = c.Properties.Where(p => !IsExtenderProperty(p.Name) && p.Frx == null).ToList();
                if (persisted.Count > 0)
                {
                    extras.Add(me + ".ReadProperties(" + ControlCatalog.HelpersInterop + "PropertyBag.FromPairs(" +
                               string.Join(", ", persisted.Select(p => Lit(p.Name) + ", " + ValueLiteral(p))) + "));");
                }
                else
                {
                    extras.Add(me + ".InitProperties();");
                }
                if (c.Properties.Any(p => !IsExtenderProperty(p.Name) && p.Frx != null)) body.Add("// TODO: " + c.MemberName + ": .frx-stored UserControl properties not converted");
                return;
        }

        switch (c.ClassName)
        {
            case "TreeView":
                ImageListRef(c, me, "ImageList", "ImageList");
                if (c.Has("Indentation")) Add(me, "Indent", Units.SizePx(c, "Indentation").ToString(Inv));
                if (c.Num("LabelEdit") == 0) Add(me, "LabelEdit", "true");
                if (c.Num("LineStyle") == 1) Add(me, "ShowRootLines", "true");
                if (c.Has("HideSelection") && !c.Bool("HideSelection", true)) Add(me, "HideSelection", "false");
                if (c.Bool("Checkboxes")) Add(me, "CheckBoxes", "true");
                if (c.Bool("FullRowSelect")) Add(me, "FullRowSelect", "true");
                if (c.Bool("HotTracking")) Add(me, "HotTracking", "true");
                if (c.Bool("Sorted")) Add(me, "Sorted", "true");
                var tvStyle = (int)c.Num("Style", 7);
                if (tvStyle is 0 or 1 or 4 or 5) Add(me, "ShowLines", "false");
                if (tvStyle is 0 or 1 or 2 or 3) Add(me, "ShowPlusMinus", "false");
                return;
            case "ListView":
                var view = (int)c.Num("View");
                Add(me, "View", W + "View." + (view switch { 1 => "SmallIcon", 2 => "List", 3 => "Details", _ => "LargeIcon" }));
                Add(me, "UseCompatibleStateImageBehavior", "false");
                ImageListRef(c, me, "Icons", "LargeImageList");
                ImageListRef(c, me, "SmallIcons", "SmallImageList");
                if (c.Num("LabelEdit") == 0) Add(me, "LabelEdit", "true");
                if (c.Bool("FullRowSelect")) Add(me, "FullRowSelect", "true");
                if (c.Bool("GridLines")) Add(me, "GridLines", "true");
                if (c.Has("HideSelection") && !c.Bool("HideSelection", true)) Add(me, "HideSelection", "false");
                if (c.Has("MultiSelect")) Add(me, "MultiSelect", B(c.Bool("MultiSelect")));
                else Add(me, "MultiSelect", "false");
                if (c.Bool("Checkboxes")) Add(me, "CheckBoxes", "true");
                if (c.Bool("Sorted")) Add(me, "Sorting", W + "SortOrder." + (c.Num("SortOrder") == 1 ? "Descending" : "Ascending"));
                foreach (var g in c.SubGroups("ColumnHeaders"))
                {
                    var pre = "ColumnHeaders." + g + ".";
                    var al = (int)c.Num(pre + "Alignment");
                    extras.Add(HS + "ListViewHelper.AddColumn(" + me + ", null, " + Lit(c.Text(pre + "Key")) + ", " + Lit(c.Text(pre + "Text")) + ", " +
                               N(Units.SizeTwips(c, pre + "Width", 1440)) + ", " + al + ");");
                }
                return;
            case "ImageList":
                return;
            case "ProgressBar":
                Add(me, "Minimum", ((int)c.Num("Min", 0)).ToString(Inv));
                Add(me, "Maximum", ((int)c.Num("Max", 100)).ToString(Inv));
                if (c.Has("Value")) Add(me, "Value", ((int)c.Num("Value")).ToString(Inv));
                if (c.Num("Scrolling") == 1) Add(me, "Style", W + "ProgressBarStyle.Continuous");
                if (c.Num("Orientation") == 1) body.Add("// TODO: " + c.MemberName + " is vertical in VB6 (no vertical WinForms ProgressBar)");
                return;
            case "Slider":
                Add(me, "Minimum", ((int)c.Num("Min", 0)).ToString(Inv));
                Add(me, "Maximum", ((int)c.Num("Max", 10)).ToString(Inv));
                Add(me, "SmallChange", ((int)c.Num("SmallChange", 1)).ToString(Inv));
                Add(me, "LargeChange", ((int)c.Num("LargeChange", 5)).ToString(Inv));
                if (c.Has("TickFrequency")) Add(me, "TickFrequency", ((int)c.Num("TickFrequency")).ToString(Inv));
                var ts = (int)c.Num("TickStyle");
                if (ts != 0) Add(me, "TickStyle", W + "TickStyle." + (ts == 1 ? "TopLeft" : ts == 2 ? "Both" : "None"));
                if (c.Num("Orientation") == 1) Add(me, "Orientation", W + "Orientation.Vertical");
                if (c.Has("Value")) Add(me, "Value", ((int)c.Num("Value")).ToString(Inv));
                return;
            case "StatusBar":
                StatusPanels(c, me);
                return;
            case "Toolbar":
                ToolbarButtons(c, me);
                return;
            case "TabStrip":
                ImageListRef(c, me, "ImageList", "ImageList");
                foreach (var g in c.SubGroups("Tabs"))
                {
                    var pre = "Tabs." + g + ".";
                    var key = c.Text(pre + "Key");
                    extras.Add(me + ".TabPages.Add(" + Lit(key != "" ? key : c.MemberName + "_" + g) + ", " + Lit(c.Text(pre + "Caption")) + ");");
                    if (c.Has(pre + "ToolTipText")) extras.Add(me + ".TabPages[" + me + ".TabPages.Count - 1].ToolTipText = " + Lit(c.Text(pre + "ToolTipText")) + ";");
                }
                return;
            case "ImageCombo":
                ImageListRef(c, me, "ImageList", "Tag");
                return;
            case "DTPicker":
                var fmt = (int)c.Num("Format", 1);
                Add(me, "Format", W + "DateTimePickerFormat." + (fmt switch { 0 => "Long", 2 => "Time", 3 => "Custom", _ => "Short" }));
                if (c.Has("CustomFormat")) Add(me, "CustomFormat", Lit(c.Text("CustomFormat")));
                if (c.Bool("CheckBox")) Add(me, "ShowCheckBox", "true");
                if (c.Bool("UpDown")) Add(me, "ShowUpDown", "true");
                if (c.Has("CurrentDate") && c.Num("CurrentDate") > 0)
                {
                    var d = DateTime.FromOADate(c.Num("CurrentDate"));
                    Add(me, "Value", "new System.DateTime(" + d.Year + ", " + d.Month + ", " + d.Day + ", 0, 0, 0, 0)");
                }
                return;
            case "MonthView":
                if (c.Bool("ShowWeekNumbers")) Add(me, "ShowWeekNumbers", "true");
                if (c.Bool("MultiSelect")) Add(me, "MaxSelectionCount", ((int)c.Num("MaxSelCount", 7)).ToString(Inv));
                else Add(me, "MaxSelectionCount", "1");
                return;
            case "UpDown":
                Add(me, "Minimum", ((int)c.Num("Min", 0)).ToString(Inv));
                Add(me, "Maximum", ((int)c.Num("Max", 10)).ToString(Inv));
                Add(me, "Increment", ((int)c.Num("Increment", 1)).ToString(Inv));
                if (c.Has("Value")) Add(me, "Value", ((int)c.Num("Value")).ToString(Inv));
                if (c.Has("BuddyControl")) body.Add("// TODO: " + c.MemberName + " was the UpDown buddy of '" + c.Text("BuddyControl") + "'; NumericUpDown shows its own value");
                if (c.Bool("Wrap")) body.Add("// TODO: " + c.MemberName + ".Wrap has no NumericUpDown equivalent");
                return;
            case "FlatScrollBar":
                ScrollRange(c, me);
                return;
            case "MSFlexGrid":
            case "MSHFlexGrid":
                foreach (var p in new[] { "Rows", "Cols", "FixedRows", "FixedCols" })
                {
                    if (c.Has(p)) Add(me, p, ((int)c.Num(p)).ToString(Inv));
                }
                if (c.Has("FormatString")) Add(me, "FormatString", Lit(c.Text("FormatString")));
                if (c.Has("AllowUserResizing"))
                {
                    var r = (int)c.Num("AllowUserResizing");
                    Add(me, "AllowUserToResizeColumns", B(r is 1 or 3));
                    Add(me, "AllowUserToResizeRows", B(r is 2 or 3));
                }
                return;
            case "SSTab":
                return;
            case "RichTextBox":
                var rtf = TextOf(controlFile, c, "TextRTF");
                if (!string.IsNullOrEmpty(rtf) && rtf.TrimStart().StartsWith("{\\rtf", StringComparison.Ordinal)) Add(me, "Rtf", Lit(rtf));
                else if (c.Has("Text")) Add(me, "Text", Lit(TextOf(controlFile, c, "Text")));
                var rsb = (int)c.Num("ScrollBars");
                if (rsb != 0) Add(me, "ScrollBars", W + "RichTextBoxScrollBars." + (rsb == 1 ? "Horizontal" : rsb == 2 ? "Vertical" : "Both"));
                if (c.Has("MultiLine")) Add(me, "Multiline", B(c.Bool("MultiLine")));
                if (c.Bool("Locked")) Add(me, "ReadOnly", "true");
                if (c.Num("MaxLength") > 0) Add(me, "MaxLength", ((int)c.Num("MaxLength")).ToString(Inv));
                if (c.Has("BorderStyle") && c.Num("BorderStyle") == 0) Add(me, "BorderStyle", W + "BorderStyle.None");
                if (c.Has("HideSelection") && !c.Bool("HideSelection", true)) Add(me, "HideSelection", "false");
                return;
        }
    }

    private void ImageListRef(ControlWithType c, string me, string vbProp, string netProp)
    {
        var il = c.Text(vbProp);
        if (il == "") return;
        var target = controlFile.Find(il);
        if (target == null)
        {
            body.Add("// TODO: " + c.MemberName + "." + vbProp + " refers to '" + il + "' outside this form");
            return;
        }
        if (netProp == "Tag") body.Add("// TODO: " + c.MemberName + " used ImageList '" + il + "' (ComboBox has no images)");
        else extras.Add(me + "." + netProp + " = " + Ref(target) + ";");
    }

    private void StatusPanels(ControlWithType c, string me)
    {
        Add(me, "SizingGrip", "true");
        if (c.Num("Style") == 1)
        {
            extras.Add(me + ".Items.Add(new " + W + "ToolStripStatusLabel(" + Lit(c.Text("SimpleText")) + ") { Spring = true, TextAlign = " + D + "ContentAlignment.MiddleLeft });");
            return;
        }
        var i = 0;
        foreach (var g in c.SubGroups("Panels"))
        {
            i++;
            var pre = "Panels." + g + ".";
            var key = c.Text(pre + "Key");
            var auto = (int)c.Num(pre + "AutoSize");
            var al = (int)c.Num(pre + "Alignment");
            var style = (int)c.Num(pre + "Style");
            var init = new List<string>
            {
                "Name = " + Lit(key != "" ? key : c.MemberName + "_Panel" + i),
                "AutoSize = " + B(auto == 2),
                "Spring = " + B(auto == 1),
                "TextAlign = " + D + "ContentAlignment." + (al == 1 ? "MiddleCenter" : al == 2 ? "MiddleRight" : "MiddleLeft"),
                "BorderSides = " + W + "ToolStripStatusLabelBorderSides." + (c.Num(pre + "Bevel", 1) == 0 ? "None" : "All"),
                "BorderStyle = " + W + "Border3DStyle." + (c.Num(pre + "Bevel", 1) == 2 ? "Raised" : "SunkenOuter"),
            };
            if (auto != 1 && auto != 2) init.Add("Width = " + Units.SizePx(c, pre + "Width", 1440));
            if (c.Has(pre + "ToolTipText")) init.Add("ToolTipText = " + Lit(c.Text(pre + "ToolTipText")));
            extras.Add(me + ".Items.Add(new " + W + "ToolStripStatusLabel(" + Lit(c.Text(pre + "Text")) + ") { " + string.Join(", ", init) + " });");
            if (style != 0) extras.Add("// TODO: " + c.MemberName + " panel " + i + " had VB6 Style " + style + " (caps/num/ins/scroll/time/date indicator); set its text in code");
        }
    }

    private void ToolbarButtons(ControlWithType c, string me)
    {
        ImageListRef(c, me, "ImageList", "ImageList");
        var i = 0;
        foreach (var g in c.SubGroups("Buttons"))
        {
            i++;
            var pre = "Buttons." + g + ".";
            var style = (int)c.Num(pre + "Style");
            var key = c.Text(pre + "Key");
            var name = Lit(key != "" ? key : c.MemberName + "_Button" + i);
            if (style is 3 or 4)
            {
                extras.Add(me + ".Items.Add(new " + W + "ToolStripSeparator() { Name = " + name + " });");
                if (style == 4) extras.Add("// TODO: " + c.MemberName + " button " + i + " was a placeholder for a control (use ToolStripControlHost)");
                continue;
            }
            var type = style == 5 ? W + "ToolStripDropDownButton" : W + "ToolStripButton";
            var init = new List<string> { "Name = " + name };
            var img = c.Get(pre + "Image");
            if (img != null)
            {
                if (img.IsQuoted) init.Add("ImageKey = " + Lit(img.Text));
                else if (img.Number() > 0) init.Add("ImageIndex = " + ((int)img.Number() - 1));
            }
            if (style is 1 or 2) init.Add("CheckOnClick = true");
            if (c.Num(pre + "Value") == 1) init.Add("Checked = true");
            if (c.Has(pre + "ToolTipText")) init.Add("ToolTipText = " + Lit(c.Text(pre + "ToolTipText")));
            if (c.Has(pre + "Enabled") && !c.Bool(pre + "Enabled", true)) init.Add("Enabled = false");
            if (c.Has(pre + "Visible") && !c.Bool(pre + "Visible", true)) init.Add("Visible = false");
            extras.Add(me + ".Items.Add(new " + type + "(" + Lit(c.Text(pre + "Caption")) + ") { " + string.Join(", ", init) + " });");
            if (style == 2) extras.Add("// TODO: " + c.MemberName + " button " + i + " belonged to a VB6 button group (mutually exclusive)");
        }
    }

    /// <summary>SSTab: one TabPage per tab; children go to the page listing them in <c>Tab(n).Control(m)</c>.</summary>
    private void TabPages(ControlWithType c, string me)
    {
        var tabs = (int)c.Num("Tabs", 3);
        var owner = new Dictionary<ControlWithType, int>();
        foreach (var p in c.Properties)
        {
            var m = System.Text.RegularExpressions.Regex.Match(p.Name, @"^Tab\((\d+)\)\.Control\(\d+\)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!m.Success) continue;
            var tab = int.Parse(m.Groups[1].Value, Inv);
            var n = p.Text;
            var idx = n.IndexOf('(');
            var baseName = idx > 0 ? n.Substring(0, idx) : n;
            int? index = idx > 0 && int.TryParse(n.Substring(idx + 1).TrimEnd(')'), out var ix) ? ix : null;
            var child = c.Children.FirstOrDefault(ch => ch.Name.Equals(baseName, StringComparison.OrdinalIgnoreCase) && ch.Index == index);
            if (child != null) owner[child] = tab;
        }
        for (var t = 0; t < tabs; t++)
        {
            var page = c.MemberName + "_TabPage" + t;
            fields.Add("public " + W + "TabPage " + page + ";");
            create.Add("this." + page + " = new " + W + "TabPage();");
            suspend.Add("this." + page + ".SuspendLayout();");
            resume.Insert(0, "this." + page + ".ResumeLayout(false);");
            body.Add("// " + page);
            Add("this." + page, "Name", Lit(page));
            Add("this." + page, "Text", Lit(c.Text("TabCaption(" + t + ")")));
            Add("this." + page, "UseVisualStyleBackColor", "true");
            var kids = c.Children.Where(ch => (owner.TryGetValue(ch, out var o) ? o : 0) == t && ctx.Info(ch).Kind is not (ControlKind.Line or ControlKind.Shape or ControlKind.Menu or ControlKind.NonVisual)).ToList();
            kids.Reverse();
            foreach (var k in kids) body.Add("this." + page + ".Controls.Add(" + Ref(k) + ");");
            body.Add(me + ".Controls.Add(this." + page + ");");
        }
        if (c.Has("Tab")) Add(me, "SelectedIndex", ((int)c.Num("Tab")).ToString(Inv));
        var shapes = c.Children.Where(ch => ctx.Info(ch).Kind is ControlKind.Line or ControlKind.Shape).ToList();
        if (shapes.Count > 0) body.Add("// TODO: lines/shapes drawn on SSTab " + c.MemberName + " were not converted");
    }

    private void ShapeProperties(ControlWithType c, string me)
    {
        Add(me, "Name", Lit(c.MemberName));
        if (c.Has("Visible") && !c.Bool("Visible", true)) Add(me, "Visible", "false");
        if (c.Has("BorderColor")) Add(me, "BorderColor", ColorConverter.WinForms(ColorConverter.Parse(c.Get("BorderColor").Raw)));
        if (c.Num("BorderWidth", 1) != 1) Add(me, "BorderWidth", ((int)c.Num("BorderWidth", 1)).ToString(Inv));
        var bs = (int)c.Num("BorderStyle", 1);
        if (bs == 0) Add(me, "BorderStyle", "System.Drawing.Drawing2D.DashStyle.Custom");
        else if (bs is >= 2 and <= 5) Add(me, "BorderStyle", "System.Drawing.Drawing2D.DashStyle." + (bs switch { 2 => "Dash", 3 => "Dot", 4 => "DashDot", _ => "DashDotDot" }));
        if (c.Type == "VB.Line")
        {
            Add(me, "X1", Units.ToPixels(Units.PosTwips(c, "X1")).ToString(Inv));
            Add(me, "Y1", Units.ToPixels(Units.PosTwips(c, "Y1")).ToString(Inv));
            Add(me, "X2", Units.ToPixels(Units.PosTwips(c, "X2")).ToString(Inv));
            Add(me, "Y2", Units.ToPixels(Units.PosTwips(c, "Y2")).ToString(Inv));
            return;
        }
        var shape = (int)c.Num("Shape");
        var x = Units.PosPx(c, "Left");
        var y = Units.PosPx(c, "Top");
        var w = Units.SizePx(c, "Width");
        var h = Units.SizePx(c, "Height");
        if (shape is 1 or 3 or 5)
        {
            var s = Math.Min(w, h);
            x += (w - s) / 2;
            y += (h - s) / 2;
            w = h = s;
        }
        Add(me, "Location", "new " + D + "Point(" + x + ", " + y + ")");
        Add(me, "Size", "new " + D + "Size(" + w + ", " + h + ")");
        if (shape is 4 or 5) Add(me, "CornerRadius", Math.Max(1, Math.Min(w, h) / 8).ToString(Inv));
        var fs = (int)c.Num("FillStyle", 1);
        if (fs != 1)
        {
            var name = fs switch { 0 => "Solid", 2 => "Horizontal", 3 => "Vertical", 4 => "BackwardDiagonal", 5 => "ForwardDiagonal", 6 => "Cross", _ => "DiagonalCross" };
            Add(me, "FillStyle", PP + "FillStyle." + name);
            Add(me, "FillColor", c.Has("FillColor") ? ColorConverter.WinForms(ColorConverter.Parse(c.Get("FillColor").Raw)) : D + "Color.Black");
        }
        if (c.Num("BackStyle") == 1)
        {
            Add(me, "BackStyle", PP + "BackStyle.Opaque");
            if (c.Has("BackColor")) Add(me, "BackColor", ColorConverter.WinForms(ColorConverter.Parse(c.Get("BackColor").Raw)));
        }
    }

    private void NonVisualProperties(ControlWithType c, string me)
    {
        switch (c.ClassName)
        {
            case "Timer":
                var interval = (int)c.Num("Interval");
                if (interval > 0) Add(me, "Interval", interval.ToString(Inv));
                // VB6: Enabled defaults to True and Interval = 0 disables the timer
                Add(me, "Enabled", B(c.Bool("Enabled", true) && interval > 0));
                return;
            case "ImageList":
                var iw = (int)c.Num("ImageWidth", 16);
                var ih = (int)c.Num("ImageHeight", 16);
                Add(me, "ImageSize", "new " + D + "Size(" + iw + ", " + ih + ")");
                Add(me, "ColorDepth", W + "ColorDepth.Depth32Bit");
                if (c.Has("MaskColor") && c.Bool("UseMaskColor", true)) Add(me, "TransparentColor", ColorConverter.WinForms(ColorConverter.Parse(c.Get("MaskColor").Raw)));
                var i = 0;
                foreach (var g in c.SubGroups("Images"))
                {
                    var pre = "Images." + g + ".";
                    var name = c.MemberName + ".Images" + i++;
                    var frx = c.Get(pre + "Picture")?.Frx;
                    var r = frx == null ? null : FrxReader.Open(controlFile.FrxPath(frx));
                    var data = r?.ReadBlob(frx.Offset);
                    if (data == null || data.Length == 0) continue;
                    var kind = FrxReader.Sniff(data);
                    if (kind is FrxBlobKind.Unknown or FrxBlobKind.Cursor or FrxBlobKind.Wmf or FrxBlobKind.Emf)
                    {
                        extras.Add("// TODO: " + c.MemberName + " image " + g + " (" + kind + ") not converted");
                        continue;
                    }
                    result.Resources.Add(new FormResource { Name = name, Data = data, Kind = kind });
                    extras.Add("{ var res = new System.ComponentModel.ComponentResourceManager(typeof(" + ctx.ClassName + ")); " + me + ".Images.Add(" + Lit(c.Text(pre + "Key")) + ", (" + D + "Image)res.GetObject(" + Lit(name) + ")); }");
                }
                return;
            case "CommonDialog":
                foreach (var p in c.Properties.Where(p => !IsExtenderProperty(p.Name)))
                {
                    switch (p.Name.ToLowerInvariant())
                    {
                        case "dialogtitle": case "filter": case "defaultext": case "initdir": case "filename": case "fontname": case "helpfile":
                            Add(me, Canon(p.Name), Lit(p.Text));
                            break;
                        case "filterindex": case "flags": case "color": case "min": case "max": case "frompage": case "topage": case "helpcommand":
                            Add(me, Canon(p.Name), ((int)p.Number()).ToString(Inv));
                            break;
                        case "copies":
                            Add(me, "Copies", "(short)" + ((int)p.Number()).ToString(Inv));
                            break;
                        case "fontsize":
                            Add(me, "FontSize", F(p.Number()));
                            break;
                        case "cancelerror": case "fontbold": case "fontitalic": case "fontunderline": case "fontstrikethru": case "printerdefault":
                            Add(me, Canon(p.Name), B(p.Bool()));
                            break;
                    }
                }
                return;
        }
    }

    private static string Canon(string vbName) => vbName.ToLowerInvariant() switch
    {
        "dialogtitle" => "DialogTitle", "filter" => "Filter", "defaultext" => "DefaultExt", "initdir" => "InitDir", "filename" => "FileName",
        "fontname" => "FontName", "helpfile" => "HelpFile", "filterindex" => "FilterIndex", "flags" => "Flags", "color" => "Color",
        "min" => "Min", "max" => "Max", "frompage" => "FromPage", "topage" => "ToPage", "helpcommand" => "HelpCommand",
        "cancelerror" => "CancelError", "fontbold" => "FontBold", "fontitalic" => "FontItalic", "fontunderline" => "FontUnderline",
        "fontstrikethru" => "FontStrikethru", "printerdefault" => "PrinterDefault", _ => vbName,
    };

    private void OcxProperties(ControlWithType c, string me)
    {
        var props = c.Properties.Where(p => !IsExtenderProperty(p.Name)).ToList();
        if (props.Count == 0) return;
        ocx.Add(me + ".CreateControl();");
        foreach (var p in props)
        {
            if (p.Frx != null)
            {
                ocx.Add("// TODO: " + c.MemberName + "." + p.Name + " is stored in the .frx (binary OCX state) and was not converted");
                continue;
            }
            ocx.Add(ControlCatalog.HelpersInterop + "OcxHelper.SetProperty(" + me + ", " + Lit(p.Name) + ", " + ValueLiteral(p) + ");");
        }
    }

    /// <summary>VB6 menus → a MenuStrip (top level) with ToolStripMenuItem trees.</summary>
    private void Menus(ControlWithType root)
    {
        var top = root.Children.Where(c => c.Type == "VB.Menu").ToList();
        if (top.Count == 0) return;
        foreach (var m in root.Descendants().Where(c => c.Type == "VB.Menu" && c.IsArrayElement && c.Text("Caption") == "-"))
        {
            body.Add("// TODO: menu array element " + m.MemberName + " is a separator; ToolStripMenuItem shows '-' as text");
        }
        const string strip = "MainMenu1";
        fields.Insert(0, "public " + W + "MenuStrip " + strip + ";");
        create.Insert(0, "this." + strip + " = new " + W + "MenuStrip();");
        suspend.Add("this." + strip + ".SuspendLayout();");
        resume.Insert(0, "this." + strip + ".ResumeLayout(false);");
        foreach (var m in root.Descendants().Where(c => c.Type == "VB.Menu")) MenuItem(m);
        body.Add("// " + strip);
        body.Add("this." + strip + ".Items.AddRange(new " + W + "ToolStripItem[] { " + string.Join(", ", top.Select(Ref)) + " });");
        body.Add("this." + strip + ".Location = new " + D + "Point(0, 0);");
        body.Add("this." + strip + ".Name = " + Lit(strip) + ";");
        var windowList = root.Descendants().FirstOrDefault(c => c.Type == "VB.Menu" && c.Bool("WindowList"));
        if (windowList != null) body.Add("this." + strip + ".MdiWindowListItem = " + Ref(windowList) + ";");
        body.Add("this.Controls.Add(this." + strip + ");");
        body.Add("this.MainMenuStrip = this." + strip + ";");
    }

    private void MenuItem(ControlWithType m)
    {
        var type = EffectiveType(m, ctx.Info(m));
        if (m.IsArrayElement) arrayTypes[m.Name] = W + "ToolStripMenuItem";
        var me = Ref(m);
        Declare(m, type);
        create.Add(me + " = new " + type + "();");
        Section(m.MemberName);
        Add(me, "Name", Lit(m.MemberName));
        if (type.EndsWith("ToolStripSeparator", StringComparison.Ordinal)) return;
        var kids = m.Children.Where(c => c.Type == "VB.Menu").ToList();
        if (kids.Count > 0) body.Add(me + ".DropDownItems.AddRange(new " + W + "ToolStripItem[] { " + string.Join(", ", kids.Select(Ref)) + " });");
        Add(me, "Text", Lit(m.Text("Caption")));
        var sc = Shortcut(m.Text("Shortcut"), W + "Keys");
        if (sc != "") Add(me, "ShortcutKeys", "(" + W + "Keys)(" + sc + ")");
        if (m.Bool("Checked")) Add(me, "Checked", "true");
        if (m.Has("Enabled") && !m.Bool("Enabled", true)) Add(me, "Enabled", "false");
        if (m.Has("Visible") && !m.Bool("Visible", true)) Add(me, "Visible", "false");
        Wire(m);
    }
}
