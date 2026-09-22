using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Vb6ToCSharp.UpgradeHelpers.Internal;

namespace Vb6ToCSharp.UpgradeHelpers.Wpf;

/// <summary>VB6 DriveListBox for WPF: items <c>"c: [label]"</c>; <see cref="Drive"/> accepts any drive text.</summary>
public class DriveListBox : ComboBox
{
    public DriveListBox()
    {
        IsEditable = false;
        if (!DesignerProperties.GetIsInDesignMode(this)) Reload();
    }

    /// <summary>Raised when the selected drive changes.</summary>
    public event EventHandler Change;

    /// <summary>Selected drive text; setting an unknown drive throws <see cref="IOException"/> ("Device unavailable").</summary>
    public string Drive
    {
        get => SelectedItem as string ?? "";
        set
        {
            var i = FileSystemListing.FindDrive(Items.Cast<string>().ToList(), value);
            if (i < 0)
            {
                Reload();
                i = FileSystemListing.FindDrive(Items.Cast<string>().ToList(), value);
            }
            if (i < 0) throw new IOException("Device unavailable");
            SelectedIndex = i;
        }
    }

    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        base.OnSelectionChanged(e);
        Change?.Invoke(this, EventArgs.Empty);
    }

    private void Reload()
    {
        var current = SelectedItem as string ?? Environment.CurrentDirectory;
        Items.Clear();
        foreach (var d in FileSystemListing.DriveItems()) Items.Add(d);
        var i = FileSystemListing.FindDrive(Items.Cast<string>().ToList(), current);
        if (i >= 0) SelectedIndex = i;
    }
}

/// <summary>
/// VB6 DirListBox for WPF: ancestors of <see cref="Path"/> (root first) then its subdirectories.
/// <c>GetList(-1)</c> = current, <c>-2</c> = parent, …; <c>0..ListCount-1</c> = subdirectories.
/// Double-clicking an entry navigates to it.
/// </summary>
public class DirListBox : ListBox
{
    private readonly FileSystemListing.DirModel _model = new();
    private string _path = "";

    public DirListBox()
    {
        if (!DesignerProperties.GetIsInDesignMode(this)) Path = Environment.CurrentDirectory;
    }

    public event EventHandler Change;

    /// <summary>Current directory; unknown directories throw <see cref="DirectoryNotFoundException"/>.</summary>
    public string Path
    {
        get => _path;
        set
        {
            var full = FileSystemListing.ResolveDirectory(value, _path.Length > 0 ? _path : null);
            var changed = !string.Equals(full, _path, StringComparison.OrdinalIgnoreCase);
            _path = full;
            Refresh();
            if (changed) Change?.Invoke(this, EventArgs.Empty);
        }
    }

    public string GetList(int index) => _model.GetList(index);

    /// <summary>Number of subdirectories (VB6 <c>ListCount</c>).</summary>
    public int ListCount => _model.SubdirectoryCount;

    /// <summary>VB6 <c>ListIndex</c>: -1 = current directory.</summary>
    public int ListIndex
    {
        get => SelectedIndex < 0 ? -1 : _model.ToIndex(SelectedIndex);
        set
        {
            var pos = _model.ToPosition(value);
            if (pos < 0 || pos >= Items.Count) throw new ArgumentOutOfRangeException(nameof(value), "Invalid property value");
            SelectedIndex = pos;
        }
    }

    /// <summary>Re-reads the subdirectories.</summary>
    public void Refresh()
    {
        if (_path.Length == 0) return;
        _model.Build(_path);
        Items.Clear();
        for (var i = 0; i < _model.Display.Count; i++)
            Items.Add(new string(' ', 2 * Math.Min(i, _model.AncestorCount)) + _model.Display[i]);
        SelectedIndex = _model.AncestorCount - 1;
    }

    protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (SelectedIndex >= 0) Path = _model.FullPaths[SelectedIndex];
    }
}

/// <summary>VB6 FileListBox for WPF: files of <see cref="Path"/> matching <see cref="Pattern"/> and the attribute flags.</summary>
public class FileListBox : ListBox
{
    private FileSystemListing.FileFilter _filter = FileSystemListing.FileFilter.Default;
    private string _path = "";
    private string _pattern = "*.*";

    public FileListBox()
    {
        if (!DesignerProperties.GetIsInDesignMode(this)) Path = Environment.CurrentDirectory;
    }

    public event EventHandler PathChange;
    public event EventHandler PatternChange;

    public string Path
    {
        get => _path;
        set
        {
            var full = FileSystemListing.ResolveDirectory(value, _path.Length > 0 ? _path : null);
            var changed = !string.Equals(full, _path, StringComparison.OrdinalIgnoreCase);
            _path = full;
            Refresh();
            if (changed) PathChange?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>';'-separated wildcard patterns (default <c>*.*</c>).</summary>
    public string Pattern
    {
        get => _pattern;
        set
        {
            var v = string.IsNullOrWhiteSpace(value) ? "*.*" : value;
            if (v == _pattern) return;
            _pattern = v;
            Refresh();
            PatternChange?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc cref="WinForms.FileListBox.FileName"/>
    public string FileName
    {
        get => SelectedItem as string ?? "";
        set
        {
            var v = value ?? "";
            var dir = global::System.IO.Path.GetDirectoryName(v);
            if (!string.IsNullOrEmpty(dir)) Path = dir;
            var name = global::System.IO.Path.GetFileName(v);
            if (name.IndexOfAny(new[] { '*', '?' }) >= 0) Pattern = name;
            else SelectedIndex = Items.Cast<string>().ToList().FindIndex(s => string.Equals(s, name, StringComparison.OrdinalIgnoreCase));
        }
    }

    public bool Archive { get => _filter.Archive; set { _filter.Archive = value; Refresh(); } }
    public bool Hidden { get => _filter.Hidden; set { _filter.Hidden = value; Refresh(); } }
    public bool Normal { get => _filter.Normal; set { _filter.Normal = value; Refresh(); } }
    public bool ReadOnly { get => _filter.ReadOnly; set { _filter.ReadOnly = value; Refresh(); } }
    public bool System { get => _filter.System; set { _filter.System = value; Refresh(); } }

    /// <summary>Re-reads the directory (VB6 <c>Refresh</c>).</summary>
    public void Refresh()
    {
        if (_path.Length == 0) return;
        var selected = FileName;
        Items.Clear();
        foreach (var f in FileSystemListing.Files(_path, _pattern, _filter)) Items.Add(f);
        if (selected.Length > 0) SelectedIndex = Items.IndexOf(selected);
    }
}
