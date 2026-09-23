using System.IO;
using Vb6ToCSharp.CodeConversion;
using Vb6ToCSharp.Tests.Fixtures;

using Vb6ToCSharp.Tests.Fixtures;
using static Vb6ToCSharp.Tests.Fixtures.ConverterTestHelpers;

namespace Vb6ToCSharp.Tests.CodeConversion;

/// <summary>VB Migration Partner '## pragmas: file / project settings and statement-level directives.</summary>
public class PragmaConverterTests : IClassFixture<ConverterFixture>
{
    private readonly ConverterFixture fixture;

    public PragmaConverterTests(ConverterFixture fixture)
    {
        this.fixture = fixture;
        ConverterUtils.ReComment("");
        ConverterUtils.InitDeString();
    }

    /// <summary>Converts with the file-level pragmas of <paramref name="fileHeader"/> in effect.</summary>
    private string WithPragmas(string fileHeader, System.Func<string> convert)
    {
        StatementsConverter.BeginFile(fileHeader.Replace("\n", "\r\n"));
        try { return convert(); }
        finally { Begin(); }
    }

    [Fact]
    public void ArrayBoundsForceZero_UsesZeroBasedArrays()
    {
        var cs = WithPragmas("'## ArrayBounds ForceZero\n", () => Segment(Sub("  Dim a(1 To 5) As Long")));
        Assert.Contains("int[] a = new int[6];", cs);
        Assert.DoesNotContain("VB6Array", cs);
    }

    [Fact]
    public void AutoNewFalse_CreatesEagerly()
    {
        var cs = WithPragmas("'## AutoNew False\n", () => TestUtil.WithTimeout(() => CodeConverter.ConvertGlobals("Private mCol As New Collection\r\n", true)));
        Assert.Contains("Collection mCol = new Collection();", cs);
    }

    [Fact]
    public void SetType_ForcesTheDeclaredType() =>
        Assert.Contains("int n = 0;", WithPragmas("'## n.SetType Long\n", () => Segment(Sub("  Dim n As Integer"))));

    [Fact]
    public void AutoDisposeForce_DisposesOnSetNothing() =>
        Assert.Contains("(o as IDisposable)?.Dispose(); o = null;", WithPragmas("'## AutoDispose Force\n", () => Segment(Sub("  Dim o As Object", "  Set o = Nothing"))));

    [Fact]
    public void StatementPragmas_InsertReplaceSkipAndKeep()
    {
        var cs = Segment(Sub("  Dim n As Long",
            "  '## InsertStatement Console.Beep();",
            "  '## ReplaceStatement n = 42;",
            "  n = 1",
            "  '## OutputMode Off",
            "  n = 2",
            "  '## OutputMode On",
            "  '## ParseMode Off",
            "  n = Weird ! Syntax",
            "  '## ParseMode On",
            "  '## Note checked by hand",
            "  n = 3"));
        Assert.Contains("Console.Beep();", cs);
        Assert.Contains("n = 42;", cs);
        Assert.DoesNotContain("n = 1;", cs);
        Assert.DoesNotContain("n = 2", cs);
        Assert.Contains("// VB6: n = Weird ! Syntax", cs);
        Assert.Contains("// NOTE: checked by hand", cs);
        Assert.Contains("n = 3;", cs);
    }

    [Fact]
    public void PreAndPostProcess_RewriteSourceAndOutput()
    {
        const string src = "'## PreProcess \"OldName\", \"NewName\"\r\n'## PostProcess \"Console\\.WriteLine\", \"Trace.WriteLine\"\r\nPublic Sub T()\r\n  OldName\r\nEnd Sub\r\n";
        Assert.Contains("NewName", PragmaConverter.PreProcess(src));
        var cs = WithPragmas(src, () => PragmaConverter.PostProcess("Console.WriteLine(1);"));
        Assert.Equal("Trace.WriteLine(1);", cs);
    }

    [Fact]
    public void ProjectPragmaFile_AppliesToEveryFile()
    {
        var file = Path.Combine(fixture.Dir, PragmaConverter.ProjectPragmaFile);
        File.WriteAllText(file, "ArrayBounds ForceZero\r\n");
        try
        {
            var cs = WithPragmas("", () => Segment(Sub("  Dim a(1 To 3) As Long")));
            Assert.Contains("int[] a = new int[4];", cs);
        }
        finally
        {
            File.Delete(file);
            Begin();
        }
    }

    [Fact]
    public void UnknownPragma_IsReported() => Assert.Contains("TODO: VB Migration Partner pragma not supported: Frobnicate", Segment(Sub("  '## Frobnicate x")));
}
