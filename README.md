# VB6 To C#

Converts a VB6 project (`.vbp`) into a C# .NET Framework 4.8 project, with WPF or WinForms forms. It follows VB Migration
Partner's conversion rules, adapted to C#. Anything it cannot convert is marked with a `// TODO:` comment and listed in `MigrationReport.md`.

## Solution (`Vb6ToCSharp.slnx`)

| Project | Type | Package | Role |
|---|---|---|---|
| [Vb6ToCSharp.Library](Vb6ToCSharp.Library/README.md) | Library | `Net4x.Vb6ToCSharp.Library` | Converter engine: parsing, statement/type conversion, `.frm` → XAML / WinForms, `.csproj`, migration report, linter |
| [Vb6ToCSharp.UpgradeHelpers](Vb6ToCSharp.UpgradeHelpers/README.md) | Library | `Net4x.Vb6ToCSharp.UpgradeHelpers` | Runtime used by converted code (`VB6Array<T>`, UDTs, fixed strings, control arrays, FlexGrid, CommonDialog…) |
| [Extras](extras/README.md) | Library | `Net4x.Extras` | Optional helpers for converted code (`Recordset`, `FixedWidthRecord`, `CsvRecord`) |
| [Vb6ToCSharp](Vb6ToCSharp/Vb6ToCSharp.csproj) | WPF app | — | GUI: `Config`, `SCAN`, `SUPPORT`, `Forms` / `Modules` / `Classes`, `Single File`, `ALL`, `Lint` |
| [Vb6ToCSharp.Console](Vb6ToCSharp.Console/README.md) | Console app | — | Scriptable front end: `all`, `file`, `scan`, `support`, `lint`, `config` |
| Vb6ToCSharp.Tests | xUnit | — | Converter unit, functional and integration tests |
| Vb6ToCSharp.UpgradeHelpers.Tests | xUnit | — | Runtime tests (WinForms, WPF) |
| `VB6/` | Solution folder | — | Sample VB6 project `Showcase.vbp` (modules, classes, interface, events, form, `CondComp`) |

All projects target `net48`. Packages are written to `Packages\` on build. Version: `Vb6ToCSharpVersion` in
[Directory.Nuget.Props](Directory.Nuget.Props) + `yyDDD` build suffix.

## Build

| Step | Command |
|---|---|
| Build | `MSBuild.exe Vb6ToCSharp.slnx -restore` (Visual Studio MSBuild) |
| Test | `dotnet test Vb6ToCSharp.slnx --no-build` (or Test Explorer) |

`dotnet build` fails with MSB4803 because the library uses an ADODB `COMReference`. Build with Visual Studio's MSBuild.

## Quick start

```bat
Vb6ToCSharp.Console config --vbp C:\src\App\App.vbp --out C:\src\App.cs --assembly App --ui winforms
Vb6ToCSharp.Console all
```

Or run `Vb6ToCSharp.exe`, set the project under `Config`, then click `ALL`.

| Output | Content |
|---|---|
| `Modules\`, `Classes\`, `Forms\`, `UserControls\` | Converted code (WPF: `.xaml` + `.xaml.cs`; WinForms: `.cs` + `.Designer.cs` + `.resx`) |
| `<project>.csproj` | SDK-style `net48` project that references `Net4x.Vb6ToCSharp.UpgradeHelpers` |
| `MigrationReport.md` | Remaining `TODO`s, grouped by category and file, with `file:line` links |

When no output folder is set, the output goes to `converted\` under the `.vbp` folder. Files marked `### CONVERTED` are not overwritten.

## Settings

| Source | Content |
|---|---|
| `VB6toCS.INI` (next to the exe, or `--ini`) | `[Settings]`: `VBPFile`, `OutputFolder`, `AssemblyName`, `UITarget` |
| Same INI, optional sections | `[DataTypes]`, `[Controls]`, `[WinFormsControls]`, `[FormRenames]`, `[PostCodeLine]`; see the [library README](Vb6ToCSharp.Library/README.md#project-specific-rules) |
| `'##` pragmas in the sources or `VBMigrationPartner.pragmas` | Per file / per project conversion options; see the [library README](Vb6ToCSharp.Library/README.md#pragmas) |

[VB6toCS.WinCDS.sample.ini](Vb6ToCSharp.Library/VB6toCS.WinCDS.sample.ini) is an example.

## Integration test

`IntegrationTests` (trait `Category=Integration`) uses the console to convert `VB6\Showcase.vbp` into `Converted\` (ignored
by git). It then builds the result and runs the converted program.

## Limits

- The output usually needs manual fixes before it compiles. Review every `// TODO:` (all are listed in `MigrationReport.md`).
- Type information comes only from declarations and from the `.vbp`'s classes, UDTs and globals. Parameterless default members
  (`Text1 = "x"`) and some late-bound calls need a manual fix.
- Handler signatures for events raised by COM objects (`WithEvents`) are flagged for review.

## License

MIT, see [LICENSE.txt](LICENSE.txt).
