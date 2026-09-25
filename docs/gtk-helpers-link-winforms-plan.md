# Vb6ToCSharp.Gtk.UpgradeHelpers links the WinForms sources

**Status: done.** The Gtk package carries no sources of its own and no `#if GTK`.

The Gtk package kept its own copy of the 14 WinForms helper files, six of them carrying 25 `#if GTK` blocks
around API `Gtk.Windows.Forms.Base` lacked. The API was added there; the copies are gone.

## What was measured

A throwaway project compiled the 14 WinForms files plus the shared projitems (34 files) against the
`Gtk.Windows.Forms.Base 1.4.2464.26267` **that was installed at the time**. One error:

```
CS0506: 'DriveListBox.OnSelectedIndexChanged(EventArgs)': cannot override inherited member
        'ComboBox.OnSelectedIndexChanged(EventArgs)' because it is not marked virtual, abstract, or override
```

So all the other guards were stale and one small fix in the Gtk library would have unblocked the link-over.

## The gaps, and where each was closed

Compiling the 14 WinForms files plus the shared projitems against the package gave **27 errors**. All were closed
in `Net4x.GtkWindowsForms`; none needed a GtkSharp change (`GetPathAtPos`, `HeadersVisible`, `Get`/`SetCursor` and
`ConvertWidgetToBinWindowCoords` are all bound already - `GetPathAtPos` is hand-written in
`Source/Libs/GtkSharp/TreeView.cs`, which is why the generated api.xml marks it `hidden`).

| Commit / patch | Closed |
|---|---|
| `6a0ce9d5` / `0150` | `ComboBox` and `ListBox` `OnSelected{Index,Item,Value}Changed` → `protected virtual` |
| `fa921407` / `0151` | 11 `FontDialog` properties; `SaveFileDialog.OverwritePrompt`/`CreatePrompt`; `Form.ActiveForm`; `Control.FromHandle`; `ComboBox.BeginUpdate`/`EndUpdate`; `TreeNode.TreeView` public; `TreeNodeCollection.Find(key, searchAllChildren)` |
| `06e30804` / `0152` | `DataGridView.ColumnHeadersVisible` (the tree view's `HeadersVisible`), `CurrentCell` (its cursor), `HitTest` (`GetPathAtPos`), `ColumnCount`, `RowHeadersVisible` |
| `38956b3e` / `0153` | `CheckedListBox.GetItemChecked` - `SetItemChecked` was there with no getter |
| `96cdce1e` / `0154` | `ToolStripMenuItem` derives from `ToolStripDropDownItem`, where `DropDown` lives, as in WinForms; `ToolStripDropDown` gains `Closing`/`Closed` |

`CheckedListBox` was **not** rebased onto `ListBox`. WinForms derives it from `ListBox`, but this one declares its
own `self`, `Items`, `SelectedItems`, `SelectedIndexChanged` and `SelectedItemChanged` - all of which `ListBox`
also declares - so rebasing is a rewrite of the control, not a base-class swap. Instead `ListHelper` matches it
through `object`, which compiles on both stacks; a stack where the two are unrelated never reaches that branch,
which is correct, because it cannot pass a `CheckedListBox` to a `ListBox` extension in the first place.

## The converter side

`Vb6ToCSharp.Gtk.UpgradeHelpers` lost all 14 files. Its project keeps the `Import` of
`Vb6ToCSharp.Base.UpgradeHelpers.Shared.projitems` - what the WinForms package gets from its `ProjectReference` -
and links the rest:

```xml
<EnableDefaultCompileItems>false</EnableDefaultCompileItems>
...
<Compile Include="..\Vb6ToCSharp.WinForms.UpgradeHelpers\**\*.cs"
         Exclude="..\Vb6ToCSharp.WinForms.UpgradeHelpers\obj\**\*.cs;..\Vb6ToCSharp.WinForms.UpgradeHelpersin\**\*.cs"
         Link="%(RecursiveDir)%(Filename)%(Extension)" />
```

A glob, so a file added to the WinForms package reaches the Gtk one with no edit here, and anything
`Gtk.Windows.Forms` cannot serve fails this build rather than drifting. `<DefineConstants>…;GTK</DefineConstants>`
and the `InternalsVisibleTo` for a test project that does not exist are gone.

## Verification

| Step | Result |
|---|---|
| `dotnet test Gtk.Windows.Forms.Tests` after every batch | 2787 passed, 22 skipped |
| `MSBuild Vb6ToCSharp.slnx -restore` | succeeded |
| `dotnet test Vb6ToCSharp.slnx --no-build` | 1304 passed |
| `grep -r "#if" Vb6ToCSharp.Gtk.UpgradeHelpers` | nothing - the project has no sources of its own |

## Note for whoever builds the Gtk repo next

`dotnet pack` defaults to **Release** while `dotnet build` defaults to Debug, and the Release path also runs
`PrepareObfuscate`. Packing without `-c Debug` packs whatever stale Release output is lying around; the shipped
packages are Debug builds.
