using System;
using System.Collections.Generic;
using Vb6ToCSharp.CodeGeneration;

namespace Vb6ToCSharp.ItemConversion;

/// <summary>VB6 event → WinForms / WPF event table (VBUC-aligned).</summary>
public static class EventCatalog
{
    private const string WF = "System.Windows.Forms.";
    private const string WI = "System.Windows.Input.";
    private const string WC = "System.Windows.Controls.";

    private static EventBinding B(string vb, string net, string del = "System.EventHandler", string args = "System.EventArgs", string guard = "", params ArgumentRole[] roles) =>
        new() { VbEvent = vb, NetEvent = net, Delegate = del, Args = args, Guard = guard, Roles = roles };

    private static EventBinding Special(string vb, string what, params ArgumentRole[] roles) => new() { VbEvent = vb, Special = what, Roles = roles };

    /// <summary>Events every visual control has.</summary>
    private static EventBinding Common(string vbEvent, UiTarget ui)
    {
        var mouse = new[] { ArgumentRole.Button, ArgumentRole.MouseShift, ArgumentRole.X, ArgumentRole.Y };
        var key = new[] { ArgumentRole.KeyCode, ArgumentRole.KeyShift };
        if (ui == UiTarget.WinForms)
        {
            switch (vbEvent)
            {
                case "Click": return B("Click", "Click");
                case "DblClick": return B("DblClick", "DoubleClick");
                case "GotFocus": return B("GotFocus", "Enter");
                case "LostFocus": return B("LostFocus", "Leave");
                case "KeyDown": return B("KeyDown", "KeyDown", WF + "KeyEventHandler", WF + "KeyEventArgs", "", key);
                case "KeyUp": return B("KeyUp", "KeyUp", WF + "KeyEventHandler", WF + "KeyEventArgs", "", key);
                case "KeyPress": return B("KeyPress", "KeyPress", WF + "KeyPressEventHandler", WF + "KeyPressEventArgs", "", ArgumentRole.KeyAscii);
                case "MouseDown": return B("MouseDown", "MouseDown", WF + "MouseEventHandler", WF + "MouseEventArgs", "", mouse);
                case "MouseUp": return B("MouseUp", "MouseUp", WF + "MouseEventHandler", WF + "MouseEventArgs", "", mouse);
                case "MouseMove": return B("MouseMove", "MouseMove", WF + "MouseEventHandler", WF + "MouseEventArgs", "", mouse);
                case "Validate": return B("Validate", "Validating", "System.ComponentModel.CancelEventHandler", "System.ComponentModel.CancelEventArgs", "", ArgumentRole.Cancel);
                case "Resize": return B("Resize", "Resize");
                case "Paint": return B("Paint", "Paint", WF + "PaintEventHandler", WF + "PaintEventArgs");
                case "Change": return B("Change", "TextChanged");
            }
            return null;
        }
        switch (vbEvent)
        {
            case "Click": return B("Click", "MouseLeftButtonUp", WI + "MouseButtonEventHandler", WI + "MouseButtonEventArgs");
            case "DblClick": return B("DblClick", "MouseLeftButtonDown", WI + "MouseButtonEventHandler", WI + "MouseButtonEventArgs", "if (e.ClickCount != 2) return;");
            case "GotFocus": return B("GotFocus", "GotFocus", "System.Windows.RoutedEventHandler", "System.Windows.RoutedEventArgs");
            case "LostFocus": return B("LostFocus", "LostFocus", "System.Windows.RoutedEventHandler", "System.Windows.RoutedEventArgs");
            case "KeyDown": return B("KeyDown", "KeyDown", WI + "KeyEventHandler", WI + "KeyEventArgs", "", key);
            case "KeyUp": return B("KeyUp", "KeyUp", WI + "KeyEventHandler", WI + "KeyEventArgs", "", key);
            case "KeyPress": return B("KeyPress", "PreviewTextInput", WI + "TextCompositionEventHandler", WI + "TextCompositionEventArgs", "", ArgumentRole.KeyAscii);
            case "MouseDown": return B("MouseDown", "MouseDown", WI + "MouseButtonEventHandler", WI + "MouseButtonEventArgs", "", mouse);
            case "MouseUp": return B("MouseUp", "MouseUp", WI + "MouseButtonEventHandler", WI + "MouseButtonEventArgs", "", mouse);
            case "MouseMove": return B("MouseMove", "MouseMove", WI + "MouseEventHandler", WI + "MouseEventArgs", "", mouse);
            case "Resize": return B("Resize", "SizeChanged", "System.Windows.SizeChangedEventHandler", "System.Windows.SizeChangedEventArgs");
        }
        return null;
    }

    /// <summary>Binding of <paramref name="vbEvent"/> for a control of kind/type <paramref name="info"/>, or null when unmapped.</summary>
    public static EventBinding Resolve(ControlInfo info, string vbEvent, UiTarget ui)
    {
        var t = info.VbType;
        var cls = t.Contains(".") ? t.Substring(t.IndexOf('.') + 1) : t;
        var wf = ui == UiTarget.WinForms;
        var sel = wf ? B(vbEvent, "SelectedIndexChanged") : B(vbEvent, "SelectionChanged", WC + "SelectionChangedEventHandler", WC + "SelectionChangedEventArgs");
        var textChanged = wf ? B(vbEvent, "TextChanged") : B(vbEvent, "TextChanged", WC + "TextChangedEventHandler", WC + "TextChangedEventArgs");
        var valueChanged = wf ? B(vbEvent, "ValueChanged") : B(vbEvent, "ValueChanged", "System.Windows.RoutedPropertyChangedEventHandler<double>", "System.Windows.RoutedPropertyChangedEventArgs<double>");
        var routedClick = B(vbEvent, "Click", "System.Windows.RoutedEventHandler", "System.Windows.RoutedEventArgs");

        switch (info.Kind)
        {
            case ControlKind.Root:
                return RootEvent(t, vbEvent, ui);
            case ControlKind.Menu:
                return vbEvent == "Click" ? (wf ? B("Click", "Click") : routedClick) : null;
            case ControlKind.NonVisual:
                if (t == "VB.Timer" && vbEvent == "Timer") return B("Timer", "Tick");
                return null;
            case ControlKind.Line:
            case ControlKind.Shape:
            case ControlKind.Placeholder:
                return null;
            case ControlKind.Hosted:
                // aximp wrappers expose parameterless events as EventHandler; events with arguments need the typelib (TODO)
                return B(vbEvent, vbEvent);
            case ControlKind.ProjectUserControl:
                return new EventBinding { VbEvent = vbEvent, NetEvent = "event" + vbEvent, Direct = true };
        }

        switch (cls)
        {
            case "CommandButton":
                if (!wf && vbEvent == "Click") return routedClick;
                break;
            case "CheckBox":
                if (vbEvent == "Click") return wf ? B("Click", "CheckStateChanged") : routedClick;
                break;
            case "OptionButton":
                if (vbEvent == "Click") return wf ? B("Click", "CheckedChanged", guard: "if (!((" + WF + "RadioButton)sender).Checked) return;") : B("Click", "Checked", "System.Windows.RoutedEventHandler", "System.Windows.RoutedEventArgs");
                break;
            case "ListBox":
                if (vbEvent == "Click") return sel;
                if (vbEvent == "ItemCheck" && wf) return B("ItemCheck", "ItemCheck", WF + "ItemCheckEventHandler", WF + "ItemCheckEventArgs", "", ArgumentRole.ItemIndex);
                break;
            case "ComboBox":
            case "ImageCombo":
                if (vbEvent == "Click") return sel;
                if (vbEvent == "Change") return wf ? textChanged : B("Change", "TextBoxBase.TextChanged", WC + "TextChangedEventHandler", WC + "TextChangedEventArgs");
                if (vbEvent == "DropDown") return wf ? B("DropDown", "DropDown") : B("DropDown", "DropDownOpened");
                break;
            case "TextBox":
            case "RichTextBox":
                if (vbEvent == "Change") return textChanged;
                if (vbEvent == "SelChange") return wf ? B("SelChange", "SelectionChanged") : B("SelChange", "SelectionChanged", "System.Windows.RoutedEventHandler", "System.Windows.RoutedEventArgs");
                break;
            case "Label":
                if (vbEvent == "Change") return wf ? textChanged : null;
                break;
            case "HScrollBar":
            case "VScrollBar":
            case "FlatScrollBar":
            case "Slider":
                if (vbEvent == "Change") return valueChanged;
                if (vbEvent == "Scroll")
                {
                    if (cls == "Slider") return wf ? B("Scroll", "Scroll") : valueChanged;
                    return wf ? B("Scroll", "Scroll", WF + "ScrollEventHandler", WF + "ScrollEventArgs")
                              : B("Scroll", "Scroll", WC + "Primitives.ScrollEventHandler", WC + "Primitives.ScrollEventArgs");
                }
                break;
            case "UpDown":
                if (vbEvent == "Change") return wf ? valueChanged : B("Change", "Change");
                break;
            case "DTPicker":
                if (vbEvent == "Change") return wf ? valueChanged : B("Change", "SelectedDateChanged", "System.EventHandler<" + WC + "SelectionChangedEventArgs>", WC + "SelectionChangedEventArgs");
                if (vbEvent == "CloseUp") return wf ? B("CloseUp", "CloseUp") : B("CloseUp", "CalendarClosed", "System.Windows.RoutedEventHandler", "System.Windows.RoutedEventArgs");
                if (vbEvent == "DropDown") return wf ? B("DropDown", "DropDown") : B("DropDown", "CalendarOpened", "System.Windows.RoutedEventHandler", "System.Windows.RoutedEventArgs");
                break;
            case "MonthView":
                if (vbEvent == "SelChange") return wf ? B("SelChange", "DateChanged", WF + "DateRangeEventHandler", WF + "DateRangeEventArgs") : B("SelChange", "SelectedDatesChanged", "System.EventHandler<" + WC + "SelectionChangedEventArgs>", WC + "SelectionChangedEventArgs");
                if (vbEvent == "DateClick" && wf) return B("DateClick", "DateSelected", WF + "DateRangeEventHandler", WF + "DateRangeEventArgs", "", ArgumentRole.Date);
                break;
            case "DirListBox":
            case "DriveListBox":
                if (vbEvent == "Change") return B("Change", "Change");
                if (vbEvent == "Click") return sel;
                break;
            case "FileListBox":
                if (vbEvent is "PathChange" or "PatternChange") return B(vbEvent, vbEvent);
                if (vbEvent == "Click") return sel;
                break;
            case "Timer":
                break;
            case "TreeView":
                if (wf)
                {
                    const string tv = WF + "TreeViewEventHandler", tva = WF + "TreeViewEventArgs";
                    if (vbEvent == "NodeClick") return B("NodeClick", "AfterSelect", tv, tva, "", ArgumentRole.Node);
                    if (vbEvent == "Expand") return B("Expand", "AfterExpand", tv, tva, "", ArgumentRole.Node);
                    if (vbEvent == "Collapse") return B("Collapse", "AfterCollapse", tv, tva, "", ArgumentRole.Node);
                    if (vbEvent == "NodeCheck") return B("NodeCheck", "AfterCheck", tv, tva, "", ArgumentRole.Node);
                    if (vbEvent == "AfterLabelEdit") return B("AfterLabelEdit", "AfterLabelEdit", WF + "NodeLabelEditEventHandler", WF + "NodeLabelEditEventArgs", "", ArgumentRole.CancelEdit, ArgumentRole.NewString);
                }
                else if (vbEvent == "NodeClick")
                {
                    return B("NodeClick", "SelectedItemChanged", "System.Windows.RoutedPropertyChangedEventHandler<object>", "System.Windows.RoutedPropertyChangedEventArgs<object>", "", ArgumentRole.Node);
                }
                break;
            case "ListView":
                if (wf)
                {
                    if (vbEvent == "ItemClick") return B("ItemClick", "ItemSelectionChanged", WF + "ListViewItemSelectionChangedEventHandler", WF + "ListViewItemSelectionChangedEventArgs", "if (!e.IsSelected) return;", ArgumentRole.Item);
                    if (vbEvent == "ColumnClick") return B("ColumnClick", "ColumnClick", WF + "ColumnClickEventHandler", WF + "ColumnClickEventArgs", "", ArgumentRole.Column);
                    if (vbEvent == "ItemCheck") return B("ItemCheck", "ItemChecked", WF + "ItemCheckedEventHandler", WF + "ItemCheckedEventArgs", "", ArgumentRole.Item);
                }
                else if (vbEvent == "ItemClick")
                {
                    return B("ItemClick", "SelectionChanged", WC + "SelectionChangedEventHandler", WC + "SelectionChangedEventArgs", "", ArgumentRole.Item);
                }
                break;
            case "Toolbar":
                if (vbEvent == "ButtonClick")
                {
                    return wf ? B("ButtonClick", "ItemClicked", WF + "ToolStripItemClickedEventHandler", WF + "ToolStripItemClickedEventArgs", "", ArgumentRole.ToolButton)
                              : B("ButtonClick", "ButtonBase.Click", "System.Windows.RoutedEventHandler", "System.Windows.RoutedEventArgs", "", ArgumentRole.ToolButton);
                }
                break;
            case "StatusBar":
                if (vbEvent == "PanelClick" && wf) return B("PanelClick", "ItemClicked", WF + "ToolStripItemClickedEventHandler", WF + "ToolStripItemClickedEventArgs", "", ArgumentRole.Panel);
                break;
            case "TabStrip":
            case "SSTab":
                if (vbEvent == "Click")
                {
                    var roles = cls == "SSTab" ? new[] { ArgumentRole.PreviousTab } : new ArgumentRole[0];
                    return wf ? B("Click", "SelectedIndexChanged", roles: roles)
                              : B("Click", "SelectionChanged", WC + "SelectionChangedEventHandler", WC + "SelectionChangedEventArgs", "if (e.OriginalSource != sender) return;", roles);
                }
                if (vbEvent == "BeforeClick" && wf) return B("BeforeClick", "Selecting", WF + "TabControlCancelEventHandler", WF + "TabControlCancelEventArgs", "", ArgumentRole.Cancel);
                break;
            case "MSFlexGrid":
            case "MSHFlexGrid":
                if (vbEvent is "RowColChange" or "EnterCell" or "LeaveCell" or "SelChange") return B(vbEvent, vbEvent);
                if (vbEvent == "Scroll" && wf) return B("Scroll", "Scroll", WF + "ScrollEventHandler", WF + "ScrollEventArgs");
                break;
        }
        return Common(vbEvent, ui);
    }

    private static EventBinding RootEvent(string vbType, string vbEvent, UiTarget ui)
    {
        var wf = ui == UiTarget.WinForms;
        if (vbType == "VB.UserControl" || vbType == "VB.PropertyPage")
        {
            switch (vbEvent)
            {
                case "Initialize": return Special("Initialize", "ctor");
                case "InitProperties": return Special("InitProperties", "InitProperties");
                case "ReadProperties": return Special("ReadProperties", "ReadProperties", ArgumentRole.PropBag);
                case "WriteProperties": return Special("WriteProperties", "WriteProperties", ArgumentRole.PropBag);
                case "Show": return wf ? B("Show", "VisibleChanged") : B("Show", "Loaded", "System.Windows.RoutedEventHandler", "System.Windows.RoutedEventArgs");
                case "Terminate": return wf ? B("Terminate", "Disposed") : B("Terminate", "Unloaded", "System.Windows.RoutedEventHandler", "System.Windows.RoutedEventArgs");
                case "EnterFocus": return wf ? B("EnterFocus", "Enter") : B("EnterFocus", "GotFocus", "System.Windows.RoutedEventHandler", "System.Windows.RoutedEventArgs");
                case "ExitFocus": return wf ? B("ExitFocus", "Leave") : B("ExitFocus", "LostFocus", "System.Windows.RoutedEventHandler", "System.Windows.RoutedEventArgs");
            }
            return Common(vbEvent, ui);
        }
        switch (vbEvent)
        {
            case "Initialize": return Special("Initialize", "ctor");
            case "Load": return wf ? B("Load", "Load") : B("Load", "Loaded", "System.Windows.RoutedEventHandler", "System.Windows.RoutedEventArgs");
            case "Unload":
                return wf ? B("Unload", "FormClosing", WF + "FormClosingEventHandler", WF + "FormClosingEventArgs", "", ArgumentRole.Cancel)
                          : B("Unload", "Closing", "System.ComponentModel.CancelEventHandler", "System.ComponentModel.CancelEventArgs", "", ArgumentRole.Cancel);
            case "QueryUnload":
                return wf ? B("QueryUnload", "FormClosing", WF + "FormClosingEventHandler", WF + "FormClosingEventArgs", "", ArgumentRole.Cancel, ArgumentRole.UnloadMode)
                          : B("QueryUnload", "Closing", "System.ComponentModel.CancelEventHandler", "System.ComponentModel.CancelEventArgs", "", ArgumentRole.Cancel, ArgumentRole.UnloadMode);
            case "Activate": return wf ? B("Activate", "Activated") : B("Activate", "Activated");
            case "Deactivate": return wf ? B("Deactivate", "Deactivate") : B("Deactivate", "Deactivated");
            case "Terminate": return wf ? B("Terminate", "Disposed") : B("Terminate", "Closed");
            case "GotFocus": return wf ? B("GotFocus", "GotFocus") : Common(vbEvent, ui);
            case "LostFocus": return wf ? B("LostFocus", "LostFocus") : Common(vbEvent, ui);
        }
        return Common(vbEvent, ui);
    }

    /// <summary>Order in which a root's events must be subscribed (QueryUnload before Unload on the same .NET event).</summary>
    public static readonly string[] RootEventOrder =
        { "Load", "Activate", "Deactivate", "QueryUnload", "Unload", "Resize", "Terminate", "Paint", "GotFocus", "LostFocus",
          "Click", "DblClick", "KeyDown", "KeyUp", "KeyPress", "MouseDown", "MouseUp", "MouseMove", "Show", "EnterFocus", "ExitFocus" };

    /// <summary>VB6 events looked for on every control (a handler <c>Name_Event</c> must exist in the code).</summary>
    public static readonly string[] AllEvents =
    {
        "Click", "DblClick", "GotFocus", "LostFocus", "KeyDown", "KeyUp", "KeyPress", "MouseDown", "MouseUp", "MouseMove",
        "Change", "Validate", "Resize", "Paint", "Scroll", "Timer", "ItemCheck", "DropDown", "SelChange", "CloseUp",
        "DateClick", "PathChange", "PatternChange", "NodeClick", "Expand", "Collapse", "NodeCheck", "AfterLabelEdit",
        "ItemClick", "ColumnClick", "ButtonClick", "PanelClick", "BeforeClick", "RowColChange", "EnterCell", "LeaveCell",
        "Load", "Unload", "QueryUnload", "Activate", "Deactivate", "Terminate", "Initialize", "InitProperties",
        "ReadProperties", "WriteProperties", "Show", "EnterFocus", "ExitFocus",
    };
}
