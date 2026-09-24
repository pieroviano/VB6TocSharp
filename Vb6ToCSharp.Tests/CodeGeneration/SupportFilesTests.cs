using Vb6ToCSharp.CodeConversion.Model;
using Vb6ToCSharp.CodeGeneration;
using Vb6ToCSharp.Parsing;
using Vb6ToCSharp.Parsing.Model;
using Vb6ToCSharp.Tests.Fixtures;

namespace Vb6ToCSharp.Tests.CodeGeneration;

/// <summary>The .csproj of a converted project: what it references for ADO (Microsoft ActiveX Data Objects).</summary>
public class SupportFilesTests : IClassFixture<ConverterFixture>
{
    private const string AdoReference =
        @"Reference=*\G{2A75196C-D9EB-4129-B803-931327F72D5C}#2.8#0#..\msado28.tlb#Microsoft ActiveX Data Objects 2.8 Library";

    private readonly ConverterFixture fixture;

    public SupportFilesTests(ConverterFixture fixture) => this.fixture = fixture;

    private static ProjectInfo Project(params string[] lines) =>
        ProjectInfo.Parse("Type=Exe\r\n" + string.Join("\r\n", lines) + "\r\n");

    /// <summary>Emits the project file for one ADO target, then restores the process-wide settings.</summary>
    private static string ProjectFile(ProjectInfo project, AdoTarget? ado)
    {
        ProjectConfigurationParser.OverrideSettings(adoTarget: ado);
        try { return SupportFiles.ProjectFile(project); }
        finally { ProjectConfigurationParser.OverrideSettings(); }
    }

    [Theory]
    [InlineData(AdoReference)]
    [InlineData(@"Reference=*\G{00000205-0000-0010-8000-00AA006D2EA4}#2.5#0#..\msado25.tlb#ADO")]
    [InlineData(@"Reference=*\G{B691E011-1797-432E-907A-4D8C69339129}#6.1#0#..\msado15.dll#Microsoft ActiveX Data Objects 6.1 Library")]
    public void UsesAdo_RecognisesTheTypeLibrary(string reference) => Assert.True(SupportFiles.UsesAdo(Project(reference)));

    [Fact]
    public void UsesAdo_IgnoresOtherTypeLibraries() =>
        Assert.False(SupportFiles.UsesAdo(Project(@"Reference=*\G{00020430-0000-0000-C000-000000000046}#2.0#0#..\stdole2.tlb#OLE Automation")));

    [Fact]
    public void ProjectFile_Ado_ReferencesTheManagedPackageByDefault()
    {
        var csproj = ProjectFile(Project(AdoReference), null); // no override: the INI has no ADOTarget either

        Assert.Contains("<PackageReference Include=\"" + SupportFiles.AdoPackage + "\" Version=\"" + SupportFiles.AdoPackageVersion + "\" />", csproj);
        Assert.DoesNotContain("COMReference", csproj);
    }

    [Fact]
    public void ProjectFile_Ado_Com_EmitsTheTypeLibraryReference()
    {
        var csproj = ProjectFile(Project(AdoReference), AdoTarget.Com);

        Assert.Contains("<COMReference Include=\"ADODB\">", csproj);
        Assert.Contains("<EmbedInteropTypes>True</EmbedInteropTypes>", csproj);
        Assert.DoesNotContain(SupportFiles.AdoPackage, csproj);
    }

    [Fact]
    public void ProjectFile_NoAdo_ReferencesNeither()
    {
        var csproj = ProjectFile(Project("Startup=\"Sub Main\""), AdoTarget.Package);

        Assert.DoesNotContain(SupportFiles.AdoPackage, csproj);
        Assert.DoesNotContain("COMReference", csproj);
    }
}
