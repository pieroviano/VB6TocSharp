# Vb6ToCSharp (WPF)

GUI front end for the VB6 → C# converter. It is a thin shell over
[Net4x.Vb6ToCSharp.Library](../Vb6ToCSharp.Library/README.md), and
[Vb6ToCSharp.Console](../Vb6ToCSharp.Console/README.md) runs the same operations from scripts. Target: .NET Framework 4.8, WPF. Not packed.

## Windows

| Window | File | Purpose |
|---|---|---|
| `VB6 -> .NET` | [Forms/MainForm.xaml](Forms/MainForm.xaml) | Main window: project file, commands, progress bar |
| `Config - VB6 To C#` | [Forms/ConfigForm.xaml](Forms/ConfigForm.xaml) | Edits `Project File`, `Output Folder`, `Assembly Name`, which are saved to the INI |
| `Lint Project` | [Forms/LinterForm.xaml](Forms/LinterForm.xaml) | Lints one file (leave the box empty to lint the whole project); results show in the window |

## Main window commands

Before every conversion command, the app runs `ModConfig.ValidateSettings()`. If the settings are invalid, it shows the error and stops.

| Button | Library call |
|---|---|
| `Config` | Opens the config window, then `ModConfig.LoadSettings()` |
| `SCAN` | `ModRefScan.ScanRefs()`: indexes procedures, enums, forms. Run it before converting files one at a time |
| `SUPPORT` | Asks, then `ModSupportFiles.CreateProjectFile(vbp)` and/or `CreateProjectSupportFiles()` |
| `Forms` / `Modules` / `Classes` | `ModConvert.ConvertFileList(...)` on that group of the `.vbp` |
| `Single File` | `ModConvert.ConvertFile(name)`. A bare name is resolved against the project folder |
| `ALL` | `ModConvert.ConvertProject(vbp)`: scan, project + support files, every file, `MigrationReport.md` |
| `Lint` | Opens the lint window (`ModQuickLint.LintFileOrProject`) |

Progress comes from `ModUtils.Progress` (wired in [App.xaml.cs](App.xaml.cs)). Messages use the library's default message box.

## Settings

The settings are stored in `VB6toCS.INI` next to the exe, under `[Settings]`: `VBPFile`, `OutputFolder`, `AssemblyName`, `UITarget`.

- The GUI has no control for the UI target. Set `UITarget=WinForms` in the INI, or use `Vb6ToCSharp.Console config --ui winforms`. The default is WPF.
- When `OutputFolder` is not set, the output goes to `converted\` under the `.vbp` folder.
- The optional project-specific sections and `'##` pragmas are described in the
  [library README](../Vb6ToCSharp.Library/README.md#project-specific-rules).

## Build

Build as part of `Vb6ToCSharp.slnx` with Visual Studio MSBuild (see the [solution README](../README.md#build)).
