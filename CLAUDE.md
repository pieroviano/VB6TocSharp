# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

VB6 → C# converter: reads a `.vbp`, writes an SDK-style `net48` C# project (WPF or WinForms forms) following VB Migration
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
  Converted projects reference `Net4x.Vb6ToCSharp.UpgradeHelpers` `1.0.*` from that feed. Rebuild the helpers
  project before the integration test if you changed the runtime.
- `IntegrationTests` converts `VB6\Showcase.vbp` into `Converted\` (git-ignored) through the console exe, builds the result
  with vswhere-located MSBuild, then loads the exe and asserts on `modMain.RunAll` / `modMain.Classes` etc. When adding a
  VB6 feature, extend the `VB6\` sample and these assertions. The same test for project groups converts `VBG\Group.vbg`
  (`Exe\` referencing the ActiveX DLL `Lib\`) into `ConvertedGroup\` and builds `Group.sln`.
- Test parallelization is disabled ([AssemblyInfo.cs](Vb6ToCSharp.Tests/AssemblyInfo.cs)): the converter uses
  process-wide static state.

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
  | `Vb6ToCSharp.Runtime` (`.Model`) | VB6 shim **for the converter's own code**, plus the VB6 constant enums |
  | `Vb6ToCSharp.UI` | WPF/MVVM plumbing the front end binds to (`CommandBase`, `PropertyIndexer`) |

  Do not name a library namespace after a `Microsoft.VisualBasic` class (`Conversion`, `Strings`, `Interaction`,
  `Information`, `FileSystem`): this code calls them unqualified, and the namespace would shadow them.
- **The converter itself was machine-converted from VB6.** Code lives in static classes with mutable static state.
  It leans heavily on `using static Microsoft.VisualBasic.*` (`Mid`, `InStr`, `Split`...) and on VB-style string handling.
  Match this style when editing. `Runtime/RuntimeExtension.cs` and `Runtime/Model/` are shims for that code, not for
  converted output. `Vb6ToCSharp.Tests/Tests/FormTest.cs` is a conversion fixture left over from self-conversion, not a
  runnable test.
- **State and config.** `ProjectConfigurationParser` loads `VB6toCS.INI` (`IniFilePath`, `OverrideSettings`, `UiTarget`).
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
- **Output support.** `SupportFiles` writes the `.csproj`, `Program.cs` and `AssemblyInfo.cs`; `ProjectGroup` writes the
  `.sln` and project references for a `.vbg`. `MigrationReport.Write()` collects every `// TODO:` in the output into
  `MigrationReport.md`.
- **Runtime ([Vb6ToCSharp.UpgradeHelpers](Vb6ToCSharp.UpgradeHelpers/)).** This is what converted code calls. Namespaces
  mirror folders here too: the root (`VbRuntime`, `IVbStruct`), `.Arrays`, `.Dialogs`, `.Interop`, `.Model`, `.Internal`,
  and the parallel `.WinForms.Controls`/`.WinForms.Helpers` and `.Wpf.Controls`/`.Wpf.Helpers`. It is distinct from
  `Vb6ToCSharp.Library`'s own `Runtime` namespace, which serves the converter's machine-converted code. VB6 semantics
  that can't be expressed inline belong here, not in generated boilerplate.
  **These namespaces are the package's public API and are emitted into every converted file.** Renaming or moving a type
  means updating, together: the `using` block in `CodeGeneration/UsingEverything.cs`, the `Helpers*` prefix constants in
  `FormConversion/ControlCatalog.cs` (used by both emitters), the XAML `clr-namespace` in `FormConversion/WpfEmitter.cs`,
  and `CompileUsings` in `Vb6ToCSharp.Tests/ConverterTypeTests.cs`. A new runtime API needs matching emission in the library.
- **Extras** is an independent optional package (`Recordset`, `FixedWidthRecord`, `CsvRecord`). The converter doesn't use it.

## Conventions

- Fix converter defects as general VB6 → C# rules. Never special-case the `VB6\Showcase` sample (names, lines, data).
- Unit tests call library internals directly (`InternalsVisibleTo` in the `.csproj`). Converter tests usually feed VB6 text
  to `ConvertClassSource` / `ConvertCodeLine` / `ConvertSub` and assert on the C#. `ConverterTypeTests` also compile the
  output with Roslyn against `UpgradeHelpers`.
