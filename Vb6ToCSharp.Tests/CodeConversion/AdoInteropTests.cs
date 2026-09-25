using Vb6ToCSharp.CodeConversion;

namespace Vb6ToCSharp.Tests.CodeConversion;

/// <summary>What the conversion knows about ADO: its objects, and the members VB6 calls with parentheses.</summary>
public class AdoInteropTests
{
    [Theory]
    [InlineData("Recordset")]
    [InlineData("ADODB.Recordset")]
    [InlineData("ADODB.Connection")]
    [InlineData("Fields")]
    public void IsType_AcceptsTheLibraryObjects_QualifiedOrNot(string type) => Assert.True(AdoInterop.IsType(type));

    [Theory]
    [InlineData("String")]
    [InlineData("MyRecordset")]
    [InlineData("DAO.Recordset")]
    public void IsType_RejectsAnythingElse(string type) => Assert.False(AdoInterop.IsType(type));

    [Theory]
    [InlineData("Recordset")]
    [InlineData("ADODB.Recordset")]
    public void IsRecordset_AcceptsBothSpellings(string type) => Assert.True(AdoInterop.IsRecordset(type));

    [Fact]
    public void IsRecordset_RejectsAnotherAdoObject() => Assert.False(AdoInterop.IsRecordset("ADODB.Connection"));

    [Theory]
    [InlineData("ADODB.Recordset", "Fields")]
    [InlineData("ADODB.Command", "Parameters")]
    [InlineData("ADODB.Connection", "Errors")]
    [InlineData("Fields", "Item")]
    public void IsIndexedMember_AcceptsAParameterizedProperty(string type, string member) =>
        Assert.True(AdoInterop.IsIndexedMember(type, member));

    [Theory]
    [InlineData("ADODB.Recordset", "Open")] // a method, called with parentheses in C# too
    [InlineData("", "Fields")] // an object of unknown type: nothing to go by
    [InlineData("MyClass", "Fields")]
    public void IsIndexedMember_RejectsAnythingElse(string type, string member) =>
        Assert.False(AdoInterop.IsIndexedMember(type, member));

    [Theory]
    [InlineData("ADODB.Command", "Execute")]
    [InlineData("Connection", "Execute")]
    [InlineData("ADODB.Recordset", "Open")]
    public void HasSignature_KnowsTheMethodsWithOmittableArguments(string type, string member) =>
        Assert.True(AdoInterop.HasSignature(type, member));

    [Theory]
    [InlineData("ADODB.Connection", "BeginTrans")] // no arguments: nothing to leave out
    [InlineData("ADODB.Recordset", "NoSuchMethod")]
    [InlineData("MyClass", "Execute")]
    public void HasSignature_RejectsAnythingElse(string type, string member) =>
        Assert.False(AdoInterop.HasSignature(type, member));

    /// <summary>
    /// cmd.Execute , , adExecuteNoRecords: the omitted ByRef RecordsAffected becomes out _, the omitted Parameters
    /// is left off altogether, and what follows it has to be named.
    /// </summary>
    [Fact]
    public void Arguments_NamesWhatFollowsAnOmittedArgument()
    {
        Assert.Equal("out _, options: adExecuteNoRecords",
            AdoInterop.Arguments("ADODB.Command", "Execute",
                new[] { "Missing", "Missing", "adExecuteNoRecords" }));
    }

    /// <summary>cn.Execute sql, , adExecuteNoRecords: nothing is skipped, so nothing needs naming.</summary>
    [Fact]
    public void Arguments_KeepsThePositionsWhenOnlyAByRefArgumentIsOmitted()
    {
        Assert.Equal("sql, out _, adExecuteNoRecords",
            AdoInterop.Arguments("ADODB.Connection", "Execute",
                new[] { "sql", "Missing", "adExecuteNoRecords" }));
    }

    [Fact]
    public void Arguments_LeavesOffAnOmittedArgumentAtTheEnd()
    {
        Assert.Equal("src, cn",
            AdoInterop.Arguments("ADODB.Recordset", "Open",
                new[] { "src", "cn", "Missing", "Missing", "Missing" }));
    }

    [Fact]
    public void Arguments_NamesEveryArgumentAfterTheFirstOmission()
    {
        Assert.Equal("crit, searchDirection: adSearchBackward",
            AdoInterop.Arguments("ADODB.Recordset", "Find",
                new[] { "crit", "Missing", "adSearchBackward" }));
    }

    [Fact]
    public void Arguments_RefusesAMethodItDoesNotKnow() =>
        Assert.Null(AdoInterop.Arguments("ADODB.Recordset", "NoSuchMethod", new[] { "Missing" }));

    [Fact]
    public void Arguments_RefusesMoreArgumentsThanTheMethodTakes() =>
        Assert.Null(AdoInterop.Arguments("ADODB.Recordset", "Requery", new[] { "Missing", "x" }));

    /// <summary>VB6 reads the record count back from the variable it passes; out _ would lose it.</summary>
    [Fact]
    public void Arguments_RefusesAVariablePassedToAByRefParameter() =>
        Assert.Null(AdoInterop.Arguments("ADODB.Connection", "Execute", new[] { "sql", "rows", "Missing" }));
}
