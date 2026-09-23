using System.Reflection;
using Extras.Model;
using Extras.Tests.Fixtures;

namespace Extras.Tests.Model;

public class RecordFieldTests
{
    [Fact]
    public void Constructor_StoresEveryArgument()
    {
        var f = new RecordField("Total", "decimal", 12, 5);
        Assert.Equal("Total", f.name);
        Assert.Equal("decimal", f.type);
        Assert.Equal(12, f.max);
        Assert.Equal(5, f.order);
    }

    [Fact]
    public void Constructor_DefaultsToEmptyMetadata()
    {
        var f = new RecordField();
        Assert.Equal("", f.name);
        Assert.Equal("", f.type);
        Assert.Equal(0, f.max);
    }

    [Fact]
    public void Order_DefaultsToTheDeclarationLine()
    {
        // The ordering FieldInfoListSource relies on comes from [CallerLineNumber].
        var alpha = typeof(SampleFieldSource).GetField("Alpha").GetCustomAttribute<RecordField>();
        var beta = typeof(SampleFieldSource).GetField("Beta").GetCustomAttribute<RecordField>();
        Assert.True(alpha.order > 0, "CallerLineNumber should fill in the declaration line");
        Assert.True(beta.order > alpha.order, "later fields must sort after earlier ones");
    }
}
