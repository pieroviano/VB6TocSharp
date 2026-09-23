using Vb6ToCSharp.UpgradeHelpers.Tests.Infrastructure;
using Vb6ToCSharp.UpgradeHelpers.WinForms;

namespace Vb6ToCSharp.UpgradeHelpers.Tests.WinForms;

public class FileSystemListBoxTests
{
    [Fact]
    public void FileListBox_PatternAttributesAndEvents()
    {
        using var dir = new TempDir();
        dir.File("a.txt");
        dir.File("b.log");
        dir.File("h.txt", FileAttributes.Hidden);
        Sta.Run(() =>
        {
            var flb = new FileListBox();
            var events = new List<string>();
            flb.PathChange += (_, _) => events.Add("path");
            flb.PatternChange += (_, _) => events.Add("pattern");
            flb.Path = dir.Path;
            Assert.Equal(new[] { "a.txt", "b.log" }, flb.Items.Cast<string>());
            flb.Pattern = "*.txt";
            Assert.Equal(new[] { "a.txt" }, flb.Items.Cast<string>());
            flb.Hidden = true;
            Assert.Equal(new[] { "a.txt", "h.txt" }, flb.Items.Cast<string>());
            Assert.Equal(new[] { "path", "pattern" }, events);

            flb.FileName = "H.TXT";
            Assert.Equal("h.txt", flb.FileName);
            flb.FileName = "*.log";
            Assert.Equal("*.log", flb.Pattern);
            Assert.Equal(new[] { "b.log" }, flb.Items.Cast<string>());

            dir.File("c.log");
            Assert.Single(flb.Items);
            flb.Refresh();
            Assert.Equal(2, flb.Items.Count);
            Assert.Throws<DirectoryNotFoundException>(() => flb.Path = Path.Combine(dir.Path, "nope"));
        });
    }

    [Fact]
    public void FileListBox_FileNameWithPath_ChangesPath()
    {
        using var dir = new TempDir();
        var sub = dir.Dir("sub");
        File.WriteAllText(Path.Combine(sub, "x.dat"), "x");
        Sta.Run(() =>
        {
            var flb = new FileListBox { Path = dir.Path };
            flb.FileName = Path.Combine(sub, "x.dat");
            Assert.Equal(sub, flb.Path);
            Assert.Equal("x.dat", flb.FileName);
        });
    }

    [Fact]
    public void DirListBox_ListsAncestorsAndSubdirectories()
    {
        using var dir = new TempDir();
        var a = dir.Dir("a");
        dir.Dir("b");
        Sta.Run(() =>
        {
            var d = new DirListBox();
            var changes = 0;
            d.Change += (_, _) => changes++;
            d.Path = dir.Path;
            Assert.Equal(1, changes);
            Assert.Equal(2, d.ListCount);
            Assert.Equal(-1, d.ListIndex);
            Assert.Equal(dir.Path, d.GetList(-1));
            Assert.Equal(a, d.GetList(0));
            d.ListIndex = 0;
            Assert.Equal(0, d.ListIndex);
            d.Path = d.GetList(d.ListIndex);
            Assert.Equal(a, d.Path);
            Assert.Equal(0, d.ListCount);
            Assert.Equal(2, changes);
            d.Path = a; // same path: no Change
            Assert.Equal(2, changes);
            Assert.Throws<ArgumentOutOfRangeException>(() => d.ListIndex = 5);
        });
    }

    [Fact]
    public void DriveListBox_SelectsByAnyDriveText()
    {
        Sta.Run(() =>
        {
            var drv = new DriveListBox();
            var system = Path.GetPathRoot(Environment.SystemDirectory)!;
            drv.Drive = system; // "C:\"
            Assert.StartsWith(system.Substring(0, 2).ToLowerInvariant(), drv.Drive);
            var used = new HashSet<char>(DriveInfo.GetDrives().Select(x => char.ToLowerInvariant(x.Name[0])));
            var free = "zyxwvutsrqponmlkjihgfedcba".First(c => !used.Contains(c));
            Assert.Throws<IOException>(() => drv.Drive = free + ":");
        });
    }
}
