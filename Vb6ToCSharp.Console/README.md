# Vb6ToCSharp.Console

Command-line front end of the VB6 → C# converter: the same operations as the WPF window (`Vb6ToCSharp.exe`), scriptable.
Built on [Net4x.Vb6ToCSharp.Library](../Vb6ToCSharp.Library/README.md). Target: `net10.0-windows`.

## Usage

```
Vb6ToCSharp.Console <command> [argument] [options]
```

| Command | Does |
|---|---|
| `all` | Scan references, generate project + support files, convert every file, write `MigrationReport.md` |
| `forms` / `modules` / `classes` / `usercontrols` | Convert that group of the `.vbp` |
| `file <file>` | Convert one `.bas` / `.cls` / `.frm` / `.ctl` (a bare name is taken from the project folder) |
| `scan` | Scan the project's references (procedures, enums, forms) |
| `support [project\|files]` | Generate the `.csproj` and/or the support files (default: both) |
| `lint [file]` | Lint one file, or the whole project |
| `config` | Show the settings; with the options below, save them to the INI |
| `help` | Show the usage |

| Option | Meaning |
|---|---|
| `--ini <file>` | Settings file (default: `VB6toCS.INI` next to the exe) |
| `--vbp <file>` | VB6 project (overrides the INI for this run) |
| `--out <folder>` | Output folder (overrides the INI; default `converted\` under the `.vbp` folder) |
| `--assembly <name>` | Assembly / root namespace of the converted project |
| `--ui <wpf\|winforms>` | UI of converted forms (default WPF) |
| `--quiet` | No progress output |

Exit codes: `0` success, `1` conversion/validation failed, `2` usage error. Messages go to stdout, progress and errors to stderr.

## Examples

```bat
rem save the settings once, then convert everything
Vb6ToCSharp.Console config --vbp C:\src\Billing\Billing.vbp --out C:\src\Billing.cs --assembly Billing --ui winforms
Vb6ToCSharp.Console all

rem one-off run with another project, without touching the INI
Vb6ToCSharp.Console all --vbp C:\src\Tools\Tools.vbp --out C:\out\Tools --quiet

rem re-convert one module
Vb6ToCSharp.Console file modMain.bas
```

## Output

| Folder / file | Content |
|---|---|
| `Modules\`, `Classes\`, `Forms\`, `UserControls\` | Converted code (forms: `.xaml` + `.xaml.cs`, or `.cs` + `.Designer.cs` + `.resx`) |
| `<project>.csproj` | `net10.0-windows` project referencing `Net4x.Vb6ToCSharp.WinForms.UpgradeHelpers` or `…WPF…`, per `--ui` |
| `MigrationReport.md` | Items left to review, by category and file, with `file:line` links |

A file already converted and marked `### CONVERTED` is not overwritten.

## Settings

The INI's `[Settings]` section holds `VBPFile`, `OutputFolder`, `AssemblyName`, `UITarget`. Optional sections tailor the
conversion to a project (`[DataTypes]`, `[Controls]`, `[WinFormsControls]`, `[FormRenames]`, `[PostCodeLine]`): see the
[library README](../Vb6ToCSharp.Library/README.md#project-specific-rules). VB Migration Partner `'##` pragmas in the
sources, or in `VBMigrationPartner.pragmas` next to the `.vbp`, are honoured.
