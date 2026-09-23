using System.Windows.Forms;
using Vb6ToCSharp.UpgradeHelpers.Tests.Infrastructure;
using Vb6ToCSharp.UpgradeHelpers.WinForms;

namespace Vb6ToCSharp.UpgradeHelpers.Tests.WinForms;

public class ListHelperTests
{
    [Fact]
    public void AddItem_Insert_KeepsItemDataAligned()
    {
        Sta.Run(() =>
        {
            var lb = new ListBox();
            Assert.Equal(-1, ListHelper.GetNewIndex(lb));
            Assert.Equal(0, ListHelper.AddItem(lb, "a"));
            ListHelper.SetItemData(lb, 0, 100);
            Assert.Equal(1, ListHelper.AddItem(lb, "c"));
            ListHelper.SetItemData(lb, 1, 300);
            Assert.Equal(1, ListHelper.AddItem(lb, "b", 1));
            ListHelper.SetItemData(lb, 1, 200);

            Assert.Equal(1, ListHelper.GetNewIndex(lb));
            Assert.Equal(3, ListHelper.GetListCount(lb));
            Assert.Equal(new[] { "a", "b", "c" }, Enumerable.Range(0, 3).Select(i => ListHelper.GetList(lb, i)));
            Assert.Equal(new[] { 100, 200, 300 }, Enumerable.Range(0, 3).Select(i => ListHelper.GetItemData(lb, i)));

            ListHelper.RemoveItem(lb, 0);
            Assert.Equal(new[] { 200, 300 }, Enumerable.Range(0, 2).Select(i => ListHelper.GetItemData(lb, i)));
            Assert.Equal(0, ListHelper.GetNewIndex(lb)); // "b" moved from 1 to 0
            ListHelper.RemoveItem(lb, 0);
            Assert.Equal(-1, ListHelper.GetNewIndex(lb));
        });
    }

    [Fact]
    public void Sorted_AddItem_ReturnsSortedIndex()
    {
        Sta.Run(() =>
        {
            var cb = new ComboBox { Sorted = true };
            ListHelper.AddItem(cb, "m");
            ListHelper.SetItemData(cb, 0, 13);
            Assert.Equal(0, ListHelper.AddItem(cb, "a", 1));
            Assert.Equal(0, ListHelper.GetNewIndex(cb));
            Assert.Equal(0, ListHelper.GetItemData(cb, 0));
            Assert.Equal(13, ListHelper.GetItemData(cb, 1));
        });
    }

    [Fact]
    public void List_Get_OutOfRange_IsEmpty_Set_KeepsItemData()
    {
        Sta.Run(() =>
        {
            var lb = new ListBox();
            ListHelper.AddItem(lb, "x");
            ListHelper.SetItemData(lb, 0, 5);
            Assert.Equal("", ListHelper.GetList(lb, 7));
            Assert.Equal("", ListHelper.GetList(lb, -1));
            ListHelper.SetList(lb, 0, "y");
            Assert.Equal("y", ListHelper.GetList(lb, 0));
            Assert.Equal(5, ListHelper.GetItemData(lb, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ListHelper.SetList(lb, 1, "z"));
            Assert.Throws<ArgumentOutOfRangeException>(() => ListHelper.GetItemData(lb, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ListHelper.AddItem(lb, "z", 5));
        });
    }

    [Fact]
    public void Clear_ResetsEverything()
    {
        Sta.Run(() =>
        {
            var lb = new ListBox();
            ListHelper.AddItem(lb, "a");
            ListHelper.SetItemData(lb, 0, 1);
            ListHelper.Clear(lb);
            Assert.Equal(0, ListHelper.GetListCount(lb));
            Assert.Equal(-1, ListHelper.GetNewIndex(lb));
            ListHelper.AddItem(lb, "b");
            Assert.Equal(0, ListHelper.GetItemData(lb, 0));
        });
    }

    [Fact]
    public void ItemsChangedOutsideHelper_AreRealigned()
    {
        Sta.Run(() =>
        {
            var lb = new ListBox();
            ListHelper.AddItem(lb, "a");
            lb.Items.Add("b");
            lb.Items.Add("c");
            Assert.Equal(0, ListHelper.GetItemData(lb, 2));
            ListHelper.SetItemData(lb, 2, 9);
            Assert.Equal(9, ListHelper.GetItemData(lb, 2));
        });
    }

    [Fact]
    public void Selected_ListBox_And_CheckedListBox()
    {
        Sta.Run(() =>
        {
            var lb = new ListBox { SelectionMode = SelectionMode.MultiSimple };
            ListHelper.AddItem(lb, "a");
            ListHelper.AddItem(lb, "b");
            ListHelper.SetSelected(lb, 1, true);
            Assert.True(ListHelper.GetSelected(lb, 1));
            Assert.False(ListHelper.GetSelected(lb, 0));

            var cl = new CheckedListBox();
            ListHelper.AddItem(cl, "a");
            ListHelper.SetSelected(cl, 0, true); // Style = 1 (checkbox): Selected = checked
            Assert.True(cl.GetItemChecked(0));
            Assert.True(ListHelper.GetSelected(cl, 0));
        });
    }

    [Fact]
    public void NotAList_Throws()
    {
        Sta.Run(() => Assert.Throws<ArgumentException>(() => ListHelper.AddItem(new TextBox(), "a")));
    }
}
