using System;
using System.Collections;
using System.Data;

namespace Extras.Model;

public class RecordsetFields : ICollection
{
    DataRow row = null;
    readonly object syncRoot = new object();

    public RecordsetFields(DataRow row) { this.row = row; }

    public int Count => row.Table.Columns.Count;
    public object SyncRoot => syncRoot;
    public bool IsSynchronized => false;

    public void CopyTo(Array array, int index) { throw new InvalidOperationException("Not valid on object"); }

    // A Fields collection enumerates its fields, not the underlying columns.
    public IEnumerator GetEnumerator()
    {
        foreach (DataColumn c in row.Table.Columns) yield return new RecordsetField(row, c.ColumnName);
    }

    public RecordsetField this[dynamic x]
    {
        get
        {
            // Columns[name] answers null for a name that is not there: report it here rather than
            // handing back a field that throws on first use.
            DataColumn C = row.Table.Columns[x];
            if (C == null) throw new ArgumentException("No field named '" + x + "' in the recordset.", nameof(x));
            return new RecordsetField(row, x);
        }
    }
}