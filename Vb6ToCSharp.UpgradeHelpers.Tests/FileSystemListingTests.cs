using Vb6ToCSharp.UpgradeHelpers.Internal;
using Vb6ToCSharp.UpgradeHelpers.Tests.Infrastructure;

namespace Vb6ToCSharp.UpgradeHelpers.Tests;

public class FileSystemListingTests
{
    [Theory]
    [InlineData("a.txt", "*.txt", true)]
    [InlineData("A.TXT", "*.txt", true)]
    [InlineData("a.txtx", "*.txt", false)] // no 8.3 short-name quirk
    [InlineData("readme", "*.*", true)]
    [InlineData("data1.csv", "data?.csv", true)]
    [InlineData("data12.csv", "data?.csv", false)]
    [InlineData("a+b(1).txt", "a+b(?).txt", true)]
    public void WildcardMatch(string name, string pattern, bool expected) =>
        Assert.Equal(expected, FileSystemListing.WildcardMatch(name, pattern));

    [Fact]
    public void SplitPatterns_DefaultsToAll()
    {
        Assert.Equal(new[] { "*.*" }, FileSystemListing.SplitPatterns(""));
        Assert.Equal(new[] { "*.txt", "*.log" }, FileSystemListing.SplitPatterns(" *.txt ; *.log ;"));
    }

    [Theory]
    [InlineData("c", "c:")]
    [InlineData("C:", "c:")]
    [InlineData(@"D:\Windows", "d:")]
    [InlineData("e: [DATA]", "e:")]
    [InlineData("", null)]
    [InlineData("12", null)]
    public void DriveLetter(string text, string? expected) => Assert.Equal(expected, FileSystemListing.DriveLetter(text));

    [Fact]
    public void FileFilter_Defaults_AndFlags()
    {
        var f = FileSystemListing.FileFilter.Default;
        Assert.True(f.Accepts(FileAttributes.Archive));
        Assert.True(f.Accepts(FileAttributes.Normal));
        Assert.True(f.Accepts(FileAttributes.ReadOnly | FileAttributes.Archive));
        Assert.False(f.Accepts(FileAttributes.Hidden | FileAttributes.Archive));
        Assert.False(f.Accepts(FileAttributes.System));

        f.Hidden = true;
        Assert.True(f.Accepts(FileAttributes.Hidden | FileAttributes.Archive));
        f.ReadOnly = false;
        Assert.False(f.Accepts(FileAttributes.ReadOnly));
        f.Archive = false;
        Assert.False(f.Accepts(FileAttributes.Archive));
        Assert.True(f.Accepts(FileAttributes.Normal));
        f.Normal = false;
        Assert.False(f.Accepts(FileAttributes.Normal));
        Assert.True(f.Accepts(FileAttributes.Hidden)); // shown because Hidden is requested
    }

    [Fact]
    public void Files_FilterByPatternsAndAttributes_Sorted()
    {
        using var dir = new TempDir();
        dir.File("b.TXT");
        dir.File("a.txt");
        dir.File("c.log");
        dir.File("d.txtx");
        dir.File("h.txt", FileAttributes.Hidden);
        dir.File("r.txt", FileAttributes.ReadOnly);
        dir.Dir("sub.txt");

        var f = FileSystemListing.FileFilter.Default;
        Assert.Equal(new[] { "a.txt", "b.TXT", "r.txt" }, FileSystemListing.Files(dir.Path, "*.txt", f));
        Assert.Equal(new[] { "a.txt", "b.TXT", "c.log", "r.txt" }, FileSystemListing.Files(dir.Path, "*.txt;*.log", f));
        f.Hidden = true;
        f.ReadOnly = false;
        Assert.Equal(new[] { "a.txt", "b.TXT", "h.txt" }, FileSystemListing.Files(dir.Path, "*.txt", f));
        Assert.Empty(FileSystemListing.Files(Path.Combine(dir.Path, "missing"), "*.*", f));
    }

    [Fact]
    public void DirModel_AncestorsThenSubdirectories()
    {
        using var dir = new TempDir();
        dir.Dir("beta");
        dir.Dir("Alpha");
        var model = new FileSystemListing.DirModel();
        model.Build(dir.Path);

        Assert.Equal(2, model.SubdirectoryCount);
        Assert.Equal(dir.Path, model.GetList(-1));
        Assert.Equal(Path.GetDirectoryName(dir.Path), model.GetList(-2));
        Assert.Equal(Path.Combine(dir.Path, "Alpha"), model.GetList(0));
        Assert.Equal(Path.Combine(dir.Path, "beta"), model.GetList(1));
        Assert.Equal("", model.GetList(2));
        Assert.Equal("", model.GetList(-100));
        Assert.Equal(Path.GetPathRoot(dir.Path)!.ToLowerInvariant(), model.Display[0]);
    }

    [Fact]
    public void ResolveDirectory_AcceptsDriveTextAndRelative()
    {
        using var dir = new TempDir();
        var sub = dir.Dir("child");
        Assert.Equal(sub, FileSystemListing.ResolveDirectory("child", dir.Path));
        Assert.Equal(dir.Path, FileSystemListing.ResolveDirectory(dir.Path + "\\", null));
        var root = Path.GetPathRoot(dir.Path)!;
        Assert.Equal(root, FileSystemListing.ResolveDirectory(root.Substring(0, 2) + " [LABEL]", null), ignoreCase: true);
        Assert.Throws<DirectoryNotFoundException>(() => FileSystemListing.ResolveDirectory(Path.Combine(dir.Path, "nope"), null));
    }
}
