using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Vb6ToCSharp.UpgradeHelpers.Arrays;
using Vb6ToCSharp.UpgradeHelpers.Model;
namespace Vb6ToCSharp.UpgradeHelpers.WinForms.Controls;

/// <summary>
/// MSFlexGrid API over a <see cref="DataGridView"/>. Every grid row/column is a DataGridView row/column;
/// fixed rows/columns are frozen and painted with the fixed colors (no DataGridView headers). Sizes are twips,
/// colors OLE, alignments <see cref="FlexAlign"/> values.
/// </summary>
[DesignerCategory("Code")]
public class FlexGrid : DataGridView
{
    private const int DefaultColWidthTwips = 960;

    private readonly Dictionary<DataGridViewColumn, int> _colAlign = new();
    private int _fixedRows = 1, _fixedCols = 1, _appliedFixedRows;
    private int _row, _col, _rowSel, _colSel;
    private int _mouseRow = -1, _mouseCol = -1;
    private bool _redraw = true, _syncing;
    private string _formatString = "";
    private Color _backColorFixed = SystemColors.Control, _foreColorFixed = SystemColors.ControlText;

    public FlexGrid()
    {
        AllowUserToAddRows = false;
        AllowUserToDeleteRows = false;
        AllowUserToOrderColumns = false;
        AllowUserToResizeRows = false;
        ReadOnly = true;
        RowHeadersVisible = false;
        ColumnHeadersVisible = false;
        SelectionMode = DataGridViewSelectionMode.CellSelect;
        MultiSelect = true;
        TextMatrix = new MatrixProperty<string>(GetText, SetText);
        ColWidth = new IndexedProperty<int>(GetColWidth, SetColWidth);
        RowHeight = new IndexedProperty<int>(GetRowHeight, SetRowHeight);
        ColAlignment = new IndexedProperty<int>(GetColAlignment, SetColAlignment);
        Cols = 2;
        Rows = 2;
        _row = _rowSel = 1;
        _col = _colSel = 1;
        SyncSelection();
    }

    public event EventHandler RowColChange;
    public event EventHandler EnterCell;
    public event EventHandler LeaveCell;
    public event EventHandler SelChange;

    /// <summary>Total rows, fixed included.</summary>
    [DefaultValue(2)]
    public new int Rows
    {
        get => base.Rows.Count;
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Invalid Row value");
            if (value > 0 && ColumnCount == 0) ColumnCount = 1;
            RowCount = value;
            if (_fixedRows > value) _fixedRows = value;
            AfterStructureChange();
        }
    }

    /// <summary>Total columns, fixed included.</summary>
    [DefaultValue(2)]
    public int Cols
    {
        get => ColumnCount;
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Invalid Col value");
            var rows = base.Rows.Count;
            var old = ColumnCount;
            ColumnCount = value;
            for (var i = old; i < value; i++)
            {
                Columns[i].SortMode = DataGridViewColumnSortMode.NotSortable;
                Columns[i].Width = Math.Max(2, Twips.ToPixelsX(DefaultColWidthTwips));
            }
            foreach (var gone in _colAlign.Keys.Where(c => c.DataGridView != this).ToList()) _colAlign.Remove(gone);
            if (value > 0 && base.Rows.Count < rows) RowCount = rows; // ColumnCount = 0 drops the rows
            if (_fixedCols > value) _fixedCols = value;
            AfterStructureChange();
        }
    }

    [DefaultValue(1)]
    public int FixedRows
    {
        get => _fixedRows;
        set
        {
            if (value < 0 || value > Rows) throw new ArgumentOutOfRangeException(nameof(value), "Invalid Row value");
            _fixedRows = value;
            AfterStructureChange();
        }
    }

    [DefaultValue(1)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int FixedCols
    {
        get => _fixedCols;
        set
        {
            if (value < 0 || value > Cols) throw new ArgumentOutOfRangeException(nameof(value), "Invalid Col value");
            _fixedCols = value;
            AfterStructureChange();
        }
    }

    /// <summary>OLE color of the fixed cells.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int BackColorFixed
    {
        get => Vb6Color.FromColor(_backColorFixed);
        set { _backColorFixed = Vb6Color.ToColor(value); ApplyFixed(); }
    }

    /// <summary>OLE text color of the fixed cells.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int ForeColorFixed
    {
        get => Vb6Color.FromColor(_foreColorFixed);
        set { _foreColorFixed = Vb6Color.ToColor(value); ApplyFixed(); }
    }

    /// <summary>Current row; setting it also resets <see cref="RowSel"/>.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Row
    {
        get => _row;
        set => SetCurrent(value, _col);
    }

    /// <summary>Current column; setting it also resets <see cref="ColSel"/>.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Col
    {
        get => _col;
        set => SetCurrent(_row, value);
    }

    /// <summary>Row of the selection corner opposite to the current cell.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int RowSel
    {
        get => _rowSel;
        set
        {
            CheckRow(value);
            if (value == _rowSel) return;
            _rowSel = value;
            SyncSelection();
            SelChange?.Invoke(this, EventArgs.Empty);
        }
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int ColSel
    {
        get => _colSel;
        set
        {
            CheckCol(value);
            if (value == _colSel) return;
            _colSel = value;
            SyncSelection();
            SelChange?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Text of the current cell.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new string Text
    {
        get => HasCell ? GetText(_row, _col) : "";
        set => SetText(_row, _col, value);
    }

    /// <summary><c>TextMatrix(row, col)</c>.</summary>
    [Browsable(false)]
    public MatrixProperty<string> TextMatrix { get; }

    /// <summary><c>ColWidth(col)</c> in twips (0 hides, -1 default).</summary>
    [Browsable(false)]
    public IndexedProperty<int> ColWidth { get; }

    /// <summary><c>RowHeight(row)</c> in twips (0 hides, -1 default).</summary>
    [Browsable(false)]
    public IndexedProperty<int> RowHeight { get; }

    /// <summary><c>ColAlignment(col)</c> (<see cref="FlexAlign"/>).</summary>
    [Browsable(false)]
    public IndexedProperty<int> ColAlignment { get; }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int TopRow
    {
        get
        {
            var i = FirstDisplayedScrollingRowIndex;
            return i < 0 ? _fixedRows : i;
        }
        set
        {
            CheckRow(value);
            try { FirstDisplayedScrollingRowIndex = value; }
            catch (InvalidOperationException) { /* fixed/hidden row or no handle */ }
        }
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int LeftCol
    {
        get
        {
            var i = FirstDisplayedScrollingColumnIndex;
            return i < 0 ? _fixedCols : i;
        }
        set
        {
            CheckCol(value);
            try { FirstDisplayedScrollingColumnIndex = value; }
            catch (InvalidOperationException) { /* fixed/hidden column or no handle */ }
        }
    }

    /// <summary>
    /// False holds back layout and repainting until set back to true, which is what a VB6 bulk fill
    /// used it for. It suspends layout rather than the window's painting, so a long fill can still
    /// show intermediate state; the repaint at the end is the same.
    /// </summary>
    [DefaultValue(true)]
    public bool Redraw
    {
        get => _redraw;
        set
        {
            if (_redraw == value) return;
            _redraw = value;
            if (value)
            {
                ResumeLayout(true);
                Invalidate(true);
            }
            else
            {
                SuspendLayout();
            }
        }
    }

    [Browsable(false)]
    public int MouseRow => _mouseRow;

    [Browsable(false)]
    public int MouseCol => _mouseCol;

    /// <summary>
    /// MSFlexGrid FormatString: <c>|</c>-separated column headers (leading <c>&lt;</c> <c>^</c> <c>&gt;</c> =
    /// alignment) written in row 0 and sizing the columns, then optionally <c>;</c> and row headers for column 0.
    /// </summary>
    [DefaultValue("")]
    public string FormatString
    {
        get => _formatString;
        set
        {
            _formatString = value ?? "";
            var f = FlexFormat.Parse(_formatString);
            if (f.ColumnTexts.Count > Cols) Cols = f.ColumnTexts.Count;
            if (f.RowTexts.Count > Rows) Rows = f.RowTexts.Count;
            for (var i = 0; i < f.ColumnTexts.Count; i++)
            {
                if (Rows > 0) SetText(0, i, f.ColumnTexts[i]);
                if (f.ColumnAlignments[i] is { } a) SetColAlignment(i, a);
                var px = TextRenderer.MeasureText(f.ColumnTexts[i], Font).Width + 8;
                SetColWidth(i, (int)Math.Round(Twips.FromPixelsX(px)));
            }
            for (var i = 0; i < f.RowTexts.Count; i++)
                if (Cols > 0) SetText(i, 0, f.RowTexts[i]);
        }
    }

    /// <summary>OLE background of the current cell (0 = default colors, like MSFlexGrid).</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CellBackColor
    {
        get => CurrentStyleColor(s => s.BackColor);
        set => CurrentFlexCell().Style.BackColor = value == 0 ? Color.Empty : Vb6Color.ToColor(value);
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CellForeColor
    {
        get => CurrentStyleColor(s => s.ForeColor);
        set => CurrentFlexCell().Style.ForeColor = value == 0 ? Color.Empty : Vb6Color.ToColor(value);
    }

    /// <summary>Alignment of the current cell (<see cref="FlexAlign"/>); defaults to the column's.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CellAlignment
    {
        get
        {
            var cell = CurrentFlexCell();
            return cell.HasStyle && cell.Style.Alignment != DataGridViewContentAlignment.NotSet
                ? FromContent(cell.Style.Alignment)
                : GetColAlignment(_col);
        }
        set => CurrentFlexCell().Style.Alignment = ToContent(value);
    }

    /// <summary>MSFlexGrid <c>AddItem</c>: tab-separated cell texts, appended or inserted at <paramref name="index"/>.</summary>
    public void AddItem(string item, int? index = null)
    {
        if (Cols == 0) throw new InvalidOperationException("The grid has no columns.");
        int at;
        if (index.HasValue)
        {
            if (index.Value < 0 || index.Value > base.Rows.Count)
                throw new ArgumentOutOfRangeException(nameof(index), "Subscript out of range");
            base.Rows.Insert(index.Value, 1);
            at = index.Value;
        }
        else
        {
            at = base.Rows.Add();
        }
        var values = (item ?? "").Split('\t');
        for (var c = 0; c < Math.Min(values.Length, Cols); c++) base.Rows[at].Cells[c].Value = values[c];
        AfterStructureChange();
    }

    /// <summary>MSFlexGrid <c>RemoveItem</c> (the last non-fixed row cannot be removed).</summary>
    public void RemoveItem(int index)
    {
        CheckRow(index);
        if (index >= _fixedRows && base.Rows.Count - _fixedRows <= 1)
            throw new InvalidOperationException("Cannot remove last non-fixed row");
        base.Rows.RemoveAt(index);
        AfterStructureChange();
    }

    /// <summary>MSFlexGrid <c>Clear</c>: empties the text and formatting of every cell (structure kept).</summary>
    public void Clear()
    {
        foreach (DataGridViewRow row in base.Rows)
        {
            foreach (DataGridViewCell cell in row.Cells)
            {
                cell.Value = null;
                if (cell.HasStyle) cell.Style = new DataGridViewCellStyle();
            }
        }
    }

    protected override void OnCurrentCellChanged(EventArgs e)
    {
        base.OnCurrentCellChanged(e);
        var cell = CurrentCell;
        if (_syncing || cell == null || (cell.RowIndex == _row && cell.ColumnIndex == _col)) return;
        LeaveCell?.Invoke(this, EventArgs.Empty);
        _row = _rowSel = cell.RowIndex;
        _col = _colSel = cell.ColumnIndex;
        EnterCell?.Invoke(this, EventArgs.Empty);
        RowColChange?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnSelectionChanged(EventArgs e)
    {
        base.OnSelectionChanged(e);
        if (_syncing) return;
        var cells = SelectedCells.Cast<DataGridViewCell>().ToList();
        if (cells.Count == 0) return;
        int minR = cells.Min(c => c.RowIndex), maxR = cells.Max(c => c.RowIndex);
        int minC = cells.Min(c => c.ColumnIndex), maxC = cells.Max(c => c.ColumnIndex);
        _rowSel = _row == minR ? maxR : minR;
        _colSel = _col == minC ? maxC : minC;
        SelChange?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        TrackMouse(e);
        base.OnMouseMove(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        TrackMouse(e);
        base.OnMouseDown(e);
    }

    internal static DataGridViewContentAlignment ToContent(int flexAlignment) =>
        (DataGridViewContentAlignment)(1 << (FlexAlign.Vertical(flexAlignment) * 4 + FlexAlign.Horizontal(flexAlignment)));

    internal static int FromContent(DataGridViewContentAlignment a)
    {
        var bit = 0;
        for (var v = (int)a; v > 1; v >>= 1) bit++;
        return FlexAlign.Combine(bit % 4, bit / 4);
    }

    private bool HasCell => _row >= 0 && _row < base.Rows.Count && _col >= 0 && _col < ColumnCount;

    private void TrackMouse(MouseEventArgs e)
    {
        var hit = HitTest(e.X, e.Y);
        _mouseRow = hit.RowIndex;
        _mouseCol = hit.ColumnIndex;
    }

    private DataGridViewCell CurrentFlexCell()
    {
        if (!HasCell) throw new InvalidOperationException("No current cell.");
        return base.Rows[_row].Cells[_col];
    }

    private int CurrentStyleColor(Func<DataGridViewCellStyle, Color> pick)
    {
        var cell = CurrentFlexCell();
        if (!cell.HasStyle) return 0;
        var c = pick(cell.Style);
        return c.IsEmpty ? 0 : Vb6Color.FromColor(c);
    }

    private string GetText(int row, int col)
    {
        CheckRow(row);
        CheckCol(col);
        return Convert.ToString(base.Rows[row].Cells[col].Value) ?? "";
    }

    private void SetText(int row, int col, string value)
    {
        CheckRow(row);
        CheckCol(col);
        base.Rows[row].Cells[col].Value = value ?? "";
    }

    private int GetColWidth(int col)
    {
        CheckCol(col);
        var c = Columns[col];
        return c.Visible ? (int)Math.Round(Twips.FromPixelsX(c.Width)) : 0;
    }

    private void SetColWidth(int col, int twips)
    {
        CheckCol(col);
        var c = Columns[col];
        if (twips == 0)
        {
            c.Visible = false;
            return;
        }
        var px = Math.Max(2, Twips.ToPixelsX(twips < 0 ? DefaultColWidthTwips : twips));
        c.Visible = true;
        if (px < c.MinimumWidth) c.MinimumWidth = px;
        c.Width = px;
    }

    private int GetRowHeight(int row)
    {
        CheckRow(row);
        var r = base.Rows[row];
        return r.Visible ? (int)Math.Round(Twips.FromPixelsY(r.Height)) : 0;
    }

    private void SetRowHeight(int row, int twips)
    {
        CheckRow(row);
        var r = base.Rows[row];
        if (twips == 0)
        {
            r.Visible = false;
            return;
        }
        var px = Math.Max(2, twips < 0 ? RowTemplate.Height : Twips.ToPixelsY(twips));
        r.Visible = true;
        if (px < r.MinimumHeight) r.MinimumHeight = px;
        r.Height = px;
    }

    private int GetColAlignment(int col)
    {
        CheckCol(col);
        return _colAlign.TryGetValue(Columns[col], out var a) ? a : FlexAlign.flexAlignGeneral;
    }

    private void SetColAlignment(int col, int alignment)
    {
        CheckCol(col);
        if (alignment is < 0 or > 9) throw new ArgumentOutOfRangeException(nameof(alignment), "Invalid property value");
        var c = Columns[col];
        _colAlign[c] = alignment;
        c.DefaultCellStyle.Alignment = ToContent(alignment);
    }

    private void SetCurrent(int row, int col)
    {
        CheckRow(row);
        CheckCol(col);
        var moved = row != _row || col != _col;
        var selChanged = moved || _rowSel != row || _colSel != col;
        if (!selChanged) return;
        if (moved) LeaveCell?.Invoke(this, EventArgs.Empty);
        _row = _rowSel = row;
        _col = _colSel = col;
        SyncSelection();
        if (moved)
        {
            EnterCell?.Invoke(this, EventArgs.Empty);
            RowColChange?.Invoke(this, EventArgs.Empty);
        }
        SelChange?.Invoke(this, EventArgs.Empty);
    }

    private void AfterStructureChange()
    {
        ApplyFixed();
        var rows = base.Rows.Count;
        var cols = ColumnCount;
        _row = Clamp(_row, rows);
        _rowSel = Clamp(_rowSel, rows);
        _col = Clamp(_col, cols);
        _colSel = Clamp(_colSel, cols);
        SyncSelection();
    }

    private static int Clamp(int v, int count) => count == 0 ? 0 : Math.Max(0, Math.Min(v, count - 1));

    private void ApplyFixed()
    {
        _syncing = true;
        try
        {
            var rows = base.Rows.Count;
            var fr = Math.Min(_fixedRows, rows);
            for (var i = Math.Min(rows, Math.Max(_appliedFixedRows, fr) + 1) - 1; i >= 0; i--)
            {
                var r = base.Rows[i];
                var isFixed = i < fr;
                r.Frozen = isFixed;
                PaintFixed(r.DefaultCellStyle, isFixed);
            }
            _appliedFixedRows = fr;
            for (var i = ColumnCount - 1; i >= 0; i--)
            {
                var c = Columns[i];
                var isFixed = i < _fixedCols;
                c.Frozen = isFixed;
                PaintFixed(c.DefaultCellStyle, isFixed);
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void PaintFixed(DataGridViewCellStyle s, bool isFixed)
    {
        s.BackColor = isFixed ? _backColorFixed : Color.Empty;
        s.ForeColor = isFixed ? _foreColorFixed : Color.Empty;
        s.SelectionBackColor = isFixed ? _backColorFixed : Color.Empty;
        s.SelectionForeColor = isFixed ? _foreColorFixed : Color.Empty;
    }

    private void SyncSelection()
    {
        if (!HasCell) return;
        _syncing = true;
        try
        {
            try
            {
                CurrentCell = base.Rows[_row].Cells[_col];
            }
            catch (InvalidOperationException)
            {
                // Hidden cell / edit in progress: keep the logical position only.
            }
            ClearSelection();
            for (var r = Math.Min(_row, _rowSel); r <= Math.Max(_row, _rowSel); r++)
            for (var c = Math.Min(_col, _colSel); c <= Math.Max(_col, _colSel); c++)
                base.Rows[r].Cells[c].Selected = true;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void CheckRow(int row)
    {
        if (row < 0 || row >= base.Rows.Count) throw new ArgumentOutOfRangeException(nameof(row), "Subscript out of range");
    }

    private void CheckCol(int col)
    {
        if (col < 0 || col >= ColumnCount) throw new ArgumentOutOfRangeException(nameof(col), "Subscript out of range");
    }

}
