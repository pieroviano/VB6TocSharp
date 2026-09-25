# ADO: provider client packages, and omitted arguments that actually run

Two defects and one feature, all reached through `Vb6Ado\Vbb6Ado.vbp`:

| # | Symptom | Cause | Fixed in |
|---|---|---|---|
| 1 | `Missing parameter does not have a default value. (Parameter 'parameters')` at run time | `VbRuntime.ComInvoke` passes `Type.Missing` through `InvokeMember`; managed reflection accepts it only for a parameter that *has* a default, which `out object recordsAffected` has not | `Vb6ToCSharp.Base.UpgradeHelpers` + converter |
| 2 | Even bound correctly, `cmd.Execute , , adExecuteNoRecords` would pass `adExecuteNoRecords` as a *parameter value*, not as `Options` | `Standard.AdoDb`'s `Command.Execute(out, int options, params parameters)` swaps ADO's `(RecordsAffected, Parameters, Options)` | `NetStandard.AdoDb` |
| 3 | The converted project must be hand-edited to add `Microsoft.Data.SqlClient` | nothing in the converter looks at connection strings | `Vb6ToCSharp.Library` |

## Blocking verification, before coding

1. `Microsoft.Data.SqlClient 7.*`, `System.Data.Odbc/OleDb 10.*`, `Npgsql 8.*`, `MySql.Data 9.*`,
   `Oracle.ManagedDataAccess.Core 23.*`, `Microsoft.Data.Sqlite 9.*`, `FirebirdSql.Data.FirebirdClient 10.*`
   must all exist on nuget.org for `net10.0-windows`. A wrong major breaks restore of every converted project.
   Verified by restoring `ConvertedVb6Ado\Vbb6Ado.csproj`; any miss is corrected in the catalog before commit.
2. `Standard.AdoDb` is consumed from the local feed `Packages\` at `1.0.0.*`. Change #2 is a **breaking change**
   in another repo: `NetStandard.AdoDb` must be rebuilt and its new `.nupkg` copied into
   `Vb6ToCSharp\Packages\` before the ADO integration test can pass.

## Decisions taken

| Decision | Consequence |
|---|---|
| Fix both the converter (direct calls) and `ComInvoke` | a call on a known ADO type is compile-time checked; anything still late bound (an unknown type library) no longer throws |
| `Standard.AdoDb` adopts ADO's parameter order | `Execute(out object, object? parameters = null, int options = …)`; the `params object?[]` tail is gone, so 7 call sites change. An old `Execute(out _, someInt)` now binds the int to `parameters` **silently** — every call site is audited, see Risks |
| Provider detected from source literals, `*.ini` next to the `.vbp`, and `[Settings] DbProvider=` | three sources, the explicit setting wins; nothing detected → no package and a warning |
| Built-in token→package table, `[AdoProviderPackages]`-overridable, floating versions | matches `ControlCatalog` / `[Controls]`; a provider the table misses needs no code change |

## Changes

### 1. `Vb6ToCSharp.Base.UpgradeHelpers` — late binding that works on managed targets

New `Vb6ToCSharp.Base.UpgradeHelpers.Shared/Internal/LateBinder.cs`: resolves the overload by name and
arity, then fills the call:

| Argument | Passed as |
|---|---|
| supplied, assignable | itself |
| supplied, an enum / numeric mismatch | `Enum.ToObject` / `Convert.ChangeType` (VB6 coerces a Variant) |
| omitted, `out`/`ref` parameter | a fresh slot, discarded — VB6 discards it too |
| omitted, parameter with a default | that default |
| omitted, no default | `default(T)` |
| not supplied at all, `params` tail | an empty array |

`VbRuntime.ComInvoke` ([VbRuntime.cs:222](../Vb6ToCSharp.Base.UpgradeHelpers.Shared/VbRuntime.cs#L222)) keeps
`InvokeMember` for `Type.IsCOMObject` (IDispatch resolves an omitted argument itself) and routes everything
else to `LateBinder`.

### 2. `Vb6ToCSharp.Library` — a call on a known ADO type is not late bound

`AdoInterop` ([AdoInterop.cs](../Vb6ToCSharp.Library/CodeConversion/AdoInterop.cs)) gains the library's
omittable-argument signatures and `OmittedArgument(type, member, position)`:

```
Connection.Open:ConnectionString=null,UserID=null,Password=null,Options=adConnectUnspecified
Connection.Execute:CommandText,RecordsAffected=out,Options=adCmdUnknown
Command.Execute:RecordsAffected=out,Parameters=null,Options=adCmdUnspecified
Recordset.Open:Source=null,ActiveConnection=null,CursorType=adOpenUnspecified,LockType=adLockUnspecified,Options=adCmdUnspecified
…
```

Both places that write `Missing` consult it first, and emit a plain call when the signature is known:

| Site | Before | After |
|---|---|---|
| statement, [CodeConverter.cs:2457](../Vb6ToCSharp.Library/CodeConversion/CodeConverter.cs#L2457) | `ComInvoke(cmd, "Execute", Missing, Missing, adExecuteNoRecords)` | `cmd.Execute(out _, null, adExecuteNoRecords)` |
| expression, [CodeConverter.cs:1952](../Vb6ToCSharp.Library/CodeConversion/CodeConverter.cs#L1952) | `cmd.Execute(Missing, Missing, adExecuteNoRecords)` — did not even compile | `cmd.Execute(out _, null, adExecuteNoRecords)` |

An unknown type library still goes through `LateBoundCall` → `ComInvoke`, now working.

### 3. `Vb6ToCSharp.Library` — the provider's client package

New `CodeGeneration/AdoProviderPackages.cs`:

- **Catalog**: every `Provider=` token `AdoProviderRegistry` knows → package + floating version;
  overridable per token from `[AdoProviderPackages]` in `VB6toCS.INI`.
- **Detection**, in order: `[Settings] DbProvider=` wins; otherwise `Provider=` / `Driver=` / `DSN=` in the
  `.vbp`'s sources and in `*.ini` beside it. Several engines → several packages.
- Nothing found while ADO is referenced → no `PackageReference`, a `Notify` warning and a TODO comment in
  the `.csproj` naming the setting.

[SupportFiles.cs:81](../Vb6ToCSharp.Library/CodeGeneration/SupportFiles.cs#L81) emits them in the ADO
`ItemGroup`, only for `AdoTarget.Package` (a COM `ADODB` reference brings its own OLE DB provider).

### 4. `NetStandard.AdoDb` — ADO's own parameter order

[Command.cs:127](../../NetStandard.AdoDb/Net4x.AdoDb/Command.cs#L127):

```csharp
public Recordset? Execute(out object recordsAffected, object? parameters = null,
    int options = (int)CommandTypeEnum.adCmdUnspecified)
```

`parameters` is ADO's Variant-or-Variant-array, normalised to `object?[]` for `BindParameters`.
Call sites updated: `Command.Execute()` (`Command.cs:112`), `Recordset.cs:510`, and
`Net5x.AdoDb.Tests/CommandTests.cs` lines 103, 120, 138, 155.

## Verification

| Step | Command |
|---|---|
| `NetStandard.AdoDb` builds and its tests pass | `dotnet test NetStandard.AdoDb.slnx` |
| new `Standard.AdoDb` on the converter's feed | copy `Packages\Standard.AdoDb.*.nupkg` into `Vb6ToCSharp\Packages\` |
| converter builds | `MSBuild.exe Vb6ToCSharp.slnx -restore` |
| unit tests, incl. the new `ComInvoke` / `AdoInterop` / `AdoProviderPackages` ones | `dotnet test Vb6ToCSharp.slnx --no-build` |
| the ADO sample converts, references `Microsoft.Data.SqlClient` and **runs** | integration test, then run `ConvertedVb6Ado\bin\…\Vbb6Ado.exe` against LocalDB |

## Risks

| Risk | Mitigation |
|---|---|
| `Execute(out _, 3)` silently binds `3` to `parameters` after the reorder | all 7 call sites audited; the 4 tests that passed options positionally are rewritten and keep asserting the option's effect |
| a floating major that does not exist for `net10.0-windows` | restore of the converted project is part of the integration test; catalog is INI-overridable |
| a connection string assembled at run time, or read from the registry, is undetectable | `[Settings] DbProvider=` escape hatch + warning |
| scanning `*.ini` beside the `.vbp` could pick up an unrelated `Provider=` | only values that parse as a connection string (`;`-separated `key=value`, a known token) count |
