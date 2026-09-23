using System.IO;
using System.Threading;
using Vb6ToCSharp.ItemConversion;
using Vb6ToCSharp.Tests.Infrastructure;
using static Vb6ToCSharp.Runtime.RuntimeExtension;

namespace Vb6ToCSharp.Tests;

public class Vb6StringSemanticsTests
{
    [Fact] public void Replace_EmptyOrNull_IsEmptyString() { Assert.Equal("", Replace("", "a", "b")); Assert.Equal("", Replace(null!, "a", "b")); }
    [Fact] public void Replace_Replaces() => Assert.Equal("xbx", Replace("aba", "a", "x"));
    [Fact] public void Replace_StartAndCount_FollowVb() { Assert.Equal("bx", Replace("aba", "a", "x", 2)); Assert.Equal("xba", Replace("aba", "a", "x", 1, 1)); }
    [Fact] public void Split_Empty_IsEmptyArray() { Assert.Empty(Split("", ",")); Assert.Empty(Split(null!, ",")); }
    [Fact] public void Split_Splits() => Assert.Equal(new[] { "a", "", "b" }, Split("a,,b", ","));

    [Fact] public void CountLines_Empty_IsZero() => Assert.Equal(0, TextFiles.CountLines("", false, ""));
    [Fact] public void CDbl_Date_IsOleDate() => Assert.Equal(2.5, CDbl(new DateTime(1900, 1, 1, 12, 0, 0)));
    [Fact] public void IsIde_WithoutDebugger_DoesNotBreak() => Assert.Equal(System.Diagnostics.Debugger.IsAttached, TestUtil.WithTimeout(() => ConversionUtility.IsIde()));

    [Fact]
    public void WriteFile_UnwritablePath_ReturnsFalse()
    {
        var f = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing-dir", "f.txt");
        Assert.False(TextFiles.WriteFile(f, "x"));
    }
}