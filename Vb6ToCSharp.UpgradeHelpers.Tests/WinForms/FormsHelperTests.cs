using System.Drawing;
using System.Windows.Forms;
using Vb6ToCSharp.UpgradeHelpers.WinForms;

namespace Vb6ToCSharp.UpgradeHelpers.Tests.WinForms;

public class FormsHelperTests
{
    [Theory]
    [InlineData(CloseReason.None, UnloadMode.vbFormCode)]
    [InlineData(CloseReason.UserClosing, UnloadMode.vbFormControlMenu)]
    [InlineData(CloseReason.WindowsShutDown, UnloadMode.vbAppWindows)]
    [InlineData(CloseReason.TaskManagerClosing, UnloadMode.vbAppTaskManager)]
    [InlineData(CloseReason.MdiFormClosing, UnloadMode.vbFormMDIForm)]
    [InlineData(CloseReason.FormOwnerClosing, UnloadMode.vbFormOwner)]
    [InlineData(CloseReason.ApplicationExitCall, UnloadMode.vbFormCode)]
    public void UnloadModeFrom_CloseReason(CloseReason reason, int expected) =>
        Assert.Equal(expected, FormsHelper.UnloadModeFrom(reason));

    [Theory]
    [InlineData(Keys.None, 0)]
    [InlineData(Keys.Shift, 1)]
    [InlineData(Keys.Control, 2)]
    [InlineData(Keys.Alt, 4)]
    [InlineData(Keys.Shift | Keys.Control | Keys.Alt, 7)]
    [InlineData(Keys.A | Keys.Control, 2)]
    [InlineData(Keys.ShiftKey, 0)]
    public void ShiftFrom_Modifiers(Keys keys, int expected) => Assert.Equal(expected, FormsHelper.ShiftFrom(keys));

    [Theory]
    [InlineData(MouseButtons.None, 0)]
    [InlineData(MouseButtons.Left, 1)]
    [InlineData(MouseButtons.Right, 2)]
    [InlineData(MouseButtons.Middle, 4)]
    [InlineData(MouseButtons.Left | MouseButtons.Middle, 5)]
    [InlineData(MouseButtons.XButton1, 0)]
    public void ButtonFrom_Buttons(MouseButtons buttons, int expected) => Assert.Equal(expected, FormsHelper.ButtonFrom(buttons));

    [Fact]
    public void Cursor_Mapping_RoundTrips()
    {
        var expected = new Dictionary<int, Cursor>
        {
            [0] = Cursors.Default, [1] = Cursors.Arrow, [2] = Cursors.Cross, [3] = Cursors.IBeam,
            [6] = Cursors.SizeNESW, [7] = Cursors.SizeNS, [8] = Cursors.SizeNWSE, [9] = Cursors.SizeWE,
            [10] = Cursors.UpArrow, [11] = Cursors.WaitCursor, [12] = Cursors.No, [13] = Cursors.AppStarting,
            [14] = Cursors.Help, [15] = Cursors.SizeAll,
        };
        foreach (var kv in expected)
        {
            Assert.Same(kv.Value, FormsHelper.CursorFromMousePointer(kv.Key));
            Assert.Equal(kv.Key, FormsHelper.MousePointerFromCursor(kv.Value));
        }
        Assert.Same(Cursors.SizeAll, FormsHelper.CursorFromMousePointer(5));
        Assert.Same(Cursors.Default, FormsHelper.CursorFromMousePointer(99));
        Assert.Equal(0, FormsHelper.MousePointerFromCursor(null!));
        Assert.Equal(99, FormsHelper.MousePointerFromCursor(Cursors.Hand));
    }

    [Fact]
    public void AllControls_IsFlatDepthFirst_And_ControlByName()
    {
        Sta.Run(() =>
        {
            var form = new Form();
            var frame = new GroupBox { Name = "fraOptions" };
            var opt1 = new RadioButton { Name = "optA" };
            var inner = new Panel { Name = "pnlInner" };
            var deep = new TextBox { Name = "txtDeep" };
            var last = new Button { Name = "cmdOK" };
            inner.Controls.Add(deep);
            frame.Controls.Add(opt1);
            frame.Controls.Add(inner);
            form.Controls.Add(frame);
            form.Controls.Add(last);

            Assert.Equal(new[] { "fraOptions", "optA", "pnlInner", "txtDeep", "cmdOK" },
                FormsHelper.AllControls(form).Select(c => c.Name));
            Assert.Same(deep, FormsHelper.ControlByName(form, "TXTDEEP"));
            Assert.Null(FormsHelper.ControlByName(form, "missing"));
            Assert.Empty(FormsHelper.AllControls(null!));
        });
    }

    [Theory]
    [InlineData(0, ToolStripDropDownDirection.BelowRight)]
    [InlineData(FormsHelper.vbPopupMenuRightAlign, ToolStripDropDownDirection.BelowLeft)]
    [InlineData(FormsHelper.vbPopupMenuCenterAlign | FormsHelper.vbPopupMenuRightButton, ToolStripDropDownDirection.Default)]
    public void PopupDirection_FromFlags(int flags, ToolStripDropDownDirection expected) =>
        Assert.Equal(expected, FormsHelper.PopupDirection(flags));

    [Fact]
    public void ShowForm_Modeless_ShowsThenActivates()
    {
        Sta.Run(() =>
        {
            using var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-2000, -2000), Size = new Size(10, 10) };
            FormsHelper.ShowForm(form);
            Assert.True(form.Visible);
            FormsHelper.ShowForm(form); // already visible: no exception, stays visible
            Assert.True(form.Visible);
            form.Close();
        });
    }
}
