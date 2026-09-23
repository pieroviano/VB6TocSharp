# Net4x.Vb6ToCSharp.Base.UpgradeHelpers

UI-independent half of the runtime for VB6 programs converted to C# by Vb6ToCSharp
(`Net4x.Vb6ToCSharp.Library`), comparable to VB Migration Partner's support library. It supplies the VB6
semantics that neither .NET nor `Microsoft.VisualBasic` covers. Target: `net10.0-windows` (no WinForms, no WPF).

Converted projects do not reference this package directly: they reference
[Net4x.Vb6ToCSharp.WinForms.UpgradeHelpers](../Vb6ToCSharp.WinForms.UpgradeHelpers/README.md) or
[Net4x.Vb6ToCSharp.WPF.UpgradeHelpers](../Vb6ToCSharp.WPF.UpgradeHelpers/README.md), which depend on it.

Namespaces mirror folders 1:1 and are shared with the UI packages (`Vb6ToCSharp.UpgradeHelpers.*`):

| Namespace | Holds |
|---|---|
| `Vb6ToCSharp.UpgradeHelpers` | `VbRuntime`, `IVbStruct`, `IVbUiBridge` |
| `…​.Arrays` | `VB6Array<T>`, `ControlArrayBase<T>`, `IndexedProperty<T>` |
| `…​.Interop` | `OcxHelper`, `IOcxValueConverter`, `PropertyBag` |
| `…​.Model` | `Twips`, `Vb6Color`, `Vb6Keys`, `UnloadMode`, `FlexAlign` |

| Type | VB6 feature |
|---|---|
| `VbRuntime.NewArray<T>(n)` / `(n1, n2)` | `Dim a(n - 1)`: elements start as VB6 values (`""`, initialized UDTs) |
| `VbRuntime.ReDim(a, count[, preserve])` | `ReDim [Preserve]` (1-D, 2-D) |
| `VB6Array<T>` | Array with any lower bound (`Dim a(1 To 10)`, `Option Base 1`); `ref` indexer, `ReDim`, `Erase`, `LBound`/`UBound` |
| `IVbStruct`, `VbRuntime.NewStruct<T>()` | `Type ... End Type` as a struct, initialized as VB6 does |
| `VbRuntime.FixedLen(s, n)` | `String * n`: pads / truncates |
| `VbRuntime.MidStmt(ref s, start[, length], value)` | `Mid(s, …) = value` statement |
| `VbRuntime.TextCompare(a, b)` | `Option Compare Text` comparison |
| `VbRuntime.DoEvents()` / `Load` / `Unload` | Delegated to the UI package through `IVbUiBridge` |
| `IndexedProperty<T>` | Properties with parameters (`ColWidth(i)`) |
| `PropertyBag` | User control `ReadProperties` / `WriteProperties` |
| `OcxHelper` | Late-bound get/set of OCX (AxHost) properties; UI types through `IOcxValueConverter` |
| `ControlArrayBase<T>` | Control arrays: sparse indexes, `LBound`/`UBound`, `Index` of a control, runtime-loaded elements |
| `Twips`, `Vb6Color`, `Vb6Keys`, `UnloadMode`, `FlexAlign` | Units, OLE colors, key codes, constants |

## The UI stack

`DoEvents`, `Load`, `Unload` and the OCX conversions of WPF colors are implemented by the UI package, which
registers itself from a module initializer (`VbRuntime.RegisterUi`, `OcxHelper.AddConverter`). WPF registers
ahead of WinForms and answers while a WPF `Application` is running. When nothing registered yet, the runtime
loads the UI package deployed with the program on first use.

## Example (converted code)

```csharp
VB6Array<string> names = new VB6Array<string>(1, 10);   // Dim names(1 To 10) As String
names[1] = "a";
names.ReDim(1, 20, true);                                // ReDim Preserve names(1 To 20)

Rec r = NewStruct<Rec>();                                // Dim r As Rec
string code = FixedLen("AB", 5);                         // Dim code As String * 5: code = "AB"
MidStmt(ref code, 2, "Z");                               // Mid(code, 2) = "Z"
```
