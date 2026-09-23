using System.Windows.Forms;
using Vb6ToCSharp.UpgradeHelpers.Model;
using Vb6ToCSharp.UpgradeHelpers.WinForms.Helpers;
using Vb6ToCSharp.UpgradeHelpers.Tests.Infrastructure;

namespace Vb6ToCSharp.UpgradeHelpers.Tests.WinForms;

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
