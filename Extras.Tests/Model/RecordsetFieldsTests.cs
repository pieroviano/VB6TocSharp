using System;
using System.Collections.Generic;
using System.Data;
using Extras.Model;

namespace Extras.Tests.Model;

public class RecordsetFieldsTests
{
    static DataRow Row()
    {
        var t = new DataTable("People");
        t.Columns.Add("Id", typeof(int));
        t.Columns.Add("Name", typeof(string));
        t.Rows.Add(1, "Ann");
        return t.Rows[0];
    }

    [Fact]
    public void Count_IsTheNumberOfColumns() => Assert.Equal(2, new RecordsetFields(Row()).Count);

    [Fact]
    public void Indexer_ByName_ReturnsAFieldBoundToTheRow()
    {
        var row = Row();
        var fields = new RecordsetFields(row);
        Assert.Equal("Ann", (string)fields["Name"].Value);
        fields["Name"].Value = "Bob";
        Assert.Equal("Bob", row["Name"]);
    }

    [Fact]
    public void Indexer_ByIndex_ReturnsAFieldBoundToTheRow() => Assert.Equal(1, (int)new RecordsetFields(Row())[0].Value);

    [Fact]
    public void Indexer_UnknownColumn_ThrowsArgumentException()
        => Assert.Throws<ArgumentException>(() => new RecordsetFields(Row())["nope"]);

    [Fact]
    public void Indexer_ColumnIndexOutOfRange_Throws()
        => Assert.Throws<IndexOutOfRangeException>(() => new RecordsetFields(Row())[9]);

    [Fact]
    public void Enumerator_YieldsOneFieldPerColumn()
    {
        var seen = new List<string>();
        foreach (RecordsetField f in new RecordsetFields(Row())) seen.Add((string)f.Value.ToString());
        Assert.Equal(new[] { "1", "Ann" }, seen);
    }

    [Fact]
    public void SyncRoot_CanBeLocked()
    {
        var fields = new RecordsetFields(Row());
        Assert.NotNull(fields.SyncRoot);
        lock (fields.SyncRoot) { }
        Assert.False(fields.IsSynchronized);
    }

    [Fact]
    public void CopyTo_IsNotSupported()
        => Assert.Throws<InvalidOperationException>(() => new RecordsetFields(Row()).CopyTo(new object[2], 0));
}
