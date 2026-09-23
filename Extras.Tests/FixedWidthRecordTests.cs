using Extras.Tests.Fixtures;

namespace Extras.Tests;

public class FixedWidthRecordTests
{
    [Fact]
    public void ToString_PadsEveryFieldToItsDeclaredWidth()
    {
        var r = new SampleFixedRecord { Name = "Ann", City = "NY", Code = "1" };
        Assert.Equal("Ann NY 1 ", r.ToString());
    }

    [Fact]
    public void ToString_TruncatesValuesWiderThanTheField()
    {
        var r = new SampleFixedRecord { Name = "Abcdefg", City = "Lond", Code = "123" };
        Assert.Equal("AbcdLon12", r.ToString());
    }

    [Fact]
    public void ToString_NullFieldValue_BecomesBlanks()
    {
        var r = new SampleFixedRecord { Name = null, City = "NY", Code = null };
        Assert.Equal("    NY   ", r.ToString());
    }

    [Fact]
    public void ToString_OverridesObjectToString()
    {
        object r = new SampleFixedRecord { Name = "Ann", City = "NY", Code = "1" };
        Assert.Equal("Ann NY 1 ", r.ToString());
    }

    [Fact]
    public void ToString_WrapsTheRecordInStartAndTerminator()
        => Assert.Equal("<ab >", new WrappedFixedRecord { Abc = "ab" }.ToString());

    [Fact]
    public void FromString_AssignsEachFieldItsOwnSlice()
    {
        var r = new SampleFixedRecord();
        r.fromString("Ann NY 1 ");
        Assert.Equal("Ann ", r.Name);
        Assert.Equal("NY ", r.City);
        Assert.Equal("1 ", r.Code);
    }

    [Fact]
    public void FromString_IgnoresCharactersPastTheLastField()
    {
        var r = new SampleFixedRecord();
        r.fromString("Ann NY 1 and more");
        Assert.Equal("Ann ", r.Name);
        Assert.Equal("1 ", r.Code);
    }

    [Fact]
    public void FromString_LineShorterThanTheRecord_PadsTheMissingFields()
    {
        var r = new SampleFixedRecord();
        r.fromString("Ann N");
        Assert.Equal("Ann ", r.Name);
        Assert.Equal("N  ", r.City);
        Assert.Equal("  ", r.Code);
    }

    [Fact]
    public void FromString_EmptyLine_LeavesEveryFieldBlank()
    {
        var r = new SampleFixedRecord { Name = "Ann", City = "NY", Code = "1" };
        r.fromString("");
        Assert.Equal("    ", r.Name);
        Assert.Equal("   ", r.City);
        Assert.Equal("  ", r.Code);
    }

    [Fact]
    public void FromString_ThenToString_ReproducesTheLine()
    {
        var line = "Ann NY 1 ";
        var r = new SampleFixedRecord();
        r.fromString(line);
        Assert.Equal(line, r.ToString());
    }
}
