# A single .vbp converts to a solution, like a group

Today a lone `.vbp` writes its project flat into the output folder; only a `.vbg` gets a `.sln` and a folder per
project. The two layouts become one.

| | Before | After |
|---|---|---|
| `.vbg` | `<out>\Group.sln`, `<out>\<Name>\<vbp>.csproj` | unchanged |
| `.vbp` | `<out>\<vbp>.csproj`, `<out>\Modules\`… | `<out>\<vbp>.sln`, `<out>\<vbp>\<vbp>.csproj`, `<out>\<vbp>\Modules\`… |

## Decisions taken

| Decision | Consequence |
|---|---|
| Always, no setting | one layout to support; every path a caller knows changes, including the integration tests |
| Folder and `.sln` named after the `.csproj` (= the `.vbp` file name) | independent of `Name=` and of `--assembly`, which keep setting only `AssemblyName`/`RootNamespace`. Differs from a group, where the folder is the `.vbp`'s `Name=` |
| `MigrationReport.md` stays at the conversion root | one report beside the `.sln`; its links gain the project folder, because it is collected from the root |
| A previous flat conversion is moved into the project folder | re-converting an existing output folder leaves no half project behind |

## Changes

### `ProjectGroup` — the solution writer serves one project too

[ProjectGroup.cs](../Vb6ToCSharp.Library/CodeGeneration/ProjectGroup.cs)

- `SolutionFile` splits: a core taking `(display name, path relative to the .sln)` pairs, and the existing
  `SolutionFile(groupName, projects)` on top of it. `ProjectGuid` likewise takes a name instead of a `ProjectInfo`.
- New `ConvertProject(vbpFile)`, the counterpart of `ConvertGroup`: moves any flat conversion aside, converts into
  `<out>\<name>\`, writes `<out>\<name>.sln`, returns its path.
- New `MoveFlatConversion(root, folder, name)`: moves `<name>.csproj`, `Program.cs`, `AdoConstants.cs` and the
  folders `Modules`, `Classes`, `Forms`, `UserControls`, `Properties` into the project folder, merging into what is
  already there. `MigrationReport.md`, `NuGet.config` and anything else stay put; `bin`/`obj` stay too, because a
  moved `obj` breaks the next build.

### `CodeConverter` — dispatch and the report's folder

[CodeConverter.cs](../Vb6ToCSharp.Library/CodeConversion/CodeConverter.cs)

- `ConvertProject` sends a non-group `.vbp` to `ProjectGroup.ConvertProject`.
- `ConvertSingleProject(vbpFile, reportFolder = null)`: `MigrationReport.Write(reportFolder)`. Null keeps a group
  project's report in its own folder; the lone project passes the root, so the report is collected from there and
  its links read `<name>/Modules/…`.

### `ProjectConfigurationParser` — keep the assembly name across the scope

[ProjectConfigurationParser.cs:100](../Vb6ToCSharp.Library/Parsing/ProjectConfigurationParser.cs#L100)

`ProjectScope(vbpFile, outputFolder, assemblyName = null)` now keeps the current override when no name is given
(`assemblyName ?? oAssemblyName`). Without this, entering the scope would clear `--assembly`, because
`OverrideSettings` assigns every override and reloads.

## Verification

| Step | How |
|---|---|
| unit tests | `SolutionFile` for one project; `MoveFlatConversion` over a built flat folder |
| the three integration tests | new paths: `<out>\<name>\<name>.csproj`, `<out>\<name>.sln`, `bin` under the project folder |
| the group is untouched | `ConvertedGroup\Group.sln` and its two project folders unchanged |
| a re-conversion over the old layout | convert twice into one folder: the second run finds nothing left at the root |

## Risks

| Risk | Mitigation |
|---|---|
| every caller's output path changes | the answer was "always"; README and both front ends' output text updated |
| the move touches files in the user's output folder | only the names the converter itself writes; nothing is deleted, and `MigrationReport.md` / `NuGet.config` are left alone |
| a `.vbp` whose file name is not a valid folder name | the `.csproj` already carries that name, so nothing new breaks |
