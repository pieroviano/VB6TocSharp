using System.Drawing;
using System.Windows.Forms;
using Vb6ToCSharp.UpgradeHelpers.WinForms.Controls;
using Vb6ToCSharp.UpgradeHelpers.Tests.Fixtures;
using Vb6ToCSharp.UpgradeHelpers.Arrays;

namespace Vb6ToCSharp.UpgradeHelpers.Tests.WinForms.Controls;

public class ControlArrayTests
{
    private static (Panel Parent, ControlArray<Button> Array, Button B0, Button B1) Setup()
    {
        var parent = new Panel();
        var b0 = new Button { Name = "cmd_0", Text = "Zero", Location = new Point(10, 20), Size = new Size(80, 30), BackColor = Color.Red, Tag = "t0", Font = new Font("Arial", 11f) };
        var b1 = new Button { Name = "cmd_1", Text = "One" };
        parent.Controls.Add(b0);
        parent.Controls.Add(b1);
        var array = new ControlArray<Button>("cmd");
        array.SetIndex(b0, 0);
        array.SetIndex(b1, 3);
        return (parent, array, b0, b1);
    }

    [Fact]
    public void Index_Lookup_And_Bounds()
    {
        Sta.Run(() =>
        {
            var (_, a, b0, b1) = Setup();
            Assert.Equal("cmd", a.Name);
            Assert.Same(b0, a[0]);
            Assert.Same(b1, a[3]);
            Assert.Equal(0, a.GetIndex(b0));
            Assert.Equal(3, a.GetIndex(b1));
            Assert.Equal(-1, a.GetIndex(new Button()));
            Assert.Equal(0, a.LBound());
            Assert.Equal(3, a.UBound());
            Assert.Equal(2, a.Count);
            Assert.True(a.Exists(3));
            Assert.False(a.Exists(1));
            var e = Assert.Throws<ControlArrayException>(() => a[1]);
            Assert.Equal(340, e.Number);
            Assert.Equal("Control array element '1' doesn't exist", e.Message);
        });
    }

    [Fact]
    public void Empty_Array_Bounds()
    {
        var a = new ControlArray<Button>("x");
        Assert.Equal(0, a.LBound());
        Assert.Equal(-1, a.UBound());
        Assert.Throws<InvalidOperationException>(() => a.Load(1));
    }

    [Fact]
    public void Load_ClonesTemplate_Invisible_InSameParent()
    {
        Sta.Run(() =>
        {
            var (parent, a, b0, _) = Setup();
            var clone = a.Load(5);

            Assert.Same(clone, a[5]);
            Assert.Equal("cmd_5", clone.Name);
            Assert.Equal("Zero", clone.Text);
            Assert.Equal(b0.Location, clone.Location);
            Assert.Equal(b0.Size, clone.Size);
            Assert.Equal(Color.Red, clone.BackColor);
            Assert.Equal("t0", clone.Tag);
            Assert.Equal("Arial", clone.Font.Name);
            Assert.True(b0.Visible);
            Assert.False(clone.Visible);
            Assert.Same(parent, clone.Parent);
            Assert.Equal(parent.Controls.GetChildIndex(b0) - 1, parent.Controls.GetChildIndex(clone));
        });
    }

    [Fact]
    public void Load_OrderDependentProperties_AreCopied()
    {
        Sta.Run(() =>
        {
            var parent = new Panel();
            var n = new NumericUpDown { Maximum = 500, Value = 300, Minimum = 10 };
            parent.Controls.Add(n);
            var a = new ControlArray<NumericUpDown>("num");
            a.SetIndex(n, 0);
            var clone = a.Load(1);
            Assert.Equal(500, clone.Maximum);
            Assert.Equal(10, clone.Minimum);
            Assert.Equal(300, clone.Value);
        });
    }

    [Fact]
    public void Load_Existing_Throws360()
    {
        Sta.Run(() =>
        {
            var (_, a, _, _) = Setup();
            var e = Assert.Throws<ControlArrayException>(() => a.Load(3));
            Assert.Equal(360, e.Number);
            Assert.Equal("Object already loaded", e.Message);
        });
    }

    [Fact]
    public void Unload_RemovesAndDisposes_OnlyRuntimeElements()
    {
        Sta.Run(() =>
        {
            var (parent, a, _, _) = Setup();
            var clone = a.Load(7);
            a.Unload(7);
            Assert.False(a.Exists(7));
            Assert.DoesNotContain(clone, parent.Controls.Cast<Control>());
            Assert.True(clone.IsDisposed);

            var e = Assert.Throws<ControlArrayException>(() => a.Unload(0));
            Assert.Equal(362, e.Number);
            Assert.Equal("Can't unload controls created at design time", e.Message);
            Assert.Equal(340, Assert.Throws<ControlArrayException>(() => a.Unload(9)).Number);
        });
    }

    [Fact]
    public void Wire_AppliesToCurrentAndLoadedElements()
    {
        Sta.Run(() =>
        {
            var (_, a, _, _) = Setup();
            var clicked = new List<int>();
            a.Wire(b => b.Click += (s, _) => clicked.Add(a.GetIndex(s!)));
            var clone = a.Load(4);
            clone.Visible = true; // Load clones invisible (VB6); PerformClick ignores controls that cannot be selected
            a[0].PerformClick();
            clone.PerformClick();
            a[3].PerformClick();
            Assert.Equal(new[] { 0, 4, 3 }, clicked);
        });
    }

    [Fact]
    public void Wire_BeforeSetIndex_AppliesOnRegistration()
    {
        Sta.Run(() =>
        {
            var a = new ControlArray<Button>("b");
            var count = 0;
            a.Wire(_ => count++);
            a.SetIndex(new Button(), 0);
            a.SetIndex(new Button(), 1);
            Assert.Equal(2, count);
        });
    }

    [Fact]
    public void SetIndex_DuplicateIndex_Throws_ReRegisterMoves()
    {
        Sta.Run(() =>
        {
            var a = new ControlArray<Button>("b");
            var b = new Button();
            a.SetIndex(b, 0);
            a.SetIndex(b, 0); // idempotent
            Assert.Throws<ControlArrayException>(() => a.SetIndex(new Button(), 0));
            a.SetIndex(b, 2);
            Assert.False(a.Exists(0));
            Assert.Same(b, a[2]);
        });
    }

    [Fact]
    public void Enumeration_IsAscendingIndexOrder()
    {
        Sta.Run(() =>
        {
            var (_, a, b0, b1) = Setup();
            var c2 = a.Load(2);
            var c9 = a.Load(9);
            Assert.Equal(new Button[] { b0, c2, b1, c9 }, a.ToArray());
        });
    }

    [Fact]
    public void MenuArray_Load_InsertsAfterHighestElement()
    {
        Sta.Run(() =>
        {
            var strip = new MenuStrip();
            var file = new ToolStripMenuItem("File");
            strip.Items.Add(file);
            var m0 = new ToolStripMenuItem("Recent 0") { Name = "mnuRecent_0", Checked = true, ShortcutKeys = Keys.Control | Keys.D1, Tag = "r", ToolTipText = "tip" };
            var m1 = new ToolStripMenuItem("Recent 1") { Name = "mnuRecent_1" };
            var exit = new ToolStripMenuItem("Exit");
            file.DropDownItems.AddRange(new ToolStripItem[] { m0, m1, new ToolStripSeparator(), exit });
            var a = new ControlArray<ToolStripMenuItem>("mnuRecent");
            a.SetIndex(m0, 0);
            a.SetIndex(m1, 1);

            var m2 = a.Load(2);

            Assert.Equal(2, file.DropDownItems.IndexOf(m2));
            Assert.Equal("mnuRecent_2", m2.Name);
            Assert.Equal("Recent 0", m2.Text);
            Assert.True(m2.Checked);
            Assert.Equal(Keys.Control | Keys.D1, m2.ShortcutKeys);
            Assert.Equal("r", m2.Tag);
            Assert.Equal("tip", m2.ToolTipText);
            Assert.False(m2.Available);
            Assert.True(m0.Available);

            a.Unload(2);
            Assert.Equal(-1, file.DropDownItems.IndexOf(m2));
            Assert.Equal(4, file.DropDownItems.Count);
        });
    }

    [Fact]
    public void ComponentWithoutContainer_IsClonedOnly()
    {
        Sta.Run(() =>
        {
            var t0 = new System.Windows.Forms.Timer { Interval = 250 };
            var a = new ControlArray<System.Windows.Forms.Timer>("tmr");
            a.SetIndex(t0, 0);
            var t1 = a.Load(1);
            Assert.Equal(250, t1.Interval);
            a.Unload(1);
        });
    }
}
