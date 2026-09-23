using System;
using System.Collections.Generic;
using Vb6ToCSharp.CodeConversion.Model;
using Vb6ToCSharp.FormConversion.Model;
using Vb6ToCSharp.Parsing.Model;
using Vb6ToCSharp.Runtime;
using static Vb6ToCSharp.Parsing.ProjectConfigurationParser;

namespace Vb6ToCSharp.FormConversion;

/// <summary>VB6 control type → WinForms / WPF equivalent (VBUC-aligned), INI-overridable.</summary>
public static class ControlCatalog
{
    public const string WinFormsNs = "System.Windows.Forms.";
    public const string HelpersWinForms = "Vb6ToCSharp.UpgradeHelpers.WinForms.";
    public const string HelpersRoot = "Vb6ToCSharp.UpgradeHelpers.";
    public const string PowerPacks = "Microsoft.VisualBasic.PowerPacks.";
    /// <summary>XAML prefix of the WPF helpers (<c>xmlns:vb6</c>).</summary>
    public const string WpfHelpersPrefix = "vb6:";

    private static readonly Dictionary<string, ControlInfo> map = new(StringComparer.OrdinalIgnoreCase);

    private static void Add(string vb, ControlKind kind, string winForms, string wpf, string def = "Caption")
    {
        map[vb] = new ControlInfo { VbType = vb, Kind = kind, WinForms = winForms, Wpf = wpf, DefaultProperty = def };
    }

    static ControlCatalog()
    {
        const ControlKind S = ControlKind.Standard, C = ControlKind.Container, N = ControlKind.NonVisual;
        const string W = WinFormsNs, H = HelpersWinForms, V = WpfHelpersPrefix;
        Add("VB.Form", ControlKind.Root, W + "Form", "Window");
        Add("VB.MDIForm", ControlKind.Root, W + "Form", "Window");
        Add("VB.UserControl", ControlKind.Root, W + "UserControl", "UserControl");
        Add("VB.PropertyPage", ControlKind.Root, W + "UserControl", "UserControl");
        Add("VB.Label", S, W + "Label", "Label");
        Add("VB.TextBox", S, W + "TextBox", "TextBox", "Text");
        Add("VB.CommandButton", S, W + "Button", "Button", "Value");
        Add("VB.CheckBox", S, W + "CheckBox", "CheckBox", "Value");
        Add("VB.OptionButton", S, W + "RadioButton", "RadioButton", "Value");
        Add("VB.Frame", C, W + "GroupBox", "GroupBox");
        Add("VB.ComboBox", S, W + "ComboBox", "ComboBox", "Text");
        Add("VB.ListBox", S, W + "ListBox", "ListBox", "Text");
        Add("VB.PictureBox", C, W + "PictureBox", "Canvas", "Picture");
        Add("VB.Image", S, W + "PictureBox", "Image", "Picture");
        Add("VB.HScrollBar", S, W + "HScrollBar", "ScrollBar", "Value");
        Add("VB.VScrollBar", S, W + "VScrollBar", "ScrollBar", "Value");
        Add("VB.Timer", N, W + "Timer", "System.Windows.Threading.DispatcherTimer", "Enabled");
        Add("VB.DriveListBox", S, H + "DriveListBox", V + "DriveListBox", "Drive");
        Add("VB.DirListBox", S, H + "DirListBox", V + "DirListBox", "Path");
        Add("VB.FileListBox", S, H + "FileListBox", V + "FileListBox", "FileName");
        Add("VB.Line", ControlKind.Line, PowerPacks + "LineShape", "Line", "Visible");
        Add("VB.Shape", ControlKind.Shape, PowerPacks + "RectangleShape", "Rectangle", "Shape");
        Add("VB.Menu", ControlKind.Menu, W + "ToolStripMenuItem", "MenuItem", "Enabled");
        Add("VB.Data", ControlKind.Placeholder, W + "Panel", "Border", "Caption");
        Add("VB.OLE", ControlKind.Placeholder, W + "Panel", "Border", "Action");

        foreach (var lib in new[] { "MSComctlLib", "ComctlLib" })
        {
            Add(lib + ".TreeView", S, W + "TreeView", "TreeView", "Nodes");
            Add(lib + ".ListView", S, W + "ListView", "ListView", "ListItems");
            Add(lib + ".ImageList", N, W + "ImageList", "System.Collections.Generic.List<System.Windows.Media.ImageSource>", "ListImages");
            Add(lib + ".ProgressBar", S, W + "ProgressBar", "ProgressBar", "Value");
            Add(lib + ".Slider", S, W + "TrackBar", "Slider", "Value");
            Add(lib + ".StatusBar", S, W + "StatusStrip", "StatusBar", "SimpleText");
            Add(lib + ".Toolbar", S, W + "ToolStrip", "ToolBar", "Buttons");
            Add(lib + ".TabStrip", S, W + "TabControl", "TabControl", "Tabs");
            Add(lib + ".ImageCombo", S, W + "ComboBox", "ComboBox", "Text");
        }
        foreach (var lib in new[] { "MSComCtl2", "ComCtl2" })
        {
            Add(lib + ".DTPicker", S, W + "DateTimePicker", "DatePicker", "Value");
            Add(lib + ".MonthView", S, W + "MonthCalendar", "Calendar", "Value");
            Add(lib + ".UpDown", S, W + "NumericUpDown", V + "UpDown", "Value");
            Add(lib + ".FlatScrollBar", S, W + "HScrollBar", "ScrollBar", "Value");
        }
        Add("MSFlexGridLib.MSFlexGrid", S, H + "FlexGrid", V + "FlexGrid", "Text");
        Add("MSHierarchicalFlexGridLib.MSHFlexGrid", S, H + "FlexGrid", V + "FlexGrid", "Text");
        Add("TabDlg.SSTab", ControlKind.TabbedContainer, W + "TabControl", "TabControl", "Tab");
        Add("RichTextLib.RichTextBox", S, W + "RichTextBox", "RichTextBox", "Text");
        Add("MSComDlg.CommonDialog", N, HelpersRoot + "CommonDialog", HelpersRoot + "CommonDialog", "Action");
    }

    /// <summary>
    /// Mapping for <paramref name="vbType"/>. INI <c>[Controls]</c> (WPF) / <c>[WinFormsControls]</c> entries
    /// <c>Lib.Ctl=Type[;container 0|1;default property]</c> win; project user controls and ActiveX fall back generically.
    /// </summary>
    public static ControlInfo Lookup(string vbType, UiTarget ui, ProjectInfo project = null)
    {
        map.TryGetValue(vbType, out var known);
        var ini = IniMap(ui == UiTarget.WinForms ? iniSectionWinFormsControls : iniSectionControls, vbType);
        if (ini != null)
        {
            var p = ini.Split(';');
            var r = Clone(known) ?? new ControlInfo { VbType = vbType, Kind = ControlKind.Standard };
            if (ui == UiTarget.WinForms) r.WinForms = p[0].Trim(); else r.Wpf = p[0].Trim();
            if (p.Length > 1 && p[1].Trim() != "")
            {
                var cont = p[1].Trim() == "1" || p[1].Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
                if (cont && !r.IsContainer) r.Kind = ControlKind.Container;
                else if (!cont && r.Kind == ControlKind.Container) r.Kind = ControlKind.Standard;
            }
            if (p.Length > 2 && p[2].Trim() != "") r.DefaultProperty = p[2].Trim();
            if (r.Kind is ControlKind.Hosted or ControlKind.Placeholder) r.Kind = ControlKind.Standard;
            return r;
        }
        if (known != null) return known;

        var dot = vbType.IndexOf('.');
        var lib = dot > 0 ? vbType.Substring(0, dot) : "";
        var cls = dot > 0 ? vbType.Substring(dot + 1) : vbType;
        if (project != null && lib.Equals(project.Name, StringComparison.OrdinalIgnoreCase) && project.UserControlNames.Contains(cls))
        {
            return new ControlInfo { VbType = vbType, Kind = ControlKind.ProjectUserControl, WinForms = "UserControls." + cls, Wpf = "usercontrols:" + cls, DefaultProperty = "" };
        }
        if (lib == "" || lib.Equals("VB", StringComparison.OrdinalIgnoreCase))
        {
            return new ControlInfo { VbType = vbType, Kind = ControlKind.Placeholder, WinForms = WinFormsNs + "Panel", Wpf = "Border", DefaultProperty = "" };
        }
        return new ControlInfo { VbType = vbType, Kind = ControlKind.Hosted, WinForms = "Ax" + lib + ".Ax" + cls, Wpf = "WindowsFormsHost", Library = lib, DefaultProperty = "" };
    }

    private static ControlInfo Clone(ControlInfo c) => c == null ? null : new ControlInfo
    {
        VbType = c.VbType, Kind = c.Kind, WinForms = c.WinForms, Wpf = c.Wpf, DefaultProperty = c.DefaultProperty, Library = c.Library,
    };

    /// <summary>.NET type for a VB6 control class used as a variable type (<c>Dim t As TextBox</c>), or null.</summary>
    public static string DataType(string vbClass, UiTarget ui)
    {
        var t = vbClass.Contains(".") ? vbClass : "VB." + vbClass;
        if (map.TryGetValue(t, out var c) && c.Kind != ControlKind.Root)
        {
            var name = c.Type(ui);
            if (ui == UiTarget.Wpf && name.StartsWith(WpfHelpersPrefix, StringComparison.Ordinal)) name = "Vb6ToCSharp.UpgradeHelpers.Wpf." + name.Substring(WpfHelpersPrefix.Length);
            return name;
        }
        var winForms = ui == UiTarget.WinForms;
        switch (vbClass.Contains(".") ? vbClass.Substring(vbClass.IndexOf('.') + 1) : vbClass)
        {
            case "Control": return winForms ? WinFormsNs + "Control" : "System.Windows.FrameworkElement";
            case "Form": case "MDIForm": return winForms ? WinFormsNs + "Form" : "System.Windows.Window";
            case "UserControl": return winForms ? WinFormsNs + "UserControl" : "System.Windows.Controls.UserControl";
            case "Node": return winForms ? WinFormsNs + "TreeNode" : "System.Windows.Controls.TreeViewItem";
            case "Nodes": return winForms ? WinFormsNs + "TreeNodeCollection" : "System.Windows.Controls.ItemCollection";
            case "ListItem": return winForms ? WinFormsNs + "ListViewItem" : "object";
            case "ListItems": return winForms ? WinFormsNs + "ListView.ListViewItemCollection" : "System.Windows.Controls.ItemCollection";
            case "ListSubItem": return winForms ? WinFormsNs + "ListViewItem.ListViewSubItem" : "object";
            case "ColumnHeader": return winForms ? WinFormsNs + "ColumnHeader" : "System.Windows.Controls.GridViewColumn";
            case "Panel": return winForms ? WinFormsNs + "ToolStripStatusLabel" : "System.Windows.Controls.Primitives.StatusBarItem";
            case "Button": return winForms ? WinFormsNs + "ToolStripItem" : "System.Windows.Controls.Button";
            case "Tab": return winForms ? WinFormsNs + "TabPage" : "System.Windows.Controls.TabItem";
            case "ListImage": return winForms ? "System.Drawing.Image" : "System.Windows.Media.ImageSource";
            case "StdPicture": case "Picture": case "IPictureDisp": return winForms ? "System.Drawing.Image" : "System.Windows.Media.ImageSource";
            case "StdFont": case "Font": return winForms ? "System.Drawing.Font" : "System.Windows.Media.FontFamily";
            case "PropertyBag": return HelpersRoot + "PropertyBag";
        }
        return null;
    }
}
