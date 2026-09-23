# Net4x.Vb6ToCSharp.WPF.UpgradeHelpers

WPF half of the runtime for VB6 programs converted to C# by Vb6ToCSharp (`Net4x.Vb6ToCSharp.Library`).
Depends on [Net4x.Vb6ToCSharp.WinForms.UpgradeHelpers](../Vb6ToCSharp.WinForms.UpgradeHelpers/README.md)
— the VB6 `CommonDialog` is the WinForms one, and through it on
[Net4x.Vb6ToCSharp.Base.UpgradeHelpers](../Vb6ToCSharp.Base.UpgradeHelpers/README.md). Target:
`net10.0-windows`, WPF.

Projects converted with `--ui wpf` reference this package and import it:

```csharp
using Vb6ToCSharp.UpgradeHelpers;
using static Vb6ToCSharp.UpgradeHelpers.VbRuntime;
using Vb6ToCSharp.UpgradeHelpers.Arrays;
using Vb6ToCSharp.UpgradeHelpers.Dialogs;
using Vb6ToCSharp.UpgradeHelpers.Interop;
using Vb6ToCSharp.UpgradeHelpers.Model;
using Vb6ToCSharp.UpgradeHelpers.Wpf.Controls;
using Vb6ToCSharp.UpgradeHelpers.Wpf.Helpers;
using Vb6Color = Vb6ToCSharp.UpgradeHelpers.Wpf.Helpers.Vb6Color; // also Model.Vb6Color (System.Drawing)
```

XAML reaches the controls through
`xmlns:vb6="clr-namespace:Vb6ToCSharp.UpgradeHelpers.Wpf.Controls;assembly=Vb6ToCSharp.WPF.UpgradeHelpers"`.

| Namespace | Holds |
|---|---|
| `…​.Wpf.Controls` | `FlexGrid`, `DriveListBox`, `DirListBox`, `FileListBox`, `ControlArray<T>`, `UpDown` |
| `…​.Wpf.Helpers` | `FormsHelper`, `Vb6Layout`, `Vb6Color`, `WpfUiBridge`, `WpfOcxValueConverter` |

| Type | VB6 feature |
|---|---|
| `ControlArray<T>` | Control arrays of the WPF stack |
| `FormsHelper` | Active window, controls by name, `PopupMenu`, `UnloadMode`, Shift / Button values, `MousePointer`, `ShowForm` (modality) |
| `FlexGrid` | `MSFlexGrid` over `DataGrid` |
| `DriveListBox`, `DirListBox`, `FileListBox`, `UpDown` | File-system and spin controls |
| `Vb6Layout`, `Vb6Color` | `Left`/`Top`/`Width`/`Height`/`Move` in twips, OLE colors as `Color` / `Brush` |
| `WpfUiBridge`, `WpfOcxValueConverter` | `DoEvents`, `Load`, `Unload` and the WPF OCX conversions for `VbRuntime` / `OcxHelper` |
