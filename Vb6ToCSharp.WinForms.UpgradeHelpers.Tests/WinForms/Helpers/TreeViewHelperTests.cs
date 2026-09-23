using System.Windows.Forms;
using Vb6ToCSharp.UpgradeHelpers.WinForms.Helpers;
using Vb6ToCSharp.UpgradeHelpers.Tests.Fixtures;

namespace Vb6ToCSharp.UpgradeHelpers.Tests.WinForms.Helpers;

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
            TreeViewHelper.AddNode(tv, key: "a", text: "A");
            TreeViewHelper.AddNode(tv, key: "c", text: "C");
            TreeViewHelper.AddNode(tv, "a", TreeViewHelper.tvwChild, "a1", "A1");
            TreeViewHelper.AddNode(tv, "a1", TreeViewHelper.tvwNext, "a2", "A2");
            TreeViewHelper.AddNode(tv, "a1", TreeViewHelper.tvwPrevious, "a0", "A0");
            TreeViewHelper.AddNode(tv, "a1", TreeViewHelper.tvwFirst, "af", "AF");
            TreeViewHelper.AddNode(tv, "a1", TreeViewHelper.tvwLast, "al", "AL");
            TreeViewHelper.AddNode(tv, "c", TreeViewHelper.tvwPrevious, "b", "B");
            TreeViewHelper.AddNode(tv, null, TreeViewHelper.tvwFirst, "z", "Z"); // no relative: last root node (VB6)
            Assert.Equal("A(AF,A0,A1,A2,AL),B,C,Z", Shape(tv.Nodes));
        });
    }

    [Fact]
    public void Relative_ByIndexOrNode()
    {
        Sta.Run(() =>
        {
            var tv = new TreeView();
            var a = TreeViewHelper.AddNode(tv, text: "A");
            TreeViewHelper.AddNode(tv, text: "B");
            TreeViewHelper.AddNode(tv, 2, TreeViewHelper.tvwChild, text: "B1"); // 1-based index → B
            TreeViewHelper.AddNode(tv, a, TreeViewHelper.tvwChild, text: "A1");
            Assert.Equal("A(A1),B(B1)", Shape(tv.Nodes));
        });
    }

    [Fact]
    public void GetNode_ByKey_AndOneBasedAddOrderIndex()
    {
        Sta.Run(() =>
        {
            var tv = new TreeView();
            var root = TreeViewHelper.AddNode(tv, key: "r", text: "Root");
            var child = TreeViewHelper.AddNode(tv, "r", TreeViewHelper.tvwChild, "c", "Child");
            var first = TreeViewHelper.AddNode(tv, "r", TreeViewHelper.tvwFirst, "f", "First");
            Assert.Same(child, TreeViewHelper.GetNode(tv, "c"));
            Assert.Same(root, TreeViewHelper.GetNode(tv, 1));
            Assert.Same(child, TreeViewHelper.GetNode(tv, 2));
            Assert.Same(first, TreeViewHelper.GetNode(tv, 3)); // add order, not tree order
            child.Remove();
            Assert.Same(first, TreeViewHelper.GetNode(tv, 2)); // renumbered like VB6
            Assert.Throws<KeyNotFoundException>(() => TreeViewHelper.GetNode(tv, "c"));
            Assert.Throws<ArgumentOutOfRangeException>(() => TreeViewHelper.GetNode(tv, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => TreeViewHelper.GetNode(tv, 0));
        });
    }

    [Fact]
    public void GetNode_IncludesNodesAddedWithoutHelper()
    {
        Sta.Run(() =>
        {
            var tv = new TreeView();
            TreeViewHelper.AddNode(tv, text: "A");
            var direct = tv.Nodes.Add("B");
            Assert.Same(direct, TreeViewHelper.GetNode(tv, 2));
        });
    }

    [Fact]
    public void DuplicateKey_Throws_AndImagesAreOneBased()
    {
        Sta.Run(() =>
        {
            var tv = new TreeView();
            var n = TreeViewHelper.AddNode(tv, key: "k", text: "K", image: 3, selectedImage: "open");
            Assert.Equal(2, n.ImageIndex);
            Assert.Equal("open", n.SelectedImageKey);
            Assert.Equal("k", n.Name);
            var e = Assert.Throws<ArgumentException>(() => TreeViewHelper.AddNode(tv, key: "k"));
            Assert.Contains("Key is not unique", e.Message);
        });
    }
}