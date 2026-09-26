# VB6 To C#

Converts a VB6 project (`.vbp`) into a C# .NET 10 project, with WPF or WinForms forms. It follows VB Migration
Partner's conversion rules, adapted to C#. Anything it cannot convert is marked with a `// TODO:` comment and listed in `MigrationReport.md`.

## Solution (`Vb6ToCSharp.slnx`)

| Project | Type | Package | Role |
|---|---|---|---|
| [Vb6ToCSharp.Library](Vb6ToCSharp.Library/README.md) | Library | `Net4x.Vb6ToCSharp.Library` | Converter engine: parsing, statement/type conversion, `.frm` → XAML / WinForms, `.csproj`, migration report, linter |
| [Vb6ToCSharp.Base.UpgradeHelpers](Vb6ToCSharp.Base.UpgradeHelpers/README.md) | Library | `Net4x.Vb6ToCSharp.Base.UpgradeHelpers` | VB6 language runtime used by converted code (`VB6Array<T>`, UDTs, fixed strings, control arrays, OLE colors, twips); no UI |
| [Vb6ToCSharp.WinForms.UpgradeHelpers](Vb6ToCSharp.WinForms.UpgradeHelpers/README.md) | Library | `Net4x.Vb6ToCSharp.WinForms.UpgradeHelpers` | WinForms controls and helpers (`FlexGrid`, file-system boxes, `CommonDialog`…) |
| [Vb6ToCSharp.WPF.UpgradeHelpers](Vb6ToCSharp.WPF.UpgradeHelpers/README.md) | Library | `Net4x.Vb6ToCSharp.WPF.UpgradeHelpers` | WPF controls and helpers (same surface, WPF stack) |
| [Vb6ToCSharp.Gtk.UpgradeHelpers](Vb6ToCSharp.Gtk.UpgradeHelpers/README.md) | Library | `Net4x.Vb6ToCSharp.Gtk.UpgradeHelpers` | The WinForms helpers' own sources, compiled against Gtk.Windows.Forms: `net10.0`, runs on Windows, Linux and macOS |
| [Vb6ToCSharp](Vb6ToCSharp/README.md) | WPF app | — | GUI: `Config`, `SCAN`, `SUPPORT`, `Forms` / `Modules` / `Classes`, `Single File`, `ALL`, `Lint` |
| [Vb6ToCSharp.Console](Vb6ToCSharp.Console/README.md) | Console app | — | Scriptable front end: `all`, `file`, `scan`, `support`, `lint`, `config` |
| [ProcessForGtk](ProcessForGtk/) | Console app | — | Retargets a converted project or solution at Gtk: drops `-windows` and `UseWindowsForms`, swaps the WinForms helpers for the Gtk ones |
| Vb6ToCSharp.Tests | xUnit | — | Converter unit, functional and integration tests |
| Vb6ToCSharp.Base.UpgradeHelpers.Tests | xUnit | — | Language runtime tests |
| Vb6ToCSharp.WinForms.UpgradeHelpers.Tests / .WPF. | xUnit | — | Runtime tests per UI stack |
| `VB6/` | Solution folder | — | Sample VB6 project `Showcase.vbp` (modules, classes, interface, events, form, `CondComp`) |

All projects target `net10.0-windows`, except `Vb6ToCSharp.Gtk.UpgradeHelpers` and `ProcessForGtk`, which are `net10.0`.
Packages are written to `Packages\` on build. Version: `Vb6ToCSharpVersion` in
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

The output is a solution with the project in a folder of its own, whether one `.vbp` or a whole `.vbg` was converted:
`<out>\<project>.sln` (`<group>.sln` for a `.vbg`) with `<out>\<project>\` beside it. A project converted on its own
takes the name of its `.csproj`, i.e. of the `.vbp` file; a project of a group takes the `.vbp`'s `Name`.

| Output | Content |
|---|---|
| `<project>.sln` | Visual Studio solution over the converted project(s) |
| `<project>\Modules\`, `…\Classes\`, `…\Forms\`, `…\UserControls\` | Converted code (WPF: `.xaml` + `.xaml.cs`; WinForms: `.cs` + `.Designer.cs` + `.resx`) |
| `<project>\<project>.csproj` | SDK-style `net10.0-windows` project that references `Net4x.Vb6ToCSharp.WinForms.UpgradeHelpers` or `…WPF…` (the base package comes with it); a project that used ADO also references `Standard.AdoDb` and the ADO.NET client its connection strings name (`Provider=MSOLEDBSQL` → `Microsoft.Data.SqlClient`) |
| `MigrationReport.md` | Remaining `TODO`s, grouped by category and file, with `file:line` links; beside the `.sln` for one project, in each project's folder for a group |

When no output folder is set, the output goes to `converted\` under the `.vbp` folder. Files marked `### CONVERTED` are not
overwritten. An output folder written before the project had a folder of its own has its project moved into the new
subfolder on the next conversion.

## Settings

| Source | Content |
|---|---|
| `VB6toCS.INI` (next to the exe, or `--ini`) | `[Settings]`: `VBPFile`, `OutputFolder`, `AssemblyName`, `UITarget`, `ADOTarget`, `DBProvider` |
| Same INI, optional sections | `[DataTypes]`, `[Controls]`, `[WinFormsControls]`, `[FormRenames]`, `[PostCodeLine]`, `[ADOProviders]`; see the [library README](Vb6ToCSharp.Library/README.md#project-specific-rules) |
| `'##` pragmas in the sources or `VBMigrationPartner.pragmas` | Per file / per project conversion options; see the [library README](Vb6ToCSharp.Library/README.md#pragmas) |

[Vb6ToCSharp.sample.ini](Vb6ToCSharp.Library/Vb6ToCSharp.sample.ini) is an example.

## Integration test

`IntegrationTests` (trait `Category=Integration`) uses the console to convert `VB6\Showcase.vbp` into `Converted\` (ignored
by git). It then builds the result and runs the converted code (`modMain.RunAll`), checking results that depend on VB6 semantics.
A second test does the same for the project group `VBG\Group.vbg` (an EXE referencing an ActiveX DLL): it converts the group
into `ConvertedGroup\`, builds `Group.sln` and runs the EXE's code and form.

## Running off Windows

A converted WinForms project can be retargeted at [Gtk.Windows.Forms](https://www.nuget.org/packages/Gtk.Windows.Forms.Base),
which re-implements `System.Windows.Forms` and `System.Drawing` over GTK 3, so the program runs on Windows, Linux
and macOS:

```bat
Vb6ToCSharp.Console all --vbp C:\src\App\App.vbp --out C:\src\App.cs --assembly App --ui winforms
ProcessForGtk C:\src\App.cs\App.sln
```

`--ui winforms` is required for this route — WPF is Windows-only. Re-run `ProcessForGtk` after every conversion,
since the `.csproj` is regenerated; a project already retargeted is reported `unchanged`. GTK 3 must be installed
on the machine that runs the program. COM/ActiveX, `Declare Lib`, `hWnd` work and Jet/ACE databases do not cross.

[Resources/Vb6ToCSharp.Article.md](Resources/Vb6ToCSharp.Article.md) walks the whole route and states the boundary.

## Limits

- The output usually needs manual fixes before it compiles. Review every `// TODO:` (all are listed in `MigrationReport.md`).
- Type information comes only from declarations and from the `.vbp`'s classes, UDTs and globals. Parameterless default members
  (`Text1 = "x"`) and some late-bound calls need a manual fix.
- Handler signatures for events raised by COM objects (`WithEvents`) are flagged for review.

## License

MIT, see [LICENSE.txt](LICENSE.txt).
