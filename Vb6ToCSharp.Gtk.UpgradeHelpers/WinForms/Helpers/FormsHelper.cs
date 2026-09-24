using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Vb6ToCSharp.UpgradeHelpers.Model;

namespace Vb6ToCSharp.UpgradeHelpers.WinForms.Helpers;

/// <summary>VB6 Form / Screen semantics over WinForms.</summary>
public static class FormsHelper
{
    public const int vbPopupMenuLeftAlign = 0;
    public const int vbPopupMenuRightButton = 2;
    public const int vbPopupMenuCenterAlign = 4;
    public const int vbPopupMenuRightAlign = 8;

#if !GTK
    /// <summary>VB6 <c>Screen.ActiveForm</c>.</summary>
    public static Form ActiveForm => Form.ActiveForm;
#endif

    /// <summary>VB6 <c>Form.Controls</c>: every nested control, flat, depth-first (pre-order).</summary>
    public static IEnumerable<Control> AllControls(Control root)
    {
        if (root == null) yield break;
        foreach (Control c in root.Controls)
        {
            yield return c;
            foreach (var d in AllControls(c)) yield return d;
        }
    }

    /// <summary>First nested control named <paramref name="name"/> (case-insensitive); null if none.</summary>
    public static Control ControlByName(Control root, string name) =>
        AllControls(root).FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// VB6 <c>PopupMenu</c>: shows <paramref name="menu"/>'s drop-down at (<paramref name="xTwips"/>,
    /// <paramref name="yTwips"/>) in <paramref name="owner"/>'s client area, a missing coordinate taken from the
    /// mouse position. <paramref name="defaultItem"/> is shown bold. Unlike VB6 the call does not block.
    /// </summary>
    public static void PopupMenu(Control owner, ToolStripMenuItem menu, int flags = 0, double? xTwips = null,
        double? yTwips = null, ToolStripItem defaultItem = null)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (menu == null) throw new ArgumentNullException(nameof(menu));
        var mouse = owner.PointToClient(Cursor.Position);
        var at = new Point(xTwips.HasValue ? Twips.ToPixelsX(xTwips.Value) : mouse.X,
            yTwips.HasValue ? Twips.ToPixelsY(yTwips.Value) : mouse.Y);
#if !GTK
        var dropDown = menu.DropDown;
        if (defaultItem != null)
        {
            var original = defaultItem.Font;
            defaultItem.Font = new Font(original, original.Style | FontStyle.Bold);
            void Restore(object s, ToolStripDropDownClosedEventArgs e)
            {
                dropDown.Closed -= Restore;
                var bold = defaultItem.Font;
                defaultItem.Font = original;
                bold.Dispose();
            }
            dropDown.Closed += Restore;
        }
        dropDown.Show(owner, at, PopupDirection(flags));
#endif
    }

    internal static ToolStripDropDownDirection PopupDirection(int flags) =>
        (flags & vbPopupMenuRightAlign) != 0 ? ToolStripDropDownDirection.BelowLeft
        : (flags & vbPopupMenuCenterAlign) != 0 ? ToolStripDropDownDirection.Default
        : ToolStripDropDownDirection.BelowRight;

    /// <summary>VB6 UnloadMode of a <see cref="FormClosingEventArgs.CloseReason"/>.</summary>
    public static int UnloadModeFrom(CloseReason reason) => reason switch
    {
        CloseReason.UserClosing => UnloadMode.vbFormControlMenu,
        CloseReason.WindowsShutDown => UnloadMode.vbAppWindows,
        CloseReason.TaskManagerClosing => UnloadMode.vbAppTaskManager,
        CloseReason.MdiFormClosing => UnloadMode.vbFormMDIForm,
        CloseReason.FormOwnerClosing => UnloadMode.vbFormOwner,
        _ => UnloadMode.vbFormCode, // None, ApplicationExitCall
    };

    /// <summary>VB6 Shift mask (1 Shift, 2 Ctrl, 4 Alt) of a modifier (or full key data) value.</summary>
    public static int ShiftFrom(Keys modifiers)
    {
        var shift = 0;
        if ((modifiers & Keys.Shift) == Keys.Shift) shift |= Vb6Keys.vbShiftMask;
        if ((modifiers & Keys.Control) == Keys.Control) shift |= Vb6Keys.vbCtrlMask;
        if ((modifiers & Keys.Alt) == Keys.Alt) shift |= Vb6Keys.vbAltMask;
        return shift;
    }

    /// <summary>VB6 Button mask (1 left, 2 right, 4 middle).</summary>
    public static int ButtonFrom(MouseButtons buttons)
    {
        var b = 0;
        if ((buttons & MouseButtons.Left) != 0) b |= Vb6Keys.vbLeftButton;
        if ((buttons & MouseButtons.Right) != 0) b |= Vb6Keys.vbRightButton;
        if ((buttons & MouseButtons.Middle) != 0) b |= Vb6Keys.vbMiddleButton;
        return b;
    }

    /// <summary>Cursor for a VB6 MousePointer (vbDefault 0 … vbSizeAll 15; vbCustom 99 / unknown → Default).</summary>
    public static Cursor CursorFromMousePointer(int mousePointer) => mousePointer switch
    {
        1 => Cursors.Arrow,
        2 => Cursors.Cross,
        3 => Cursors.IBeam,
        4 => Cursors.Arrow, // vbIconPointer: no .NET equivalent
        5 => Cursors.SizeAll,
        6 => Cursors.SizeNESW,
        7 => Cursors.SizeNS,
        8 => Cursors.SizeNWSE,
        9 => Cursors.SizeWE,
        10 => Cursors.UpArrow,
        11 => Cursors.WaitCursor,
        12 => Cursors.No,
        13 => Cursors.AppStarting,
        14 => Cursors.Help,
        15 => Cursors.SizeAll,
        _ => Cursors.Default,
    };

    /// <summary>VB6 MousePointer of a cursor (non-standard cursors → vbCustom 99).</summary>
    /// <remarks>Compares instances: <see cref="Cursors.Default"/> and <see cref="Cursors.Arrow"/> share one handle.</remarks>
    public static int MousePointerFromCursor(Cursor c)
    {
        if (c == null || ReferenceEquals(c, Cursors.Default)) return 0;
        if (ReferenceEquals(c, Cursors.Arrow)) return 1;
        if (ReferenceEquals(c, Cursors.Cross)) return 2;
        if (ReferenceEquals(c, Cursors.IBeam)) return 3;
        if (ReferenceEquals(c, Cursors.SizeNESW)) return 6;
        if (ReferenceEquals(c, Cursors.SizeNS)) return 7;
        if (ReferenceEquals(c, Cursors.SizeNWSE)) return 8;
        if (ReferenceEquals(c, Cursors.SizeWE)) return 9;
        if (ReferenceEquals(c, Cursors.UpArrow)) return 10;
        if (ReferenceEquals(c, Cursors.WaitCursor)) return 11;
        if (ReferenceEquals(c, Cursors.No)) return 12;
        if (ReferenceEquals(c, Cursors.AppStarting)) return 13;
        if (ReferenceEquals(c, Cursors.Help)) return 14;
        if (ReferenceEquals(c, Cursors.SizeAll)) return 15;
        return 99;
    }

    /// <summary>VB6 <c>Form.Show [modal], [owner]</c>: modal 1 → ShowDialog; a visible form is just activated.</summary>
    public static void ShowForm(Form form, int modal = 0, IWin32Window owner = null)
    {
        if (form == null) throw new ArgumentNullException(nameof(form));
        if (modal == 1)
        {
            if (owner == null) form.ShowDialog();
            else form.ShowDialog(owner);
            return;
        }
        if (form.Visible)
        {
            form.Activate();
            return;
        }
        if (owner == null) form.Show();
        else form.Show(owner);
    }

    /// <inheritdoc cref="ShowForm(Form, int, IWin32Window)"/>
    public static void ShowForm(Form form, int modal, Form owner) => ShowForm(form, modal, (IWin32Window)owner);
}
