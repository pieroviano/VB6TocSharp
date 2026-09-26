# From VB6 to Windows, Linux and macOS

## The problem

Visual Basic 6 shipped in 1998 and left support in 2008. The applications did not leave. They are still
running payroll, still driving shop-floor terminals, still the only thing that knows how a particular
invoice is numbered. And they are Windows-only twice over: the VB6 runtime is Windows, and the whole
programming model — COM, ActiveX, `Printer`, twips, `Form1.Show 1` — assumes it.

So when "it has to run on Linux now" arrives, the estimate that comes back is a rewrite. Every form, every
class, every business rule, re-done and re-tested, to arrive exactly where you already were.

This article describes the other route, in two mechanical steps:

1. **Convert** the VB6 project to a C# WinForms project ([Vb6ToCSharp](../README.md)).
2. **Retarget** that project from `System.Windows.Forms` to a GTK implementation of the same API
   ([ProcessForGtk](../ProcessForGtk/) + Gtk.Windows.Forms).

```
 .vbp  ──[ Vb6ToCSharp ]──►  C# + WinForms          ──[ ProcessForGtk ]──►  C# + Gtk.Windows.Forms
 .frm                        net10.0-windows                                net10.0
 .cls                        Windows only                                   Windows · Linux · macOS
```

Nothing is rewritten by hand in either step. What you review by hand is the `// TODO:` list the converter
leaves, and the platform boundary at the end of this article.

## Step 0 — Lint the VB6 first

```bat
Vb6ToCSharp.Console lint --vbp C:\src\App\App.vbp
```

The linter reports what the converter will struggle with while it is still cheap to fix *in VB6*, where the
original compiler can still check you. Late binding, `Variant` arithmetic and missing `Option Explicit` are
the usual finds.

## Step 1 — Convert the project

```bat
Vb6ToCSharp.Console config --vbp C:\src\App\App.vbp --out C:\src\App.cs --assembly App --ui winforms
Vb6ToCSharp.Console all
```

`--ui winforms` is not a matter of taste here. The alternative, `--ui wpf`, produces XAML, and WPF exists
only on Windows — it is the right choice for a Windows-only modernisation and a dead end for this one.
**Cross-platform means WinForms.**

What comes out:

| Output | Content |
|---|---|
| `App.sln` + `App\` | Solution over the converted project (a `.vbg` group becomes one solution over several projects) |
| `App\Modules\`, `Classes\`, `Forms\`, `UserControls\` | Converted code, plus `.cs` + `.Designer.cs` + `.resx` per form |
| `App\App.csproj` | SDK-style `net10.0-windows`, referencing the runtime package |
| `MigrationReport.md` | Every remaining `// TODO:`, grouped and with `file:line` links |

The output usually needs manual fixes before it compiles. That is the honest state of any VB6 conversion:
type information in VB6 is thin, and some constructs (parameterless default members, late-bound COM calls,
`WithEvents` handler signatures) cannot be resolved from source text alone. The converter's contract is that
it never guesses silently — everything it could not do is a `// TODO:` in the code and a line in the report.

Work the report to zero before moving on. Do not start step 2 on a project that does not build on Windows.

## What carries the VB6 semantics

A converted line like `s = Mid(t, 3, 2)` has to keep meaning what VB6 meant, 1-based and all. That is the job
of the UpgradeHelpers packages, which the generated `.csproj` references and every converted file imports:

| Package | Holds |
|---|---|
| `Net4x.Vb6ToCSharp.Base.UpgradeHelpers` | The language runtime: `VB6Array<T>` with its arbitrary bounds, UDTs, fixed-length strings, OLE colors, twips |
| `Net4x.Vb6ToCSharp.WinForms.UpgradeHelpers` | The UI runtime: `FlexGrid`, `DriveListBox`/`DirListBox`/`FileListBox`, `ControlArray<T>`, `CommonDialog`, the `AddItem`/`ItemData`/`Move`-in-twips helpers |
| `Net4x.Vb6ToCSharp.WPF.UpgradeHelpers` | The same UI surface on WPF (Windows only) |

Two properties of these packages matter for what comes next:

- **There is no P/Invoke in them.** Not one `DllImport` across the base, WinForms and WPF helpers. Everything
  is managed code over the UI stack's own API — so nothing in the runtime is inherently Windows-bound.
- **They are the public API of converted code.** Their namespaces are emitted into every generated file, so
  the set of types a converted program can call is fixed and known.

## Step 2 — Retarget at GTK

Gtk.Windows.Forms re-implements `System.Windows.Forms` and `System.Drawing` over GTK 3 (through GtkSharp) and
Cairo. A WinForms application compiles against it unchanged; see its own article, *Running Windows Forms
Applications on Windows, Linux and macOS with Gtk.Windows.Forms*, for how the emulation works.

Pointing a converted project at it is three edits to the `.csproj`, and `ProcessForGtk` makes them:

```bat
ProcessForGtk C:\src\App.cs\App.sln
```

Before:

```xml
<TargetFramework>net10.0-windows</TargetFramework>
<UseWindowsForms>true</UseWindowsForms>
...
<PackageReference Include="Net4x.Vb6ToCSharp.WinForms.UpgradeHelpers" Version="1.0.*" />
```

After:

```xml
<TargetFramework>net10.0</TargetFramework>
...
<PackageReference Include="Net4x.Vb6ToCSharp.Gtk.UpgradeHelpers" Version="1.0.*" />
<PackageReference Include="Gtk.Windows.Forms.Base" Version="1.4.2464.*" />
```

Three changes, each one load-bearing:

| Change | Why |
|---|---|
| `net10.0-windows` → `net10.0` | The `-windows` platform is what forbids a Linux or macOS build in the first place |
| `UseWindowsForms` removed | That property is what makes the SDK reference the real Windows Desktop `System.Windows.Forms.dll`; without it the types resolve to Gtk.Windows.Forms instead |
| WinForms helpers → Gtk helpers | The VB6 UI runtime, compiled against the GTK substrate rather than the Windows one |

An ADODB `COMReference`, if one is still there, is swapped for the managed `Standard.AdoDb` package in the
same pass.

Point the tool at a `.sln`/`.slnx` and it rewrites every project in it; point it at one `.csproj` and it
rewrites that. A project already in that shape is reported `unchanged`, so it is safe to re-run after every
conversion — and it should be re-run, because the converter regenerates the `.csproj`.

## Why the Gtk runtime package cannot drift

`Net4x.Vb6ToCSharp.Gtk.UpgradeHelpers` is worth a paragraph, because the obvious implementation of it — fork
the WinForms helpers, port them — would rot within a month.

It carries **no source of its own**. Its
[project file](../Vb6ToCSharp.Gtk.UpgradeHelpers/Vb6ToCSharp.Gtk.UpgradeHelpers.csproj) globs the WinForms
helpers' sources and compiles them against GTK:

```xml
<Compile Include="..\Vb6ToCSharp.WinForms.UpgradeHelpers\**\*.cs" ... />
<PackageReference Include="Gtk.Windows.Forms.Base" ... />
```

Same files, same namespaces, different substrate. A helper added for Windows reaches the GTK package with no
edit, and anything GTK cannot serve does not silently diverge — it **fails the build** of the GTK package.
The compiler, rather than a discipline, keeps the two halves identical.

## Data access without COM

A VB6 application that talks to a database talks to ADO, which is COM, which is Windows. This is usually the
sharpest edge of a port, and it is handled at conversion time rather than at retarget time.

By default (`ADOTarget=Package`) the converter references the **managed** `Standard.AdoDb` package instead of
the ADODB type library, and then reads the connection strings in your source to reference the ADO.NET client
each one actually asks for:

| `Provider=` in the VB6 source | Package referenced | Crosses platforms |
|---|---|---|
| `SQLOLEDB`, `SQLNCLI*`, `MSOLEDBSQL*` | `Microsoft.Data.SqlClient` | yes |
| `MySQLProv`, `MySQL` | `MySql.Data` | yes |
| `PostgreSQL*`, `PGNP` | `Npgsql` | yes |
| `OraOLEDB.Oracle`, `MSDAORA` | `Oracle.ManagedDataAccess.Core` | yes |
| `SQLite`, `SQLiteOLEDB` | `Microsoft.Data.Sqlite` | yes |
| `Firebird`, `LCPI.IBProvider` | `FirebirdSql.Data.FirebirdClient` | yes |
| `Microsoft.Jet.OLEDB.*`, `Microsoft.ACE.OLEDB.*` | `System.Data.OleDb` | **no** — OLE DB is Windows-only |
| `MSDASQL`, `Driver=`/`DSN=` | `System.Data.Odbc` | with a native ODBC driver installed |

`rs.Open sql, cn` keeps working, and the `Recordset` underneath is managed. The one row to look for is
Jet/ACE: an application whose database *is* an `.mdb` or `.accdb` file has a data-layer problem to solve
before it has a portability story, and no conversion tool can solve it for you. The provider map is
overridable per token from the INI's `[ADOProviders]` section if your estate needs a different client.

## Step 3 — Run it

```bash
dotnet restore
dotnet build
dotnet run
```

GTK is a native dependency: the managed layer arrives by NuGet, but the toolkit has to exist on the machine
that runs the application.

| OS | What to install |
|---|---|
| Linux | GTK 3 is on any mainstream desktop distribution; on a minimal or container image install `libgtk-3-0` (Debian/Ubuntu) or `gtk3` (Fedora/openSUSE) |
| macOS | `brew install gtk+3` |
| Windows | A GTK 3 runtime |

Put this in the installer or the container image on day one. It is the one genuinely new deployment concern
compared with the VB6 original, and a miserable thing to discover on a customer's machine.

## The boundary

A compatibility layer that claims no limits will surprise you in production. These are the limits, and they
are the reason step 0 exists.

**Does not cross, by nature:**

| VB6 feature | Why |
|---|---|
| ActiveX controls (`.ocx`) and COM objects (`CreateObject`, `WithEvents` on COM) | COM is a Windows facility, and `AxHost` is Windows-only in the GTK layer too. A third-party grid or report control has to be replaced, not ported |
| `Declare Sub`/`Function Lib "user32"` | A Win32 P/Invoke is a Win32 dependency wherever it is written |
| The Windows message pump (`hWnd` arithmetic, subclassing) | `Control.Handle` on GTK is a GTK widget handle, not an `HWND`; `WndProc` throws |
| Registry, `SendKeys`, drive letters, `\` paths | Windows concepts. Portable equivalents exist, but they are edits you make |
| DDE, OLE embedding, `Printer.Print` to a Windows printer object | No cross-platform counterpart |
| Jet/ACE (`.mdb`, `.accdb`) | See above |
| WPF output (`--ui wpf`) | Windows-only stack; use `--ui winforms` |

**Crosses, with caveats worth reading in the GTK layer's own `EMULATION-GAPS.md`:** owner-draw and
`CreateGraphics`, `Form.TransparencyKey`, `WebBrowser` (WebKitGTK on Linux and macOS, WebView2 on Windows,
out-of-process either way), metafiles, and the legacy `DataGrid`/`ToolBar` families.

A useful triage rule: grep the VB6 sources for `CreateObject`, `Declare`, `.ocx` in the `.vbp`, and
`Provider=`. Those four searches find most of what will not cross, in minutes, before anyone estimates
anything.

## Verify it yourself

Be clear-eyed about what is and is not proven for *your* application:

| Proven by | What |
|---|---|
| This repository's integration tests | VB6 → C# → build → **run**, on Windows, asserting on VB6 semantics (`Showcase.vbp`, the `.vbg` group, the ADO sample) |
| The GTK layer's own suites | `System.Windows.Forms` / `System.Drawing` behaviour on GTK (`API-COMPLETENESS.md`, `EMULATION-GAPS.md`) |
| Nothing, yet | The two halves joined: there is **no** automated test in this repository that builds a Gtk-retargeted converted project, let alone runs one on Linux or macOS |

So make that your acceptance step, not an assumption. Convert, retarget, then build and run on each target OS
early, on a real machine or container, while the schedule can still absorb the answer.

## The order of work

1. `lint` the VB6 project; fix what you can in VB6.
2. Grep for `CreateObject`, `Declare`, `.ocx`, `Provider=`. Decide the replacements for what will not cross.
3. Convert with `--ui winforms`. Drive `MigrationReport.md` to zero. Build and run on Windows.
4. `ProcessForGtk` over the solution. Build.
5. Run on Linux and macOS. Fix what the boundary shows.
6. Re-run `ProcessForGtk` after every re-conversion.

The result is not a rewrite, and does not pretend to be a native port. It is the original program — the same
business rules, the same forms, the same numbering of that particular invoice — compiled for three operating
systems, with the Windows-bound parts named explicitly rather than discovered later.
