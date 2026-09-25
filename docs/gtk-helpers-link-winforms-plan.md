# Vb6ToCSharp.Gtk.UpgradeHelpers links the WinForms sources

**Status: blocked.** The converter side is not changed. See *What went wrong* below.

The Gtk package keeps its own copy of the 14 WinForms helper files, six of them carrying 25 `#if GTK` blocks
around API `Gtk.Windows.Forms.Base` lacked. The goal is to delete the copies and link the WinForms files.

## What was measured

A throwaway project compiled the 14 WinForms files plus the shared projitems (34 files) against the
`Gtk.Windows.Forms.Base 1.4.2464.26267` **that was installed at the time**. One error:

```
CS0506: 'DriveListBox.OnSelectedIndexChanged(EventArgs)': cannot override inherited member
        'ComboBox.OnSelectedIndexChanged(EventArgs)' because it is not marked virtual, abstract, or override
```

So all the other guards were stale and one small fix in the Gtk library would have unblocked the link-over.

## What went wrong

The `1.4.2464.26267` that was installed is **not** the `1.4.2464.26267` on nuget.org. The installed one carried
`DataGridView.RowHeadersVisible` / `ColumnCount` / `CurrentCell` / `HitTest`, `FontDialog.ShowEffects` and its
eight siblings, `Form.ActiveForm`, `SaveFileDialog.OverwritePrompt` / `CreatePrompt`, `Control.FromHandle`,
`CheckedListBox.GetItemChecked`, `ListBox.BeginUpdate` / `EndUpdate`. nuget.org's does not, and **no source in
`Net4x.GtkWindowsForms` implements them** — not the generated tree, not `Gtk.Windows.Forms.LatestBackup`, not the
`gtksystem-windows-forms` submodule, and not any commit in either history (the names appear only in
`Resources/System.Windows.Forms.xml`, the Microsoft doc reference used for doc merging).

That package must therefore have been built from sources not in this checkout. Rebuilding
`Gtk.Windows.Forms.csproj` here produces a package that is **missing that whole API surface**, so it is a
regression, not an update.

Worse: `Net4x.NuGetUtility`'s `Delete_OldPackageFiles` target deletes `$(PackageOutputPath)$(PackageId).*.nupkg`
on every build, and `Delete_OldPackage` deletes `$(NuGetPackageRoot)$(PackageId)` — the whole extracted folder,
all versions — after every pack. Building and packing the Gtk library therefore **deleted the good package from
the shared feed `D:\Starb\Packages` and from the NuGet global cache**. Restoring `1.4.2464.26267` afterwards
fetched nuget.org's poorer build under the same version number. No copy of the good assembly survives on disk
(searched `D:\CommonLibrary` and `D:\Starb` for any `Gtk.Windows.Forms.dll` containing `RowHeadersVisible`).

Measured A/B, same probe, only the package version changed:

| Referenced package | Errors |
|---|---|
| the `26267` that was installed before | 1 (the `virtual` one) |
| nuget.org `26267`, and any local rebuild | ~20, the list above |

## What is committed

| Repository | State |
|---|---|
| `Net4x.GtkWindowsForms` | two commits kept: `ComboBox`/`ListBox` `OnSelected{Index,Item,Value}Changed` made `protected virtual` (WinForms declares all six virtual; the library already had 377 virtual `OnXxx` against 9 that were not), plus `patches/0150-*.patch` so the prepare pipeline reapplies it. Correct and independently useful; it only takes effect when the library is next built from the right sources. |
| `Vb6ToCSharp` | **unchanged**. The Gtk project keeps its 14 files and its `#if GTK` blocks, the solution builds, the working tree is clean. |
| the shared feed | the `26268` packages I built were removed again; nothing now resolves to the regressed build. |

## To finish this

1. Restore or rebuild `Gtk.Windows.Forms.Base` from the sources that produced the installed `26267` — another
   machine, another branch, or a `PrepareProjects` run against the right upstream revision — and put it on the
   shared feed under a **new** build number. Publishing it also fixes a latent hazard: the feed and nuget.org
   disagreeing about what `1.4.2464.26267` contains.
2. Apply `patches/0150` (or rebuild after it) so `ComboBox.OnSelectedIndexChanged` is virtual.
3. Then the converter-side change is small and already designed: delete the 14 files under
   `Vb6ToCSharp.Gtk.UpgradeHelpers/`, drop `<DefineConstants>…;GTK</DefineConstants>` and the stale
   `InternalsVisibleTo Vb6ToCSharp.Gtk.UpgradeHelpers.Tests`, keep the `Import` of the shared projitems, and add:

```xml
<ItemGroup>
    <Compile Include="..\Vb6ToCSharp.WinForms.UpgradeHelpers\**\*.cs"
             Exclude="..\Vb6ToCSharp.WinForms.UpgradeHelpers\obj\**\*.cs;..\Vb6ToCSharp.WinForms.UpgradeHelpers\bin\**\*.cs"
             Link="%(RecursiveDir)%(Filename)%(Extension)" />
</ItemGroup>
```

A glob, so a file added to the WinForms package reaches the Gtk one with no further edit, and anything
`Gtk.Windows.Forms` cannot serve fails the build instead of drifting.

## Note for whoever builds that repo next

`dotnet pack` defaults to **Release**; `dotnet build` defaults to Debug. The Release path also runs
`PrepareObfuscate`. Packing without `-c` therefore packs whatever stale Release output is lying around — that cost
an hour here.
