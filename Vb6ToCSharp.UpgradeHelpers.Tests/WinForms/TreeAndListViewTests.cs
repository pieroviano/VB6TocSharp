using System.Windows.Forms;
using Vb6ToCSharp.UpgradeHelpers.WinForms;
using static Vb6ToCSharp.UpgradeHelpers.WinForms.TreeViewHelper;

namespace Vb6ToCSharp.UpgradeHelpers.Tests.WinForms;

public class TreeViewHelperTests
{
    private static string Shape(TreeNodeCollection nodes) =>
        string.Join(",", nodes.Cast<TreeNode>().Select(n => n.Nodes.Count == 0 ? n.Text : $"{n.Text}({Shape(n.Nodes)})"));

    [Fact]
    public void Relationships_PlaceNodes()
    {
        Sta.Run(() =>
        {
            var tv = new TreeView();
            AddNode(tv, key: "a", text: "A");
            AddNode(tv, key: "c", text: "C");
            AddNode(tv, "a", tvwChild, "a1", "A1");
            AddNode(tv, "a1", tvwNext, "a2", "A2");
            AddNode(tv, "a1", tvwPrevious, "a0", "A0");
            AddNode(tv, "a1", tvwFirst, "af", "AF");
            AddNode(tv, "a1", tvwLast, "al", "AL");
            AddNode(tv, "c", tvwPrevious, "b", "B");
            AddNode(tv, null, tvwFirst, "z", "Z"); // no relative: last root node (VB6)
            Assert.Equal("A(AF,A0,A1,A2,AL),B,C,Z", Shape(tv.Nodes));
        });
    }

    [Fact]
    public void Relative_ByIndexOrNode()
    {
        Sta.Run(() =>
        {
            var tv = new TreeView();
            var a = AddNode(tv, text: "A");
            AddNode(tv, text: "B");
            AddNode(tv, 2, tvwChild, text: "B1"); // 1-based index → B
            AddNode(tv, a, tvwChild, text: "A1");
            Assert.Equal("A(A1),B(B1)", Shape(tv.Nodes));
        });
    }

    [Fact]
    public void GetNode_ByKey_AndOneBasedAddOrderIndex()
    {
        Sta.Run(() =>
        {
            var tv = new TreeView();
            var root = AddNode(tv, key: "r", text: "Root");
            var child = AddNode(tv, "r", tvwChild, "c", "Child");
            var first = AddNode(tv, "r", tvwFirst, "f", "First");
            Assert.Same(child, GetNode(tv, "c"));
            Assert.Same(root, GetNode(tv, 1));
            Assert.Same(child, GetNode(tv, 2));
            Assert.Same(first, GetNode(tv, 3)); // add order, not tree order
            child.Remove();
            Assert.Same(first, GetNode(tv, 2)); // renumbered like VB6
            Assert.Throws<KeyNotFoundException>(() => GetNode(tv, "c"));
            Assert.Throws<ArgumentOutOfRangeException>(() => GetNode(tv, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => GetNode(tv, 0));
        });
    }

    [Fact]
    public void GetNode_IncludesNodesAddedWithoutHelper()
    {
        Sta.Run(() =>
        {
            var tv = new TreeView();
            AddNode(tv, text: "A");
            var direct = tv.Nodes.Add("B");
            Assert.Same(direct, GetNode(tv, 2));
        });
    }

    [Fact]
    public void DuplicateKey_Throws_AndImagesAreOneBased()
    {
        Sta.Run(() =>
        {
            var tv = new TreeView();
            var n = AddNode(tv, key: "k", text: "K", image: 3, selectedImage: "open");
            Assert.Equal(2, n.ImageIndex);
            Assert.Equal("open", n.SelectedImageKey);
            Assert.Equal("k", n.Name);
            var e = Assert.Throws<ArgumentException>(() => AddNode(tv, key: "k"));
            Assert.Contains("Key is not unique", e.Message);
        });
    }
}

public class ListViewHelperTests
{
    [Fact]
    public void AddItem_OneBasedInsert_AndGetItem()
    {
        Sta.Run(() =>
        {
            var lv = new ListView();
            var a = ListViewHelper.AddItem(lv, key: "a", text: "A", icon: 2);
            var c = ListViewHelper.AddItem(lv, text: "C", smallIcon: "small");
            var b = ListViewHelper.AddItem(lv, 2, "b", "B");
            Assert.Equal(new[] { "A", "B", "C" }, lv.Items.Cast<ListViewItem>().Select(i => i.Text));
            Assert.Equal(1, a.ImageIndex);
            Assert.Equal("small", c.ImageKey);
            Assert.Same(b, ListViewHelper.GetItem(lv, 2));
            Assert.Same(a, ListViewHelper.GetItem(lv, "a"));
            Assert.Same(c, ListViewHelper.GetItem(lv, 3L));
            Assert.Throws<ArgumentOutOfRangeException>(() => ListViewHelper.GetItem(lv, 0));
            Assert.Throws<KeyNotFoundException>(() => ListViewHelper.GetItem(lv, "zz"));
            Assert.Throws<ArgumentException>(() => ListViewHelper.AddItem(lv, key: "a"));
            Assert.Throws<ArgumentOutOfRangeException>(() => ListViewHelper.AddItem(lv, 9));
        });
    }

    [Fact]
    public void AddColumn_WidthTwips_Alignment_Index()
    {
        Sta.Run(() =>
        {
            var lv = new ListView { View = View.Details };
            var name = ListViewHelper.AddColumn(lv, key: "name", text: "Name", widthTwips: Twips.FromPixelsX(120));
            var size = ListViewHelper.AddColumn(lv, text: "Size", alignment: ListViewHelper.lvwColumnRight);
            var mid = ListViewHelper.AddColumn(lv, 2, "type", "Type", alignment: ListViewHelper.lvwColumnCenter);
            Assert.Equal(120, name.Width);
            Assert.Equal("name", name.Name);
            Assert.Equal(HorizontalAlignment.Right, size.TextAlign);
            Assert.Equal(HorizontalAlignment.Center, mid.TextAlign);
            Assert.Equal(new[] { "Name", "Type", "Size" }, lv.Columns.Cast<ColumnHeader>().Select(h => h.Text));
        });
    }
}
