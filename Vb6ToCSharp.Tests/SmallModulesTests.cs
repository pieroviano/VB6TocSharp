using System.IO;
using Vb6ToCSharp.Modules;

namespace Vb6ToCSharp.Tests;

public class ModConvertUtilsTests
{
    public ModConvertUtilsTests()
    {
        ModConvertUtils.ReComment(""); // flush any pending end-of-line comment
        ModConvertUtils.InitDeString();
    }

    [Fact]
    public void DeComment_StripsCommentAndReCommentRestoresIt()
    {
        Assert.Equal("x = 1", ModConvertUtils.DeComment("x = 1 ' hi"));
        Assert.Equal("y // hi", ModConvertUtils.ReComment("y"));
        Assert.Equal("z", ModConvertUtils.ReComment("z"));
    }

    [Fact] public void DeComment_IgnoresApostropheInString() => Assert.Equal("x = \"it's\"", ModConvertUtils.DeComment("x = \"it's\""));
    [Fact] public void DeComment_AfterQuotedApostrophe() => Assert.Equal("x = \"it's\"", ModConvertUtils.DeComment("x = \"it's\" ' c", true));

    [Fact]
    public void DeComment_Discard_DoesNotKeepComment()
    {
        ModConvertUtils.DeComment("a ' b", true);
        Assert.Equal("q", ModConvertUtils.ReComment("q"));
    }

    [Fact]
    public void ReComment_KeepVbComments_UsesApostrophe()
    {
        ModConvertUtils.DeComment("a ' b");
        Assert.Equal("q ' b", ModConvertUtils.ReComment("q", true));
    }

    [Fact]
    public void ReComment_MultiLine_AttachesToFirstLine()
    {
        ModConvertUtils.DeComment("a ' b");
        Assert.Equal("l1// b\r\nl2", ModConvertUtils.ReComment("l1\r\nl2"));
    }

    [Fact]
    public void DeString_ReString_RoundTrips()
    {
        const string src = "a = \"x\" & \"y\"\"z\"";
        var de = ModConvertUtils.DeString(src);
        Assert.DoesNotContain("\"", de);
        Assert.Contains(ModConvertUtils.deStringTokenBase, de);
        Assert.Equal(src, ModConvertUtils.ReString(de));
    }

    [Fact]
    public void ReString_ConvertString_EscapesForCSharp()
    {
        var de = ModConvertUtils.DeString("p = \"C:\\a\"\"b\"");
        Assert.Equal("p = \"C:\\\\a\\\"b\"", ModConvertUtils.ReString(de, true));
    }

    [Fact] public void DeString_NoQuotes_Unchanged() => Assert.Equal("a = b", ModConvertUtils.DeString("a = b"));
}

public class ModIniTests : IDisposable
{
    private readonly string dir = TestUtil.TempDir();
    public void Dispose() { try { Directory.Delete(dir, true); } catch { } }

    [Fact]
    public void WriteThenRead_RoundTrips()
    {
        var ini = Path.Combine(dir, "t.ini");
        Assert.True(ModIni.IniWrite("S1", "k", "v", ini));
        Assert.Equal("v", ModIni.IniRead("S1", "k", ini));
        Assert.Equal("w", ModIni.WriteIniValue(ini, "S2", "k2", "w"));
        Assert.Equal("w", ModIni.ReadIniValue(ini, "S2", "k2"));
        Assert.Equal("def", ModIni.ReadIniValue(ini, "S2", "missing", "def"));
    }

    [Fact]
    public void Sections_And_Keys_AreListed()
    {
        var ini = Path.Combine(dir, "s.ini");
        ModIni.IniWrite("A", "k1", "1", ini);
        ModIni.IniWrite("A", "k2", "2", ini);
        ModIni.IniWrite("B", "k3", "3", ini);
        Assert.Equal(new[] { "A", "B" }, ModIni.IniSections(ini));
        Assert.Equal(new[] { "k1", "k2" }, ModIni.IniSectionKeys(ini, "A"));
    }

    [Fact] public void Sections_MissingFile_IsEmpty() => Assert.Empty(ModIni.IniSections(Path.Combine(dir, "none.ini")));

    [Fact]
    public void Sections_LargeFile_GrowsBuffer()
    {
        var ini = Path.Combine(dir, "big.ini");
        for (var i = 0; i < 100; i++) ModIni.IniWrite("Section_" + i.ToString("000"), "k", "v", ini);
        var s = ModIni.IniSections(ini);
        Assert.Equal(100, s.Length);
        Assert.Equal("Section_099", s[99]);
    }
}

public class ModDirStackTests
{
    [Fact]
    public void PushPopPeek_RestoresDirectories()
    {
        var start = Directory.GetCurrentDirectory();
        var d1 = TestUtil.TempDir();
        var d2 = TestUtil.TempDir();
        try
        {
            Assert.Equal(d1, ModDirStack.PushDir(d1), StringComparer.OrdinalIgnoreCase);
            ModDirStack.PushDir(d2);
            Assert.Equal(d2, Directory.GetCurrentDirectory(), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(d1, ModDirStack.PeekDir(false), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(d1, ModDirStack.PopDir(), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(d1, Directory.GetCurrentDirectory(), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(start, ModDirStack.PopDir(), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(start, Directory.GetCurrentDirectory(), StringComparer.OrdinalIgnoreCase);
            Assert.Equal("", ModDirStack.PopDir());
        }
        finally
        {
            Directory.SetCurrentDirectory(start);
        }
    }
}

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
