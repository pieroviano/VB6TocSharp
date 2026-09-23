using Vb6ToCSharp.Infrastructure;
using Vb6ToCSharp.Tests.Infrastructure;

namespace Vb6ToCSharp.Tests;

public class DirStackTests
{
    [Fact]
    public void PushPopPeek_RestoresDirectories()
    {
        var start = Directory.GetCurrentDirectory();
        var d1 = TestUtil.TempDir();
        var d2 = TestUtil.TempDir();
        try
        {
            Assert.Equal(d1, DirStack.PushDir(d1), StringComparer.OrdinalIgnoreCase);
            DirStack.PushDir(d2);
            Assert.Equal(d2, Directory.GetCurrentDirectory(), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(d1, DirStack.PeekDir(false), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(d1, DirStack.PopDir(), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(d1, Directory.GetCurrentDirectory(), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(start, DirStack.PopDir(), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(start, Directory.GetCurrentDirectory(), StringComparer.OrdinalIgnoreCase);
            Assert.Equal("", DirStack.PopDir());
        }
        finally
        {
            Directory.SetCurrentDirectory(start);
        }
    }
}