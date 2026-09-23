# Retarget: converter core to `net10.0`, Windows-only code to its own package — plan

Goal: `Vb6ToCSharp.Library` stops being a desktop project (`UseWPF`/`UseWindowsForms`/ADODB force `net48`).
Everything Windows-only moves to a new `net10.0-windows` package; exe, app, tests and **converted projects**
target `net10.0-windows`.

## Blocking checks (before coding)

| # | Check | Result |
|---|---|---|
| 1 | `netstandard2.0` VB runtime surface | ⬜ **moot since [79b1fcb]**: the core no longer references `Microsoft.VisualBasic` at all. Its VB6 semantics are plain C# in `Vb6ToCSharp.Library/Runtime/Vb*.cs` (+ `Model/VbCollection`), pinned by `Vb6ToCSharp.Tests/Runtime/Vb*Tests.cs`. `netstandard2.0` is therefore open again, and decision 1 is free of this constraint |
| 2 | `net10.0` VB runtime surface | ⬜ moot, same reason. `VbFileSystem.Dir` is `System.IO`, so the `CA1416` note is gone too. What remains Windows-only in the shim is `VbInteraction.MsgBox` (WPF `MessageBox`, one caller: `GitInteraction.gitPull`) — it moves with the WPF half under decision 2, or goes through `ConversionUtility.Notify` |
| 3 | ADODB `COMReference` used by library code? | ✔ **no** — only emitted as text ([Vb6ToCsConverter.cs:120](../Vb6ToCSharp.Library/CodeConversion/Vb6ToCsConverter.cs#L120), [SupportFiles.cs:79](../Vb6ToCSharp.Library/CodeGeneration/SupportFiles.cs#L79)). Removed it and both VS MSBuild and `dotnet build` succeed (this also removes the MSB4803 note in [CLAUDE.md](../CLAUDE.md)) |
| 4 | `VisualBasic.PowerPacks.Vs` on `net10.0-windows` | ❌ package ships a bare `lib/*.dll` (.NET Framework). The `Printer` members that need it have **no callers** → dropped (decision 5) |
| 5 | .NET 10 SDK + `net10.0-windows` targeting pack | ✔ 10.0.301, probe project builds |
| 6 | **Typed `.resx` type string** resolvable on `net10.0-windows`: `System.Drawing.Bitmap, System.Drawing` vs `…, System.Drawing.Common` | ⬜ **do first**: emit one of each, build a converted WinForms project carrying an icon + a picture, load the resource. Decides a constant in `ResxWriter` |
| 7 | `Net4x.NuGetUtility` restores/packs under `net10.0(-windows)` | ⬜ verify on the first build; fallback = plain `dotnet pack` properties |

## Decisions

| # | Decision | Consequence |
|---|---|---|
| 1 | Core `Vb6ToCSharp.Library` → **`net10.0`** (not `netstandard2.0`, per check 1) | Zero churn in the machine-converted code; no .NET Framework / netstandard consumer any more (nothing in the solution needs one) |
| 2 | New project **`Vb6ToCSharp.Library.Windows`** (`net10.0-windows`, packed as `Net4x.Vb6ToCSharp.Library.Windows`) | Holds the WPF shim, `ScreenMetrics`, `Timer`, `UI/*`. The app and tests reference it |
| 3 | `ResxWriter` stays in the core and **emits the `.resx` XML directly** (base64 FRX bytes + TypeConverter mimetype) | No `System.Drawing` in the core, no `BinaryFormatter`; the WinForms emitter stays whole. Format verified by check 6 |
| 4 | `Vb6ToCSharp.UpgradeHelpers` → **`net10.0-windows` only** | Projects converted earlier (`net48`) cannot take a newer helpers version; they must be retargeted |
| 5 | The PowerPacks `Printer` members of `RuntimeExtension` are **deleted** (no callers, package is net4x-only) | ✔ **done in [79b1fcb]**, together with `PrinterName`/`ResetPrinters` and the `VisualBasic.PowerPacks.Vs` reference; `PackageImage`/`getImage` keep working (WPF `BitmapImage`) |
| 6 | Package ids keep the **`Net4x.`** prefix | No change to `NuGet.Config`, the `Packages\` feed, the emitted `PackageReference` or the READMEs |
| 7 | `Extras` stays `netstandard2.0` | Not part of this pass; PowerPacks is gone from it (the two `Strings.Left/Right` calls are plain string code, the `System.Windows.Forms` using was dead); it is referenced by nothing |

## Target layout

| Project | Now | After | Contains |
|---|---|---|---|
| `Vb6ToCSharp.Library` | `net48`, WindowsDesktop SDK, WPF+WinForms+ADODB | **`net10.0`**, `Microsoft.NET.Sdk` | parsing, code conversion, form conversion, code generation, linting, infrastructure, the non-UI half of `Runtime` |
| `Vb6ToCSharp.Library.Windows` *(new)* | — | **`net10.0-windows`**, `UseWPF` | `Runtime/RuntimeExtension.cs` (WPF half), `Runtime/ScreenMetrics.cs`, `Runtime/Timer.cs`, `UI/*` |
| `Vb6ToCSharp.UpgradeHelpers` | `net48` | **`net10.0-windows`**, `UseWPF`+`UseWindowsForms` | unchanged |
| `Vb6ToCSharp` (WPF app) | `net48` | **`net10.0-windows`** | references both libraries |
| `Vb6ToCSharp.Console` | `net48` | **`net10.0-windows`** | core only |
| `Vb6ToCSharp.Tests` | `net48` | **`net10.0-windows`** | core + helpers + console |
| `Vb6ToCSharp.Library.Windows.Tests` *(new)* | — | **`net10.0-windows`** | the moved tests (repo rule: one test project per project) |
| `Extras` | `net48` | `net48` | untouched |
| converted projects | `net48` | **`net10.0-windows`** | emitted by `SupportFiles` |

Namespaces: the new project uses root namespace `Vb6ToCSharp.Windows`, folders mirrored 1:1 →
`Vb6ToCSharp.Windows.Runtime.RuntimeExtension`, `Vb6ToCSharp.Windows.UI.*` (no collision with the core's
`Vb6ToCSharp.Runtime.RuntimeExtension`, which keeps its name and its ~80 non-UI members).

## Changes by file

### 1. `Vb6ToCSharp.Library.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">              <!-- was Microsoft.NET.Sdk.WindowsDesktop -->
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>   <!-- was net48 -->
        <!-- UseWPF / UseWindowsForms removed -->
        <NoWarn>$(NoWarn);CA1416</NoWarn>            <!-- FileSystem.Dir is Windows-only by attribute -->
        …unchanged…
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Net4x.NuGetUtility" Version="$(NuGetUtilityVersion)" PrivateAssets="All" />
        <PackageReference Include="System.Text.Encoding.CodePages" Version="8.0.0" />   <!-- §4 -->
    </ItemGroup>
    <!-- the whole <Reference …/> group is gone: Microsoft.CSharp, System.Data.DataSetExtensions,
         System.Net.Http, System.Xaml, WindowsBase, PresentationCore, PresentationFramework — all either in
         the shared framework or no longer used. The Microsoft.VisualBasic(.Compatibility[.Data]) references
         and VisualBasic.PowerPacks.Vs are already gone (decision 5, [79b1fcb]) -->
    <!-- the ADODB <COMReference> group is gone (check 3) -->
</Project>
```

### 2. Code that moves

| From | Lines | To |
|---|---|---|
| [Runtime/RuntimeExtension.cs](../Vb6ToCSharp.Library/Runtime/RuntimeExtension.cs) | ~140 of its 220 members (WPF/`Printer`-typed, e.g. `Shift` :87, `AddItem` :90-160, `CenterInScreen` :235, `Controls`/`ControlOf` :277-371, `GetCell`/`GetRow` :417-497, `getImage` :1549, `unloadControls` :2065) | `Vb6ToCSharp.Library.Windows/Runtime/RuntimeExtension.cs` (minus the `Printer` members, decision 5) |
| [Runtime/ScreenMetrics.cs](../Vb6ToCSharp.Library/Runtime/ScreenMetrics.cs) | all | `…Windows/Runtime/ScreenMetrics.cs` |
| [Runtime/Timer.cs](../Vb6ToCSharp.Library/Runtime/Timer.cs) | all | `…Windows/Runtime/Timer.cs` |
| [UI/CommandBase.cs](../Vb6ToCSharp.Library/UI/CommandBase.cs), [UI/TreeViewItemObject.cs](../Vb6ToCSharp.Library/UI/TreeViewItemObject.cs), [UI/ComboboxItem.cs](../Vb6ToCSharp.Library/UI/ComboboxItem.cs), [UI/PropertyIndexer.IJV.cs](../Vb6ToCSharp.Library/UI/PropertyIndexer.IJV.cs), [UI/PropertyIndexer.IV.cs](../Vb6ToCSharp.Library/UI/PropertyIndexer.IV.cs) | all | `…Windows/UI/` |

Stays in the core: the VB6 string/convert/array half of `RuntimeExtension` (`IsInStr`, `IIf`, `CStr`, `LBound`,
`SubArr`, `CDate`, …) — the ~15 core files that do `using static Vb6ToCSharp.Runtime.RuntimeExtension;` are
untouched.

Consumers to update: [Vb6ToCSharp/Forms/MainForm.xaml.cs:14](../Vb6ToCSharp/Forms/MainForm.xaml.cs#L14) and
[LinterForm.xaml.cs:6](../Vb6ToCSharp/Forms/LinterForm.xaml.cs#L6) add
`using static Vb6ToCSharp.Windows.Runtime.RuntimeExtension;`, and their `using Vb6ToCSharp.UI;` becomes
`using Vb6ToCSharp.Windows.UI;`. No XAML change (no `clr-namespace` references these types).

### 3. `ResxWriter` without `System.Drawing` (decision 3)

[FormConversion/ResxWriter.cs:1-31](../Vb6ToCSharp.Library/FormConversion/ResxWriter.cs#L1-L31) — `Write` is
replaced; `WriteFiles` (WPF loose files) is unchanged. Callers
([CodeConverter.cs:167,173](../Vb6ToCSharp.Library/CodeConversion/CodeConverter.cs#L167-L173)) do not change.

```csharp
// the type strings the target framework resolves (blocking check 6)
private const string Bitmap = "System.Drawing.Bitmap, System.Drawing.Common";
private const string Icon   = "System.Drawing.Icon, System.Drawing.Common";
private const string ByteArray = "application/x-microsoft.net.object.bytearray.base64";

public static void Write(string path, IEnumerable<FormResource> resources)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
    var root = new XElement("root",
        ResHeader("resmimetype", "text/microsoft-resx"),
        ResHeader("version", "2.0"),
        ResHeader("reader", "System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a2c561934e089"),
        ResHeader("writer", "System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a2c561934e089"));
    foreach (var r in resources)
        root.Add(new XElement("data",
            new XAttribute("name", r.Name),
            new XAttribute("type", r.Kind == FrxBlobKind.Icon ? Icon : Bitmap),
            new XAttribute("mimetype", ByteArray),
            new XElement("value", Convert.ToBase64String(r.Data, Base64FormattingOptions.InsertLineBreaks))));
    new XDocument(new XDeclaration("1.0", "utf-8", null), root).Save(path);
}
```

The bytes written are the raw `.frx` blob (BMP/GIF/JPEG/PNG/ICO), which `ImageConverter`/`IconConverter`
reconstruct — the same form the WinForms designer writes, without `BinaryFormatter`.

### 4. `Encoding.Default` is **not** ANSI on .NET (silent corruption if missed)

On `net48` it is the ANSI code page; on .NET it is UTF-8 — VB6 sources, `.frx` strings and the files the
converter writes are ANSI. New `Infrastructure/Vb6Encoding.cs`:

```csharp
internal static class Vb6Encoding
{
    /// <summary>The ANSI code page, as VB6/FSO used it (Encoding.Default was ANSI on .NET Framework, UTF-8 here).</summary>
    public static readonly Encoding Ansi = Load();

    private static Encoding Load()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        try { return Encoding.GetEncoding(GetACP()); } catch { return Encoding.GetEncoding(1252); }
    }

    [DllImport("kernel32.dll")] private static extern int GetACP();
}
```

Call sites to change (all `Encoding.Default` → `Vb6Encoding.Ansi`):

| File | Line |
|---|---|
| [Infrastructure/TextFiles.cs](../Vb6ToCSharp.Library/Infrastructure/TextFiles.cs#L105) | 105 (read), 450, 454 (write) |
| [Parsing/FrmParser.cs](../Vb6ToCSharp.Library/Parsing/FrmParser.cs#L15) | 15 |
| [Parsing/FrxReader.cs](../Vb6ToCSharp.Library/Parsing/FrxReader.cs#L122) | 122 |
| [Parsing/Model/ProjectInfo.cs](../Vb6ToCSharp.Library/Parsing/Model/ProjectInfo.cs#L55) | 55 |
| [Parsing/Model/ProjectGroupInfo.cs](../Vb6ToCSharp.Library/Parsing/Model/ProjectGroupInfo.cs#L21) | 21 |
| [FormConversion/WpfEmitter.cs:546](../Vb6ToCSharp.Library/FormConversion/WpfEmitter.cs#L546) | **emitted** code: `System.Text.Encoding.Default.GetBytes(rtf)` → an UpgradeHelpers ANSI encoding property (new `VbRuntime.Ansi`), same reason |

### 5. `ConversionUtility.Notify`

[CodeConversion/ConversionUtility.cs:151](../Vb6ToCSharp.Library/CodeConversion/ConversionUtility.cs#L151)
defaults to `Interaction.MsgBox` — a library must not show UI, and on .NET it needs the Windows desktop stack.
Default becomes `Console.Error.WriteLine`; the WPF app already injects its own.

### 6. Emitted converted project — [SupportFiles.cs:52-89](../Vb6ToCSharp.Library/CodeGeneration/SupportFiles.cs#L52-L89)

```diff
-    s.Append("    <TargetFramework>net48</TargetFramework>" + n);
+    s.Append("    <TargetFramework>net10.0-windows</TargetFramework>" + n);
…
-    s.Append("    <Reference Include=\"Microsoft.VisualBasic\" />" + n);
-    s.Append("    <Reference Include=\"Microsoft.CSharp\" />" + n); // dynamic (Object)
+    // Microsoft.VisualBasic.Core and Microsoft.CSharp are in the shared framework
```

`UseWPF`/`UseWindowsForms`, `StartupObject`, `DefineConstants`, `NoWarn`, the `Net4x.Vb6ToCSharp.UpgradeHelpers`
`PackageReference` and the ADODB `COMReference` block (supported on Windows in the .NET SDK) stay as they are.
Also update the class summary at [SupportFiles.cs:17-20](../Vb6ToCSharp.Library/CodeGeneration/SupportFiles.cs#L17-L20)
(“.NET Framework 4.8 project”).

### 7. The other projects

| Project | Change |
|---|---|
| `Vb6ToCSharp.Console` | `net48` → `net10.0-windows`; drop `AutoGenerateBindingRedirects` |
| `Vb6ToCSharp` (app) | SDK → `Microsoft.NET.Sdk`, `net10.0-windows`, keep `UseWPF`; drop `ProjectTypeGuids`, `AutoGenerateBindingRedirects` and the `<Reference …/>` group; add the `Library.Windows` project reference |
| `Vb6ToCSharp.UpgradeHelpers` | `net48` → `net10.0-windows` (keeps `UseWPF`+`UseWindowsForms`); no code change expected — it uses no `net48`-only API |
| `Vb6ToCSharp.UpgradeHelpers.Tests` | `net10.0-windows`; drop the `<Reference …/>` group (framework refs come from `UseWPF`/`UseWindowsForms`) |
| `Vb6ToCSharp.Tests` | `net10.0-windows`; drop the `<Reference …/>` group; keep the three project references |
| `Vb6ToCSharp.slnx` | add `Vb6ToCSharp.Library.Windows` (Libraries) and `Vb6ToCSharp.Library.Windows.Tests` (Tests) |

### 8. Tests

| Test | Change |
|---|---|
| [Tests/Runtime/RuntimeExtensionTests.cs](../Vb6ToCSharp.Tests/Runtime/RuntimeExtensionTests.cs) (41 facts) | split: WPF/`Timer`/`DoEvents` facts → `Vb6ToCSharp.Library.Windows.Tests/Runtime/RuntimeExtensionTests.cs`; conversion/culture facts stay |
| [Tests/UI/UiPlumbingTests.cs](../Vb6ToCSharp.Tests/UI/UiPlumbingTests.cs) | moves wholesale to `Vb6ToCSharp.Library.Windows.Tests/UI/` |
| [Tests/Runtime/Vb6StringSemanticsTests.cs](../Vb6ToCSharp.Tests/Runtime/Vb6StringSemanticsTests.cs) | stays — it is the guard that the VB runtime behaves the same on .NET 10 (`Mid`, `InStr`, `Split`, `Left` clamping) |
| [Tests/IntegrationTests.cs:153](../Vb6ToCSharp.Tests/IntegrationTests.cs#L153), [:197](../Vb6ToCSharp.Tests/IntegrationTests.cs#L197) | `bin\Debug\net48` → `bin\Debug\net10.0-windows`, and **`Showcase.exe` → `Showcase.dll`** / `Exe.exe` → `Exe.dll` (on .NET the `.exe` is a native apphost, `Assembly.Load(bytes)` needs the managed dll) |
| new | `ResxWriterTests`: a known FRX blob produces a `<data>` entry with the expected `type`/`mimetype`/base64; plus the integration project carrying a picture (check 6) |

### 9. Docs

[README.md:19,43](../README.md#L19), [CLAUDE.md:5](../CLAUDE.md#L5) and the per-project READMEs: `net48` →
`net10.0`/`net10.0-windows`, and the “`dotnet build` fails (MSB4803)” note disappears — the build command
becomes plain `dotnet build Vb6ToCSharp.slnx` (VS MSBuild still needed only while `Extras` stays `net48`).

## Verification

1. `dotnet build Vb6ToCSharp.slnx` (no VS MSBuild) — the point of the exercise.
2. `dotnet test Vb6ToCSharp.slnx` — 848 existing tests + the moved ones, all green.
3. Integration: `Showcase.vbp` and `VBG\Group.vbg` convert, **build as `net10.0-windows`**, load and run
   (`modMain.RunAll`, `Classes`, `Udts`, `Files` …) — this covers the emitted `.csproj`, the helpers package and
   the `.resx`.
4. Convert a form with an icon and a picture in both UI targets; build it; assert the resource loads (check 6).
5. Round-trip an ANSI source with accented characters (`é`, `£`) through the converter and compare bytes, to
   prove §4.

## Risks

| Risk | Mitigation |
|---|---|
| `.resx` type string does not resolve on .NET 10 | Blocking check 6 before anything else; the constant is one line |
| VB runtime behaves differently on .NET 10 (`Chr`/`Asc` code page, `Dir`, `FileDateTime`, `Format`) | `Vb6StringSemanticsTests` + the existing converter tests; `Vb6Encoding` registers the code-page provider at startup |
| `Assembly.Load` of the converted app in the test process (default ALC, `AssemblyResolve` for `Lib.dll`) behaves differently on .NET | Covered by integration test 3; if the resolve hook is not enough, switch to `AssemblyLoadContext` |
| Splitting `RuntimeExtension` (140 of 220 members, interleaved) breaks a call by overload resolution | Move mechanically, then build the app + tests; the WPF half has no callers in the core |
| `Net4x.NuGetUtility` misbehaves under the new TFMs | Check 7; fallback is plain pack properties |
| Old `net48` converted projects can no longer take new helpers | Accepted (decision 4); mention in the README |

## Order of work

1. Check 6 (resx) and check 7 (pack) on a throwaway project.
2. Core csproj → `net10.0`, drop ADODB/PowerPacks/`Reference`s, `ResxWriter` rewrite, `Vb6Encoding`, `Notify`. Build + tests (still `net48` tests referencing a `net10.0` library will not load → do step 3 in the same commit).
3. New `Vb6ToCSharp.Library.Windows`, move the code, retarget app/console/tests/helpers, split the tests, update `.slnx`.
4. Emitted project (`SupportFiles`) → `net10.0-windows`; integration test paths.
5. Docs + `CLAUDE.md`.
