using static Extras.CsvHandler;

namespace Extras.Tests;

public class CsvHandlerTests
{
    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("", "")]
    [InlineData(" spaced ", " spaced ")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("line\nbreak", "\"line\nbreak\"")]
    [InlineData("carriage\rreturn", "\"carriage\rreturn\"")]
    public void ProtectCsv_QuotesOnlyWhenTheValueNeedsIt(string value, string expected)
        => Assert.Equal(expected, ProtectCsv(value));

    [Fact]
    public void ProtectCsv_NullBecomesEmptyString() => Assert.Equal("", ProtectCsv(null));

    [Fact]
    public void CsvLine_JoinsFieldsWithCommas() => Assert.Equal("a,b,c", CsvLine(new[] { "a", "b", "c" }));

    [Fact]
    public void CsvLine_ProtectsEachField() => Assert.Equal("\"a,1\",b,\"q\"\"q\"", CsvLine(new[] { "a,1", "b", "q\"q" }));

    [Fact]
    public void CsvLine_NullFieldBecomesEmptyField() => Assert.Equal("a,,c", CsvLine(new[] { "a", null, "c" }));

    [Fact]
    public void CsvLine_NullArray_ReturnsEmpty() => Assert.Equal("", CsvLine(null));

    [Fact]
    public void CsvLine_EmptyArray_ReturnsEmpty() => Assert.Equal("", CsvLine(new string[0]));

    [Fact]
    public void CsvLine_SingleEmptyField_ReturnsEmpty() => Assert.Equal("", CsvLine(new[] { "" }));

    [Theory]
    [InlineData("a,b,c", 0, "a")]
    [InlineData("a,b,c", 1, "b")]
    [InlineData("a,b,c", 2, "c")]
    [InlineData("a,b,c", 3, "")]
    [InlineData("a,b,c", 99, "")]
    [InlineData("a,,c", 1, "")]
    [InlineData("a,", 1, "")]
    [InlineData(",b", 0, "")]
    [InlineData("", 0, "")]
    public void CsvField_ReturnsTheFieldAtTheIndex(string line, int index, string expected)
        => Assert.Equal(expected, CsvField(line, index));

    [Theory]
    [InlineData("\"a,b\",c", 0, "a,b")]
    [InlineData("\"a,b\",c", 1, "c")]
    [InlineData("\"say \"\"hi\"\"\",c", 0, "say \"hi\"")]
    [InlineData("a,\"multi\nline\"", 1, "multi\nline")]
    [InlineData("\"\",x", 0, "")]
    public void CsvField_UnescapesQuotedFields(string line, int index, string expected)
        => Assert.Equal(expected, CsvField(line, index));

    [Theory]
    [InlineData("", 0)]
    [InlineData("a", 1)]
    [InlineData("a,b", 2)]
    [InlineData("a,b,c", 3)]
    [InlineData("a,", 2)]
    [InlineData(",", 2)]
    [InlineData(",,", 3)]
    [InlineData("\"a,b\",c", 2)]
    [InlineData("\"a\"\"b\"", 1)]
    public void CsvFieldCount_CountsFieldsRespectingQuotes(string line, int expected)
        => Assert.Equal(expected, CsvFieldCount(line));

    [Fact]
    public void CsvFieldCount_NullLine_IsZero() => Assert.Equal(0, CsvFieldCount(null));

    [Theory]
    [InlineData("plain")]
    [InlineData("with,comma")]
    [InlineData("with \"quotes\"")]
    [InlineData("with\nnewline")]
    [InlineData("")]
    public void CsvLine_ThenCsvField_RoundTripsAnyValue(string value)
    {
        var line = CsvLine(new[] { "before", value, "after" });
        Assert.Equal(value, CsvField(line, 1));
        Assert.Equal("before", CsvField(line, 0));
        Assert.Equal("after", CsvField(line, 2));
    }

    [Fact]
    public void CsvRecords_SplitsOnLineBreaks()
        => Assert.Equal(new[] { "a,b", "c,d" }, CsvRecords("a,b\r\nc,d\r\n"));

    [Fact]
    public void CsvRecords_AcceptsAnyLineEnding()
        => Assert.Equal(new[] { "a", "b", "c" }, CsvRecords("a\nb\rc"));

    [Fact]
    public void CsvRecords_KeepsANewlineThatBelongsToAQuotedField()
        => Assert.Equal(new[] { "\"multi\nline\",x", "next" }, CsvRecords("\"multi\nline\",x\nnext"));

    [Fact]
    public void CsvRecords_KeepsACarriageReturnPairInsideAQuotedField()
        => Assert.Single(CsvRecords("\"multi\r\nline\",x"));

    [Fact]
    public void CsvRecords_IsNotConfusedByAnEscapedQuote()
        => Assert.Equal(new[] { "\"a\"\"b\"", "next" }, CsvRecords("\"a\"\"b\"\nnext"));

    [Fact]
    public void CsvRecords_KeepsEmptyRecordsForEmptyLines()
        => Assert.Equal(new[] { "a", "", "b" }, CsvRecords("a\n\nb"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CsvRecords_WithNothingToSplit_IsEmpty(string contents) => Assert.Empty(CsvRecords(contents));
}
