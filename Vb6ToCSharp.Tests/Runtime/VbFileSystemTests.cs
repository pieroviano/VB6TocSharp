using System;
using System.IO;
using Vb6ToCSharp.Runtime.Model;
using static Vb6ToCSharp.Runtime.VbFileSystem;

namespace Vb6ToCSharp.Tests.Runtime;

/// <summary>
/// VbFileSystem replaces Microsoft.VisualBasic.FileSystem: Dir answers the bare file name, "" when
/// nothing matches, and continues through Dir().
/// </summary>
public class VbFileSystemTests : IDisposable
{
    private readonly string folder;
    private readonly string wasCurrent;

    public VbFileSystemTests()
    {
        wasCurrent = Directory.GetCurrentDirectory();
        folder = Path.Combine(Path.GetTempPath(), "vbfs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(wasCurrent);
        try { Directory.Delete(folder, true); }
        catch (IOException) { }
    }

    string File_(string name, string text = "x")
    {
        var p = Path.Combine(folder, name);
        System.IO.File.WriteAllText(p, text);
        return p;
    }

    [Fact]
    public void Dir_AnswersTheFileNameOfAMatch() => Assert.Equal("a.txt", Dir(File_("a.txt")));

    [Fact]
    public void Dir_AnswersEmptyWhenNothingMatches()
    {
        Assert.Equal("", Dir(Path.Combine(folder, "missing.txt")));
        Assert.Equal("", Dir(Path.Combine(folder, "nope", "missing.txt")));
    }

    [Fact]
    public void Dir_WalksEveryMatchOfAPattern()
    {
        File_("a.txt");
        File_("b.txt");
        File_("c.dat");

        var first = Dir(Path.Combine(folder, "*.txt"));
        var second = Dir();
        var third = Dir();

        Assert.Equal(new[] { "a.txt", "b.txt" }, new[] { first, second });
        Assert.Equal("", third);
    }

    [Fact]
    public void Dir_WithoutTheDirectoryAttribute_SkipsFolders()
    {
        Directory.CreateDirectory(Path.Combine(folder, "sub"));
        Assert.Equal("", Dir(Path.Combine(folder, "sub")));
        Assert.Equal("sub", Dir(Path.Combine(folder, "sub"), FileAttribute.Directory));
    }

    [Fact]
    public void CurDirAndChDir_MoveTheProcess()
    {
        ChDir(folder);
        // The temp folder may be reached through a link, so compare what the OS reports.
        Assert.Equal(Directory.GetCurrentDirectory(), CurDir());
        Assert.EndsWith(Path.GetFileName(folder), CurDir());
    }

    [Fact]
    public void ChDir_OfNothing_Throws()
    {
        Assert.Throws<ArgumentException>(() => ChDir(""));
        Assert.Throws<DirectoryNotFoundException>(() => ChDir(Path.Combine(folder, "missing")));
    }

    [Fact]
    public void FileLen_IsTheByteCount() => Assert.Equal(5, FileLen(File_("len.txt", "12345")));

    [Fact]
    public void FileLen_OfAMissingFile_Throws()
        => Assert.Throws<FileNotFoundException>(() => FileLen(Path.Combine(folder, "missing.txt")));

    [Fact]
    public void FileDateTime_IsTheLastWriteTime()
    {
        var p = File_("when.txt");
        Assert.Equal(System.IO.File.GetLastWriteTime(p), FileDateTime(p));
    }

    [Fact]
    public void SetAttr_ChangesTheAttributes()
    {
        var p = File_("attr.txt");
        SetAttr(p, FileAttribute.ReadOnly);
        Assert.True(System.IO.File.GetAttributes(p).HasFlag(FileAttributes.ReadOnly));

        SetAttr(p, FileAttribute.Normal);
        Assert.False(System.IO.File.GetAttributes(p).HasFlag(FileAttributes.ReadOnly));
    }
}
