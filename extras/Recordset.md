# RecordSet

Simulates most of the interface for the ADODB recordset (just continue extending if you need
something that's missing). The C# type is `RecordSet`, in namespace `Extras`.

## Create a new RecordSet

| Constructor | Use |
|---|---|
| `new RecordSet()` | empty; `RecordCount` is 0 |
| `new RecordSet(SQL, File, [QuietErrors], [Parameters])` | runs `SQL` against the Jet database `File` |
| `new RecordSet(table, adapter, connection)` | wraps a `DataTable` you already have |

A recordset built from a `DataTable` (the third form) is *disconnected*: it has no adapter to write
back through, so `Update()` is a no-op - the edits are already in the table - and `Delete()` just
drops the row from it.

## Record access

- `RS.Fields["Name"].Value`, `RS.Field["Name"]`, `RS["Name"]` - all read and write the current record
- `RS.Fields` enumerates `RecordsetField`; an unknown field name throws `ArgumentException`
- `RS.FieldNames` lists the columns, `RS.FieldExists("Name")` tests for one
- `RS.GetRows()` returns the values of every record in view

## Navigation

- `MoveFirst` / `MoveNext` / `MovePrevious` / `MoveLast`, `AbsolutePosition` (alias `Position`)
- `MoveNext` stops one past the last record (`EOF`), `MovePrevious` one before the first (`BOF`)
- both `BOF` and `EOF` are true when there are no records
- `Fields` throws `ArgumentOutOfRangeException` at `BOF` or `EOF`

## Filtering

- `RS.Filter = "Id > 1"` restricts every read to the matching records and starts again on the first
  of them; writes still reach the table. `RS.Filter = ""` shows everything again.
- `RS.Find("Name = 'Cid'")` positions on the first record matching the criteria, searching only what
  the filter shows, and returns `false` if there is none.

## Editing

- `AddNew()` appends a record and makes it current, dropping any filter that would hide it
- `Update()` writes pending changes back through the adapter
- `Delete()` deletes the current record
