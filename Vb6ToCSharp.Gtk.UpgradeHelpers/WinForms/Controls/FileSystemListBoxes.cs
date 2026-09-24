using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Vb6ToCSharp.UpgradeHelpers.Internal;

namespace Vb6ToCSharp.UpgradeHelpers.WinForms.Controls;

/// <summary>VB6 DriveListBox: items <c>"c: [label]"</c>; <see cref="Drive"/> accepts any drive text ("c", "C:\x").</summary>
[DesignerCategory("Code")]
public class DriveListBox : ComboBox
{
    public DriveListBox()
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        if (!IsDesignMode) Reload();
        base.SelectedIndexChanged += DriveListBox_SelectedIndexChanged;
    }

    private void DriveListBox_SelectedIndexChanged(object sender, EventArgs e)
    {
        Change?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised when the selected drive changes.</summary>
    public event EventHandler Change;

    /// <summary>Selected drive text; setting an unknown drive throws <see cref="IOException"/> ("Device unavailable").</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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

    private void Reload()
    {
        var current = SelectedItem as string ?? Environment.CurrentDirectory;
#if !GTK
        BeginUpdate();
#endif
        try
        {
            Items.Clear();
            foreach (var d in FileSystemListing.DriveItems()) Items.Add(d);
        }
        finally
        {
#if !GTK
            EndUpdate();
#endif
        }
        var i = FileSystemListing.FindDrive(Items.Cast<string>().ToList(), current);
        if (i >= 0) SelectedIndex = i;
    }

    private static bool IsDesignMode => LicenseManager.UsageMode == LicenseUsageMode.Designtime;
}

/// <summary>
/// VB6 DirListBox: the ancestors of <see cref="Path"/> (root first) then its subdirectories. VB6 indexes:
/// <c>List(-1)</c> = current, <c>-2</c> = parent, …; <c>0..ListCount-1</c> = subdirectories.
/// Double-clicking an entry navigates to it.
/// </summary>
[DesignerCategory("Code")]
public class DirListBox : ListBox
{
    private readonly FileSystemListing.DirModel _model = new();
    private string _path = "";

    public DirListBox()
    {
        IntegralHeight = false;
        if (LicenseManager.UsageMode != LicenseUsageMode.Designtime) Path = Environment.CurrentDirectory;
    }

    /// <summary>Raised when <see cref="Path"/> changes.</summary>
    public event EventHandler Change;

    /// <summary>Current directory; unknown directories throw <see cref="DirectoryNotFoundException"/> ("Path not found").</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Path
    {
        get => _path;
        set
        {
            var full = FileSystemListing.ResolveDirectory(value, _path.Length > 0 ? _path : null);
            var changed = !string.Equals(full, _path, StringComparison.OrdinalIgnoreCase);
            _path = full;
            Rebuild();
            if (changed) Change?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>VB6 <c>List(index)</c>: full path ("" when out of range).</summary>
    public string GetList(int index) => _model.GetList(index);

    /// <summary>Number of subdirectories (VB6 <c>ListCount</c>).</summary>
    [Browsable(false)]
    public int ListCount => _model.SubdirectoryCount;

    /// <summary>VB6 <c>ListIndex</c>: -1 = current directory.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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
    public override void Refresh()
    {
        if (_path.Length > 0) Rebuild();
        base.Refresh();
    }

    protected override void OnDoubleClick(EventArgs e)
    {
        base.OnDoubleClick(e);
        if (SelectedIndex >= 0) Path = _model.FullPaths[SelectedIndex];
    }

    private void Rebuild()
    {
        _model.Build(_path);
        BeginUpdate();
        try
        {
            Items.Clear();
            for (var i = 0; i < _model.Display.Count; i++)
                Items.Add(new string(' ', 2 * Math.Min(i, _model.AncestorCount)) + _model.Display[i]);
            SelectedIndex = _model.AncestorCount - 1;
        }
        finally
        {
            EndUpdate();
        }
    }
}

/// <summary>VB6 FileListBox: files of <see cref="Path"/> matching <see cref="Pattern"/> and the attribute flags.</summary>
[DesignerCategory("Code")]
public class FileListBox : ListBox
{
    private FileSystemListing.FileFilter _filter = FileSystemListing.FileFilter.Default;
    private string _path = "";
    private string _pattern = "*.*";

    public FileListBox()
    {
        IntegralHeight = false;
        if (LicenseManager.UsageMode != LicenseUsageMode.Designtime) Path = Environment.CurrentDirectory;
    }

    public event EventHandler PathChange;
    public event EventHandler PatternChange;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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
    [DefaultValue("*.*")]
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

    /// <summary>
    /// Selected file name. Setting a path changes <see cref="Path"/>, a wildcard name changes <see cref="Pattern"/>,
    /// a plain name selects that file.
    /// </summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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

    [DefaultValue(true)]
    public bool Archive { get => _filter.Archive; set { _filter.Archive = value; Refresh(); } }

    [DefaultValue(false)]
    public bool Hidden { get => _filter.Hidden; set { _filter.Hidden = value; Refresh(); } }

    [DefaultValue(true)]
    public bool Normal { get => _filter.Normal; set { _filter.Normal = value; Refresh(); } }

    [DefaultValue(true)]
    public bool ReadOnly { get => _filter.ReadOnly; set { _filter.ReadOnly = value; Refresh(); } }

    [DefaultValue(false)]
    public bool System { get => _filter.System; set { _filter.System = value; Refresh(); } }

    /// <summary>Re-reads the directory (VB6 <c>Refresh</c>).</summary>
    public new void Refresh()
    {
        if (_path.Length > 0)
        {
            var selected = FileName;
            BeginUpdate();
            try
            {
                Items.Clear();
                foreach (var f in FileSystemListing.Files(_path, _pattern, _filter)) Items.Add(f);
                if (selected.Length > 0) SelectedIndex = Items.IndexOf(selected);
            }
            finally
            {
                EndUpdate();
            }
        }
        base.Refresh();
    }
}
