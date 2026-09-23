using System.IO;
using Vb6ToCSharp.Infrastructure;
using Vb6ToCSharp.Tests.Fixtures;

namespace Vb6ToCSharp.Tests.Infrastructure;

public class IniInteropTests : IDisposable
{
    private readonly string dir = TestUtil.TempDir();
    public void Dispose() { try { Directory.Delete(dir, true); } catch { } }

    [Fact]
    public void WriteThenRead_RoundTrips()
    {
        var ini = Path.Combine(dir, "t.ini");
        Assert.True(IniInterop.IniWrite("S1", "k", "v", ini));
        Assert.Equal("v", IniInterop.IniRead("S1", "k", ini));
        Assert.Equal("w", IniInterop.WriteIniValue(ini, "S2", "k2", "w"));
        Assert.Equal("w", IniInterop.ReadIniValue(ini, "S2", "k2"));
        Assert.Equal("def", IniInterop.ReadIniValue(ini, "S2", "missing", "def"));
    }

    [Fact]
    public void Sections_And_Keys_AreListed()
    {
        var ini = Path.Combine(dir, "s.ini");
        IniInterop.IniWrite("A", "k1", "1", ini);
        IniInterop.IniWrite("A", "k2", "2", ini);
        IniInterop.IniWrite("B", "k3", "3", ini);
        Assert.Equal(new[] { "A", "B" }, IniInterop.IniSections(ini));
        Assert.Equal(new[] { "k1", "k2" }, IniInterop.IniSectionKeys(ini, "A"));
    }

    [Fact] public void Sections_MissingFile_IsEmpty() => Assert.Empty(IniInterop.IniSections(Path.Combine(dir, "none.ini")));

    [Fact]
    public void Sections_LargeFile_GrowsBuffer()
    {
        var ini = Path.Combine(dir, "big.ini");
        for (var i = 0; i < 100; i++) IniInterop.IniWrite("Section_" + i.ToString("000"), "k", "v", ini);
        var s = IniInterop.IniSections(ini);
        Assert.Equal(100, s.Length);
        Assert.Equal("Section_099", s[99]);
    }

    string Write(string name, params string[] lines)
    {
        var p = Path.Combine(dir, name);
        File.WriteAllText(p, string.Join("\r\n", lines) + "\r\n");
        return p;
    }

    [Fact]
    public void Read_IsCaseInsensitiveInSectionAndKey()
    {
        var ini = Write("case.ini", "[Alpha]", "Plain=value");
        Assert.Equal("value", IniInterop.IniRead("ALPHA", "PLAIN", ini));
        Assert.Equal("value", IniInterop.IniRead("alpha", "plain", ini));
    }

    [Fact]
    public void Read_TrimsSpacesAndTabsAroundTheValue()
    {
        var ini = Write("trim.ini", "[A]", "spaced   =   padded value   ", "tabbed=\ttabbed\tvalue\t");
        Assert.Equal("padded value", IniInterop.IniRead("A", "spaced", ini));
        Assert.Equal("tabbed\tvalue", IniInterop.IniRead("A", "tabbed", ini));
    }

    [Fact]
    public void Read_DropsOneSurroundingPairOfQuotes()
    {
        var ini = Write("quote.ini", "[A]", "quoted=\"in quotes\"", "half=\"oops", "inner=has \"quotes\" in it");
        Assert.Equal("in quotes", IniInterop.IniRead("A", "quoted", ini));
        Assert.Equal("\"oops", IniInterop.IniRead("A", "half", ini));
        Assert.Equal("has \"quotes\" in it", IniInterop.IniRead("A", "inner", ini));
    }

    [Fact]
    public void Read_KeepsEverythingAfterTheFirstEquals()
        => Assert.Equal("a=b", IniInterop.IniRead("A", "k", Write("eq.ini", "[A]", "k=a=b")));

    [Fact]
    public void Read_TakesTheFirstOfRepeatedKeysAndSections()
    {
        var ini = Write("dup.ini", "[A]", "dup=first", "dup=second", "[B]", "k=v", "[A]", "late=x");
        Assert.Equal("first", IniInterop.IniRead("A", "dup", ini));
        // A section repeated later in the file is dead, as it was for the profile API.
        Assert.Equal("", IniInterop.IniRead("A", "late", ini));
    }

    [Fact]
    public void Read_SkipsSemicolonCommentsButNotHashes()
    {
        var ini = Write("cmt.ini", "[A]", "; k=commented", "#k2=hash", "k=real");
        Assert.Equal("real", IniInterop.IniRead("A", "k", ini));
        Assert.Equal("hash", IniInterop.IniRead("A", "#k2", ini));
    }

    [Fact]
    public void Read_AnswersEmptyForWhatIsNotThere()
    {
        var ini = Write("miss.ini", "[A]", "k=v", "noequals");
        Assert.Equal("", IniInterop.IniRead("A", "nope", ini));
        Assert.Equal("", IniInterop.IniRead("Zeta", "k", ini));
        Assert.Equal("", IniInterop.IniRead("A", "noequals", ini));
        Assert.Equal("", IniInterop.IniRead("A", "k", Path.Combine(dir, "none.ini")));
    }

    [Fact]
    public void Read_HandlesAValueLongerThanTheOldBuffer()
    {
        // The API this replaced cut the value at 255 characters.
        var long_ = new string('x', 400);
        Assert.Equal(long_, IniInterop.IniRead("A", "k", Write("long.ini", "[A]", "k=" + long_)));
    }

    [Fact]
    public void SectionKeys_ListsEntriesInFileOrderAndSkipsComments()
    {
        var ini = Write("keys.ini", "; head", "[A]", "k1=1", "; comment", "", "k2=2", "[B]", "k3=3");
        Assert.Equal(new[] { "k1", "k2" }, IniInterop.IniSectionKeys(ini, "A"));
        Assert.Null(IniInterop.IniSectionKeys(ini, "Zeta"));
        Assert.Null(IniInterop.IniSectionKeys(ini, "A2"));
    }

    [Fact]
    public void Sections_AreListedInFileOrder()
        => Assert.Equal(new[] { "A", "B" }, IniInterop.IniSections(Write("secs.ini", "; c", "[A]", "k=1", "[B]", "k=2")));

    [Fact]
    public void Write_ReplacesTheValueAndLeavesTheRestOfTheFileAlone()
    {
        var ini = Write("edit.ini", "; keep me", "[A]", "k1=1", "; comment", "k2=2", "[B]", "k3=3");
        Assert.True(IniInterop.IniWrite("A", "k1", "changed", ini));

        Assert.Equal(new[] { "; keep me", "[A]", "k1=changed", "; comment", "k2=2", "[B]", "k3=3" },
            File.ReadAllLines(ini));
    }

    [Fact]
    public void Write_PutsANewKeyAtTheEndOfItsOwnSection()
    {
        var ini = Write("insert.ini", "[A]", "k1=1", "", "[B]", "k3=3");
        IniInterop.IniWrite("A", "k2", "2", ini);
        Assert.Equal(new[] { "[A]", "k1=1", "k2=2", "", "[B]", "k3=3" }, File.ReadAllLines(ini));
    }

    [Fact]
    public void Write_PutsANewSectionAtTheEndOfTheFile()
    {
        var ini = Write("newsec.ini", "[A]", "k=1");
        IniInterop.IniWrite("Z", "k", "9", ini);
        Assert.Equal(new[] { "[A]", "k=1", "[Z]", "k=9" }, File.ReadAllLines(ini));
        Assert.Equal("9", IniInterop.IniRead("Z", "k", ini));
    }

    [Fact]
    public void Write_CreatesTheFileWhenItIsNotThere()
    {
        var ini = Path.Combine(dir, "created.ini");
        Assert.True(IniInterop.IniWrite("A", "k", "v", ini));
        Assert.True(File.Exists(ini));
        Assert.Equal("v", IniInterop.IniRead("A", "k", ini));
    }

    [Fact]
    public void Write_KeepsTheLineEndingTheFileUses()
    {
        var ini = Path.Combine(dir, "lf.ini");
        File.WriteAllText(ini, "[A]\nk=1\n");
        IniInterop.IniWrite("A", "k", "2", ini);
        var text = File.ReadAllText(ini);
        Assert.DoesNotContain("\r", text);
        Assert.Equal("[A]\nk=2\n", text);
    }

    [Fact]
    public void Write_MatchesTheKeyCaseInsensitivelyAndKeepsTheFilesSpelling()
    {
        var ini = Write("keycase.ini", "[A]", "MyKey=1");
        IniInterop.IniWrite("a", "MYKEY", "2", ini);
        Assert.Equal(new[] { "[A]", "MyKey=2" }, File.ReadAllLines(ini));
    }

    [Fact]
    public void Write_ANullValueRemovesTheKey()
    {
        var ini = Write("delkey.ini", "[A]", "k1=1", "k2=2");
        IniInterop.IniWrite("A", "k1", null, ini);
        Assert.Equal(new[] { "[A]", "k2=2" }, File.ReadAllLines(ini));
    }

    [Fact]
    public void Write_ANullKeyRemovesTheSection()
    {
        var ini = Write("delsec.ini", "[A]", "k=1", "[B]", "k=2");
        IniInterop.IniWrite("A", null, null, ini);
        Assert.Equal(new[] { "[B]", "k=2" }, File.ReadAllLines(ini));
        Assert.Equal(new[] { "B" }, IniInterop.IniSections(ini));
    }

    [Fact]
    public void Write_WritesTheValueVerbatimEvenThoughReadingTrimsIt()
    {
        var ini = Path.Combine(dir, "verbatim.ini");
        IniInterop.IniWrite("A", "k", "trailing   ", ini);
        Assert.Contains("k=trailing   ", File.ReadAllText(ini));
        Assert.Equal("trailing", IniInterop.IniRead("A", "k", ini));
    }

    [Fact]
    public void Write_ToAnUnwritablePath_ReturnsFalse()
        => Assert.False(IniInterop.IniWrite("A", "k", "v", Path.Combine(dir, "no-such-dir", "x.ini")));
}
