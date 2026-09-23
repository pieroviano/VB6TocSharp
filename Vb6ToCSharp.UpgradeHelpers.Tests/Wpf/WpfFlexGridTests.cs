using System.Windows;
using System.Windows.Controls;
using Vb6ToCSharp.UpgradeHelpers.Wpf.Controls;
using Vb6ToCSharp.UpgradeHelpers.Tests.Infrastructure;
using static Vb6ToCSharp.UpgradeHelpers.Model.FlexAlign;

namespace Vb6ToCSharp.UpgradeHelpers.Tests.Wpf;

public class WpfFlexGridTests
{
    [Fact]
    public void Defaults_LikeMsFlexGrid()
    {
        Sta.Run(() =>
        {
            var g = new FlexGrid();
            Assert.Equal((2, 2, 1, 1, 1, 1), (g.Rows, g.Cols, g.FixedRows, g.FixedCols, g.Row, g.Col));
            Assert.Equal(DataGridHeadersVisibility.None, g.HeadersVisibility);
            Assert.False(g.CanUserAddRows);
            Assert.True(g.IsReadOnly);
            Assert.Equal(1, g.FrozenColumnCount);
            Assert.Equal(2, g.Columns.Count);
            Assert.Equal(2, g.Items.Count);
            Assert.NotNull(g.Columns[0].CellStyle);
            Assert.Null(g.Columns[1].CellStyle);
        });
    }

    [Fact]
    public void TextMatrix_Rows_Cols()
    {
        Sta.Run(() =>
        {
            var g = new FlexGrid();
            g.TextMatrix[1, 1] = "keep";
            g.Rows = 6;
            g.Cols = 4;
            Assert.Equal("keep", g.TextMatrix[1, 1]);
            Assert.Equal("", g.TextMatrix[5, 3]);
            Assert.Equal(6, g.Table.Rows.Count);
            Assert.Equal(4, g.Columns.Count);
            g.Row = 1;
            Assert.Equal("keep", g.Text);
            g.Cols = 1;
            Assert.Equal(1, g.FixedCols);
            Assert.Single(g.Columns);
            Assert.Throws<ArgumentOutOfRangeException>(() => g.TextMatrix[0, 1]);
            Assert.Throws<ArgumentOutOfRangeException>(() => g.FixedCols = 2);
        });
    }

    [Fact]
    public void AddItem_Insert_ShiftsRowsInDisplayOrder()
    {
        Sta.Run(() =>
        {
            var g = new FlexGrid { Cols = 3 };
            g.TextMatrix[1, 0] = "first";
            g.AddItem("a\tb\tc");
            g.AddItem("x\ty", 1);
            Assert.Equal(4, g.Rows);
            Assert.Equal(new[] { "", "x", "first", "a" }, Enumerable.Range(0, 4).Select(r => g.TextMatrix[r, 0]));
            Assert.Equal("", g.TextMatrix[1, 2]);
            // the DataGrid shows the same order as TextMatrix
            Assert.Equal("x", ((System.Data.DataRowView)g.Items[1])["C0"]);
            g.RemoveItem(1);
            Assert.Equal("first", g.TextMatrix[1, 0]);
            g.RemoveItem(2);
            Assert.Throws<InvalidOperationException>(() => g.RemoveItem(1));
        });
    }

    [Fact]
    public void FormatString_And_Alignment()
    {
        Sta.Run(() =>
        {
            var g = new FlexGrid();
            g.FormatString = "|<Name|>Amount;|r1|r2|r3";
            Assert.Equal((3, 4), (g.Cols, g.Rows));
            Assert.Equal("Name", g.TextMatrix[0, 1]);
            Assert.Equal("r3", g.TextMatrix[3, 0]);
            Assert.Equal(flexAlignLeftCenter, g.ColAlignment[1]);
            Assert.Equal(flexAlignRightCenter, g.ColAlignment[2]);
            Assert.Equal(flexAlignGeneral, g.ColAlignment[0]);
            Assert.True(g.ColWidth[1] > g.ColWidth[0]);
            var style = ((DataGridTextColumn)g.Columns[2]).ElementStyle;
            Assert.Contains(style.Setters.OfType<Setter>(), s => s.Property == TextBlock.TextAlignmentProperty && (TextAlignment)s.Value == TextAlignment.Right);
        });
    }

    [Fact]
    public void ColWidth_RowHeight_Twips()
    {
        Sta.Run(() =>
        {
            var g = new FlexGrid();
            g.ColWidth[1] = 1500;
            Assert.Equal(100, g.Columns[1].Width.Value);
            Assert.Equal(1500, g.ColWidth[1]);
            g.ColWidth[1] = 0;
            Assert.Equal(Visibility.Collapsed, g.Columns[1].Visibility);
            Assert.Equal(0, g.ColWidth[1]);
            g.ColWidth[1] = -1;
            Assert.Equal(960, g.ColWidth[1]);

            Assert.Equal(240, g.RowHeight[1]);
            g.RowHeight[1] = 450;
            Assert.Equal(450, g.RowHeight[1]);
            g.AddItem("new", 0); // row keys shift with inserted rows
            Assert.Equal(450, g.RowHeight[2]);
            Assert.Equal(240, g.RowHeight[1]);
        });
    }

    [Fact]
    public void Events_Selection_CellFormats()
    {
        Sta.Run(() =>
        {
            var g = new FlexGrid { Rows = 4, Cols = 4 };
            var log = new List<string>();
            g.LeaveCell += (_, _) => log.Add("leave");
            g.EnterCell += (_, _) => log.Add("enter");
            g.RowColChange += (_, _) => log.Add("rowcol");
            g.SelChange += (_, _) => log.Add("sel");
            g.Col = 2;
            Assert.Equal(new[] { "leave", "enter", "rowcol", "sel" }, log);
            g.RowSel = 3;
            Assert.Equal((1, 2, 3, 2), (g.Row, g.Col, g.RowSel, g.ColSel));

            g.CellBackColor = 0x0000FF;
            g.CellAlignment = flexAlignCenterTop;
            Assert.Equal(0x0000FF, g.CellBackColor);
            Assert.Equal(flexAlignCenterTop, g.CellAlignment);
            g.Col = 3;
            Assert.Equal(0, g.CellBackColor);
            Assert.Equal(flexAlignGeneral, g.CellAlignment);
            g.Col = 2;
            g.Clear();
            Assert.Equal(0, g.CellBackColor);
        });
    }

    [Fact]
    public void AlignmentMapping()
    {
        Assert.Equal((HorizontalAlignment.Left, VerticalAlignment.Top, TextAlignment.Left), FlexGrid.ToWpf(flexAlignLeftTop));
        Assert.Equal((HorizontalAlignment.Center, VerticalAlignment.Center, TextAlignment.Center), FlexGrid.ToWpf(flexAlignCenterCenter));
        Assert.Equal((HorizontalAlignment.Right, VerticalAlignment.Bottom, TextAlignment.Right), FlexGrid.ToWpf(flexAlignRightBottom));
        Assert.Equal((HorizontalAlignment.Left, VerticalAlignment.Center, TextAlignment.Left), FlexGrid.ToWpf(flexAlignGeneral));
    }
}
