using System.IO;
using Vb6ToCSharp.Linting;
using Vb6ToCSharp.Tests.Infrastructure;

namespace Vb6ToCSharp.Tests;

public class QuickLintHelperTests
{
    [Fact] public void CleanLine_MasksStringsAndDropsComment() => Assert.Equal("x = SSSSS", QuickLint.CleanLine("x = \"a'b\" ' c"));
    [Fact] public void CleanLine_EscapedQuotes() => Assert.Equal("x = SSSSSS", QuickLint.CleanLine("x = \"a\"\"b\""));
    [Fact] public void CleanLine_UnterminatedString_Returns() => Assert.Equal("\"a\"\"", TestUtil.WithTimeout(() => QuickLint.CleanLine("\"a\"\"")));

    [Fact]
    public void StartsWith_And_StripLeft()
    {
        Assert.True(QuickLint.StartsWith("Dim x", "Dim "));
        Assert.Equal("x", QuickLint.StripLeft("Dim x", "Dim "));
        Assert.Equal("Private x", QuickLint.StripLeft("Private x", "Dim "));
    }

    [Fact]
    public void RecordError_FormatsAndCounts()
    {
        var errors = "";
        var n = 0;
        QuickLint.RecordError(ref errors, ref n, "Style", 7, "bad");
        Assert.Equal("[Style] Line     7: bad", errors);
        QuickLint.RecordError(ref errors, ref n, "Style", 8, "worse");
        Assert.Equal(2, n);
        Assert.Equal(2, errors.Split(new[] { "\r\n" }, StringSplitOptions.None).Length);
    }

    [Fact]
    public void RecordError_UnknownType_ReportsOnSeparateLine()
    {
        var errors = "";
        var n = 0;
        QuickLint.RecordError(ref errors, ref n, "Nope!", 1, "e");
        var lines = errors.Split(new[] { "\r\n" }, StringSplitOptions.None);
        Assert.Equal(2, lines.Length);
        Assert.Contains("Unknown error type", lines[0]);
        Assert.EndsWith(": e", lines[1]);
    }
}
