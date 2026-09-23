using Extras.Model;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Linq;
using System.Windows.Forms;

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
    DataTable filteredTable;
    string mFilter;

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
        filteredTable = null;
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
        MessageBox.Show("Database Error: " + T, "Error");
        //CheckStandardErrors(); // Bookmark/updateable query
    }

    private string ConnectionString(string file) { return "PROVIDER=Microsoft.Jet.OLEDB.4.0;Data Source=" + file + ";"; }

    public int AbsolutePosition { get; set; }
    public int Position { get => AbsolutePosition; set => AbsolutePosition = value; }
    public int RecordCount => table == null ? 0 : table.Rows == null ? 0 : table.Rows.Count;
    public bool EOF => AbsolutePosition >= RecordCount;
    public bool BOF => AbsolutePosition == 0;

    public bool FieldExists(string F) { return table?.Columns?.Contains(F) ?? false; }


    public int MoveFirst() { return AbsolutePosition = 0; }
    public int MoveNext() { return ++AbsolutePosition < RecordCount ? AbsolutePosition : AbsolutePosition = RecordCount; }
    public int MovePrevious() { return --AbsolutePosition >= 0 ? AbsolutePosition : AbsolutePosition = 0; }
    public int MoveLast() { return AbsolutePosition = RecordCount - 1; }

    public RecordsetFields Fields
    {
        get
        {
            if (AbsolutePosition >= 0 && AbsolutePosition < RecordCount) return new RecordsetFields(table.Rows[AbsolutePosition]);
            throw new ArgumentOutOfRangeException("Either EOF or BOF is true.");
        }
    }

    public List<string> FieldNames
    {
        get
        {
            if (table == null) return null;
            var result = new List<string>();
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
        var tableEnumerable = table.Rows.Cast<DataRow>();
        var tableList = tableEnumerable.ToArray().ToList();
        return tableList.ToList().Select((r) => r.ItemArray.ToList()).ToList();
    }

    public string Filter
    {
        get => mFilter;
        set
        {
            mFilter = value;
            if (string.IsNullOrEmpty(value))
            {
                filteredTable = null;
                return;
            }

            filteredTable = table.Select(mFilter).CopyToDataTable();
        }
    }

    internal bool Find(string v)
    {
        var temp = table.Select(mFilter).CopyToDataTable();
        if (temp.Rows.Count == 0) return false;
        var x = table.Rows.IndexOf(temp.Rows[0]);
        AbsolutePosition = x;
        return true;
    }

    private void Open()
    {
        if (!System.IO.File.Exists(Database))
        {
            MessageBox.Show("Database Not Found: " + Database);
            return;
        }

        var result = new DataSet();
        connection = new OleDbConnection(ConnectionString(Database));
        var command = new OleDbCommand(Source, connection);
        foreach(var key in Parameters.Keys)
        {
            var param = command.CreateParameter();
            param.ParameterName = key;
            param.Value = Parameters[key];
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
            if (!QuietErrors) sqlExecutionError(adapter.DeleteCommand.ToString(), e);
        }
        finally { connection.Close(); }

        mAddingRow = false;
    }

    public void AddNew()
    {
        var newRow = table.NewRow();
        table.Rows.InsertAt(newRow, table.Rows.Count);
        AbsolutePosition = table.Rows.Count - 1;
        mAddingRow = true;
    }

    public void Delete()
    {
        var cb = new OleDbCommandBuilder(adapter);
        try
        {
            connection.Open();
            adapter.DeleteCommand = cb.GetDeleteCommand();
            adapter.Update(table);
        }
        catch (Exception e)
        {
            if (!QuietErrors) sqlExecutionError(adapter.UpdateCommand.ToString(), e);
        }
        finally { connection.Close(); }
    }
}