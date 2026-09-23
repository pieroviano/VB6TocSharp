using System;
using System.Collections;
using System.Data;

namespace Extras.Model;

public class RecordsetFields : ICollection
{
    DataRow row = null;

    public RecordsetFields(DataRow row) { this.row = row; }

    public int Count => row.Table.Columns.Count;
    public object SyncRoot => null;
    public bool IsSynchronized => false;

    public void CopyTo(Array array, int index) { throw new InvalidOperationException("Not valid on object"); }

    public IEnumerator GetEnumerator() { return row.Table.Columns.GetEnumerator(); }

    public RecordsetField this[dynamic x]
    {
        get
        {
            DataColumn C = row.Table.Columns[x];
            return new RecordsetField(row, x);
        }
    }
}