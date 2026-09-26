# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

VB6 → C# converter: reads a `.vbp`, writes an SDK-style `net10.0-windows` C# project (WPF or WinForms forms) following VB Migration
Partner's rules. See [README.md](README.md) and the per-project READMEs for user-facing behaviour.

## Commands

| Task | Command |
|---|---|
| Build | `"C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe" Vb6ToCSharp.slnx -restore` |
| All tests | `dotnet test Vb6ToCSharp.slnx --no-build` |
| One test | `dotnet test Vb6ToCSharp.Tests --no-build --filter "FullyQualifiedName~ConverterClassTests.SomeTest"` |
| Skip integration | `--filter "Category!=Integration"` |
| Convert the sample | `Vb6ToCSharp.Console all --vbp VB6\Showcase.vbp --out Converted --assembly Showcase --ui winforms` |

- `dotnet build` fails (MSB4803) because of the ADODB `COMReference` in the library. Use Visual Studio MSBuild.
- Building writes `Net4x.*` packages to `Packages\`. [NuGet.Config](NuGet.Config) uses `Packages\` as a local feed.
  Converted projects reference `Net4x.Vb6ToCSharp.WinForms.UpgradeHelpers` / `…WPF…` `1.0.*` from that feed.
  Rebuild the helpers projects before the integration test if you changed the runtime.
- `IntegrationTests` converts `VB6\Showcase.vbp` into `Converted\` (git-ignored) through the console exe, builds the result
  with vswhere-located MSBuild, then loads the exe and asserts on `modMain.RunAll` / `modMain.Classes` etc. When adding a
  VB6 feature, extend the `VB6\` sample and these assertions. The same test for project groups converts `VBG\Group.vbg`
  (`Exe\` referencing the ActiveX DLL `Lib\`) into `ConvertedGroup\` and builds `Group.sln`. A third converts the ADO
  sample `Vb6Ado\Vbb6Ado.vbp` into `ConvertedVb6Ado\` and only builds it: it talks to SQL Server LocalDB and shows
  message boxes, so it cannot run unattended. A fourth converts `Showcase.vbp` again into `ConvertedGtk\`, runs
  the `ProcessForGtk` exe over `Showcase.sln` and builds that: it pins the cross-platform route, i.e. that converted code
  compiles against `Gtk.Windows.Forms` with no `System.Windows.Forms`. It is not run - that needs the native GTK runtime
  and a process of its own.
- Test parallelization is disabled ([AssemblyInfo.cs](Vb6ToCSharp.Tests/Properties/AssemblyInfo.cs)): the converter uses
  process-wide static state.
- **Each test project mirrors the folders, namespaces and type names of the project it tests.** A test for
  `Vb6ToCSharp.<Area>.<Type>` lives in `Vb6ToCSharp.Tests/<Area>/<Type>Tests.cs`, namespace `Vb6ToCSharp.Tests.<Area>`
  (likewise for the three `Vb6ToCSharp.*.UpgradeHelpers.Tests`, which keep the `Vb6ToCSharp.UpgradeHelpers.Tests.*`
  namespaces of the code they test). A new test file goes where the type it exercises lives; if that
  means a new folder, create it. The two exceptions, both at the project root: `Fixtures/` (shared test helpers —
  `TestUtil`, `Sta`, `TempDir`, `ConverterFixture`, `ConverterTestHelpers`, the `IVbStruct` sample types, and the
  self-conversion leftovers `FormTest.cs`/`TestCases.cs`) and `IntegrationTests.cs`, which spans the whole pipeline.
- Converter test classes are one class per library type, sharing `ConverterFixture` through `IClassFixture` and the
  conversion helpers through `using static Vb6ToCSharp.Tests.Fixtures.ConverterTestHelpers;`. Do not reintroduce a
  partial class spanning files — it cannot be split across the mirrored namespaces.

## Architecture

- **Library ([Vb6ToCSharp.Library](Vb6ToCSharp.Library/))** is the engine. The WPF app and the console are thin front ends
  that call the same static entry points (`CodeConverter.ConvertProject/ConvertFile/ConvertFileList`, `RefScanner.ScanRefs`,
  `SupportFiles.*`, `QuickLint.LintFileOrProject`).
- **Namespaces mirror folders 1:1.** A `.Model` namespace holds the plain data types its parent produces or consumes;
  the parent holds the services.

  | Namespace | Holds |
  |---|---|
  | `Vb6ToCSharp.Parsing` (`.Model`) | reads `.vbp`/`.vbg`/`.frm`/`.ctl`/`.frx`/`.cls` → `ProjectInfo`, `FormControlFile`, `ControlWithType`, `ClassDefinition` |
  | `Vb6ToCSharp.CodeConversion` (`.Model`) | the VB6 → C# code pipeline and its per-procedure state |
  | `Vb6ToCSharp.FormConversion` (`.Model`) | designer emission: control/event catalogs, WPF and WinForms emitters |
  | `Vb6ToCSharp.CodeGeneration` (`.Model`) | the output project: `.csproj`, `Program.cs`, `.sln` for a group, migration report |
  | `Vb6ToCSharp.Linting` | `QuickLint`, the pre-conversion VB6 linter |
  | `Vb6ToCSharp.Infrastructure` | host plumbing: text files, INI, shell, git, directory stack, regex |
  | `Vb6ToCSharp.Runtime` (`.Model`) | VB6 shim **for the converter's own code**: `VbStrings`/`VbConstants`/`VbConversion`/`VbInformation`/`VbFileSystem`/`VbInteraction`/`VbOperators`, `RuntimeExtension`, and the VB6 enums plus `VbCollection` in `.Model` |
  | `Vb6ToCSharp.UI` | WPF/MVVM plumbing the front end binds to (`CommandBase`, `PropertyIndexer`) |

  Do not name a library namespace after a VB6 function class (`Conversion`, `Strings`, `Interaction`, `Information`,
  `FileSystem`): this code calls those members unqualified, and the namespace would shadow them.
- **The converter itself was machine-converted from VB6.** Code lives in static classes with mutable static state.
  It leans heavily on `using static Vb6ToCSharp.Runtime.Vb*` (`Mid`, `InStr`, `Split`, `vbCrLf`...) and on VB-style
  string handling. Match this style when editing. `Runtime/` holds that shim - plain C#, no `Microsoft.VisualBasic`
  anywhere in the library or the app - and each `Vb*` class mirrors its VB counterpart **signature for signature**
  (same parameter types, same optional tail): keep it that way, or overload resolution against `RuntimeExtension`'s
  competing members shifts. VB semantics that differ from the BCL are preserved and pinned by
  `Vb6ToCSharp.Tests/Runtime/Vb*Tests.cs`, whose expectations come from the VB runtime itself (`Trim` leaves tabs,
  `InStr` is 1-based, `Replace("")` is `null`...). `Runtime/RuntimeExtension.cs` and `Runtime/Model/` are shims for
  this code, not for converted output. **Converted output still uses `Microsoft.VisualBasic`**: the `using` block in
  `CodeGeneration/UsingEverything.cs`, the assembly reference in `SupportFiles`, the PowerPacks prefix in
  `ControlCatalog` and `CompileUsings` in the tests are emitted text - do not "clean" them up.
  `Vb6ToCSharp.Tests/Tests/FormTest.cs` is a conversion fixture left over from self-conversion, not a runnable test.
- **State and config.** `ProjectConfigurationParser` loads `VB6toCS.INI` (`IniFilePath`, `OverrideSettings`, `UiTarget`, `AdoTarget`).
  Front ends inject UI through the delegates `ConversionUtility.Notify` / `ConversionUtility.Progress`. The library must
  never show UI directly.
- **Pipeline, per file (text-based, line by line):**
  1. `RefScanner.ScanRefs()` indexes the project's procedures, enums, forms, UDTs and globals. The conversion needs this
     index to tell a call without parentheses from a variable.
  2. `PragmaConverter.PreProcess` applies `'##` pragmas and `VBMigrationPartner.pragmas`.
  3. `StatementsConverter.BeginFile` sets up per-file options (`Option Base/Compare/Explicit`, `DefType`) and
     `#If`/`#Const` handling.
  4. `CodeConverter`: `ConvertGlobals` handles declarations, `ConvertCodeSegment` → `ConvertSub` → `ConvertCodeLine` →
     `ConvertElement`/`ConvertValue` handle procedures. Statement-level rules (error handling, `ReDim`, file I/O, implicit
     conversions, `Select Case`) are in `StatementsConverter`. Type and control mappings are in `Vb6ToCsConverter`.
     Class semantics are in `ClassesConverter`. Per-procedure variable/property tracking is in `SubTracking`.
  5. `ProjectSpecificConverter` / `PostConvertCodeLine` apply the INI `[PostCodeLine]` rules. `PostProcess` applies the
     pragmas. `WriteOut` writes the file, skipping files marked `### CONVERTED`.
- **Forms.** `FrmParser`/`FrxReader` ([Parsing/](Vb6ToCSharp.Library/Parsing/)) parse `.frm`/`.ctl` + `.frx` into
  `FormControlFile`. In [FormConversion/](Vb6ToCSharp.Library/FormConversion/), `ControlCatalog` and `EventCatalog` map
  VB6 controls and events to .NET (both overridable from the INI); `WpfEmitter` (XAML) or `WinFormsEmitter`
  (`Designer.cs` + `.resx` via `ResxWriter`) emits the designer. `FormContext` shares designer facts with the code
  conversion, e.g. `EventAdapters`, which bridge .NET handler signatures to the VB6-signature handlers.
- **Output support.** `SupportFiles` writes the `.csproj`, `Program.cs` and `AssemblyInfo.cs` (a project that used ADO
  references the managed `Standard.AdoDb` package, or the ADODB `COMReference` when `ADOTarget=COM`); `ProjectGroup` writes the
  `.sln` and project references for a `.vbg`. `MigrationReport.Write()` collects every `// TODO:` in the output into
  `MigrationReport.md`.
- **Runtime (four packages).** This is what converted code calls, split by UI stack; namespaces are shared
  (`Vb6ToCSharp.UpgradeHelpers.*`) and mirror folders, so only the assembly/package names say which half a type is in:

  | Project / package (`Net4x.` + name) | Holds |
  |---|---|
  | [Vb6ToCSharp.Base.UpgradeHelpers](Vb6ToCSharp.Base.UpgradeHelpers/) | the root (`VbRuntime`, `IVbStruct`, `IVbUiBridge`), `.Arrays`, `.Interop`, `.Model`, `.Internal`; no WinForms/WPF (`System.Drawing.Common` for colors and twips) |
  | [Vb6ToCSharp.WinForms.UpgradeHelpers](Vb6ToCSharp.WinForms.UpgradeHelpers/) | `.Dialogs` (`CommonDialog`, used by WPF too), `.WinForms.Controls`, `.WinForms.Helpers`; depends on Base |
  | [Vb6ToCSharp.WPF.UpgradeHelpers](Vb6ToCSharp.WPF.UpgradeHelpers/) | `.Wpf.Controls`, `.Wpf.Helpers`; depends on WinForms (for `CommonDialog`) and so on Base |
  | [Vb6ToCSharp.Gtk.UpgradeHelpers](Vb6ToCSharp.Gtk.UpgradeHelpers/) | no sources of its own: globs the WinForms project's `**\*.cs` and imports `Vb6ToCSharp.Base.UpgradeHelpers.Shared`, compiled against `Gtk.Windows.Forms.Base` for `net10.0`. Add helpers to the WinForms project, never here; what Gtk cannot serve fails this build |

  A converted project references only its UI package. [ProcessForGtk](ProcessForGtk/) rewrites a converted
  `.csproj`/`.sln` from the WinForms package to the Gtk one (dropping `-windows` and `UseWindowsForms`), so a
  converted program runs on Linux and macOS; its package/version constants live in `ProcessForGtk/ProjectRewriter.cs`.
  [Resources/Vb6ToCSharp.Article.md](Resources/Vb6ToCSharp.Article.md) documents that route and its boundary. `DoEvents` / `Load` / `Unload` and the WPF OCX conversions
  live in the UI packages and reach `VbRuntime` / `OcxHelper` through `IVbUiBridge` / `IOcxValueConverter`,
  registered from a module initializer (the runtime loads the UI package on first use if it has not run yet).
  This is distinct from `Vb6ToCSharp.Library`'s own `Runtime` namespace, which serves the converter's
  machine-converted code. VB6 semantics that can't be expressed inline belong here, not in generated boilerplate.
  **These namespaces are the packages' public API and are emitted into every converted file.** Renaming or moving a
  type means updating, together: the `using` block in `CodeGeneration/UsingEverything.cs`, the `Helpers*` prefix and
  `HelpersAssembly*` constants in `FormConversion/ControlCatalog.cs` (used by both emitters and by `SupportFiles`
  for the `PackageReference`), the XAML `clr-namespace` in `FormConversion/WpfEmitter.cs`, and `CompileUsings` in
  `Vb6ToCSharp.Tests/Fixtures/ConverterTestHelpers.cs`. A new runtime API needs matching emission in the library.

## Conventions

- Fix converter defects as general VB6 → C# rules. Never special-case the `VB6\Showcase` sample (names, lines, data).
- Unit tests call library internals directly (`InternalsVisibleTo` in the `.csproj`). Converter tests usually feed VB6 text
  to `ConvertClassSource` / `ConvertCodeLine` / `ConvertSub` and assert on the C#. `CodeConverterTypeTests` also compiles
  the output with Roslyn against the UpgradeHelpers packages; its `CompileUsings` must list the same namespaces
  `UsingEverything` emits.
