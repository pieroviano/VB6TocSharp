using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Microsoft.Win32;
using WinForms = System.Windows.Forms;

namespace Vb6ToCSharp.UI;

/// <summary>"..." browse buttons: pick a file or a folder and write it back into the path textbox.</summary>
internal static class BrowseDialog
{
    public const string ProjectFilter = "VB6 projects (*.vbp;*.vbg)|*.vbp;*.vbg|All files (*.*)|*.*";
    public const string SourceFilter = "VB6 sources (*.bas;*.cls;*.frm;*.ctl)|*.bas;*.cls;*.frm;*.ctl|All files (*.*)|*.*";
    public const string LintFilter = "VB6 projects and sources (*.vbp;*.vbg;*.bas;*.cls;*.frm;*.ctl)|*.vbp;*.vbg;*.bas;*.cls;*.frm;*.ctl|All files (*.*)|*.*";

    public static void BrowseFile(Window owner, TextBox target, string filter)
    {
        var dlg = new OpenFileDialog { Filter = filter, CheckFileExists = true };
        var current = target.Text.Trim();
        if (File.Exists(current))
        {
            dlg.InitialDirectory = Path.GetDirectoryName(current);
            dlg.FileName = Path.GetFileName(current);
        }
        else if (ExistingFolder(current) is { } folder)
        {
            dlg.InitialDirectory = folder;
        }

        if (dlg.ShowDialog(owner) == true)
        {
            target.Text = dlg.FileName;
        }
    }

    public static void BrowseFolder(Window owner, TextBox target)
    {
        var picked = PickFolder(owner, ExistingFolder(target.Text.Trim()));
        if (picked != null)
        {
            target.Text = picked;
        }
    }

    /// <summary>The path itself if it is a folder, else its nearest existing parent.</summary>
    private static string ExistingFolder(string path)
    {
        try
        {
            while (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
            {
                path = Path.GetDirectoryName(path);
            }
            return string.IsNullOrEmpty(path) ? null : path;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    // WPF has no folder picker of its own, so this uses the WinForms one: fully managed, and it
    // becomes the modern shell picker by itself once this app targets .NET 5 or later.
    private static string PickFolder(Window owner, string initialFolder)
    {
        using (var dialog = new WinForms.FolderBrowserDialog { ShowNewFolderButton = true })
        {
            if (initialFolder != null) dialog.SelectedPath = initialFolder;
            return dialog.ShowDialog(new OwnerWindow(owner)) == WinForms.DialogResult.OK ? dialog.SelectedPath : null;
        }
    }

    /// <summary>Lets the WinForms dialog take the WPF window as its owner, so it stays modal to it.</summary>
    private sealed class OwnerWindow : WinForms.IWin32Window
    {
        public OwnerWindow(Window owner)
        {
            Handle = owner == null ? IntPtr.Zero : new WindowInteropHelper(owner).Handle;
        }

        public IntPtr Handle { get; }
    }
}
