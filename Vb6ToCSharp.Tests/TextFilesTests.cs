using System.IO;
using Vb6ToCSharp.Tests.Infrastructure;
using static Vb6ToCSharp.ItemConversion.TextFiles;

namespace Vb6ToCSharp.Tests;

public class TextFilesTests : IDisposable
{
    private readonly string dir = TestUtil.TempDir();

    public void Dispose()
    {
        try { Directory.Delete(dir, true); } catch { /* best effort */ }
    }

    private string F(string name = "f.txt") => Path.Combine(dir, Guid.NewGuid().ToString("N") + "_" + name);

    [Fact]
    public void WriteFile_WritesAndAppendsNewLine()
    {
        var f = F();
        Assert.True(WriteFile(f, "a"));
        Assert.True(WriteFile(f, "b"));
        Assert.Equal("a\r\nb\r\n", File.ReadAllText(f));
    }

    [Fact]
    public void WriteFile_OverWrite_ReplacesContents()
    {
        var f = F();
        WriteFile(f, "old");
        WriteFile(f, "new", true);
        Assert.Equal("new\r\n", File.ReadAllText(f));
    }

    [Fact]
    public void WriteFile_OverWrite_CreatesMissingFile()
    {
        var f = F();
        Assert.True(WriteFile(f, "x", true));
        Assert.Equal("x\r\n", File.ReadAllText(f));
    }

    [Fact]
    public void WriteFile_PreventNl_AndExistingCrLf_DoNotAddNewLine()
    {
        var f = F();
        WriteFile(f, "a", preventNl: true);
        WriteFile(f, "b\r\n");
        Assert.Equal("ab\r\n", File.ReadAllText(f));
    }

    [Fact]
    public void ReadEntireFile_ReturnsContents()
    {
        var f = F();
        File.WriteAllText(f, "x\r\ny");
        Assert.Equal("x\r\ny", ReadEntireFile(f));
    }

    [Fact] public void ReadEntireFile_Missing_IsEmpty() => Assert.Equal("", ReadEntireFile(F()));

    [Fact]
    public void ReadEntireFile_Empty_IsEmpty()
    {
        var f = F();
        File.WriteAllText(f, "");
        Assert.Equal("", ReadEntireFile(f));
    }

    [Fact]
    public void ReadEntireFileAndDelete_ReadsThenDeletes()
    {
        var f = F();
        File.WriteAllText(f, "z");
        Assert.Equal("z", ReadEntireFileAndDelete(f));
        Assert.False(File.Exists(f));
    }

    [Fact]
    public void DeleteFileIfExists_DeletesAndReports()
    {
        var f = F();
        File.WriteAllText(f, "z");
        Assert.True(DeleteFileIfExists(f));
        Assert.False(File.Exists(f));
        Assert.False(DeleteFileIfExists(f));
    }

    [Fact]
    public void ReadFile_Lines()
    {
        var f = F();
        File.WriteAllText(f, "l1\r\nl2\r\nl3");
        Assert.Equal("l1\r\nl2\r\nl3", ReadFile(f));
        Assert.Equal("l2", ReadFile(f, 2, 1));
        Assert.Equal("l2\r\nl3", ReadFile(f, 2));
        Assert.Equal("", ReadFile(f, 10));
        Assert.Equal("", ReadFile(F("missing.txt")));
    }

    [Fact]
    public void CountLines_IgnoresBlankAndCommentsByDefault()
    {
        const string s = "a\r\n\r\n  ' c\r\nd";
        Assert.Equal(2, CountLines(s));
        Assert.Equal(4, CountLines(s, false, ""));
        Assert.Equal(3, CountLines(s, true, ""));
    }

    [Fact]
    public void CountFileLines_CountsAllByDefault()
    {
        var f = F();
        File.WriteAllText(f, "a\r\n\r\nb");
        Assert.Equal(3, CountFileLines(f));
    }

    [Theory]
    [InlineData(1, 1, "a")]
    [InlineData(2, 1, "b")]
    [InlineData(2, 2, "b\r\nc")]
    [InlineData(2, 0, "b\r\nc")]
    [InlineData(3, 5, "c")]
    [InlineData(5, 1, "")]
    [InlineData(0, 1, "a")]
    public void LineByNumber_ReturnsRequestedLines(int start, int count, string expected)
        => Assert.Equal(expected, LineByNumber("a\r\nb\r\nc", start, count));

    [Fact]
    public void VbFileCountLines_BreaksDown()
    {
        var f = F("m.bas");
        File.WriteAllText(f, "Option Explicit\r\n\r\n' comment\r\nDim x\r\n");
        int t = 0, c = 0, b = 0, m = 0;
        Assert.True(VbFileCountLines(f, ref t, ref c, ref b, ref m));
        Assert.Equal(5, t);
        Assert.Equal(2, c);
        Assert.Equal(2, b);
        Assert.Equal(1, m);
        Assert.False(VbFileCountLines(F("missing.bas"), ref t, ref c, ref b, ref m));
    }
}
