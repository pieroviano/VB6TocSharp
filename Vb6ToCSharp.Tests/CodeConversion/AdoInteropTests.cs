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
}
