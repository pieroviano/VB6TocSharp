using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Extras.Tests;

public class DataRowExtensionsTests
{
    static DataTable People()
    {
        var t = new DataTable("People");
        t.Columns.Add("Id", typeof(int));
        t.Columns.Add("Name", typeof(string));
        t.Rows.Add(1, "Ann");
        t.Rows.Add(2, "Bob");
        return t;
    }

    [Fact]
    public void CopyToDataTable_ClonesTheSchemaAndCopiesTheRows()
    {
        var copy = People().Select("Id > 0").CopyToDataTable();
        Assert.Equal(2, copy.Rows.Count);
        Assert.Equal(new[] { "Id", "Name" }, copy.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
        Assert.Equal(typeof(int), copy.Columns["Id"].DataType);
        Assert.Equal("Bob", copy.Rows[1]["Name"]);
    }

    [Fact]
    public void CopyToDataTable_NoRows_ReturnsAnEmptyTableInsteadOfThrowing()
    {
        var copy = People().Select("Id > 99").CopyToDataTable();
        Assert.Equal(0, copy.Rows.Count);
    }

    [Fact]
    public void CopyToDataTable_SkipsNullRows()
    {
        var t = People();
        var rows = new List<DataRow> { null, t.Rows[0], null };
        Assert.Single(rows.CopyToDataTable().Rows);
    }

    [Fact]
    public void CopyToDataTable_OnlyNullRows_ReturnsAnEmptyTable()
        => Assert.Equal(0, new List<DataRow> { null }.CopyToDataTable().Rows.Count);

    [Fact]
    public void CopyToDataTable_NullSequence_ThrowsArgumentNullException()
        => Assert.Throws<ArgumentNullException>(() => ((IEnumerable<DataRow>)null).CopyToDataTable());

    [Fact]
    public void CopyToDataTable_LeavesTheSourceTableAlone()
    {
        var t = People();
        t.Select("Id > 0").CopyToDataTable();
        Assert.Equal(2, t.Rows.Count);
    }

    [Fact]
    public void CopyToDataTable_ProducesAnIndependentTable()
    {
        var t = People();
        var copy = t.Select("Id = 1").CopyToDataTable();
        copy.Rows[0]["Name"] = "Changed";
        Assert.Equal("Ann", t.Rows[0]["Name"]);
    }
}
