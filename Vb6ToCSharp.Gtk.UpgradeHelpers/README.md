# Net4x.Vb6ToCSharp.Gtk.UpgradeHelpers

WinForms half of the runtime for VB6 programs converted to C# by Vb6ToCSharp
(`Net4x.Vb6ToCSharp.Library`). Depends on
[Net4x.Vb6ToCSharp.Base.UpgradeHelpers](../Vb6ToCSharp.Base.UpgradeHelpers/README.md) (the VB6 language
runtime). Target: `net10.0-windows`, Gtk WinForms.

Projects converted with `--ui winforms` reference this package and import it:

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
