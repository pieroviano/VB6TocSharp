using System;
using System.Data;
using System.IO;

namespace Extras.Tests;

public class RecordSetTests
{
    static DataTable People()
    {
        var t = new DataTable("People");
        t.Columns.Add("Id", typeof(int));
        t.Columns.Add("Name", typeof(string));
        t.Rows.Add(1, "Ann");
        t.Rows.Add(2, "Bob");
        t.Rows.Add(3, "Cid");
        return t;
    }

    static RecordSet Open(DataTable t = null) => new RecordSet(t ?? People(), null, null);

    static DataTable Empty()
    {
        var t = People();
        t.Rows.Clear();
        return t;
    }

    [Fact]
    public void RecordCount_CountsTheRows() => Assert.Equal(3, Open().RecordCount);

    [Fact]
    public void RecordCount_WithoutATable_IsZero() => Assert.Equal(0, new RecordSet().RecordCount);

    [Fact]
    public void NewRecordset_StartsOnTheFirstRecord()
    {
        var rs = Open();
        Assert.Equal(0, rs.AbsolutePosition);
        Assert.False(rs.EOF);
    }

    [Fact]
    public void Position_IsAnAliasOfAbsolutePosition()
    {
        var rs = Open();
        rs.Position = 2;
        Assert.Equal(2, rs.AbsolutePosition);
        rs.AbsolutePosition = 1;
        Assert.Equal(1, rs.Position);
    }

    [Fact]
    public void MoveNext_WalksToTheEndAndStaysThere()
    {
        var rs = Open();
        Assert.Equal(1, rs.MoveNext());
        Assert.Equal(2, rs.MoveNext());
        Assert.Equal(3, rs.MoveNext());
        Assert.True(rs.EOF);
        Assert.Equal(3, rs.MoveNext());
    }

    [Fact]
    public void MovePrevious_StopsOnTheFirstRecord()
    {
        var rs = Open();
        rs.MoveLast();
        Assert.Equal(1, rs.MovePrevious());
        Assert.Equal(0, rs.MovePrevious());
        Assert.Equal(0, rs.MovePrevious());
    }

    [Fact]
    public void MoveFirstAndMoveLast_GoToTheEnds()
    {
        var rs = Open();
        Assert.Equal(2, rs.MoveLast());
        Assert.Equal(0, rs.MoveFirst());
    }

    [Fact]
    public void MoveLast_OnAnEmptyRecordset_LeavesAValidPosition()
    {
        var rs = Open(Empty());
        Assert.Equal(0, rs.MoveLast());
        Assert.True(rs.EOF);
    }

    [Fact]
    public void Eof_IsTrueForAnEmptyRecordset() => Assert.True(Open(Empty()).EOF);

    [Fact]
    public void Bof_IsTrueBeforeMovingOffTheFirstRecord()
    {
        var rs = Open();
        Assert.True(rs.BOF);
        rs.MoveNext();
        Assert.False(rs.BOF);
    }

    [Fact]
    public void FieldExists_AnswersForTheTablesColumns()
    {
        var rs = Open();
        Assert.True(rs.FieldExists("Name"));
        Assert.False(rs.FieldExists("Missing"));
        Assert.False(new RecordSet().FieldExists("Name"));
    }

    [Fact]
    public void FieldNames_ListsTheColumnsInOrder() => Assert.Equal(new[] { "Id", "Name" }, Open().FieldNames);

    [Fact]
    public void Fields_ReadsTheCurrentRow()
    {
        var rs = Open();
        rs.MoveNext();
        Assert.Equal("Bob", (string)rs.Fields["Name"].Value);
    }

    [Fact]
    public void Fields_WhenPastTheLastRecord_Throws()
    {
        var rs = Open();
        rs.MoveNext(); rs.MoveNext(); rs.MoveNext();
        Assert.Throws<ArgumentOutOfRangeException>(() => rs.Fields);
    }

    [Fact]
    public void Indexer_ReadsAndWritesTheCurrentRow()
    {
        var rs = Open();
        Assert.Equal("Ann", (string)rs["Name"]);
        rs["Name"] = "Anna";
        Assert.Equal("Anna", (string)rs.GetField("Name"));
    }

    [Fact]
    public void SetField_WritesThroughToTheTable()
    {
        var t = People();
        var rs = Open(t);
        rs.SetField("Name", "Zed");
        Assert.Equal("Zed", t.Rows[0]["Name"]);
    }

    [Fact]
    public void Field_IndexerReadsAndWritesTheCurrentRow()
    {
        var rs = Open();
        rs.MoveLast();
        Assert.Equal("Cid", (string)rs.Field["Name"]);
        rs.Field["Name"] = "Sid";
        Assert.Equal("Sid", (string)rs["Name"]);
    }

    [Fact]
    public void GetRows_ReturnsEveryRowsValues()
    {
        var rows = Open().GetRows();
        Assert.Equal(3, rows.Count);
        Assert.Equal(2, rows[0].Count);
        Assert.Equal("Ann", (string)rows[0][1]);
    }

    [Fact]
    public void GetRows_WithoutATable_IsEmpty() => Assert.Empty(new RecordSet().GetRows());

    [Fact]
    public void Filter_RestrictsWhatTheRecordsetSees()
    {
        var rs = Open();
        rs.Filter = "Id > 1";
        Assert.Equal("Id > 1", rs.Filter);
        Assert.Equal(2, rs.RecordCount);
        Assert.Equal("Bob", (string)rs["Name"]);
        rs.MoveNext();
        Assert.Equal("Cid", (string)rs["Name"]);
        Assert.Equal(2, rs.GetRows().Count);
    }

    [Fact]
    public void Filter_ThatMatchesNothing_EmptiesTheRecordset()
    {
        var rs = Open();
        rs.Filter = "Id > 99";
        Assert.Equal(0, rs.RecordCount);
        Assert.True(rs.EOF);
    }

    [Fact]
    public void Filter_RepositionsOnTheFirstMatchingRecord()
    {
        var rs = Open();
        rs.MoveLast();
        rs.Filter = "Id = 2";
        Assert.Equal(0, rs.AbsolutePosition);
        Assert.Equal("Bob", (string)rs["Name"]);
    }

    [Fact]
    public void Filter_ClearedAgain_ShowsEveryRow()
    {
        var rs = Open();
        rs.Filter = "Id = 2";
        rs.Filter = "";
        Assert.Equal(3, rs.RecordCount);
        Assert.Equal("Ann", (string)rs["Name"]);
    }

    [Fact]
    public void Filter_WithoutATable_IsAccepted()
    {
        var rs = new RecordSet();
        rs.Filter = "Id = 1";
        Assert.Equal(0, rs.RecordCount);
    }

    [Fact]
    public void Find_PositionsOnTheFirstMatchingRecord()
    {
        var rs = Open();
        Assert.True(rs.Find("Name = 'Cid'"));
        Assert.Equal(2, rs.AbsolutePosition);
        Assert.Equal("Cid", (string)rs["Name"]);
    }

    [Fact]
    public void Find_WithoutAMatch_ReturnsFalseAndKeepsThePosition()
    {
        var rs = Open();
        rs.MoveNext();
        Assert.False(rs.Find("Name = 'Nobody'"));
        Assert.Equal(1, rs.AbsolutePosition);
    }

    [Fact]
    public void Find_SearchesInsideTheActiveFilter()
    {
        var rs = Open();
        rs.Filter = "Id > 1";
        Assert.True(rs.Find("Name = 'Cid'"));
        Assert.Equal(1, rs.AbsolutePosition);
        Assert.Equal("Cid", (string)rs["Name"]);
    }

    [Fact]
    public void AddNew_AppendsARowAndMakesItCurrent()
    {
        var t = People();
        var rs = Open(t);
        rs.AddNew();
        Assert.True(rs.AddingRow);
        Assert.Equal(4, rs.RecordCount);
        Assert.Equal(3, rs.AbsolutePosition);
        rs["Name"] = "Dot";
        Assert.Equal("Dot", t.Rows[3]["Name"]);
    }

    [Fact]
    public void AddNew_ClearsAnActiveFilterSoTheNewRowIsCurrent()
    {
        var rs = Open();
        rs.Filter = "Id = 1";
        rs.AddNew();
        Assert.Equal(4, rs.RecordCount);
        Assert.Equal("", rs.Filter ?? "");
        Assert.Equal(3, rs.AbsolutePosition);
    }

    [Fact]
    public void AddingRow_IsFalseUntilAddNew() => Assert.False(Open().AddingRow);

    [Fact]
    public void Close_DropsTheData()
    {
        var rs = Open();
        rs.Close();
        Assert.Equal(0, rs.RecordCount);
        Assert.False(rs.FieldExists("Name"));
    }

    [Fact]
    public void Close_IsSafeToCallTwice()
    {
        var rs = Open();
        rs.Close();
        rs.Close();
        Assert.Equal(0, rs.RecordCount);
    }

    [Fact]
    public void Constructor_WithAMissingDatabase_LeavesTheRecordsetEmpty()
    {
        var missing = Path.Combine(Path.GetTempPath(), "extras-no-db-" + Guid.NewGuid().ToString("N") + ".mdb");
        var rs = new RecordSet("SELECT * FROM People", missing, QuietErrors: true);
        Assert.Equal(0, rs.RecordCount);
        Assert.True(rs.EOF);
    }

    [Fact]
    public void Constructor_WithoutParameters_DoesNotThrow()
    {
        // Parameters defaults to null: opening must not dereference it.
        var file = Path.Combine(Path.GetTempPath(), "extras-bad-db-" + Guid.NewGuid().ToString("N") + ".mdb");
        File.WriteAllText(file, "not a database");
        try
        {
            var rs = new RecordSet("SELECT * FROM People", file, QuietErrors: true);
            Assert.Equal(0, rs.RecordCount);
        }
        finally { File.Delete(file); }
    }
}
