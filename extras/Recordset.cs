using Extras.Model;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Extras;

public class RecordSet
{
    public string Source = "";
    public Dictionary<dynamic, dynamic> Parameters = null;
    public string Database = "";
    public bool QuietErrors = false;

    private bool mAddingRow = false;
    public bool AddingRow => mAddingRow;

    OleDbConnection connection;
    OleDbDataAdapter adapter;
    DataTable table;
    // The rows the Filter selects, or null when no filter is active. They are the table's own rows,
    // so writing through Fields still reaches the table.
    DataRow[] filteredRows;
    string mFilter;

    private IEnumerable<DataRow> Rows()
    {
        if (filteredRows != null) return filteredRows;
        if (table?.Rows == null) return new DataRow[0];
        return table.Rows.Cast<DataRow>();
    }

    private DataRow RowAt(int i) { return filteredRows != null ? filteredRows[i] : table.Rows[i]; }

    // The filter selects rows, so it has to be re-run whenever the table gains or loses one.
    private void ReapplyFilter()
    {
        filteredRows = string.IsNullOrEmpty(mFilter) || table == null ? null : table.Select(mFilter);
    }

    public RecordSet() { }

    public RecordSet(DataTable table, OleDbDataAdapter adapter, OleDbConnection connection)
    {
        this.connection = connection;
        this.adapter = adapter;
        this.table = table;
    }


    public RecordSet(string SQL, string File, bool QuietErrors = false, Dictionary<dynamic, dynamic> Parameters = null)
    {
        Source = SQL;
        this.Parameters = Parameters;
        Database = File;
        this.QuietErrors = QuietErrors;

        Open();
    }

    public void Close()
    {
        try { connection?.Close(); }
        catch { }

        connection = null;
        adapter = null;
        table = null;
        filteredRows = null;
        mFilter = null;
    }

    public static void sqlExecutionError(string mSQL, Exception e)
    {
        var T = "";
        T += "getRecordSet Failed: " + e.Message + "\r\n";
        T += "\r\n";
        T += mSQL + "\r\n";
        T += "\r\n";
        T += "ERROR:" + e.Message;

        T = T.Replace("$EDESC", e.Message);
        //ErrMsg = Replace(ErrMsg, "$ENO", Err().Number);
        T = T.Replace("$ESRC", e.Source);
        LoggerFactoryContainer.Instance.LoggerFactory.GetLogger().Log(DiagnosticLevel.Error, null, "Database Error: " + T, "Error");
        //CheckStandardErrors(); // Bookmark/updateable query
    }

    private string ConnectionString(string file) { return "PROVIDER=Microsoft.Jet.OLEDB.4.0;Data Source=" + file + ";"; }

    public int AbsolutePosition { get; set; }
    public int Position { get => AbsolutePosition; set => AbsolutePosition = value; }
    public int RecordCount => filteredRows != null ? filteredRows.Length : table?.Rows?.Count ?? 0;
    public bool EOF => AbsolutePosition >= RecordCount;
    // As in ADO: before the first record, and true together with EOF when there are no records.
    public bool BOF => AbsolutePosition < 0 || RecordCount == 0;

    public bool FieldExists(string F) { return table?.Columns?.Contains(F) ?? false; }


    public int MoveFirst() { return AbsolutePosition = 0; }
    public int MoveNext() { return ++AbsolutePosition < RecordCount ? AbsolutePosition : AbsolutePosition = RecordCount; }
    // Stops one before the first record, the mirror of MoveNext stopping one past the last.
    public int MovePrevious() { return --AbsolutePosition >= -1 ? AbsolutePosition : AbsolutePosition = -1; }
    public int MoveLast() { return AbsolutePosition = RecordCount == 0 ? 0 : RecordCount - 1; }

    public RecordsetFields Fields
    {
        get
        {
            if (AbsolutePosition >= 0 && AbsolutePosition < RecordCount) return new RecordsetFields(RowAt(AbsolutePosition));
            throw new ArgumentOutOfRangeException("Either EOF or BOF is true.");
        }
    }

    public List<string> FieldNames
    {
        get
        {
            var result = new List<string>();
            if (table == null) return result;
            foreach (DataColumn item in table.Columns) result.Add(item.ColumnName);
            return result;
        }
    }

    public PropIndexer<dynamic, dynamic> Field =>
        new PropIndexer<dynamic, dynamic>(
            (k) => Fields[k].Value,
            (k, v) => { Fields[k].Value = v; }
        );

    public dynamic this[dynamic field]
    {
        get => GetField(field);
        set => SetField(field, value);
    }

    public dynamic GetField(dynamic key) => Fields[key].Value;
    public void SetField(dynamic key, dynamic value) => Fields[key].Value = value;

    public List<List<dynamic>> GetRows()
    {
        return Rows().Select(r => r.ItemArray.ToList()).ToList();
    }

    public string Filter
    {
        get => mFilter;
        set
        {
            // Changing the filter changes what the recordset sees, so it starts again on the first
            // record of the new view.
            mFilter = value;
            ReapplyFilter();
            AbsolutePosition = 0;
        }
    }

    // Positions on the first record matching the criteria, searching only what the Filter shows.
    public bool Find(string criteria)
    {
        if (table == null) return false;
        var view = Rows().ToList();
        foreach (var match in table.Select(criteria))
        {
            var x = view.IndexOf(match);
            if (x < 0) continue;
            AbsolutePosition = x;
            return true;
        }
        return false;
    }

    private void Open()
    {
        if (!System.IO.File.Exists(Database))
        {
            LoggerFactoryContainer.Instance.LoggerFactory.GetLogger().Log(DiagnosticLevel.Error, null, "Database Not Found: " + Database, Assembly.GetExecutingAssembly().FullName);
            return;
        }

        var result = new DataSet();
        connection = new OleDbConnection(ConnectionString(Database));
        var command = new OleDbCommand(Source, connection);
        foreach (var key in Parameters?.Keys ?? Enumerable.Empty<dynamic>())
        {
            var param = command.CreateParameter();
            param.ParameterName = key;
            param.Value = Parameters[key];
            command.Parameters.Add(param);
        }
        adapter = new OleDbDataAdapter(command);
        try
        {
            connection.Open();
            adapter.FillSchema(result, SchemaType.Source);
            adapter.Fill(result, "Default");
        }
        catch (Exception e)
        {
            if (!QuietErrors) sqlExecutionError(Source, e);
        }
        finally { connection.Close(); }

        table = result.Tables["Default"];
    }

    public void Update()
    {
        // A recordset that was handed its table has nowhere to write back to: the edits are already
        // in the table, so there is nothing left to do.
        if (adapter == null || connection == null)
        {
            mAddingRow = false;
            return;
        }

        var cb = new OleDbCommandBuilder(adapter);
        cb.QuotePrefix = "[";
        cb.QuoteSuffix = "]";
        try
        {
            connection.Open();
            adapter.UpdateCommand = cb.GetUpdateCommand();
            adapter.Update(table);
        }
        catch (Exception e)
        {
            if (!QuietErrors) sqlExecutionError(adapter.UpdateCommand?.ToString() ?? Source, e);
        }
        finally { connection.Close(); }

        mAddingRow = false;
    }

    public void AddNew()
    {
        // The new row goes into the table itself: drop any filter, which would hide it.
        mFilter = null;
        filteredRows = null;
        var newRow = table.NewRow();
        table.Rows.InsertAt(newRow, table.Rows.Count);
        AbsolutePosition = table.Rows.Count - 1;
        mAddingRow = true;
    }

    // As in ADO, deletes the current record.
    public void Delete()
    {
        if (AbsolutePosition < 0 || AbsolutePosition >= RecordCount)
            throw new ArgumentOutOfRangeException("Either EOF or BOF is true.");

        var row = RowAt(AbsolutePosition);

        if (adapter == null || connection == null)
        {
            // Nothing to write back to: drop the row from the table.
            table.Rows.Remove(row);
            ReapplyFilter();
            mAddingRow = false;
            return;
        }

        row.Delete();

        var cb = new OleDbCommandBuilder(adapter);
        try
        {
            connection.Open();
            adapter.DeleteCommand = cb.GetDeleteCommand();
            adapter.Update(table);
        }
        catch (Exception e)
        {
            if (!QuietErrors) sqlExecutionError(adapter.DeleteCommand?.ToString() ?? Source, e);
        }
        finally { connection.Close(); }

        ReapplyFilter();
        mAddingRow = false;
    }
}