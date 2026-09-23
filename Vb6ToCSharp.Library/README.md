# Net4x.Vb6ToCSharp.Library

The VB6 → C# converter engine used by `Vb6ToCSharp.exe` (WPF) and `Vb6ToCSharp.Console`. It reads a `.vbp` and writes a
C# project (WPF or WinForms forms). The conversion follows VB Migration Partner's rules, adapted to C#. Target: `net10.0-windows`.

Converted code runs on the `Net4x.Vb6ToCSharp.UpgradeHelpers` package, which the generated `.csproj` references.

## Use

```csharp
using Vb6ToCSharp.CodeConversion;
using Vb6ToCSharp.CodeConversion.Model;   // UiTarget
using Vb6ToCSharp.CodeGeneration;
using Vb6ToCSharp.Linting;
using Vb6ToCSharp.Parsing;

ConversionUtility.Notify = Console.WriteLine;                    // default: a message box
ConversionUtility.Progress = (value, max, caption) => { };        // default: none
ProjectConfigurationParser.IniFilePath = @"C:\conv\VB6toCS.INI";          // default: VB6toCS.INI next to the exe
ProjectConfigurationParser.OverrideSettings(@"C:\src\App\App.vbp", @"C:\src\App.cs", "App", UiTarget.WinForms);

var error = ProjectConfigurationParser.ValidateSettings();                // "" when the settings are usable
if (error == "") CodeConverter.ConvertProject(ProjectConfigurationParser.VbpFile);
```

| Entry point | Does |
|---|---|
| `CodeConverter.ConvertProject(vbp)` | Scan, `.csproj` + support files, every file, `MigrationReport.md` |
| `CodeConverter.ConvertFile(path)` | One `.bas` / `.cls` / `.frm` / `.ctl`; `false` if not converted |
| `CodeConverter.ConvertClassSource(source)` | A class module's source → C# text (no file I/O) |
| `RefScanner.ScanRefs()` | Index the project's procedures, enums, forms (needed before converting files one by one) |
| `SupportFiles.CreateProjectFile(vbp)` / `CreateProjectSupportFiles()` | Generated project scaffolding |
| `QuickLint.LintFileOrProject(path)` | Lint results (`""` when clean) |
| `MigrationReport.Write()` | (Re)write the report from the output folder |

## What is converted

| Area | Result |
|---|---|
| Types | `Integer`→`short`, `Long`→`int`, `Single`→`float`, `Double`→`double`, `Currency`→`decimal`, `Variant`→`object`, `Object`→`dynamic`, `Null`→`DBNull.Value` |
| Implicit conversions | Assignments and arguments convert as VB6 does (`Conversions.ToShort`...: rounding to even, string parsing); `/` divides as `Double`; `CInt`/`CLng`/`CStr`... → `Conversions` |
| Arrays | `T[]` / `T[,]`; non-zero lower bounds and `Option Base 1` → `VB6Array<T>`; `ReDim [Preserve]`, `Erase` |
| UDTs | `struct : IVbStruct` (value semantics), `Initialize()` for strings / fixed arrays, `MarshalAs` for API calls |
| Strings | `String * n` keeps its length; `Mid`/`LSet`/`RSet` statements; `Option Compare Text` comparisons |
| Control flow | All VB6 statements: `Select Case` (`Is`, `To`), `Do`/`While`/`For [Each]`, `Exit` out of nested blocks, `GoTo`, line numbers, `On … GoTo`, `GoSub` (local functions) |
| Error handling | `On Error Resume Next` (per statement), `On Error GoTo` (try/catch), `Resume`/`Resume Next`/`Resume label`, `Err`, `Erl` |
| Declarations | `Dim` hoisted (procedure scope), `Static` locals → fields, `DefType`, implicit variables without `Option Explicit`, `Declare` → `DllImport` |
| Classes | `Class_Initialize`/`Terminate` → constructor / `IDisposable`; `Implements` → interfaces; default member → indexer; `NewEnum` → `IEnumerable`; `VB_PredeclaredId`; `WithEvents`; properties with parameters |
| Conditional compilation | `#If`/`#Else`; `#Const` → `#define`; `.vbp` `CondComp` → `DefineConstants` |
| File I/O | `Open`/`Print #`/`Input #`/`Get`/`Put`... → `Microsoft.VisualBasic.FileSystem` |

Whatever the converter cannot settle becomes a `// TODO:` comment, and `MigrationReport.md` lists these comments.

## Pragmas

VB Migration Partner `'##` pragmas steer the conversion. Put them in a source file (before the first procedure for the whole
file, or inside a procedure from that line on), or in `VBMigrationPartner.pragmas` next to the `.vbp` for the whole project.

| Pragma | Effect |
|---|---|
| `ArrayBounds Unchanged\|ForceZero\|Shift\|VB6Array` | Arrays with a lower bound: `VB6Array<T>` or zero-based |
| `AutoNew True\|False` | Module-level `As New`: auto-instancing property or eager `new` |
| `AutoDispose No\|Yes\|Force` | `Set x = Nothing` disposes (classes with `Class_Terminate` / all objects) |
| `name.SetType <VB type>` | Forces a variable's type |
| `PreProcess "regex", "repl"` / `PostProcess "regex", "repl"` | Rewrites the VB6 source / the generated C# |
| `InsertStatement <code>` / `ReplaceStatement <code>` | Adds C# code / replaces the next statement |
| `OutputMode Off\|On`, `ParseMode Off\|On` | Leaves code out / keeps it as VB6 comments |
| `Note <text>` | `// NOTE:` in the output |

## Project-specific rules

INI sections that tailor the conversion to a project:

| Section | Entry | Effect |
|---|---|---|
| `[DataTypes]` | `VBType=CSharpType` | Overrides / extends the type mapping |
| `[Controls]` | `Lib.Ctl=WpfType[;container 0/1;default prop;features]` | WPF control mapping |
| `[WinFormsControls]` | `Lib.Ctl=WinFormsType[;container 0/1;default prop]` | WinForms control mapping |
| `[FormRenames]` | `frmOld.frm=frmNew` | Renames a form listed in the `.vbp` |
| `[PostCodeLine]` | `n=replace\|find\|repl`, `n=ifcontains\|trigger\|find\|repl`, `n=regex\|pattern\|repl`, `n=blankif\|trigger` | Rewrites every converted line, in order |

`Vb6ToCSharp.sample.ini` is an example.

## Limits

- The converter works line by line with the type information it can collect: the declared types, the `.vbp`'s classes, UDTs and
  public globals. Default members without parameters (`Text1 = "x"`) and some late-bound calls need a manual fix.
- Handler signatures of events raised by COM objects (`WithEvents`) are flagged for review.
