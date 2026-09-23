# Net4x.Vb6ToCSharp.UpgradeHelpers

Runtime for VB6 programs converted to C# by Vb6ToCSharp (`Net4x.Vb6ToCSharp.Library`), comparable to VB Migration
Partner's support library. It supplies the VB6 semantics that neither .NET nor `Microsoft.VisualBasic` covers. Target:
.NET Framework 4.8, WinForms and WPF.

Generated projects reference this package and import it:

```csharp
using Vb6ToCSharp.UpgradeHelpers;
using static Vb6ToCSharp.UpgradeHelpers.VbRuntime;
using Vb6ToCSharp.UpgradeHelpers.Arrays;
using Vb6ToCSharp.UpgradeHelpers.Dialogs;
using Vb6ToCSharp.UpgradeHelpers.Interop;
using Vb6ToCSharp.UpgradeHelpers.Model;
using Vb6ToCSharp.UpgradeHelpers.WinForms.Controls; // or .Wpf.Controls
using Vb6ToCSharp.UpgradeHelpers.WinForms.Helpers;  // or .Wpf.Helpers
```

Namespaces mirror folders 1:1:

| Namespace | Holds |
|---|---|
| `Vb6ToCSharp.UpgradeHelpers` | `VbRuntime`, `IVbStruct` — the VB6 language core |
| `…​.Arrays` | `VB6Array<T>`, `ControlArrayBase<T>`, `IndexedProperty<T>`, `MatrixProperty<T>` |
| `…​.Dialogs` | `CommonDialog` and its constants |
| `…​.Interop` | `OcxHelper`, `PropertyBag` |
| `…​.Model` | `Twips`, `Vb6Color`, `Vb6Keys`, `UnloadMode`, `FlexAlign` |
| `…​.WinForms.Controls` / `.Wpf.Controls` | `FlexGrid`, `DriveListBox`, `DirListBox`, `FileListBox`, `ControlArray<T>`, `UpDown` (WPF) |
| `…​.WinForms.Helpers` / `.Wpf.Helpers` | `FormsHelper`, `ListHelper`, `TreeViewHelper`, `Vb6Layout`, … |

Two names exist in more than one namespace, so converted code qualifies or aliases them:
`Vb6Color` (`Model` = `System.Drawing`, `Wpf.Helpers` = `System.Windows.Media`) and `CommonDialog`
(also `System.Windows.Forms.CommonDialog`).

WPF XAML reaches the controls through
`xmlns:vb6="clr-namespace:Vb6ToCSharp.UpgradeHelpers.Wpf.Controls;assembly=Vb6ToCSharp.UpgradeHelpers"`.

## Language runtime (`Vb6ToCSharp.UpgradeHelpers` and siblings)

| Type | VB6 feature |
|---|---|
| `VbRuntime.NewArray<T>(n)` / `(n1, n2)` | `Dim a(n - 1)`: elements start as VB6 values (`""`, initialized UDTs) |
| `VbRuntime.ReDim(a, count[, preserve])` | `ReDim [Preserve]` (1-D, 2-D) |
| `VB6Array<T>` | Array with any lower bound (`Dim a(1 To 10)`, `Option Base 1`); `ref` indexer, `ReDim`, `Erase`, `LBound`/`UBound` |
| `IVbStruct`, `VbRuntime.NewStruct<T>()` | `Type ... End Type` as a struct, initialized as VB6 does |
| `VbRuntime.FixedLen(s, n)` | `String * n`: pads / truncates |
| `VbRuntime.MidStmt(ref s, start[, length], value)` | `Mid(s, …) = value` statement |
| `VbRuntime.TextCompare(a, b)` | `Option Compare Text` comparison |
| `IndexedProperty<T>`, `MatrixProperty<T>` | Properties with parameters (`ColWidth(i)`, `TextMatrix(r, c)`) |
| `PropertyBag` | User control `ReadProperties` / `WriteProperties` |
| `Twips`, `Vb6Color`, `Vb6Keys`, `UnloadMode`, `FlexAlign` | Units, OLE colors, key codes, constants |
| `CommonDialog` | `MSComDlg.CommonDialog` (`ShowOpen`, `ShowSave`, `ShowColor`, `ShowFont`, `CancelError`) |
| `OcxHelper` | Late-bound get/set of OCX (AxHost) properties, converting values to the property type |
| `ControlArrayBase<T>` | Control arrays: sparse indexes, `LBound`/`UBound`, `Index` of a control, runtime-loaded elements |

## WinForms (`…UpgradeHelpers.WinForms.*`) / WPF (`…UpgradeHelpers.Wpf.*`)

| Type | VB6 feature |
|---|---|
| `ControlArray<T>` | Control arrays for the UI stack |
| `FormsHelper` | `Screen.ActiveForm`, controls by name, `PopupMenu`, `UnloadMode`, Shift / Button values, `MousePointer`; `ShowForm` (WPF modality) |
| `FlexGrid` | `MSFlexGrid` over `DataGridView` / `DataGrid` |
| `DriveListBox`, `DirListBox`, `FileListBox` | File-system controls |
| `Vb6Layout`, `Vb6Font` (WinForms), `Vb6Color` (WPF) | `Left`/`Top`/`Width`/`Height`/`Move` in twips, font property changes, colors |
| `ListHelper`, `ListViewHelper`, `TreeViewHelper`, `ScrollBarHelper` (WinForms), `UpDown` (WPF) | `AddItem`/`ItemData`/`NewIndex`, `ListItems`/`Nodes.Add` with keys, VB6 scroll bar `Max`, UpDown control |

## Example (converted code)

```csharp
VB6Array<string> names = new VB6Array<string>(1, 10);   // Dim names(1 To 10) As String
names[1] = "a";
names.ReDim(1, 20, true);                                // ReDim Preserve names(1 To 20)

Rec r = NewStruct<Rec>();                                // Dim r As Rec
string code = FixedLen("AB", 5);                         // Dim code As String * 5: code = "AB"
MidStmt(ref code, 2, "Z");                               // Mid(code, 2) = "Z"
```
