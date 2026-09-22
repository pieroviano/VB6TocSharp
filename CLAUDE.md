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
  VB6 feature, extend the `VB6\` sample and these assertions.
- Test parallelization is disabled ([AssemblyInfo.cs](Vb6ToCSharp.Tests/AssemblyInfo.cs)): the converter uses
  process-wide static state.

## Architecture

- **Library ([Vb6ToCSharp.Library](Vb6ToCSharp.Library/))** is the engine. The WPF app and the console are thin front ends
  that call the same static entry points (`ModConvert.ConvertProject/ConvertFile/ConvertFileList`, `ModRefScan.ScanRefs`,
  `ModSupportFiles.*`, `ModQuickLint.LintFileOrProject`).
- **The converter itself was machine-converted from VB6.** Code lives in static `Mod*` classes with mutable static state.
  It leans heavily on `using static Microsoft.VisualBasic.*` (`Mid`, `InStr`, `Split`...) and on VB-style string handling.
  Match this style when editing. `VbExtension.cs`/`VbConstants.cs` are shims for that code, not for converted output.
  `ModTestCases.cs`/`FormTest.cs` are conversion fixtures left over from self-conversion, not runnable tests.
- **State and config.** `ModConfig` loads `VB6toCS.INI` (`IniFilePath`, `OverrideSettings`, `UiTarget`). Front ends inject
  UI through the delegates `ModUtils.Notify` / `ModUtils.Progress`. The library must never show UI directly.
- **Pipeline, per file (text-based, line by line):**
  1. `ModRefScan.ScanRefs()` indexes the project's procedures, enums, forms, UDTs and globals. The conversion needs this
     index to tell a call without parentheses from a variable.
  2. `ModConvertPragmas.PreProcess` applies `'##` pragmas and `VBMigrationPartner.pragmas`.
  3. `ModConvertStatements.BeginFile` sets up per-file options (`Option Base/Compare/Explicit`, `DefType`) and
     `#If`/`#Const` handling.
  4. `ModConvert`: `ConvertGlobals` handles declarations, `ConvertCodeSegment` → `ConvertSub` → `ConvertCodeLine` →
     `ConvertElement`/`ConvertValue` handle procedures. Statement-level rules (error handling, `ReDim`, file I/O, implicit
     conversions, `Select Case`) are in `ModConvertStatements`. Type and control mappings are in `ModVb6ToCs`.
     Class semantics are in `ModConvertClasses`. Per-procedure variable/property tracking is in `ModSubTracking`.
  5. `ModProjectSpecific` / `PostConvertCodeLine` apply the INI `[PostCodeLine]` rules. `PostProcess` applies the pragmas.
     `WriteOut` writes the file, skipping files marked `### CONVERTED`.
- **Forms ([Forms/](Vb6ToCSharp.Library/Forms/), namespace `Vb6ToCSharp.FormConversion`).** `FrmParser`/`FrxReader`
  parse `.frm`/`.ctl` + `.frx` into `VbFormFile`. `ControlCatalog` and `EventCatalog` map VB6 controls and events to
  .NET (both overridable from the INI). `WpfEmitter` (XAML) or `WinFormsEmitter` (`Designer.cs` + `.resx` via `ResxWriter`)
  emits the designer. `FormContext` shares designer facts with the code conversion, e.g. `EventAdapters`, which bridge
  .NET handler signatures to the VB6-signature handlers.
- **Output support.** `ModSupportFiles` writes the `.csproj`, `Program.cs` and `AssemblyInfo.cs`.
  `ModMigrationReport.Write()` collects every `// TODO:` in the output into `MigrationReport.md`.
- **Runtime ([Vb6ToCSharp.UpgradeHelpers](Vb6ToCSharp.UpgradeHelpers/)).** This is what converted code calls (`VbRuntime`,
  `VB6Array<T>`, `IVbStruct`, control arrays, FlexGrid, CommonDialog), with parallel `WinForms/` and `Wpf/` namespaces.
  VB6 semantics that can't be expressed inline belong here, not in generated boilerplate. A new runtime API needs matching
  emission in the library.
- **Extras** is an independent optional package (`Recordset`, `FixedWidthRecord`, `CsvRecord`). The converter doesn't use it.

## Conventions

- Fix converter defects as general VB6 → C# rules. Never special-case the `VB6\Showcase` sample (names, lines, data).
- Unit tests call library internals directly (`InternalsVisibleTo` in the `.csproj`). Converter tests usually feed VB6 text
  to `ConvertClassSource` / `ConvertCodeLine` / `ConvertSub` and assert on the C#. `ConverterTypeTests` also compile the
  output with Roslyn against `UpgradeHelpers`.
