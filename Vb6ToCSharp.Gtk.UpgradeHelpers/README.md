# Net4x.Vb6ToCSharp.Gtk.UpgradeHelpers

Cross-platform (Windows, Linux, macOS) half of the runtime for VB6 programs converted to C# by Vb6ToCSharp
(`Net4x.Vb6ToCSharp.Library`): the [WinForms package](../Vb6ToCSharp.WinForms.UpgradeHelpers/README.md)
compiled against [Gtk.Windows.Forms](https://www.nuget.org/packages/Gtk.Windows.Forms.Base) instead of
`System.Windows.Forms`. Target: `net10.0` — no `-windows` platform, which is what lets a converted project
build and run off Windows.

It carries no source of its own: its `.csproj` globs the WinForms package's sources and imports the same
shared VB6 language runtime the [base package](../Vb6ToCSharp.Base.UpgradeHelpers/README.md) is built from,
so it needs no dependency on that package and cannot drift from the WinForms half — a file added there
reaches this package with no edit, and anything Gtk.Windows.Forms cannot serve fails this build.

A project is converted with `--ui winforms`, which references the WinForms package;
[ProcessForGtk](../ProcessForGtk/) then swaps that reference for this one and drops the `-windows` platform.
The imports are the same either way:

```csharp
using Vb6ToCSharp.UpgradeHelpers;
using static Vb6ToCSharp.UpgradeHelpers.VbRuntime;
using Vb6ToCSharp.UpgradeHelpers.Arrays;
using Vb6ToCSharp.UpgradeHelpers.Dialogs;
using Vb6ToCSharp.UpgradeHelpers.Interop;
using Vb6ToCSharp.UpgradeHelpers.Model;
using Vb6ToCSharp.UpgradeHelpers.WinForms.Controls;
using Vb6ToCSharp.UpgradeHelpers.WinForms.Helpers;
using CommonDialog = Vb6ToCSharp.UpgradeHelpers.Dialogs.CommonDialog; // also System.Windows.Forms.CommonDialog
```

| Namespace | Holds |
|---|---|
| `…​.Dialogs` | `CommonDialog` and its constants — the VB6 common dialogs, used by WPF programs too |
| `…​.WinForms.Controls` | `FlexGrid`, `DriveListBox`, `DirListBox`, `FileListBox`, `ControlArray<T>` |
| `…​.WinForms.Helpers` | `FormsHelper`, `ListHelper`, `ListViewHelper`, `TreeViewHelper`, `ScrollBarHelper`, `Vb6Layout`, `Vb6Font`, `WinFormsUiBridge` |

| Type | VB6 feature |
|---|---|
| `CommonDialog` | `MSComDlg.CommonDialog` (`ShowOpen`, `ShowSave`, `ShowColor`, `ShowFont`, `ShowPrinter`, `CancelError`) |
| `ControlArray<T>` | Control arrays of the WinForms stack |
| `FormsHelper` | `Screen.ActiveForm`, controls by name, `PopupMenu`, `UnloadMode`, Shift / Button values, `MousePointer`, `ShowForm` |
| `FlexGrid` | `MSFlexGrid` over `DataGridView` |
| `DriveListBox`, `DirListBox`, `FileListBox` | File-system controls |
| `Vb6Layout`, `Vb6Font` | `Left`/`Top`/`Width`/`Height`/`Move` in twips, font property changes |
| `ListHelper`, `ListViewHelper`, `TreeViewHelper`, `ScrollBarHelper` | `AddItem`/`ItemData`/`NewIndex`, `ListItems`/`Nodes.Add` with keys, VB6 scroll bar `Max` |
| `WinFormsUiBridge` | `DoEvents`, `Load`, `Unload` for `VbRuntime` |
