using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vb6ToCSharp.CodeGeneration;
using Vb6ToCSharp.Parsing;
using Vb6ToCSharp.Runtime;
using static Vb6ToCSharp.CodeGeneration.Emit;

namespace Vb6ToCSharp.ItemConversion;

/// <summary>Emits WPF XAML (+ code-behind members) for a VB6 form / user control.</summary>
public sealed class WpfEmitter
{
    private const string HP = "Vb6ToCSharp.UpgradeHelpers.Wpf.";
    private const string HR = ControlCatalog.HelpersRoot;

    private readonly FormContext ctx;
    private readonly FormControlFile controlFile;
    private readonly string assembly;
    private readonly EmitResult result = new();
    private readonly StringBuilder xaml = new();
    private readonly List<string> members = new();
    private readonly List<string> ctor = new();
    private readonly List<string> loaded = new();
    private readonly HashSet<string> arrays = new(StringComparer.OrdinalIgnoreCase);
    private int indent;
    private bool codeWiredLoad;

    public WpfEmitter(FormContext ctx, string assembly)
    {
        this.ctx = ctx;
        controlFile = ctx.ControlFile;
        this.assembly = assembly;
    }

    public static EmitResult Generate(FormContext ctx, string assembly) => new WpfEmitter(ctx, assembly).Run();

    /// <summary>CLR type of a XAML element name used by the emitter.</summary>
    public string ClrType(string element)
    {
        if (element.StartsWith(ControlCatalog.WpfHelpersPrefix, StringComparison.Ordinal)) return HP + element.Substring(ControlCatalog.WpfHelpersPrefix.Length);
        if (element.StartsWith("usercontrols:", StringComparison.Ordinal)) return assembly + ".UserControls." + element.Substring(13);
        if (element.Contains(".")) return element;
        return element switch
        {
            "Line" or "Rectangle" or "Ellipse" => "System.Windows.Shapes." + element,
            "ScrollBar" or "StatusBarItem" => "System.Windows.Controls.Primitives." + element,
            "StatusBar" => "System.Windows.Controls.Primitives.StatusBar",
            "WindowsFormsHost" => "System.Windows.Forms.Integration.WindowsFormsHost",
            "Window" => "System.Windows.Window",
            _ => "System.Windows.Controls." + element,
        };
    }

    private string ResourceFolder => controlFile.IsUserControl ? "UserControls" : "Forms";

    private EmitResult Run()
    {
        var root = controlFile.Root;
        var hosted = controlFile.AllControls().Any(c => ctx.Info(c).Kind == ControlKind.Hosted);
        codeWiredLoad = hosted;
        var uc = controlFile.IsUserControl || root.Type == "VB.PropertyPage";
        var tag = uc ? "UserControl" : "Window";
        var ns = assembly + (uc ? ".UserControls" : ".Forms");
        var a = new List<string>
        {
            "x:Class=" + Xml(ns + "." + ctx.ClassName),
            "xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"",
            "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"",
            "xmlns:d=\"http://schemas.microsoft.com/expression/blend/2008\"",
            "xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\"",
            "xmlns:local=" + Xml("clr-namespace:" + ns),
            "xmlns:usercontrols=" + Xml("clr-namespace:" + assembly + ".UserControls"),
            "xmlns:vb6=\"clr-namespace:Vb6ToCSharp.UpgradeHelpers.Wpf;assembly=Vb6ToCSharp.UpgradeHelpers\"",
        };
        // the generated partial class must match the code-behind's accessibility (a form of an ActiveX project is internal)
        if (ProjectGroup.TypeModifier(ProjectGroup.IsExposed(controlFile)) == "internal") a.Insert(1, "x:ClassModifier=\"internal\"");
        if (hosted)
        {
            a.Add("xmlns:wfi=\"clr-namespace:System.Windows.Forms.Integration;assembly=WindowsFormsIntegration\"");
            result.UsesWindowsFormsHost = true;
        }
        a.Add("mc:Ignorable=\"d\"");
        var w = Units.ToPixels(root.Num("ClientWidth", root.Num("Width")));
        var h = Units.ToPixels(root.Num("ClientHeight", root.Num("Height")));
        if (!uc)
        {
            a.Add("Title=" + Xml(root.Text("Caption")));
            a.Add("SizeToContent=\"WidthAndHeight\""); // the layout root has the VB6 client size
            var sp = (int)root.Num("StartUpPosition", 3);
            if (sp == 0)
            {
                a.Add("WindowStartupLocation=\"Manual\"");
                a.Add("Left=" + Xml(Units.ToPixels(root.Num("ClientLeft")).ToString(Inv)));
                a.Add("Top=" + Xml(Units.ToPixels(root.Num("ClientTop")).ToString(Inv)));
            }
            else if (sp == 1) a.Add("WindowStartupLocation=\"CenterOwner\"");
            else if (sp == 2) a.Add("WindowStartupLocation=\"CenterScreen\"");
            var bs = (int)root.Num("BorderStyle", 2);
            var fixedDialog = bs is 1 or 3 or 4;
            var canMin = root.Bool("MinButton", !fixedDialog);
            var canMax = root.Bool("MaxButton", !fixedDialog);
            var sizable = bs is 2 or 5;
            a.Add("ResizeMode=" + Xml(sizable ? (canMax ? "CanResize" : canMin ? "CanMinimize" : "NoResize") : canMin ? "CanMinimize" : "NoResize"));
            if (bs == 0) a.Add("WindowStyle=\"None\"");
            if (bs is 4 or 5) a.Add("WindowStyle=\"ToolWindow\"");
            if (root.Has("ShowInTaskbar") && !root.Bool("ShowInTaskbar", true)) a.Add("ShowInTaskbar=\"False\"");
            var ws = (int)root.Num("WindowState");
            if (ws is 1 or 2) a.Add("WindowState=" + Xml(ws == 1 ? "Minimized" : "Maximized"));
            var icon = Resource(root, "Icon", "Icon", icon: true);
            if (icon != null) a.Add("Icon=" + Xml(icon));
            a.Add("Background=" + Xml(root.Has("BackColor") ? ColorConverter.Xaml(ColorConverter.Parse(root.Get("BackColor").Raw)) : ColorConverter.Xaml(0x8000000F)));
            if (controlFile.IsMdiForm) xaml.Append("<!-- TODO: WPF has no MDI; ").Append(ctx.ClassName).Append(" was an MDIForm (child forms open as separate windows) -->\r\n");
        }
        else
        {
            a.Add("Width=" + Xml(w.ToString(Inv)));
            a.Add("Height=" + Xml(h.ToString(Inv)));
            if (root.Has("BackColor")) a.Add("Background=" + Xml(ColorConverter.Xaml(ColorConverter.Parse(root.Get("BackColor").Raw))));
        }
        if (root.Has("ForeColor")) a.Add("Foreground=" + Xml(ColorConverter.Xaml(ColorConverter.Parse(root.Get("ForeColor").Raw))));
        a.AddRange(FontAttributes(root));
        a.AddRange(EventAttributes(root));

        xaml.Append('<').Append(tag).Append(' ').Append(string.Join("\r\n    ", a)).Append(">\r\n");
        indent = 1;
        var menus = root.Children.Where(c => c.Type == "VB.Menu").ToList();
        if (menus.Count > 0)
        {
            Line("<DockPanel LastChildFill=\"True\">");
            indent++;
            Line("<Menu DockPanel.Dock=\"Top\">");
            indent++;
            foreach (var m in menus) Menu(m);
            indent--;
            Line("</Menu>");
        }
        var bg = uc ? null : Resource(root, "Picture", "Picture");
        Line("<Grid x:Name=\"LayoutRoot\" Width=" + Xml(w.ToString(Inv)) + " Height=" + Xml(h.ToString(Inv)) + " HorizontalAlignment=\"Left\" VerticalAlignment=\"Top\">");
        indent++;
        if (bg != null) Line("<Grid.Background><ImageBrush ImageSource=" + Xml(bg) + " Stretch=\"None\" AlignmentX=\"Left\" AlignmentY=\"Top\" /></Grid.Background>");
        Children(root, false);
        indent--;
        Line("</Grid>");
        if (menus.Count > 0)
        {
            indent--;
            Line("</DockPanel>");
        }
        xaml.Append("</").Append(tag).Append(">\r\n");

        foreach (var ev in ctx.EventsOf(root).Where(e => codeWiredLoad && e.vbEvent == "Load"))
        {
            loaded.Add(ev.handler + "(this, new System.Windows.RoutedEventArgs());");
        }
        if (loaded.Count > 0) ctor.Add("this.Loaded += (s, e) => { " + string.Join(" ", loaded) + " };");
        result.Designer = xaml.ToString();
        var sb = new StringBuilder();
        foreach (var m in members) sb.Append(m).Append("\r\n");
        result.CodeMembers = sb.ToString();
        result.ConstructorCode = string.Join("\r\n", ctor);
        return result;
    }

    private void Line(string s) => xaml.Append(new string(' ', indent * 2)).Append(s).Append("\r\n");

    /// <summary>Writes the .frx picture as a resource file; returns the XAML-relative path or null.</summary>
    private string Resource(ControlWithType c, string vbProp, string suffix, bool icon = false)
    {
        var res = Picture(controlFile, c, vbProp, "");
        if (res == null) return null;
        if (res.Kind is FrxBlobKind.Unknown or FrxBlobKind.Wmf or FrxBlobKind.Emf or FrxBlobKind.Cursor || (icon && res.Kind != FrxBlobKind.Icon))
        {
            Line("<!-- TODO: " + c.MemberName + "." + vbProp + " (" + res.Kind + " in the .frx) not converted -->");
            return null;
        }
        var fname = ctx.ClassName + "." + (c == controlFile.Root ? "Form" : c.MemberName) + "." + suffix + FrxReader.Extension(res.Kind);
        res.Name = ResourceFolder + "\\Resources\\" + fname;
        result.Resources.Add(res);
        return "Resources/" + fname;
    }

    private IEnumerable<string> FontAttributes(ControlWithType c)
    {
        string name;
        double size;
        bool bold, italic;
        if (c.Has("Font.Name"))
        {
            name = c.Text("Font.Name");
            size = c.Num("Font.Size", 8.25);
            bold = c.Num("Font.Weight", 400) >= 600 || c.Bool("Font.Bold");
            italic = c.Bool("Font.Italic");
        }
        else if (c.Has("FontName"))
        {
            name = c.Text("FontName");
            size = c.Num("FontSize", 8.25);
            bold = c.Bool("FontBold");
            italic = c.Bool("FontItalic");
        }
        else yield break;
        yield return "FontFamily=" + Xml(name);
        yield return "FontSize=" + Xml((size * 96 / 72).ToString("0.##", Inv)); // points → device-independent units
        if (bold) yield return "FontWeight=\"Bold\"";
        if (italic) yield return "FontStyle=\"Italic\"";
    }

    private bool IsCodeWired(ControlWithType c, EventBinding b) =>
        (c != controlFile.Root && ctx.Arrays.Contains(c.Name)) || ctx.Info(c).Kind is ControlKind.NonVisual or ControlKind.Hosted || b.Direct;

    private IEnumerable<string> EventAttributes(ControlWithType c)
    {
        var keyPreview = c == controlFile.Root && c.Bool("KeyPreview");
        foreach (var (ev, b, handler) in ctx.EventsOf(c))
        {
            if (c == controlFile.Root && codeWiredLoad && ev == "Load") continue;
            if (IsCodeWired(c, b))
            {
                CodeWire(c, b, handler);
                continue;
            }
            var net = keyPreview && b.NetEvent is "KeyDown" or "KeyUp" ? "Preview" + b.NetEvent : b.NetEvent;
            yield return net + "=" + Xml(handler);
        }
    }

    private void CodeWire(ControlWithType c, EventBinding b, string handler)
    {
        var info = ctx.Info(c);
        if (b.Direct)
        {
            ctor.Add("this." + c.MemberName + "." + b.NetEvent + " += this." + handler + ";");
            return;
        }
        if (info.Kind == ControlKind.Hosted)
        {
            ctor.Add("this." + c.MemberName + "." + b.NetEvent + " += new System.EventHandler(this." + handler + ");");
            return;
        }
        string hook;
        if (b.NetEvent.Contains("."))
        {
            var owner = b.NetEvent.Split('.')[0];
            var ownerType = owner is "TextBoxBase" or "ButtonBase" ? "System.Windows.Controls.Primitives." + owner : owner;
            hook = "x.AddHandler(" + ownerType + "." + b.NetEvent.Split('.')[1] + "Event, new " + b.Delegate + "(this." + handler + "))";
        }
        else hook = "x." + b.NetEvent + " += new " + b.Delegate + "(this." + handler + ")";
        if (ctx.Arrays.Contains(c.Name) && c != controlFile.Root)
        {
            if (c.Index != controlFile.AllControls().Where(x => x.Name == c.Name).Min(x => x.Index)) return;
            ctor.Add("this." + c.Name + ".Wire(x => " + hook + ");");
        }
        else
        {
            ctor.Add("{ var x = this." + c.MemberName + "; " + hook + "; }");
        }
    }

    private void RegisterArray(ControlWithType c, string clrType)
    {
        if (!c.IsArrayElement) return;
        if (arrays.Add(c.Name))
        {
            members.Add("public " + HP + "ControlArray<" + clrType + "> " + c.Name + ";");
            ctor.Insert(0, "this." + c.Name + " = new " + HP + "ControlArray<" + clrType + ">(" + Lit(c.Name) + ");");
        }
        ctor.Add("this." + c.Name + ".SetIndex(this." + c.MemberName + ", " + c.Index + ");");
    }

    private string Brush(ControlWithType c, string prop) => ColorConverter.Xaml(ColorConverter.Parse(c.Get(prop).Raw));

    private List<string> Placement(ControlWithType c, bool canvas)
    {
        var x = Units.PosTwips(c, "Left");
        var y = Units.PosTwips(c, "Top");
        if (c.Parent?.Type == "TabDlg.SSTab")
        {
            if (x < -60000) x += 75000;
            y = Math.Max(0, y - c.Parent.Num("TabHeight", 353));
        }
        var px = Units.ToPixels(x);
        var py = Units.ToPixels(y);
        var a = new List<string>();
        var align = (int)c.Num("Align", c.ClassName == "StatusBar" ? 2 : c.ClassName == "Toolbar" ? 1 : 0);
        if (align != 0 && !canvas)
        {
            a.Add(align is 1 or 2 ? "VerticalAlignment=" + Xml(align == 1 ? "Top" : "Bottom") + " HorizontalAlignment=\"Stretch\"" : "HorizontalAlignment=" + Xml(align == 3 ? "Left" : "Right") + " VerticalAlignment=\"Stretch\"");
            if (align is 1 or 2) a.Add("Height=" + Xml(Units.SizePx(c, "Height", 375).ToString(Inv)));
            else a.Add("Width=" + Xml(Units.SizePx(c, "Width", 1200).ToString(Inv)));
            return a;
        }
        if (canvas)
        {
            a.Add("Canvas.Left=" + Xml(px.ToString(Inv)));
            a.Add("Canvas.Top=" + Xml(py.ToString(Inv)));
        }
        else
        {
            a.Add("Margin=" + Xml(px + "," + py + ",0,0"));
            a.Add("HorizontalAlignment=\"Left\" VerticalAlignment=\"Top\"");
        }
        a.Add("Width=" + Xml(Units.SizePx(c, "Width").ToString(Inv)));
        a.Add("Height=" + Xml(Units.SizePx(c, "Height").ToString(Inv)));
        return a;
    }

    private void Common(ControlWithType c, List<string> a)
    {
        if (c.Has("TabIndex")) a.Add("TabIndex=" + Xml(((int)c.Num("TabIndex")).ToString(Inv)));
        if (c.Has("TabStop") && !c.Bool("TabStop", true)) a.Add("IsTabStop=\"False\"");
        if (c.Has("Enabled") && !c.Bool("Enabled", true)) a.Add("IsEnabled=\"False\"");
        if (c.Has("Visible") && !c.Bool("Visible", true)) a.Add("Visibility=\"Hidden\"");
        if (c.Has("BackColor")) a.Add("Background=" + Xml(Brush(c, "BackColor")));
        if (c.Has("ForeColor")) a.Add("Foreground=" + Xml(Brush(c, "ForeColor")));
        if (c.Has("ToolTipText")) a.Add("ToolTip=" + Xml(TextOf(controlFile, c, "ToolTipText")));
        if (c.Has("Tag")) a.Add("Tag=" + Xml(c.Text("Tag")));
        var mp = (int)c.Num("MousePointer");
        if (mp > 0 && mp < WpfCursors.Length) a.Add("Cursor=" + Xml(WpfCursors[mp]));
        a.AddRange(FontAttributes(c));
    }

    /// <summary>VB6 access keys (<c>&amp;File</c>) as WPF access text (<c>_File</c>).</summary>
    public static string AccessText(string s)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == '_') sb.Append("__");
            else if (s[i] == '&' && i + 1 < s.Length && s[i + 1] == '&') { sb.Append('&'); i++; }
            else if (s[i] == '&') sb.Append('_');
            else sb.Append(s[i]);
        }
        return sb.ToString();
    }

    private void Children(ControlWithType container, bool canvas)
    {
        foreach (var c in container.Children) Control(c, canvas);
    }

    private void Control(ControlWithType c, bool canvas)
    {
        var info = ctx.Info(c);
        var element = info.Wpf;
        var me = c.MemberName;
        switch (info.Kind)
        {
            case ControlKind.Menu:
                return;
            case ControlKind.NonVisual:
                NonVisual(c, info);
                return;
            case ControlKind.Line:
                LineShape(c);
                return;
            case ControlKind.Shape:
                ShapeElement(c);
                return;
            case ControlKind.Hosted:
                Hosted(c, info, canvas);
                return;
        }

        var a = new List<string> { "x:Name=" + Xml(me) };
        a.AddRange(Placement(c, canvas));
        Common(c, a);
        var content = new List<string>();
        string childPanel = null;
        switch (c.ClassName)
        {
            case "Label":
                a.Add("Content=" + Xml(AccessText(TextOf(controlFile, c, "Caption") ?? "")));
                a.Add("Padding=\"0\"");
                var la = (int)c.Num("Alignment");
                if (la != 0) a.Add("HorizontalContentAlignment=" + Xml(la == 1 ? "Right" : "Center"));
                if (c.Num("BorderStyle") == 1) a.Add("BorderBrush=\"Black\" BorderThickness=\"1\"");
                break;
            case "TextBox":
                var t = TextOf(controlFile, c, "Text");
                if (t != null) a.Add("Text=" + Xml(t));
                var sb = (int)c.Num("ScrollBars");
                if (c.Bool("MultiLine"))
                {
                    a.Add("AcceptsReturn=\"True\"");
                    if (sb is not (1 or 3)) a.Add("TextWrapping=\"Wrap\"");
                }
                if (sb is 1 or 3) a.Add("HorizontalScrollBarVisibility=\"Visible\"");
                if (sb is 2 or 3) a.Add("VerticalScrollBarVisibility=\"Visible\"");
                if (c.Num("MaxLength") > 0) a.Add("MaxLength=" + Xml(((int)c.Num("MaxLength")).ToString(Inv)));
                if (c.Bool("Locked")) a.Add("IsReadOnly=\"True\"");
                var ta = (int)c.Num("Alignment");
                if (ta != 0) a.Add("TextAlignment=" + Xml(ta == 1 ? "Right" : "Center"));
                if (c.Num("BorderStyle", 1) == 0) a.Add("BorderThickness=\"0\"");
                if (c.Text("PasswordChar") != "") content.Add("<!-- TODO: PasswordChar: use a PasswordBox (Password instead of Text) -->");
                break;
            case "CommandButton":
                a.Add("Content=" + Xml(AccessText(TextOf(controlFile, c, "Caption") ?? "")));
                if (c.Bool("Default")) a.Add("IsDefault=\"True\"");
                if (c.Bool("Cancel")) a.Add("IsCancel=\"True\"");
                break;
            case "CheckBox":
                a.Add("Content=" + Xml(AccessText(TextOf(controlFile, c, "Caption") ?? "")));
                var v = (int)c.Num("Value");
                if (v == 1) a.Add("IsChecked=\"True\"");
                if (v == 2) a.Add("IsThreeState=\"True\" IsChecked=\"{x:Null}\"");
                break;
            case "OptionButton":
                a.Add("Content=" + Xml(AccessText(TextOf(controlFile, c, "Caption") ?? "")));
                if (c.Bool("Value")) a.Add("IsChecked=\"True\"");
                break;
            case "Frame":
                a.Add("Header=" + Xml(AccessText(TextOf(controlFile, c, "Caption") ?? "")));
                if (c.Num("BorderStyle", 1) == 0) a.Add("BorderThickness=\"0\"");
                childPanel = "<Grid Margin=\"-6,-17,-6,-6\">"; // put the children at the frame's outer origin, like VB6
                break;
            case "ComboBox":
            case "ImageCombo":
                a.Add("IsEditable=" + Xml(c.Num("Style") == 2 ? "False" : "True"));
                if (c.Num("Style") != 2 && c.Has("Text")) a.Add("Text=" + Xml(TextOf(controlFile, c, "Text")));
                if (c.Bool("Sorted")) content.Add("<!-- TODO: Sorted: sort Items.SortDescriptions -->");
                foreach (var it in ListOf(controlFile, c, "List")) content.Add("<ComboBoxItem Content=" + Xml(it) + " />");
                if (c.Has("ItemData")) content.Add("<!-- TODO: ItemData values not converted (store them in Tag) -->");
                break;
            case "ListBox":
                var ms = (int)c.Num("MultiSelect");
                if (ms != 0) a.Add("SelectionMode=" + Xml(ms == 1 ? "Multiple" : "Extended"));
                foreach (var it in ListOf(controlFile, c, "List")) content.Add("<ListBoxItem Content=" + Xml(it) + " />");
                if (c.Has("ItemData")) content.Add("<!-- TODO: ItemData values not converted (store them in Tag) -->");
                break;
            case "PictureBox":
                a.Add("ClipToBounds=\"True\"");
                var pic = Resource(c, "Picture", "Picture");
                if (pic != null) content.Add("<Canvas.Background><ImageBrush ImageSource=" + Xml(pic) + " Stretch=\"None\" AlignmentX=\"Left\" AlignmentY=\"Top\" /></Canvas.Background>");
                break;
            case "Image":
                var src = Resource(c, "Picture", "Picture");
                if (src != null) a.Add("Source=" + Xml(src));
                a.Add("Stretch=" + Xml(c.Bool("Stretch") ? "Fill" : "None"));
                break;
            case "HScrollBar":
            case "VScrollBar":
            case "FlatScrollBar":
                var vertical = c.ClassName == "VScrollBar" || (c.ClassName == "FlatScrollBar" && c.Num("Orientation", 1) == 0);
                a.Add("Orientation=" + Xml(vertical ? "Vertical" : "Horizontal"));
                a.Add("Minimum=" + Xml(((int)c.Num("Min")).ToString(Inv)));
                a.Add("Maximum=" + Xml(((int)c.Num("Max", 32767)).ToString(Inv)));
                a.Add("SmallChange=" + Xml(((int)c.Num("SmallChange", 1)).ToString(Inv)));
                a.Add("LargeChange=" + Xml(((int)c.Num("LargeChange", 1)).ToString(Inv)));
                if (c.Has("Value")) a.Add("Value=" + Xml(((int)c.Num("Value")).ToString(Inv)));
                break;
            case "FileListBox":
                if (c.Has("Pattern")) a.Add("Pattern=" + Xml(c.Text("Pattern")));
                break;
            case "TreeView":
                break;
            case "ListView":
                if (c.Num("View") == 3)
                {
                    content.Add("<ListView.View><GridView>");
                    foreach (var g in c.SubGroups("ColumnHeaders"))
                    {
                        var pre = "ColumnHeaders." + g + ".";
                        content.Add("  <GridViewColumn Header=" + Xml(c.Text(pre + "Text")) + " Width=" + Xml(Units.ToPixels(Units.SizeTwips(c, pre + "Width", 1440)).ToString(Inv)) + " />");
                    }
                    content.Add("</GridView></ListView.View>");
                }
                if (!c.Bool("MultiSelect")) a.Add("SelectionMode=\"Single\"");
                break;
            case "ProgressBar":
                a.Add("Minimum=" + Xml(c.Num("Min").ToString(Inv)));
                a.Add("Maximum=" + Xml(c.Num("Max", 100).ToString(Inv)));
                if (c.Has("Value")) a.Add("Value=" + Xml(c.Num("Value").ToString(Inv)));
                if (c.Num("Orientation") == 1) a.Add("Orientation=\"Vertical\"");
                break;
            case "Slider":
                a.Add("Minimum=" + Xml(c.Num("Min").ToString(Inv)));
                a.Add("Maximum=" + Xml(c.Num("Max", 10).ToString(Inv)));
                a.Add("SmallChange=" + Xml(c.Num("SmallChange", 1).ToString(Inv)));
                a.Add("LargeChange=" + Xml(c.Num("LargeChange", 5).ToString(Inv)));
                a.Add("TickFrequency=" + Xml(c.Num("TickFrequency", 1).ToString(Inv)));
                var tsl = (int)c.Num("TickStyle");
                a.Add("TickPlacement=" + Xml(tsl switch { 1 => "TopLeft", 2 => "Both", 3 => "None", _ => "BottomRight" }));
                if (c.Num("Orientation") == 1) a.Add("Orientation=\"Vertical\"");
                if (c.Has("Value")) a.Add("Value=" + Xml(c.Num("Value").ToString(Inv)));
                break;
            case "StatusBar":
                if (c.Num("Style") == 1) content.Add("<StatusBarItem Content=" + Xml(c.Text("SimpleText")) + " />");
                else
                {
                    foreach (var g in c.SubGroups("Panels"))
                    {
                        var pre = "Panels." + g + ".";
                        var wpx = c.Num(pre + "AutoSize") == 0 ? " Width=" + Xml(Units.ToPixels(Units.SizeTwips(c, pre + "Width", 1440)).ToString(Inv)) : "";
                        content.Add("<StatusBarItem Content=" + Xml(c.Text(pre + "Text")) + wpx + (c.Has(pre + "Key") ? " Tag=" + Xml(c.Text(pre + "Key")) : "") + " />");
                    }
                }
                break;
            case "Toolbar":
                foreach (var g in c.SubGroups("Buttons"))
                {
                    var pre = "Buttons." + g + ".";
                    var style = (int)c.Num(pre + "Style");
                    if (style is 3 or 4) content.Add("<Separator />");
                    else
                    {
                        var el = style is 1 or 2 ? "ToggleButton" : "Button";
                        content.Add("<" + el + " Content=" + Xml(c.Text(pre + "Caption")) + (c.Has(pre + "Key") ? " Tag=" + Xml(c.Text(pre + "Key")) : "") +
                                    (c.Has(pre + "ToolTipText") ? " ToolTip=" + Xml(c.Text(pre + "ToolTipText")) : "") + " />");
                    }
                }
                break;
            case "TabStrip":
                foreach (var g in c.SubGroups("Tabs")) content.Add("<TabItem Header=" + Xml(c.Text("Tabs." + g + ".Caption")) + (c.Has("Tabs." + g + ".Key") ? " Tag=" + Xml(c.Text("Tabs." + g + ".Key")) : "") + " />");
                break;
            case "DTPicker":
                if (c.Has("CurrentDate") && c.Num("CurrentDate") > 0) a.Add("SelectedDate=" + Xml(DateTime.FromOADate(c.Num("CurrentDate")).ToString("yyyy-MM-dd", Inv)));
                var fmt = (int)c.Num("Format", 1);
                a.Add("SelectedDateFormat=" + Xml(fmt == 0 ? "Long" : "Short"));
                if (fmt is 2 or 3) content.Add("<!-- TODO: DTPicker time/custom format has no DatePicker equivalent -->");
                break;
            case "UpDown":
                a.Add("Min=" + Xml(((int)c.Num("Min")).ToString(Inv)));
                a.Add("Max=" + Xml(((int)c.Num("Max", 10)).ToString(Inv)));
                a.Add("Increment=" + Xml(((int)c.Num("Increment", 1)).ToString(Inv)));
                if (c.Bool("Wrap")) a.Add("Wrap=\"True\"");
                if (c.Has("Value")) a.Add("Value=" + Xml(((int)c.Num("Value")).ToString(Inv)));
                break;
            case "MSFlexGrid":
            case "MSHFlexGrid":
                foreach (var p in new[] { "Rows", "Cols", "FixedRows", "FixedCols" })
                {
                    if (c.Has(p)) a.Add(p + "=" + Xml(((int)c.Num(p)).ToString(Inv)));
                }
                if (c.Has("FormatString")) a.Add("FormatString=" + Xml(c.Text("FormatString")));
                break;
            case "RichTextBox":
                var rtf = TextOf(controlFile, c, "TextRTF");
                if (!string.IsNullOrEmpty(rtf) && rtf.TrimStart().StartsWith("{\\rtf", StringComparison.Ordinal))
                {
                    ctor.Add("{ var r = new System.Windows.Documents.TextRange(this." + me + ".Document.ContentStart, this." + me + ".Document.ContentEnd); " +
                             "using (var s = new System.IO.MemoryStream(System.Text.Encoding.Default.GetBytes(" + Lit(rtf) + "))) r.Load(s, System.Windows.DataFormats.Rtf); }");
                }
                else if (c.Has("Text"))
                {
                    content.Add("<FlowDocument><Paragraph><Run Text=" + Xml(TextOf(controlFile, c, "Text")) + " /></Paragraph></FlowDocument>");
                }
                if (c.Bool("Locked")) a.Add("IsReadOnly=\"True\"");
                break;
        }
        if (info.Kind == ControlKind.Placeholder) xaml.Append(new string(' ', indent * 2)).Append("<!-- TODO: VB6 control ").Append(c.Type).Append(" '").Append(c.Name).Append("' has no WPF equivalent (placeholder) -->\r\n");
        if (info.Kind == ControlKind.Placeholder) a.Add("BorderBrush=\"Gray\" BorderThickness=\"1\"");
        if (info.Kind == ControlKind.ProjectUserControl) UserControlProperties(c);
        a.AddRange(EventAttributes(c));
        RegisterArray(c, ClrType(element));

        var hasKids = c.Children.Any(ch => ctx.Info(ch).Kind != ControlKind.Menu);
        if (!hasKids && content.Count == 0 && info.Kind != ControlKind.TabbedContainer)
        {
            Line("<" + element + " " + string.Join(" ", a) + " />");
            return;
        }
        Line("<" + element + " " + string.Join(" ", a) + ">");
        indent++;
        foreach (var l in content) Line(l);
        if (info.Kind == ControlKind.TabbedContainer) TabItems(c);
        else if (hasKids)
        {
            var inCanvas = c.ClassName == "PictureBox";
            if (childPanel != null) Line(childPanel);
            else if (!inCanvas) Line("<Grid>");
            indent++;
            Children(c, inCanvas);
            indent--;
            if (childPanel != null || !inCanvas) Line("</Grid>");
        }
        indent--;
        Line("</" + element + ">");
    }

    private void TabItems(ControlWithType c)
    {
        var tabs = (int)c.Num("Tabs", 3);
        var owner = new Dictionary<ControlWithType, int>();
        foreach (var p in c.Properties)
        {
            var m = System.Text.RegularExpressions.Regex.Match(p.Name, @"^Tab\((\d+)\)\.Control\(\d+\)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!m.Success) continue;
            var n = p.Text;
            var idx = n.IndexOf('(');
            var baseName = idx > 0 ? n.Substring(0, idx) : n;
            int? index = idx > 0 && int.TryParse(n.Substring(idx + 1).TrimEnd(')'), out var ix) ? ix : null;
            var child = c.Children.FirstOrDefault(ch => ch.Name.Equals(baseName, StringComparison.OrdinalIgnoreCase) && ch.Index == index);
            if (child != null) owner[child] = int.Parse(m.Groups[1].Value, Inv);
        }
        for (var t = 0; t < tabs; t++)
        {
            Line("<TabItem Header=" + Xml(AccessText(c.Text("TabCaption(" + t + ")"))) + ">");
            indent++;
            Line("<Grid>");
            indent++;
            foreach (var ch in c.Children.Where(ch => (owner.TryGetValue(ch, out var o) ? o : 0) == t)) Control(ch, false);
            indent--;
            Line("</Grid>");
            indent--;
            Line("</TabItem>");
        }
    }

    private void Menu(ControlWithType m)
    {
        if (m.Text("Caption") == "-" && !m.IsArrayElement)
        {
            Line("<Separator x:Name=" + Xml(m.MemberName) + " />");
            return;
        }
        var a = new List<string> { "x:Name=" + Xml(m.MemberName), "Header=" + Xml(AccessText(m.Text("Caption"))) };
        var sc = m.Text("Shortcut");
        var keys = Shortcut(sc, "System.Windows.Input.Key");
        if (keys != "")
        {
            var parts = keys.Split(new[] { " | " }, StringSplitOptions.None).Select(p => p.Substring("System.Windows.Input.Key.".Length)).ToList();
            var key = parts.Last();
            var mods = parts.Take(parts.Count - 1).Select(p => p == "Control" ? "Ctrl" : p).ToList();
            a.Add("InputGestureText=" + Xml(string.Join("+", mods.Concat(new[] { key.StartsWith("D") && key.Length == 2 ? key.Substring(1) : key }))));
            var modExpr = mods.Count == 0 ? "System.Windows.Input.ModifierKeys.None" : string.Join(" | ", mods.Select(x => "System.Windows.Input.ModifierKeys." + (x == "Ctrl" ? "Control" : x)));
            ctor.Add("{ var cmd = new System.Windows.Input.RoutedCommand(); var mi = this." + m.MemberName + "; " +
                     "this.CommandBindings.Add(new System.Windows.Input.CommandBinding(cmd, (s, e) => mi.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent, mi)), (s, e) => e.CanExecute = mi.IsEnabled)); " +
                     "this.InputBindings.Add(new System.Windows.Input.KeyBinding(cmd, System.Windows.Input.Key." + key + ", " + modExpr + ")); }");
        }
        if (m.Bool("Checked")) a.Add("IsCheckable=\"True\" IsChecked=\"True\"");
        if (m.Has("Enabled") && !m.Bool("Enabled", true)) a.Add("IsEnabled=\"False\"");
        if (m.Has("Visible") && !m.Bool("Visible", true)) a.Add("Visibility=\"Collapsed\"");
        a.AddRange(EventAttributes(m));
        RegisterArray(m, "System.Windows.Controls.MenuItem");
        var kids = m.Children.Where(c => c.Type == "VB.Menu").ToList();
        if (kids.Count == 0)
        {
            Line("<MenuItem " + string.Join(" ", a) + " />");
            return;
        }
        Line("<MenuItem " + string.Join(" ", a) + ">");
        indent++;
        foreach (var k in kids) Menu(k);
        indent--;
        Line("</MenuItem>");
    }

    private string Stroke(ControlWithType c)
    {
        var s = new List<string>();
        s.Add("Stroke=" + Xml(c.Has("BorderColor") ? Brush(c, "BorderColor") : "Black"));
        s.Add("StrokeThickness=" + Xml(((int)c.Num("BorderWidth", 1)).ToString(Inv)));
        var bs = (int)c.Num("BorderStyle", 1);
        if (bs == 0) s.Add("Visibility=\"Hidden\"");
        if (bs is >= 2 and <= 5) s.Add("StrokeDashArray=" + Xml(bs switch { 2 => "4 2", 3 => "1 2", 4 => "4 2 1 2", _ => "4 2 1 2 1 2" }));
        if (c.Has("Visible") && !c.Bool("Visible", true)) s.Add("Visibility=\"Hidden\"");
        return string.Join(" ", s.Distinct());
    }

    private void LineShape(ControlWithType c)
    {
        Line("<Line x:Name=" + Xml(c.MemberName) + " X1=" + Xml(Units.ToPixels(Units.PosTwips(c, "X1")).ToString(Inv)) + " Y1=" + Xml(Units.ToPixels(Units.PosTwips(c, "Y1")).ToString(Inv)) +
             " X2=" + Xml(Units.ToPixels(Units.PosTwips(c, "X2")).ToString(Inv)) + " Y2=" + Xml(Units.ToPixels(Units.PosTwips(c, "Y2")).ToString(Inv)) + " " + Stroke(c) + " />");
        RegisterArray(c, "System.Windows.Shapes.Line");
    }

    private void ShapeElement(ControlWithType c)
    {
        var shape = (int)c.Num("Shape");
        var el = shape is 2 or 3 ? "Ellipse" : "Rectangle";
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
        var a = "x:Name=" + Xml(c.MemberName) + " Margin=" + Xml(x + "," + y + ",0,0") + " Width=" + Xml(w.ToString(Inv)) + " Height=" + Xml(h.ToString(Inv)) +
                " HorizontalAlignment=\"Left\" VerticalAlignment=\"Top\" " + Stroke(c);
        if (shape is 4 or 5) a += " RadiusX=" + Xml(Math.Max(1, Math.Min(w, h) / 8).ToString(Inv)) + " RadiusY=" + Xml(Math.Max(1, Math.Min(w, h) / 8).ToString(Inv));
        var fs = (int)c.Num("FillStyle", 1);
        if (fs == 0) a += " Fill=" + Xml(c.Has("FillColor") ? Brush(c, "FillColor") : "Black");
        else if (c.Num("BackStyle") == 1 && c.Has("BackColor")) a += " Fill=" + Xml(Brush(c, "BackColor"));
        Line("<" + el + " " + a + " />");
        if (fs >= 2) Line("<!-- TODO: " + c.MemberName + " hatch FillStyle " + fs + " (use a DrawingBrush) -->");
        RegisterArray(c, "System.Windows.Shapes." + el);
    }

    private void NonVisual(ControlWithType c, ControlInfo info)
    {
        var me = c.MemberName;
        var type = info.Wpf;
        members.Add("public " + type + " " + me + ";");
        var s = new StringBuilder("this." + me + " = new " + type + "();");
        switch (c.ClassName)
        {
            case "Timer":
                var interval = (int)c.Num("Interval");
                if (interval > 0) s.Append(" this." + me + ".Interval = System.TimeSpan.FromMilliseconds(" + interval + ");");
                ctor.Add(s.ToString());
                foreach (var (_, b, handler) in ctx.EventsOf(c)) CodeWire(c, b, handler);
                ctor.Add("this." + me + ".IsEnabled = " + B(c.Bool("Enabled", true) && interval > 0) + ";");
                break;
            case "CommonDialog":
                ctor.Add(s.ToString());
                foreach (var p in c.Properties.Where(p => !IsExtenderProperty(p.Name)))
                {
                    var name = p.Name;
                    var value = p.IsQuoted ? Lit(p.Text) : name.ToLowerInvariant() is "cancelerror" or "fontbold" or "fontitalic" or "fontunderline" or "fontstrikethru" or "printerdefault" ? B(p.Bool()) : name.Equals("FontSize", StringComparison.OrdinalIgnoreCase) ? F(p.Number()) : name.Equals("Copies", StringComparison.OrdinalIgnoreCase) ? "(short)" + (int)p.Number() : ((int)p.Number()).ToString(Inv);
                    ctor.Add(HR + "OcxHelper.SetProperty(this." + me + ", " + Lit(name) + ", " + value + ");");
                }
                break;
            case "ImageList":
                ctor.Add(s.ToString());
                foreach (var g in c.SubGroups("Images"))
                {
                    var frx = c.Get("Images." + g + ".Picture")?.Frx;
                    var r = frx == null ? null : FrxReader.Open(controlFile.FrxPath(frx));
                    var data = r?.ReadBlob(frx.Offset);
                    if (data == null || data.Length == 0) continue;
                    var kind = FrxReader.Sniff(data);
                    if (kind is FrxBlobKind.Unknown or FrxBlobKind.Wmf or FrxBlobKind.Emf or FrxBlobKind.Cursor) continue;
                    var fname = ctx.ClassName + "." + me + "." + g + FrxReader.Extension(kind);
                    result.Resources.Add(new FormResource { Name = ResourceFolder + "\\Resources\\" + fname, Data = data, Kind = kind });
                    ctor.Add("this." + me + ".Add(System.Windows.Media.Imaging.BitmapFrame.Create(new System.Uri(" + Lit("pack://application:,,,/" + ResourceFolder + "/Resources/" + fname) + ")));");
                }
                break;
            default:
                ctor.Add(s.ToString());
                break;
        }
    }

    private void Hosted(ControlWithType c, ControlInfo info, bool canvas)
    {
        result.HostedLibraries.Add(info.Library);
        var host = "host_" + c.MemberName;
        var a = new List<string> { "x:Name=" + Xml(host) };
        a.AddRange(Placement(c, canvas));
        if (c.Has("Visible") && !c.Bool("Visible", true)) a.Add("Visibility=\"Hidden\"");
        Line("<wfi:WindowsFormsHost " + string.Join(" ", a) + " />");
        var type = info.WinForms;
        members.Add("public " + type + " " + c.MemberName + ";");
        ctor.Add("this." + c.MemberName + " = new " + type + "(); ((System.ComponentModel.ISupportInitialize)this." + c.MemberName + ").BeginInit(); this." + host + ".Child = this." + c.MemberName + "; ((System.ComponentModel.ISupportInitialize)this." + c.MemberName + ").EndInit();");
        RegisterArray(c, type);
        var props = c.Properties.Where(p => !IsExtenderProperty(p.Name)).ToList();
        if (props.Count > 0)
        {
            var set = new List<string> { "this." + c.MemberName + ".CreateControl();" };
            foreach (var p in props)
            {
                if (p.Frx != null) Line("<!-- TODO: " + c.MemberName + "." + p.Name + " is stored in the .frx (binary OCX state) and was not converted -->");
                else set.Add(HR + "OcxHelper.SetProperty(this." + c.MemberName + ", " + Lit(p.Name) + ", " + ValueLiteral(p) + ");");
            }
            loaded.Insert(0, string.Join(" ", set));
        }
        foreach (var (ev, b, handler) in ctx.EventsOf(c))
        {
            if (Handlerless(c, ev)) CodeWire(c, b, handler);
            else Line("<!-- TODO: ActiveX event " + ev + " of " + c.Name + " has arguments; wire the aximp event manually -->");
        }
    }

    private bool Handlerless(ControlWithType c, string ev) =>
        System.Text.RegularExpressions.Regex.IsMatch(controlFile.Code, @"Sub\s+" + System.Text.RegularExpressions.Regex.Escape(ctx.HandlerName(c, ev)) + @"\s*\(\s*\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private void UserControlProperties(ControlWithType c)
    {
        var persisted = c.Properties.Where(p => !IsExtenderProperty(p.Name) && p.Frx == null).ToList();
        ctor.Add(persisted.Count > 0
            ? "this." + c.MemberName + ".ReadProperties(" + HR + "PropertyBag.FromPairs(" + string.Join(", ", persisted.Select(p => Lit(p.Name) + ", " + ValueLiteral(p))) + "));"
            : "this." + c.MemberName + ".InitProperties();");
    }
}
