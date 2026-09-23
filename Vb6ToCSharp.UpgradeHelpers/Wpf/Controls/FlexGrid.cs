using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Vb6ToCSharp.UpgradeHelpers.Arrays;
using Vb6ToCSharp.UpgradeHelpers.Model;
using Vb6ToCSharp.UpgradeHelpers.Wpf.Helpers;
// both Model and Wpf.Helpers declare Vb6Color; a WPF control means the Media one
using Vb6Color = Vb6ToCSharp.UpgradeHelpers.Wpf.Helpers.Vb6Color;

namespace Vb6ToCSharp.UpgradeHelpers.Wpf.Controls;

/// <summary>
/// MSFlexGrid API over a WPF <see cref="DataGrid"/> backed by a <see cref="DataTable"/> (one string column per grid
/// column). Fixed columns are frozen; fixed rows are painted with the fixed colors but scroll (WPF DataGrid has no
/// frozen rows). Sizes are twips (1 DIP = 15 twips), colors OLE, alignments <see cref="FlexAlign"/> values.
/// Per-cell formats are stored and applied to realized cells.
/// </summary>
public class FlexGrid : DataGrid
{
    private const double TwipsPerDip = 15.0;
    private const int DefaultColWidthTwips = 960;
    private const int DefaultRowHeightTwips = 240;

    private sealed class CellFormat
    {
        public int BackColor, ForeColor;
        public int? Alignment;
    }

    private readonly DataTable _table = new("FlexGrid");
    private readonly List<int> _colAlign = new();
    private readonly Dictionary<int, int> _rowHeights = new(); // twips
    private readonly Dictionary<(int Row, int Col), CellFormat> _formats = new();
    private int _fixedRows = 1, _fixedCols = 1;
    private int _row, _col, _rowSel, _colSel, _topRow = -1, _leftCol = -1;
    private int _mouseRow = -1, _mouseCol = -1;
    private bool _syncing;
    private string _formatString = "";
    private int _backColorFixed = unchecked((int)0x8000000F), _foreColorFixed = unchecked((int)0x80000012);

    public FlexGrid()
    {
        AutoGenerateColumns = false;
        HeadersVisibility = DataGridHeadersVisibility.None;
        CanUserAddRows = false;
        CanUserDeleteRows = false;
        CanUserSortColumns = false;
        CanUserReorderColumns = false;
        CanUserResizeRows = false;
        IsReadOnly = true;
        SelectionUnit = DataGridSelectionUnit.Cell;
        SelectionMode = DataGridSelectionMode.Extended;
        TextMatrix = new MatrixProperty<string>(GetText, SetText);
        ColWidth = new IndexedProperty<int>(GetColWidth, SetColWidth);
        RowHeight = new IndexedProperty<int>(GetRowHeight, SetRowHeight);
        ColAlignment = new IndexedProperty<int>(GetColAlignment, SetColAlignment);
        ItemsSource = _table.DefaultView;
        LoadingRow += (_, e) => StyleRow(e.Row);
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

    /// <summary>Backing table (one row per grid row, columns C0..Cn).</summary>
    internal DataTable Table => _table;

    public int Rows
    {
        get => _table.Rows.Count;
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Invalid Row value");
            if (value > 0 && Cols == 0) Cols = 1;
            while (_table.Rows.Count > value) _table.Rows.RemoveAt(_table.Rows.Count - 1);
            while (_table.Rows.Count < value) _table.Rows.Add(NewEmptyRow());
            foreach (var k in _rowHeights.Keys.Where(k => k >= value).ToList()) _rowHeights.Remove(k);
            foreach (var k in _formats.Keys.Where(k => k.Row >= value).ToList()) _formats.Remove(k);
            if (_fixedRows > value) _fixedRows = value;
            AfterStructureChange();
        }
    }

    public int Cols
    {
        get => _table.Columns.Count;
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Invalid Col value");
            while (_table.Columns.Count > value)
            {
                var last = _table.Columns.Count - 1;
                Columns.RemoveAt(last);
                _table.Columns.RemoveAt(last);
                _colAlign.RemoveAt(last);
            }
            while (_table.Columns.Count < value)
            {
                var i = _table.Columns.Count;
                var dc = _table.Columns.Add("C" + i.ToString(CultureInfo.InvariantCulture), typeof(string));
                dc.DefaultValue = "";
                foreach (DataRow r in _table.Rows) r[dc] = "";
                _colAlign.Add(FlexAlign.flexAlignGeneral);
                Columns.Add(new DataGridTextColumn
                {
                    Binding = new Binding(dc.ColumnName) { Mode = BindingMode.OneWay },
                    Width = new DataGridLength(DefaultColWidthTwips / TwipsPerDip),
                    ElementStyle = AlignmentStyle(FlexAlign.flexAlignGeneral),
                });
            }
            foreach (var k in _formats.Keys.Where(k => k.Col >= value).ToList()) _formats.Remove(k);
            if (_fixedCols > value) _fixedCols = value;
            AfterStructureChange();
        }
    }

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

    public int BackColorFixed
    {
        get => _backColorFixed;
        set { _backColorFixed = value; AfterStructureChange(); }
    }

    public int ForeColorFixed
    {
        get => _foreColorFixed;
        set { _foreColorFixed = value; AfterStructureChange(); }
    }

    public int Row
    {
        get => _row;
        set => SetCurrent(value, _col);
    }

    public int Col
    {
        get => _col;
        set => SetCurrent(_row, value);
    }

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
    public string Text
    {
        get => HasCell ? GetText(_row, _col) : "";
        set => SetText(_row, _col, value);
    }

    public MatrixProperty<string> TextMatrix { get; }

    /// <summary><c>ColWidth(col)</c> in twips (0 hides, -1 default).</summary>
    public IndexedProperty<int> ColWidth { get; }

    /// <summary><c>RowHeight(row)</c> in twips (0 hides, -1 default).</summary>
    public new IndexedProperty<int> RowHeight { get; }

    public IndexedProperty<int> ColAlignment { get; }

    public int TopRow
    {
        get => _topRow >= 0 && _topRow < Rows ? _topRow : _fixedRows;
        set
        {
            CheckRow(value);
            _topRow = value;
            if (value < Items.Count) ScrollIntoView(Items[value]);
        }
    }

    public int LeftCol
    {
        get => _leftCol >= 0 && _leftCol < Cols ? _leftCol : _fixedCols;
        set
        {
            CheckCol(value);
            _leftCol = value;
            if (Items.Count > 0) ScrollIntoView(Items[Math.Min(_row, Items.Count - 1)], Columns[value]);
        }
    }

    /// <summary>Kept for source compatibility (WPF batches rendering by itself).</summary>
    public bool Redraw { get; set; } = true;

    public int MouseRow => _mouseRow;

    public int MouseCol => _mouseCol;

    /// <inheritdoc cref="Vb6ToCSharp.UpgradeHelpers.WinForms.Controls.FlexGrid.FormatString"/>
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
                SetColWidth(i, (int)Math.Round((MeasureText(f.ColumnTexts[i]) + 8) * TwipsPerDip));
            }
            for (var i = 0; i < f.RowTexts.Count; i++)
                if (Cols > 0) SetText(i, 0, f.RowTexts[i]);
        }
    }

    /// <summary>OLE background of the current cell (0 = default colors).</summary>
    public int CellBackColor
    {
        get => CurrentFormat(false)?.BackColor ?? 0;
        set { CurrentFormat(true).BackColor = value; ApplyCell(_row, _col); }
    }

    public int CellForeColor
    {
        get => CurrentFormat(false)?.ForeColor ?? 0;
        set { CurrentFormat(true).ForeColor = value; ApplyCell(_row, _col); }
    }

    public int CellAlignment
    {
        get => CurrentFormat(false)?.Alignment ?? GetColAlignment(_col);
        set
        {
            if (value is < 0 or > 9) throw new ArgumentOutOfRangeException(nameof(value), "Invalid property value");
            CurrentFormat(true).Alignment = value;
            ApplyCell(_row, _col);
        }
    }

    /// <summary>MSFlexGrid <c>AddItem</c>: tab-separated cell texts, appended or inserted at <paramref name="index"/>.</summary>
    public void AddItem(string item, int? index = null)
    {
        if (Cols == 0) throw new InvalidOperationException("The grid has no columns.");
        var count = _table.Rows.Count;
        var at = index ?? count;
        if (at < 0 || at > count) throw new ArgumentOutOfRangeException(nameof(index), "Subscript out of range");
        _table.Rows.Add(NewEmptyRow());
        // DataView order follows insertion, so shift values down instead of InsertAt.
        for (var r = count; r > at; r--) _table.Rows[r].ItemArray = _table.Rows[r - 1].ItemArray;
        var values = (item ?? "").Split('\t');
        var target = _table.Rows[at];
        for (var c = 0; c < Cols; c++) target[c] = c < values.Length ? values[c] : "";
        ShiftRowKeys(at, +1);
        AfterStructureChange();
    }

    /// <summary>MSFlexGrid <c>RemoveItem</c> (the last non-fixed row cannot be removed).</summary>
    public void RemoveItem(int index)
    {
        CheckRow(index);
        if (index >= _fixedRows && _table.Rows.Count - _fixedRows <= 1)
            throw new InvalidOperationException("Cannot remove last non-fixed row");
        _table.Rows.RemoveAt(index);
        ShiftRowKeys(index, -1);
        AfterStructureChange();
    }

    /// <summary>MSFlexGrid <c>Clear</c>: empties the text and formatting of every cell (structure kept).</summary>
    public void Clear()
    {
        foreach (DataRow r in _table.Rows)
            for (var c = 0; c < _table.Columns.Count; c++)
                r[c] = "";
        _formats.Clear();
        RestyleRealizedRows();
    }

    protected override void OnCurrentCellChanged(EventArgs e)
    {
        base.OnCurrentCellChanged(e);
        if (_syncing || !CurrentCell.IsValid) return;
        var r = Items.IndexOf(CurrentCell.Item);
        var c = Columns.IndexOf(CurrentCell.Column);
        if (r < 0 || c < 0 || (r == _row && c == _col)) return;
        LeaveCell?.Invoke(this, EventArgs.Empty);
        _row = _rowSel = r;
        _col = _colSel = c;
        EnterCell?.Invoke(this, EventArgs.Empty);
        RowColChange?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnSelectedCellsChanged(SelectedCellsChangedEventArgs e)
    {
        base.OnSelectedCellsChanged(e);
        if (_syncing || SelectedCells.Count == 0) return;
        var rows = SelectedCells.Select(c => Items.IndexOf(c.Item)).ToList();
        var cols = SelectedCells.Select(c => Columns.IndexOf(c.Column)).ToList();
        _rowSel = _row == rows.Min() ? rows.Max() : rows.Min();
        _colSel = _col == cols.Min() ? cols.Max() : cols.Min();
        SelChange?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        TrackMouse(e.OriginalSource as DependencyObject);
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseDown(e);
        TrackMouse(e.OriginalSource as DependencyObject);
    }

    internal static (HorizontalAlignment H, VerticalAlignment V, TextAlignment T) ToWpf(int flexAlignment)
    {
        var h = FlexAlign.Horizontal(flexAlignment);
        var v = FlexAlign.Vertical(flexAlignment);
        return (h switch { 1 => HorizontalAlignment.Center, 2 => HorizontalAlignment.Right, _ => HorizontalAlignment.Left },
            v switch { 0 => VerticalAlignment.Top, 2 => VerticalAlignment.Bottom, _ => VerticalAlignment.Center },
            h switch { 1 => TextAlignment.Center, 2 => TextAlignment.Right, _ => TextAlignment.Left });
    }

    private bool HasCell => _row >= 0 && _row < Rows && _col >= 0 && _col < Cols;

    private DataRow NewEmptyRow()
    {
        var row = _table.NewRow();
        for (var c = 0; c < _table.Columns.Count; c++) row[c] = "";
        return row;
    }

    private void TrackMouse(DependencyObject source)
    {
        _mouseRow = _mouseCol = -1;
        for (var d = source; d != null; d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
        {
            if (d is DataGridCell cell)
            {
                _mouseCol = Columns.IndexOf(cell.Column);
            }
            else if (d is DataGridRow row)
            {
                _mouseRow = row.GetIndex();
                return;
            }
        }
    }

    private CellFormat CurrentFormat(bool create)
    {
        if (!HasCell) throw new InvalidOperationException("No current cell.");
        if (_formats.TryGetValue((_row, _col), out var f) || !create) return f;
        return _formats[(_row, _col)] = new CellFormat();
    }

    private string GetText(int row, int col)
    {
        CheckRow(row);
        CheckCol(col);
        return _table.Rows[row][col] as string ?? "";
    }

    private void SetText(int row, int col, string value)
    {
        CheckRow(row);
        CheckCol(col);
        _table.Rows[row][col] = value ?? "";
    }

    private int GetColWidth(int col)
    {
        CheckCol(col);
        var c = Columns[col];
        if (c.Visibility != Visibility.Visible) return 0;
        var dip = c.Width.IsAbsolute ? c.Width.Value : c.ActualWidth;
        return (int)Math.Round(dip * TwipsPerDip);
    }

    private void SetColWidth(int col, int twips)
    {
        CheckCol(col);
        var c = Columns[col];
        if (twips == 0)
        {
            c.Visibility = Visibility.Collapsed;
            return;
        }
        c.Visibility = Visibility.Visible;
        var dip = Math.Max(2, (twips < 0 ? DefaultColWidthTwips : twips) / TwipsPerDip);
        if (dip < c.MinWidth) c.MinWidth = dip;
        c.Width = new DataGridLength(dip);
    }

    private int GetRowHeight(int row)
    {
        CheckRow(row);
        if (_rowHeights.TryGetValue(row, out var t)) return t;
        return double.IsNaN(base.RowHeight) ? DefaultRowHeightTwips : (int)Math.Round(base.RowHeight * TwipsPerDip);
    }

    private void SetRowHeight(int row, int twips)
    {
        CheckRow(row);
        if (twips < 0) _rowHeights.Remove(row);
        else _rowHeights[row] = twips;
        if (ItemContainerGenerator.ContainerFromIndex(row) is DataGridRow r) StyleRow(r);
    }

    private int GetColAlignment(int col)
    {
        CheckCol(col);
        return _colAlign[col];
    }

    private void SetColAlignment(int col, int alignment)
    {
        CheckCol(col);
        if (alignment is < 0 or > 9) throw new ArgumentOutOfRangeException(nameof(alignment), "Invalid property value");
        _colAlign[col] = alignment;
        if (Columns[col] is DataGridTextColumn tc) tc.ElementStyle = AlignmentStyle(alignment);
    }

    private static Style AlignmentStyle(int alignment)
    {
        var (h, v, t) = ToWpf(alignment);
        var s = new Style(typeof(TextBlock));
        s.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, h));
        s.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, v));
        s.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, t));
        s.Seal();
        return s;
    }

    private void SetCurrent(int row, int col)
    {
        CheckRow(row);
        CheckCol(col);
        var moved = row != _row || col != _col;
        if (!moved && _rowSel == row && _colSel == col) return;
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
        var rows = Rows;
        var cols = Cols;
        _row = Clamp(_row, rows);
        _rowSel = Clamp(_rowSel, rows);
        _col = Clamp(_col, cols);
        _colSel = Clamp(_colSel, cols);
        FrozenColumnCount = Math.Min(_fixedCols, Columns.Count);
        var fixedCell = new Style(typeof(DataGridCell));
        fixedCell.Setters.Add(new Setter(BackgroundProperty, Vb6Color.ToBrush(_backColorFixed)));
        fixedCell.Setters.Add(new Setter(ForegroundProperty, Vb6Color.ToBrush(_foreColorFixed)));
        fixedCell.Setters.Add(new Setter(BorderBrushProperty, Vb6Color.ToBrush(_backColorFixed)));
        fixedCell.Seal();
        for (var i = 0; i < Columns.Count; i++) Columns[i].CellStyle = i < _fixedCols ? fixedCell : null;
        RestyleRealizedRows();
        SyncSelection();
    }

    private static int Clamp(int v, int count) => count == 0 ? 0 : Math.Max(0, Math.Min(v, count - 1));

    private void ShiftRowKeys(int from, int delta)
    {
        var heights = _rowHeights.ToList();
        _rowHeights.Clear();
        foreach (var kv in heights)
        {
            if (delta < 0 && kv.Key == from) continue;
            _rowHeights[kv.Key >= from ? kv.Key + delta : kv.Key] = kv.Value;
        }
        var formats = _formats.ToList();
        _formats.Clear();
        foreach (var kv in formats)
        {
            if (delta < 0 && kv.Key.Row == from) continue;
            var r = kv.Key.Row >= from ? kv.Key.Row + delta : kv.Key.Row;
            _formats[(r, kv.Key.Col)] = kv.Value;
        }
    }

    private void RestyleRealizedRows()
    {
        for (var i = 0; i < Items.Count; i++)
            if (ItemContainerGenerator.ContainerFromIndex(i) is DataGridRow r)
                StyleRow(r);
    }

    private void StyleRow(DataGridRow row)
    {
        var index = row.GetIndex();
        if (index < 0) return;
        if (index < _fixedRows)
        {
            row.Background = Vb6Color.ToBrush(_backColorFixed);
            row.Foreground = Vb6Color.ToBrush(_foreColorFixed);
        }
        else
        {
            row.ClearValue(BackgroundProperty);
            row.ClearValue(ForegroundProperty);
        }
        if (_rowHeights.TryGetValue(index, out var twips))
        {
            row.Visibility = twips == 0 ? Visibility.Collapsed : Visibility.Visible;
            if (twips > 0) row.Height = twips / TwipsPerDip;
        }
        else
        {
            row.Visibility = Visibility.Visible;
            row.ClearValue(HeightProperty);
        }
        if (row.IsLoaded) ApplyRowFormats(row, index);
        else row.Loaded += OnRowLoaded;
    }

    private void OnRowLoaded(object sender, RoutedEventArgs e)
    {
        var row = (DataGridRow)sender;
        row.Loaded -= OnRowLoaded;
        ApplyRowFormats(row, row.GetIndex());
    }

    private void ApplyRowFormats(DataGridRow row, int index)
    {
        for (var c = 0; c < Columns.Count; c++) ApplyCell(row, index, c);
    }

    private void ApplyCell(int r, int c)
    {
        if (ItemContainerGenerator.ContainerFromIndex(r) is DataGridRow row) ApplyCell(row, r, c);
    }

    private void ApplyCell(DataGridRow row, int r, int c)
    {
        if (Columns[c].GetCellContent(row) is not FrameworkElement content || content.Parent is not DataGridCell cell) return;
        _formats.TryGetValue((r, c), out var f);
        if (f is { BackColor: not 0 }) cell.Background = Vb6Color.ToBrush(f.BackColor);
        else cell.ClearValue(BackgroundProperty);
        if (f is { ForeColor: not 0 }) cell.Foreground = Vb6Color.ToBrush(f.ForeColor);
        else cell.ClearValue(ForegroundProperty);
        if (content is TextBlock tb)
        {
            if (f?.Alignment is { } a)
            {
                var (h, v, t) = ToWpf(a);
                tb.HorizontalAlignment = h;
                tb.VerticalAlignment = v;
                tb.TextAlignment = t;
            }
            else
            {
                tb.ClearValue(HorizontalAlignmentProperty);
                tb.ClearValue(VerticalAlignmentProperty);
                tb.ClearValue(TextBlock.TextAlignmentProperty);
            }
        }
    }

    private double MeasureText(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var ft = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily, FontStyle, FontWeight, FontStretch), FontSize, Brushes.Black,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        return ft.WidthIncludingTrailingWhitespace;
    }

    private void SyncSelection()
    {
        if (!HasCell || Items.Count <= _row) return;
        _syncing = true;
        try
        {
            CurrentCell = new DataGridCellInfo(Items[_row], Columns[_col]);
            SelectedCells.Clear();
            for (var r = Math.Min(_row, _rowSel); r <= Math.Max(_row, _rowSel); r++)
            for (var c = Math.Min(_col, _colSel); c <= Math.Max(_col, _colSel); c++)
                SelectedCells.Add(new DataGridCellInfo(Items[r], Columns[c]));
        }
        catch (InvalidOperationException)
        {
            // Selection not possible in the current state: the logical position is kept.
        }
        finally
        {
            _syncing = false;
        }
    }

    private void CheckRow(int row)
    {
        if (row < 0 || row >= Rows) throw new ArgumentOutOfRangeException(nameof(row), "Subscript out of range");
    }

    private void CheckCol(int col)
    {
        if (col < 0 || col >= Cols) throw new ArgumentOutOfRangeException(nameof(col), "Subscript out of range");
    }
}
