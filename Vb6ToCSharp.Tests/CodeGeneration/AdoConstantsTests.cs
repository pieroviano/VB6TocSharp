using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Vb6ToCSharp.CodeGeneration;

namespace Vb6ToCSharp.Tests.CodeGeneration;

/// <summary>The ADO constants file emitted into a project that references Microsoft ActiveX Data Objects.</summary>
public class AdoConstantsTests
{
    private static readonly string File = AdoConstants.File("Sample");

    [Fact]
    public void File_IsValidCSharp()
    {
        var errors = CSharpSyntaxTree.ParseText(File).GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    [Fact]
    public void File_DeclaresTheConstantsInTheProjectNamespace()
    {
        Assert.Contains("namespace Sample", File);
        Assert.Contains("public static class " + AdoConstants.ClassName, File);
    }

    [Theory]
    [InlineData("adCmdText", "CommandTypeEnum")]
    [InlineData("adExecuteNoRecords", "ExecuteOptionEnum")]
    [InlineData("adStateClosed", "ObjectStateEnum")]
    [InlineData("adVarWChar", "DataTypeEnum")]
    [InlineData("adOpenForwardOnly", "CursorTypeEnum")]
    public void File_TakesEachConstantFromItsOwnEnum(string constant, string enumType) =>
        Assert.Contains("AdoValue " + constant + " = new AdoValue((int)ADODB." + enumType + "." + constant + ");", File);

    [Fact]
    public void File_DeclaresAConstantOnlyOnce()
    {
        // two enums declare adPosBOF (PositionEnum and the alias PositionEnum_Param), as they do in the type library
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(File, @"AdoValue adPosBOF ="));
    }

    [Fact]
    public void Value_ConvertsToIntAndToEveryEnumOfTheLibrary()
    {
        // VB6 sees a type library's enums as Long: the same constant serves an enum parameter and an Integer one
        Assert.Contains("public static implicit operator int(AdoValue c)", File);
        Assert.Contains("public static implicit operator ADODB.CommandTypeEnum(AdoValue c)", File);
        Assert.Contains("public static implicit operator ADODB.LockTypeEnum(AdoValue c)", File);
        Assert.Contains("public readonly struct AdoValue : IVbLibraryConstant", File); // late-bound calls read the number back
    }
}
