using System.IO;
using Vb6ToCSharp.Modules;
using Vb6ToCSharp.Tests.Infrastructure;

namespace Vb6ToCSharp.Tests;

public class ModQuickLintHelperTests
{
    [Fact] public void CleanLine_MasksStringsAndDropsComment() => Assert.Equal("x = SSSSS", ModQuickLint.CleanLine("x = \"a'b\" ' c"));
    [Fact] public void CleanLine_EscapedQuotes() => Assert.Equal("x = SSSSSS", ModQuickLint.CleanLine("x = \"a\"\"b\""));
    [Fact] public void CleanLine_UnterminatedString_Returns() => Assert.Equal("\"a\"\"", TestUtil.WithTimeout(() => ModQuickLint.CleanLine("\"a\"\"")));

    [Fact]
    public void StartsWith_And_StripLeft()
    {
        Assert.True(ModQuickLint.StartsWith("Dim x", "Dim "));
        Assert.Equal("x", ModQuickLint.StripLeft("Dim x", "Dim "));
        Assert.Equal("Private x", ModQuickLint.StripLeft("Private x", "Dim "));
    }

    [Fact]
    public void RecordError_FormatsAndCounts()
    {
        var errors = "";
        var n = 0;
        ModQuickLint.RecordError(ref errors, ref n, "Style", 7, "bad");
        Assert.Equal("[Style] Line     7: bad", errors);
        ModQuickLint.RecordError(ref errors, ref n, "Style", 8, "worse");
        Assert.Equal(2, n);
        Assert.Equal(2, errors.Split(new[] { "\r\n" }, StringSplitOptions.None).Length);
    }

    [Fact]
    public void RecordError_UnknownType_ReportsOnSeparateLine()
    {
        var errors = "";
        var n = 0;
        ModQuickLint.RecordError(ref errors, ref n, "Nope!", 1, "e");
        var lines = errors.Split(new[] { "\r\n" }, StringSplitOptions.None);
        Assert.Equal(2, lines.Length);
        Assert.Contains("Unknown error type", lines[0]);
        Assert.EndsWith(": e", lines[1]);
    }
}
