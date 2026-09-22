using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Microsoft.Win32;

namespace Vb6ToCSharp.Forms;

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

    // net48 WPF has no folder picker; use the Vista IFileOpenDialog in FOS_PICKFOLDERS mode.
    private static string PickFolder(Window owner, string initialFolder)
    {
        var dialog = (IFileOpenDialog)new FileOpenDialogRcw();
        try
        {
            dialog.GetOptions(out var options);
            dialog.SetOptions(options | FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM | FOS_PATHMUSTEXIST);
            if (initialFolder != null &&
                SHCreateItemFromParsingName(initialFolder, IntPtr.Zero, typeof(IShellItem).GUID, out var folderItem) == 0)
            {
                dialog.SetFolder(folderItem);
            }

            var hwnd = owner == null ? IntPtr.Zero : new WindowInteropHelper(owner).Handle;
            if (dialog.Show(hwnd) != 0) // cancelled (HRESULT_FROM_WIN32(ERROR_CANCELLED)) or failed
            {
                return null;
            }

            dialog.GetResult(out var result);
            result.GetDisplayName(SIGDN_FILESYSPATH, out var path);
            return path;
        }
        finally
        {
            Marshal.ReleaseComObject(dialog);
        }
    }

    private const uint FOS_PICKFOLDERS = 0x20;
    private const uint FOS_FORCEFILESYSTEM = 0x40;
    private const uint FOS_PATHMUSTEXIST = 0x800;
    private const uint SIGDN_FILESYSPATH = 0x80058000;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(string pszPath, IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IShellItem ppv);

    [ComImport, Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
    private class FileOpenDialogRcw { }

    // Vtable order matters: IModalWindow → IFileDialog → IFileOpenDialog.
    [ComImport, Guid("d57c7288-d4ad-4768-be02-9d969532d960"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOpenDialog
    {
        [PreserveSig] int Show(IntPtr hwndOwner);
        void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);
        void SetFileTypeIndex(uint iFileType);
        void GetFileTypeIndex(out uint piFileType);
        void Advise(IntPtr pfde, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOptions(uint fos);
        void GetOptions(out uint pfos);
        void SetDefaultFolder(IShellItem psi);
        void SetFolder(IShellItem psi);
        void GetFolder(out IShellItem ppsi);
        void GetCurrentSelection(out IShellItem ppsi);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        void GetResult(out IShellItem ppsi);
        void AddPlace(IShellItem psi, int fdap);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        void Close(int hr);
        void SetClientGuid(ref Guid guid);
        void ClearClientData();
        void SetFilter(IntPtr pFilter);
        void GetResults(out IntPtr ppenum);
        void GetSelectedItems(out IntPtr ppsai);
    }

    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }
}
