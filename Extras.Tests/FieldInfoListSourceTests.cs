using System;
using Extras.Tests.Fixtures;

namespace Extras.Tests;

public class FieldInfoListSourceTests
{
    [Fact]
    public void FieldInfoList_KeepsOnlyAnnotatedFields_InDeclarationOrder()
        => Assert.Equal(new[] { "Alpha", "Beta" }, new SampleFieldSource().Names());

    [Fact]
    public void FieldInfoList_WithoutAnnotatedFields_IsEmpty()
        => Assert.Equal(0, new EmptyFieldSource().DeclaredCount());

    [Fact]
    public void FieldInfoList_IsCachedPerType_NotPerSimpleTypeName()
    {
        // Two types called "Ambiguous" in different namespaces must not share a cache entry.
        Assert.Equal(new[] { "OnlyOnTheLeft" }, new Fixtures.Left.Ambiguous().Names());
        Assert.Equal(new[] { "OnlyOnTheRight", "AndASecondOne" }, new Fixtures.Right.Ambiguous().Names());
    }

    [Fact]
    public void ThisField_ByIndex_ReturnsTheDeclaredField()
        => Assert.Equal("Beta", new SampleFieldSource().FieldAt(1).Name);

    [Fact]
    public void ThisField_IndexPastTheEnd_ReturnsNull() => Assert.Null(new SampleFieldSource().FieldAt(2));

    [Fact]
    public void ThisField_NegativeIndex_ReturnsNull() => Assert.Null(new SampleFieldSource().FieldAt(-1));

    [Fact]
    public void ThisField_OnASourceWithNoFields_ReturnsNull() => Assert.Null(new EmptyFieldSource().FieldAt(0));

    [Fact]
    public void ThisField_ByName_IgnoresCase() => Assert.Equal("Alpha", new SampleFieldSource().FieldNamed("ALPHA").Name);

    [Fact]
    public void ThisField_UnknownName_ReturnsNull() => Assert.Null(new SampleFieldSource().FieldNamed("nope"));

    [Fact]
    public void ThisFieldMod_ReturnsTheRecordFieldAttribute()
    {
        var s = new SampleFieldSource();
        Assert.Equal(4, s.ModAt(0).max);
        Assert.Equal(3, s.ModNamed("beta").max);
    }

    [Fact]
    public void ThisFieldMod_UnknownField_ReturnsNull() => Assert.Null(new SampleFieldSource().ModNamed("nope"));

    [Fact]
    public void Indexer_ByName_ReadsAndWritesTheField()
    {
        var s = new SampleFieldSource { Alpha = "one" };
        Assert.Equal("one", s["Alpha"]);
        s["alpha"] = "two";
        Assert.Equal("two", s.Alpha);
    }

    [Fact]
    public void Indexer_ByIndex_ReadsAndWritesTheField()
    {
        var s = new SampleFieldSource();
        s[1] = "b";
        Assert.Equal("b", s.Beta);
        Assert.Equal("b", s[1]);
    }

    [Fact]
    public void Indexer_NullFieldValue_ReadsAsEmptyString()
        => Assert.Equal("", new SampleFieldSource { Alpha = null }["Alpha"]);

    [Fact]
    public void Indexer_UnknownName_ThrowsArgumentException()
    {
        var s = new SampleFieldSource();
        Assert.Throws<ArgumentException>(() => s["nope"]);
        Assert.Throws<ArgumentException>(() => s["nope"] = "x");
    }

    [Fact]
    public void Indexer_IndexPastTheEnd_ThrowsArgumentOutOfRangeException()
    {
        var s = new SampleFieldSource();
        Assert.Throws<ArgumentOutOfRangeException>(() => s[9]);
        Assert.Throws<ArgumentOutOfRangeException>(() => s[9] = "x");
    }
}
