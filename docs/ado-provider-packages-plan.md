# ADO: provider client packages, and omitted arguments that actually run

Two defects and one feature, all reached through `Vb6Ado\Vbb6Ado.vbp`:

| # | Symptom | Cause | Fixed in |
|---|---|---|---|
| 1 | `Missing parameter does not have a default value. (Parameter 'parameters')` at run time | `VbRuntime.ComInvoke` passes `Type.Missing` through `InvokeMember`; managed reflection accepts it only for a parameter that *has* a default, which `out object recordsAffected` has not | `Vb6ToCSharp.Base.UpgradeHelpers` + converter |
| 2 | Even bound correctly, `cmd.Execute , , adExecuteNoRecords` would pass `adExecuteNoRecords` as a *parameter value*, not as `Options` | `Standard.AdoDb`'s `Command.Execute(out, int options, params parameters)` swaps ADO's `(RecordsAffected, Parameters, Options)` | `NetStandard.AdoDb` |
| 3 | The converted project must be hand-edited to add `Microsoft.Data.SqlClient` | nothing in the converter looks at connection strings | `Vb6ToCSharp.Library` |

## Blocking verification — done

1. Package majors checked on nuget.org before the catalog was written: `Microsoft.Data.SqlClient 7.*`,
   `System.Data.Odbc/OleDb 10.*`, `Npgsql 10.*` (**not** 8.*), `MySql.Data 9.*`,
   `Oracle.ManagedDataAccess.Core 23.*`, `Microsoft.Data.Sqlite 10.*` (**not** 9.*),
   `FirebirdSql.Data.FirebirdClient 10.*`. A wrong major would break restore of every converted project.
   Confirmed again by restoring `ConvertedVb6Ado\Vbb6Ado.csproj`.
2. `Packages\` turned out to be a symlink to the shared feed `D:\Starb\Packages`, which all three repos publish
   into, so rebuilding `NetStandard.AdoDb` put the new `Standard.AdoDb 1.0.0.26268` on the converter's feed with
   no copy step. Change #2 is still a breaking change in that repo.

## Decisions taken

| Decision | Consequence |
|---|---|
| Fix both the converter (direct calls) and `ComInvoke` | a call on a known ADO type is compile-time checked; anything still late bound (an unknown type library) no longer throws |
| `Standard.AdoDb` adopts ADO's parameter order | `Execute(out object, object? parameters = null, int options = …)`; the `params object?[]` tail is gone, so 7 call sites change. An old `Execute(out _, someInt)` now binds the int to `parameters` **silently** — every call site is audited, see Risks |
| Provider detected from source literals, `*.ini` next to the `.vbp`, and `[Settings] DBProvider=` | three sources, the explicit setting wins; nothing detected → no package and a warning |
| Built-in token→package table, `[ADOProviders]`-overridable, floating versions | matches `ControlCatalog` / `[Controls]`; a provider the table misses needs no code change |

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
omittable-argument signatures and `Arguments(type, member, args)`:

```
Connection.Execute:CommandText,&RecordsAffected,Options
Command.Execute:&RecordsAffected,Parameters,Options
Recordset.Open:Source,ActiveConnection,CursorType,LockType,Options
…                                       (& marks a ByRef parameter)
```

An omitted argument is written as **no argument at all**, and the ones after it become **named**, so the
parameter's own default applies. That beats a placeholder value: the default is then the one the type library
declares, which a `default` literal would get wrong (an omitted `CursorType` is `adOpenUnspecified`, not `0`).
Only the parameter names are needed, so the table cannot drift out of step with the defaults.

Both places that wrote `Missing` consult it first:

| Site | Before | After |
|---|---|---|
| statement | `ComInvoke(cmd, "Execute", Missing, Missing, adExecuteNoRecords)` | `cmd.Execute(out _, options: adExecuteNoRecords)` |
| expression | `cmd.Execute(Missing, Missing, adExecuteNoRecords)` — did not even compile | `cmd.Execute(out _, options: adExecuteNoRecords)` |

An unknown type library still goes through `LateBoundCall` → `ComInvoke`, now working.

**Not done:** a call that passes a *variable* to a ByRef parameter and reads it back (`cn.Execute sql, rows`)
needs a temporary and a conversion back, which the line-by-line converter cannot express yet. Such a call stays
late bound, so the value is discarded — as it was before.

### 3. `Vb6ToCSharp.Library` — the provider's client package

New `CodeGeneration/AdoProviderPackages.cs`:

- **Catalog**: every `Provider=` token `AdoProviderRegistry` knows → package + floating version;
  overridable per token from `[ADOProviders]` in `VB6toCS.INI`.
- **Detection**, in order: `[Settings] DBProvider=` wins; otherwise `Provider=` / `Driver=` / `DSN=` in the
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

## Verification — results

| Step | Result |
|---|---|
| `dotnet test NetStandard.AdoDb.slnx` | 769 passed |
| `MSBuild.exe Vb6ToCSharp.slnx -restore` | succeeded |
| `dotnet test Vb6ToCSharp.slnx --no-build` | 1299 passed (895 converter, both ADO integration tests included) |
| the converted ADO sample runs against LocalDB | created the database, inserted and read back the GUID, showed it, dropped the database, exited 0 |

One flake: `Group_ConvertsWithTheConsole…` failed once in a full-solution run right after the build had
republished the shared package feed, and passed on its own and in two later full runs. Unrelated to ADO.

## Risks

| Risk | Mitigation |
|---|---|
| `Execute(out _, 3)` silently binds `3` to `parameters` after the reorder | all 7 call sites audited and updated; a new test pins the parameter order by reflection |
| a floating major that does not exist for `net10.0-windows` | restore of the converted project is part of the integration test; catalog is INI-overridable |
| a connection string assembled at run time, or read from the registry, is undetectable | `[Settings] DBProvider=` escape hatch + warning |
| scanning `*.ini` beside the `.vbp` could pick up an unrelated `Provider=` | only values that parse as a connection string (`;`-separated `key=value`, a known token) count |
