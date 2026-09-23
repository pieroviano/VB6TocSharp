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
}