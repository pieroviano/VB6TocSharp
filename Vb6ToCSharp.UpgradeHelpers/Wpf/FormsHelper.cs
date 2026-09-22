using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Vb6ToCSharp.UpgradeHelpers.Wpf;

/// <summary>VB6 Form / Screen semantics over WPF.</summary>
public static class FormsHelper
{
    /// <summary>VB6 <c>Screen.ActiveForm</c>: the active window of the application (null if none).</summary>
    public static Window ActiveForm =>
        Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);

    /// <summary>VB6 <c>Form.Controls</c>: named elements of the logical tree, flat, depth-first.</summary>
    public static IEnumerable<FrameworkElement> AllControls(DependencyObject root)
    {
        if (root == null) yield break;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (child is FrameworkElement { Name.Length: > 0 } fe) yield return fe;
            foreach (var d in AllControls(child)) yield return d;
        }
    }

    /// <summary>
    /// VB6 <c>PopupMenu</c>: opens a context menu at the mouse with proxies of <paramref name="menu"/>'s items
    /// (the original menu is left untouched; a proxy click raises the original item's Click).
    /// </summary>
    public static void PopupMenu(FrameworkElement owner, MenuItem menu)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        var cm = BuildContextMenu(menu);
        cm.PlacementTarget = owner;
        cm.Placement = PlacementMode.MousePoint;
        cm.IsOpen = true;
    }

    internal static ContextMenu BuildContextMenu(MenuItem menu)
    {
        if (menu == null) throw new ArgumentNullException(nameof(menu));
        var cm = new ContextMenu();
        foreach (var item in menu.Items) cm.Items.Add(Proxy(item));
        return cm;
    }

    private static object Proxy(object item)
    {
        switch (item)
        {
            case Separator:
                return new Separator();
            case MenuItem mi:
                var proxy = new MenuItem
                {
                    Header = HeaderText(mi.Header),
                    IsCheckable = mi.IsCheckable,
                    IsChecked = mi.IsChecked,
                    IsEnabled = mi.IsEnabled,
                    Visibility = mi.Visibility,
                    InputGestureText = mi.InputGestureText,
                    Command = mi.Command,
                    CommandParameter = mi.CommandParameter,
                    CommandTarget = mi.CommandTarget,
                    ToolTip = mi.ToolTip as string,
                };
                foreach (var sub in mi.Items) proxy.Items.Add(Proxy(sub));
                proxy.Click += (_, e) =>
                {
                    if (!ReferenceEquals(e.OriginalSource, proxy)) return;
                    if (mi.IsCheckable) mi.IsChecked = proxy.IsChecked;
                    mi.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, mi));
                };
                return proxy;
            default:
                return new MenuItem { Header = Convert.ToString(item) };
        }
    }

    private static object HeaderText(object header) => header switch
    {
        null or string => header,
        TextBlock tb => tb.Text,
        AccessText at => at.Text,
        _ => header.ToString(),
    };

    /// <summary>VB6 Shift mask (1 Shift, 2 Ctrl, 4 Alt).</summary>
    public static int ShiftFrom(ModifierKeys m)
    {
        var shift = 0;
        if ((m & ModifierKeys.Shift) != 0) shift |= Vb6Keys.vbShiftMask;
        if ((m & ModifierKeys.Control) != 0) shift |= Vb6Keys.vbCtrlMask;
        if ((m & ModifierKeys.Alt) != 0) shift |= Vb6Keys.vbAltMask;
        return shift;
    }

    /// <summary>
    /// VB6 Button mask: for button events the changed button (VB6 MouseDown/MouseUp), otherwise the pressed buttons
    /// (MouseMove).
    /// </summary>
    public static int ButtonFrom(MouseEventArgs e)
    {
        if (e == null) return 0;
        if (e is MouseButtonEventArgs mb) return ButtonFrom(mb.ChangedButton);
        return ButtonFrom(e.LeftButton == MouseButtonState.Pressed, e.RightButton == MouseButtonState.Pressed,
            e.MiddleButton == MouseButtonState.Pressed);
    }

    internal static int ButtonFrom(MouseButton button) => button switch
    {
        MouseButton.Left => Vb6Keys.vbLeftButton,
        MouseButton.Right => Vb6Keys.vbRightButton,
        MouseButton.Middle => Vb6Keys.vbMiddleButton,
        _ => 0,
    };

    internal static int ButtonFrom(bool left, bool right, bool middle) =>
        (left ? Vb6Keys.vbLeftButton : 0) | (right ? Vb6Keys.vbRightButton : 0) | (middle ? Vb6Keys.vbMiddleButton : 0);

    /// <summary>VB6 <c>Form.Show [modal], [owner]</c>: modal 1 → ShowDialog; a visible window is just activated.</summary>
    public static void ShowForm(Window w, int modal = 0, Window owner = null)
    {
        if (w == null) throw new ArgumentNullException(nameof(w));
        if (owner != null && !ReferenceEquals(w.Owner, owner)) w.Owner = owner;
        if (modal == 1)
        {
            w.ShowDialog();
            return;
        }
        if (w.IsVisible) w.Activate();
        else w.Show();
    }
}
