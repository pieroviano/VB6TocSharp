using System;
using System.Collections.Generic;
using Extras.Tests.Fixtures;

namespace Extras.Tests;

public class CsvRecordTests
{
    static SampleCsvRecord Sample() => new SampleCsvRecord { Name = "Ann", City = "NY", Code = "1" };

    [Fact]
    public void HeaderLine_ListsTheDeclaredFieldNames() => Assert.Equal("Name,City,Code", Sample().HeaderLine());

    [Fact]
    public void HeaderLine_Commented_StartsWithAHash() => Assert.Equal("# Name,City,Code", Sample().HeaderLine(Commented: true));

    [Fact]
    public void HeaderLine_AddNL_EndsWithANewline() => Assert.Equal("Name,City,Code\n", Sample().HeaderLine(addNL: true));

    [Fact]
    public void ToLine_WritesTheDeclaredFieldsInOrder() => Assert.Equal("Ann,NY,1", Sample().ToLine());

    [Fact]
    public void ToLine_ProtectsValuesThatNeedQuoting()
        => Assert.Equal("\"Ann, A\",NY,\"q\"\"\"", new SampleCsvRecord { Name = "Ann, A", City = "NY", Code = "q\"" }.ToLine());

    [Fact]
    public void ToLine_NullFieldValue_WritesAnEmptyField()
        => Assert.Equal("Ann,,1", new SampleCsvRecord { Name = "Ann", City = null, Code = "1" }.ToLine());

    [Fact]
    public void FromLine_AssignsTheDeclaredFields()
    {
        var r = new SampleCsvRecord();
        r.FromLine("Ann,NY,1");
        Assert.Equal("Ann", r.Name);
        Assert.Equal("NY", r.City);
        Assert.Equal("1", r.Code);
    }

    [Fact]
    public void Constructor_WithALine_ParsesIt() => Assert.Equal("NY", new SampleCsvRecord("Ann,NY,1").City);

    [Fact]
    public void FromLine_UnquotesQuotedFields() => Assert.Equal("Ann, A", new SampleCsvRecord("\"Ann, A\",NY,1").Name);

    [Fact]
    public void FromLine_FewerFieldsThanDeclared_BlanksTheRest()
    {
        var r = Sample();
        r.FromLine("Bob");
        Assert.Equal("Bob", r.Name);
        Assert.Equal("", r.City);
        Assert.Equal("", r.Code);
    }

    [Fact]
    public void FromLine_KeepsTheValuesOfUndeclaredTrailingFields()
    {
        var r = new SampleCsvRecord("Ann,NY,1,extra1,extra2");
        Assert.Equal(new[] { "extra1", "extra2" }, r.ExtraValues);
    }

    [Fact]
    public void FromLine_ResetsExtraFieldsFromAPreviousLine()
    {
        var r = new SampleCsvRecord("Ann,NY,1,extra1");
        r.FromLine("Bob,LA,2");
        Assert.Empty(r.ExtraValues);
    }

    [Fact]
    public void FromLine_ThenToLine_RoundTripsExtraFields()
    {
        var line = "Ann,NY,1,extra1,extra2";
        Assert.Equal(line, new SampleCsvRecord(line).ToLine());
    }

    [Fact]
    public void Indexer_ByName_ReadsAndWritesADeclaredField()
    {
        var r = Sample();
        Assert.Equal("NY", r["city"]);
        r["City"] = "LA";
        Assert.Equal("LA", r.City);
    }

    [Fact]
    public void Indexer_ByName_Unknown_ThrowsArgumentException()
    {
        var r = Sample();
        Assert.Throws<ArgumentException>(() => r["nope"]);
        Assert.Throws<ArgumentException>(() => r["nope"] = "x");
    }

    [Fact]
    public void Indexer_ByIndex_ReadsADeclaredField() => Assert.Equal("NY", Sample()[1]);

    [Fact]
    public void Indexer_ByIndex_ReadsAnExtraField() => Assert.Equal("extra1", new SampleCsvRecord("Ann,NY,1,extra1")[3]);

    [Fact]
    public void Indexer_ByIndex_PastEverything_ReadsAsEmpty() => Assert.Equal("", Sample()[7]);

    [Fact]
    public void Indexer_ByIndex_WritesAnExtraFieldGrowingTheList()
    {
        var r = Sample();
        r[4] = "fifth";
        Assert.Equal(new[] { "", "fifth" }, r.ExtraValues);
        Assert.Equal("Ann,NY,1,,fifth", r.ToLine());
    }

    [Fact]
    public void Indexer_NegativeIndex_ThrowsArgumentOutOfRangeException()
    {
        var r = Sample();
        Assert.Throws<ArgumentOutOfRangeException>(() => r[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => r[-1] = "x");
    }

    [Fact]
    public void FromCsvFile_ReadsEveryDataLine()
    {
        var list = CsvRecord.FromCsvFile<SampleCsvRecord>("Ann,NY,1\r\nBob,LA,2\r\n");
        Assert.Equal(2, list.Count);
        Assert.Equal("Bob", list[1].Name);
        Assert.Equal("2", list[1].Code);
    }

    [Fact]
    public void FromCsvFile_SkipsBlankAndCommentedLines()
    {
        var list = CsvRecord.FromCsvFile<SampleCsvRecord>("# Name,City,Code\n\nAnn,NY,1\n\n");
        Assert.Single(list);
        Assert.Equal("Ann", list[0].Name);
    }

    [Fact]
    public void FromCsvFile_EmptyContents_ReturnsAnEmptyList()
        => Assert.Empty(CsvRecord.FromCsvFile<SampleCsvRecord>(""));

    [Fact]
    public void ToCsvFile_WritesOneCrLfTerminatedLinePerRecord()
    {
        var list = new List<SampleCsvRecord> { Sample(), new SampleCsvRecord("Bob,LA,2") };
        Assert.Equal("Ann,NY,1\r\nBob,LA,2\r\n", CsvRecord.ToCsvFile(list));
    }

    [Fact]
    public void ToCsvFile_AddHeader_PutsTheHeaderFirst()
        => Assert.Equal("Name,City,Code\r\nAnn,NY,1\r\n", CsvRecord.ToCsvFile(new List<SampleCsvRecord> { Sample() }, addHeader: true));

    [Fact]
    public void ToCsvFile_ThenFromCsvFile_RoundTripsTheRecords()
    {
        var written = CsvRecord.ToCsvFile(new List<SampleCsvRecord> { Sample(), new SampleCsvRecord("\"Bob, B\",LA,2") }, addHeader: true);
        var read = CsvRecord.FromCsvFile<SampleCsvRecord>(written);
        Assert.Equal(2, read.Count);
        Assert.Equal("Ann", read[0].Name);
        Assert.Equal("Bob, B", read[1].Name);
    }

    [Fact]
    public void ToString_RendersTheCsvLine()
    {
        // CsvRecord.md documents record.ToString() as the way to render a record.
        object r = Sample();
        Assert.Equal("Ann,NY,1", r.ToString());
    }

    [Fact]
    public void HeaderLine_UsesTheNameTheAttributeDeclares()
        => Assert.Equal("Total Amount,Due Date", new NamedFieldRecord().HeaderLine());

    [Fact]
    public void Indexer_FindsAFieldByItsDeclaredNameAndByItsMemberName()
    {
        var r = new NamedFieldRecord();
        r["total amount"] = "10";
        Assert.Equal("10", r.Total);
        Assert.Equal("10", r["Total"]);
    }

    [Fact]
    public void ARecordWithoutFieldDefinitions_IsAddressedByPosition()
    {
        // The usage CsvRecord.md documents under "Without Field Definitions".
        var record = new CsvRecord();
        record[0] = "field1";
        record[1] = "field2 \"with quotes\"";
        record[3] = "Field4";
        Assert.Equal("field1,\"field2 \"\"with quotes\"\"\",,Field4", record.ToString());
    }

    [Fact]
    public void ARecordWithoutFieldDefinitions_ReadsBackEveryField()
    {
        var record = new CsvRecord();
        record.FromLine("a,b,c");
        Assert.Equal("b", record[1]);
        Assert.Equal("a,b,c", record.ToLine());
    }

    [Fact]
    public void FromCsvFile_KeepsANewlineInsideAQuotedField()
    {
        var list = CsvRecord.FromCsvFile<SampleCsvRecord>("\"Ann\nA\",NY,1\r\nBob,LA,2\r\n");
        Assert.Equal(2, list.Count);
        Assert.Equal("Ann\nA", list[0].Name);
        Assert.Equal("Bob", list[1].Name);
    }

    [Fact]
    public void ToCsvFile_ThenFromCsvFile_RoundTripsAValueWithANewline()
    {
        var written = CsvRecord.ToCsvFile(new List<SampleCsvRecord> { new SampleCsvRecord { Name = "Ann\r\nA", City = "NY", Code = "1" } });
        var read = CsvRecord.FromCsvFile<SampleCsvRecord>(written);
        Assert.Single(read);
        Assert.Equal("Ann\r\nA", read[0].Name);
    }
}
