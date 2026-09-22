# Forms / OCX support aligned with VBUC — plan

## Blocking checks (before coding)

| Check | Result |
|---|---|
| Build + tests green on `main` (VS MSBuild, `dotnet test --no-build`) | 259/259 pass |
| `.frx` blob layouts (picture `lt\0\0`+size, `FF`+u16 / u8 / u32 strings, `List` u16 count + u16-len items, `ItemData` u16 count + i32) | Not officially documented → reader uses tolerant detection + magic-sniffing, covered by synthetic-blob tests |
| AxHost wrappers need the OCX registered on the build machine (`COMReference` `WrapperTool=aximp`) | Inherent (same as VS/VBUC); documented |

## Decisions (user)

| # | Decision | Consequence |
|---|---|---|
| 1 | UI back-end **selectable**: WPF (existing, default) or WinForms (VBUC-style) | `[Settings] UITarget=WPF\|WinForms`, CLI `--ui`, Config form combo; one form model, two emitters |
| 2 | Unmapped OCX → **AxHost interop** | `.vbp`/`.frm` `Object=` lines → `COMReference` (aximp) → `Ax<Lib>.Ax<Class>`; WPF hosts it in `WindowsFormsHost` |
| 3 | Runtime helpers in a **new library** `Vb6ToCSharp.UpgradeHelpers` (net48, NuGet `Net4x.Vb6ToCSharp.UpgradeHelpers`) | Converted projects reference `lib\Vb6ToCSharp.UpgradeHelpers.dll` (copied by the converter) |
| 4 | Scope: core forms + intrinsic controls, ComCtl/ComCtl2/FlexGrid/SSTab/RTB/CommonDialog, `.ctl` + MDI, code-side form semantics | All below |

## Current gaps (baseline)

| Area | Today | Where |
|---|---|---|
| Parsing | line-by-line, `DeQuote` loses `.frx` refs, `BeginProperty` nesting flattened ad hoc | `Modules/ModConvertForm.cs:99-199` |
| Layout | absolute `Margin` in one `Grid`, twips/14, window +435 twips fudge, ScaleMode ignored | `ModConvertForm.cs:234-296`, `ModUtils.cs:108` |
| Controls | ~45 hard-coded types, unknown → `Label`; Line/Shape/Timer dropped | `ModVb6ToCs.cs:135-358`, `ModConvertForm.cs:227` |
| Arrays | `Index` → `name_N` | `ModConvertForm.cs:217-221` |
| Properties | font, caption/text, tooltip only | `ModConvertForm.cs:299-330` |
| Events | ~10 by name, no args, `EventStub` only Click/Change/QueryUnload | `ModConvertForm.cs:348-479` |
| Menus / `.ctl` / MDI / `Object=` | not converted | `ModConvert.cs:98`, `ModProjectFiles.cs` |
| Code side | `Load`/`Unload`/`Show`/`Controls`/list methods not rewritten; property renames WPF-only | `ModControlProperties.cs`, `ModConvert.cs:1625` |

## Design

New folder `Vb6ToCSharp.Library/Forms/` (converter side):

| File | Role |
|---|---|
| `FrmModel.cs` | `VbFormFile` / `VbControl` (type, name, index, ordered props incl. `Group.Sub.Prop`, children, parent) + parser for `.frm`/`.ctl`/`.dob` |
| `FrxReader.cs` | Resolves `"x.frx":0123` → picture bytes (+kind by magic), string, list, itemdata |
| `VbpInfo.cs` | `.vbp`: `Name`, `Startup`, `Object=`, `Reference=`, `UserControl=`, MDI form |
| `Units.cs` / `VbColor.cs` | ScaleMode 0-7 → twips → px (15 twips/px @96 DPI); OLE/system colors → `Color`/`Brush` |
| `ControlCatalog.cs` | VB6 type → {WinForms type, WPF type, container, non-visual, default prop}; INI `[Controls]` (WPF) / `[WinFormsControls]` override |
| `EventCatalog.cs` | VB6 event (+args, per control type) → .NET event, args type, adapter binding (Cancel, KeyAscii, KeyCode/Shift, Button/Shift/X/Y twips, Index) |
| `MemberCatalog.cs` | Code-side member rewrites per back-end/control type: renames, read/write templates (twips, OLE color, CheckState, fonts, 1-based collections, list methods) |
| `WinFormsEmitter.cs` | `X.Designer.cs` (fields, `InitializeComponent`, arrays, menus → `MenuStrip`, Line/Shape → PowerPacks, SSTab → `TabControl`, ToolTip component) + `X.resx` (typed images) |
| `WpfEmitter.cs` | XAML (replaces `StartControl`): same model, menus, shapes, timers/non-visual in code-behind, SSTab, `WindowsFormsHost`+AxHost; images → `Forms\Resources\` |
| `FormConversion.cs` | Orchestration; current-form context used by `EventStub` / code conversion |
| `OcxInterop.cs` | LIBID → typelib name (registry/`LoadRegTypeLib`, known-lib fallback); `COMReference` items |

Helpers library `Vb6ToCSharp.UpgradeHelpers`:

| Namespace | Types |
|---|---|
| root | `Twips`, `CommonDialog` (VB6 API over WinForms dialogs), `PropertyBag`, `UnloadMode`, `Vb6Keys` |
| `.WinForms` | `ControlArray<T>` (VB index, `Load`/`Unload` clones + rewires handlers), `FormsHelper` (flat `Controls`, `PopupMenu`, `CloseReason`→UnloadMode, Shift/Button masks), `Vb6Layout`, `Vb6Font`, `ListHelper` (ItemData/NewIndex), `ScrollBarHelper`, `TreeViewHelper`, `FlexGrid` (DataGridView), `DriveListBox`/`DirListBox`/`FileListBox` |
| `.Wpf` | `ControlArray<T>`, `Vb6Layout` (twips ↔ Margin/Width), `Vb6Color`, `FlexGrid` (DataGrid), `UpDown`, `DriveListBox`/`DirListBox`/`FileListBox` |

## Control mapping (VBUC-aligned)

| VB6 | WinForms | WPF |
|---|---|---|
| Form / MDIForm / UserControl | `Form` / `Form{IsMdiContainer}` / `UserControl` | `Window` / `Window`+TODO / `UserControl` |
| Label, TextBox, CommandButton, CheckBox, OptionButton, Frame, ComboBox, ListBox | Label, TextBox, Button, CheckBox, RadioButton, GroupBox, ComboBox, ListBox (`CheckedListBox` if Style=1) | Label, TextBox, Button, CheckBox, RadioButton, GroupBox, ComboBox, ListBox |
| PictureBox / Image | PictureBox (container) / PictureBox | Canvas+Image / Image |
| H/VScrollBar | HScrollBar/VScrollBar (Max+LargeChange−1) | ScrollBar (Orientation) |
| Timer | `System.Windows.Forms.Timer` | `DispatcherTimer` (code-behind) |
| Line / Shape | PowerPacks `LineShape` / `RectangleShape`/`OvalShape` in `ShapeContainer` | `Line` / `Rectangle`/`Ellipse` |
| Menu | `MenuStrip` + `ToolStripMenuItem` (`&`, `-`, Shortcut, Checked) ; Visible=False top → also popup | `Menu` + `MenuItem` / `Separator` |
| Drive/Dir/FileListBox | helpers | helpers |
| TreeView, ListView, ImageList, ProgressBar, Slider, StatusBar, Toolbar, TabStrip, ImageCombo | TreeView, ListView, ImageList, ProgressBar, TrackBar, StatusStrip, ToolStrip, TabControl, ComboBox | TreeView, ListView, (field), ProgressBar, Slider, StatusBar, ToolBar, TabControl, ComboBox |
| DTPicker, MonthView, UpDown, FlatScrollBar | DateTimePicker, MonthCalendar, NumericUpDown, H/VScrollBar | DatePicker, Calendar, `UpDown` helper, ScrollBar |
| MSFlexGrid / MSHFlexGrid | helper `FlexGrid` | helper `FlexGrid` |
| SSTab | TabControl (children split by `Tab(n).Control(m)`, −75000 offset undone) | TabControl |
| RichTextBox | RichTextBox (`TextRTF`→`Rtf`) | RichTextBox |
| CommonDialog | helper `CommonDialog` (component) | helper `CommonDialog` (field) |
| Other `Lib.Class` | `Ax<Lib>.Ax<Class>` (AxHost), props applied on Load | `WindowsFormsHost` + AxHost |
| `<vbp Name>.<ctl>` | project UserControl | project UserControl |

## Verification

| Step | How |
|---|---|
| Unit | new tests per module (`FrmModelTests`, `FrxReaderTests`, `UnitsColorTests`, `WinFormsEmitterTests`, `WpfEmitterTests`, `EventAdapterTests`, `CodeSideFormTests`, `VbpInfoTests`), helpers tests project |
| Integration | fixture VB6 project (form with arrays, menus, frx images/lists, ComCtl, SSTab, OCX, `.ctl`, MDI) → convert both targets → generated WinForms project **compiles** with VS MSBuild (test marked, skipped when MSBuild missing) |
| Regression | existing 259 tests stay green |

## Risks

| Risk | Mitigation |
|---|---|
| `.frx` string/picture variants | tolerant reader + magic sniffing; unknown → TODO comment, raw bytes kept in resx |
| Textual code converter can't see full expressions (read vs write) | assignment-target flag in `ConvertCodeLine`; templates degrade to rename + `// TODO` |
| AxHost properties set before handle creation | applied in `Load` adapter, not in `InitializeComponent` |
| WPF has no MDI | MDIForm → Window + TODO (documented) |
| Wrong known-LIBID table entry | registry lookup first; unresolved libs are kept (never dropped) |
