using System.Drawing;
using System.Windows.Forms;
using Vb6ToCSharp.UpgradeHelpers.Tests.Infrastructure;
using Vb6ToCSharp.UpgradeHelpers.WinForms;
using static Vb6ToCSharp.UpgradeHelpers.FlexAlign;

namespace Vb6ToCSharp.UpgradeHelpers.Tests.WinForms;

public class FlexGridTests
{
    private static int RoundTrip(int twips) => (int)Math.Round(Twips.FromPixelsX(Twips.ToPixelsX(twips)));

    [Fact]
    public void Defaults_LikeMsFlexGrid()
    {
        Sta.Run(() =>
        {
            var g = new FlexGrid();
            Assert.Equal((2, 2, 1, 1, 1, 1), (g.Rows, g.Cols, g.FixedRows, g.FixedCols, g.Row, g.Col));
            Assert.False(g.RowHeadersVisible);
            Assert.False(g.ColumnHeadersVisible);
            Assert.False(g.AllowUserToAddRows);
            Assert.True(g.ReadOnly);
            Assert.Equal(DataGridViewSelectionMode.CellSelect, g.SelectionMode);
            var dgv = (DataGridView)g;
            Assert.True(dgv.Rows[0].Frozen);
            Assert.False(dgv.Rows[1].Frozen);
            Assert.True(dgv.Columns[0].Frozen);
            Assert.False(dgv.Columns[1].Frozen);
            Assert.Equal(SystemColors.Control, dgv.Rows[0].DefaultCellStyle.BackColor);
        });
    }

    [Fact]
    public void TextMatrix_And_Text()
    {
        Sta.Run(() =>
        {
            var g = new FlexGrid { Rows = 3, Cols = 3 };
            g.TextMatrix[2, 1] = "x";
            Assert.Equal("x", g.TextMatrix[2, 1]);
            Assert.Equal("", g.TextMatrix[0, 0]);
            g.Row = 2;
            Assert.Equal("x", g.Text);
            g.Text = "y";
            Assert.Equal("y", g.TextMatrix[2, 1]);
            Assert.Throws<ArgumentOutOfRangeException>(() => g.TextMatrix[3, 0]);
            Assert.Throws<ArgumentOutOfRangeException>(() => g.TextMatrix[0, 3] = "z");
        });
    }

    [Fact]
    public void Rows_Cols_Resize_KeepContent_AndClampFixed()
    {
        Sta.Run(() =>
        {
            var g = new FlexGrid();
            g.TextMatrix[1, 1] = "keep";
            g.Rows = 10;
            g.Cols = 5;
            Assert.Equal("keep", g.TextMatrix[1, 1]);
            Assert.Equal(10, ((DataGridView)g).Rows.Count);
            g.FixedRows = 2;
            g.FixedCols = 2;
            Assert.True(((DataGridView)g).Rows[1].Frozen);
            Assert.True(((DataGridView)g).Columns[1].Frozen);
            g.FixedRows = 0;
            Assert.False(((DataGridView)g).Rows[0].Frozen);
            Assert.Throws<ArgumentOutOfRangeException>(() => g.FixedRows = 11);
            g.Rows = 1;
            g.Cols = 1;
            Assert.Equal((1, 1, 0), (g.Rows, g.Cols, g.Row));
            Assert.Equal(1, g.FixedCols);
        });
    }

    [Fact]
    public void AddItem_TabSeparated_AppendAndInsert()
    {
        Sta.Run(() =>
        {
            var g = new FlexGrid { Cols = 3 };
            g.AddItem("a\tb\tc");
            g.AddItem("x\ty", 1);
            Assert.Equal(4, g.Rows);
            Assert.Equal(new[] { "x", "y", "" }, new[] { g.TextMatrix[1, 0], g.TextMatrix[1, 1], g.TextMatrix[1, 2] });
            Assert.Equal("c", g.TextMatrix[3, 2]);
            Assert.Throws<ArgumentOutOfRangeException>(() => g.AddItem("q", 9));
        });
    }

    [Fact]
    public void RemoveItem_CannotRemoveLastNonFixedRow()
    {
        Sta.Run(() =>
        {
            var g = new FlexGrid();
            g.AddItem("r2");
            g.RemoveItem(1);
            Assert.Equal(2, g.Rows);
            Assert.Equal("r2", g.TextMatrix[1, 0]);
            Assert.Throws<InvalidOperationException>(() => g.RemoveItem(1));
        });
    }

    [Fact]
    public void Clear_EmptiesTextAndFormatting()
    {
        Sta.Run(() =>
        {
            var g = new FlexGrid();
            g.TextMatrix[0, 1] = "h";
            g.TextMatrix[1, 1] = "v";
            g.CellBackColor = 0x0000FF;
            g.Clear();
            Assert.Equal("", g.TextMatrix[0, 1]);
            Assert.Equal("", g.TextMatrix[1, 1]);
            Assert.Equal(0, g.CellBackColor);
            Assert.Equal(2, g.Rows);
        });
    }

    [Fact]
    public void FormatString_SetsHeadersAlignmentWidthsAndRowHeaders()
    {
        Sta.Run(() =>
        {
            var g = new FlexGrid();
            g.FormatString = "|<Col1|>Col2|^Col3;|Row1|Row2";
            Assert.Equal(4, g.Cols);
            Assert.Equal(3, g.Rows);
            Assert.Equal(new[] { "", "Col1", "Col2", "Col3" }, Enumerable.Range(0, 4).Select(c => g.TextMatrix[0, c]));
            Assert.Equal("Row1", g.TextMatrix[1, 0]);
            Assert.Equal("Row2", g.TextMatrix[2, 0]);
            Assert.Equal(flexAlignGeneral, g.ColAlignment[0]);
            Assert.Equal(flexAlignLeftCenter, g.ColAlignment[1]);
            Assert.Equal(flexAlignRightCenter, g.ColAlignment[2]);
            Assert.Equal(flexAlignCenterCenter, g.ColAlignment[3]);
            Assert.Equal(DataGridViewContentAlignment.MiddleRight, ((DataGridView)g).Columns[2].DefaultCellStyle.Alignment);
            Assert.True(g.ColWidth[1] > g.ColWidth[0]); // sized to the header text
            Assert.Equal("|<Col1|>Col2|^Col3;|Row1|Row2", g.FormatString);
        });
    }

    [Fact]
    public void ColWidth_RowHeight_AreTwips()
    {
        Sta.Run(() =>
        {
            var g = new FlexGrid();
            g.ColWidth[1] = 1500;
            Assert.Equal(Twips.ToPixelsX(1500), ((DataGridView)g).Columns[1].Width);
            Assert.Equal(RoundTrip(1500), g.ColWidth[1]);
            g.ColWidth[1] = 0;
            Assert.Equal(0, g.ColWidth[1]);
            Assert.False(((DataGridView)g).Columns[1].Visible);
            g.ColWidth[1] = -1;
            Assert.Equal(RoundTrip(960), g.ColWidth[1]);

            g.RowHeight[1] = 600;
            Assert.Equal(Twips.ToPixelsY(600), ((DataGridView)g).Rows[1].Height);
            g.RowHeight[1] = 30; // below DataGridView's default minimum
            Assert.Equal(Math.Max(2, Twips.ToPixelsY(30)), ((DataGridView)g).Rows[1].Height);
        });
    }

    [Fact]
    public void RowCol_Events_InMsFlexGridOrder()
    {
        Sta.Run(() =>
        {
            var g = new FlexGrid { Rows = 5, Cols = 5 };
            var log = new List<string>();
            g.LeaveCell += (_, _) => log.Add($"leave {g.Row},{g.Col}");
            g.EnterCell += (_, _) => log.Add($"enter {g.Row},{g.Col}");
            g.RowColChange += (_, _) => log.Add("rowcol");
            g.SelChange += (_, _) => log.Add("sel");
            g.Row = 3;
            Assert.Equal(new[] { "leave 1,1", "enter 3,1", "rowcol", "sel" }, log);
            log.Clear();
            g.Row = 3;
            Assert.Empty(log);
            g.RowSel = 4;
            g.ColSel = 2;
            Assert.Equal(new[] { "sel", "sel" }, log);
            Assert.Equal((3, 1, 4, 2), (g.Row, g.Col, g.RowSel, g.ColSel));
            Assert.Equal(4, ((DataGridView)g).SelectedCells.Count);
            g.Col = 2; // moving the current cell resets the selection
            Assert.Equal((3, 2), (g.RowSel, g.ColSel));
            Assert.Throws<ArgumentOutOfRangeException>(() => g.Row = 5);
        });
    }

    [Fact]
    public void CellFormatting_OnCurrentCell()
    {
        Sta.Run(() =>
        {
            var g = new FlexGrid();
            Assert.Equal(0, g.CellBackColor);
            g.CellBackColor = 0x00FF00;
            g.CellForeColor = unchecked((int)0x80000008);
            Assert.Equal(0x00FF00, g.CellBackColor);
            Assert.Equal(unchecked((int)0x80000008), g.CellForeColor);
            Assert.Equal(Color.FromArgb(0, 255, 0).ToArgb(), ((DataGridView)g).Rows[1].Cells[1].Style.BackColor.ToArgb());
            g.CellBackColor = 0;
            Assert.Equal(0, g.CellBackColor);

            Assert.Equal(flexAlignGeneral, g.CellAlignment);
            g.CellAlignment = flexAlignRightBottom;
            Assert.Equal(flexAlignRightBottom, g.CellAlignment);
            Assert.Equal(DataGridViewContentAlignment.BottomRight, ((DataGridView)g).Rows[1].Cells[1].Style.Alignment);
        });
    }

    [Fact]
    public void Alignment_Mapping_RoundTrips()
    {
        for (var a = 0; a <= 8; a++) Assert.Equal(a, FlexGrid.FromContent(FlexGrid.ToContent(a)));
        Assert.Equal(DataGridViewContentAlignment.TopLeft, FlexGrid.ToContent(flexAlignLeftTop));
        Assert.Equal(DataGridViewContentAlignment.MiddleCenter, FlexGrid.ToContent(flexAlignCenterCenter));
        Assert.Equal(DataGridViewContentAlignment.MiddleLeft, FlexGrid.ToContent(flexAlignGeneral));
    }

    [Fact]
    public void TopRow_LeftCol_Redraw_WithoutHandle()
    {
        Sta.Run(() =>
        {
            var g = new FlexGrid { Rows = 20 };
            Assert.Equal(1, g.TopRow);
            Assert.Equal(1, g.LeftCol);
            g.Redraw = false;
            Assert.False(g.Redraw);
            g.Redraw = true;
            Assert.Equal(-1, g.MouseRow);
            Assert.Equal(-1, g.MouseCol);
        });
    }
}
