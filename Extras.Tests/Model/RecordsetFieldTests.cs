using System;
using System.Data;
using Extras.Model;

namespace Extras.Tests.Model;

public class RecordsetFieldTests
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
    public void Value_ReadsTheColumnByName() => Assert.Equal("Ann", (string)new RecordsetField(Row(), "Name").Value);

    [Fact]
    public void Value_ReadsTheColumnByIndex() => Assert.Equal(1, (int)new RecordsetField(Row(), 0).Value);

    [Fact]
    public void Value_WritesThroughToTheRow()
    {
        var row = Row();
        new RecordsetField(row, "Name").Value = "Bob";
        Assert.Equal("Bob", row["Name"]);
    }

    [Fact]
    public void Type_IsTheColumnDataType()
    {
        Assert.Equal(typeof(int), new RecordsetField(Row(), "Id").Type);
        Assert.Equal(typeof(string), new RecordsetField(Row(), "Name").Type);
    }

    [Fact]
    public void Name_KeepsWhatTheCallerAskedFor() => Assert.Equal("Name", (string)new RecordsetField(Row(), "Name").Name);

    [Fact]
    public void Size_IsTheColumnsDefinedSize()
    {
        var t = new DataTable("T");
        t.Columns.Add("Short", typeof(string)).MaxLength = 10;
        t.Columns.Add("Any", typeof(string));
        t.Rows.Add("abc", "def");

        Assert.Equal(10, new RecordsetField(t.Rows[0], "Short").Size);
        Assert.Equal(-1, new RecordsetField(t.Rows[0], "Any").Size);
    }
}
