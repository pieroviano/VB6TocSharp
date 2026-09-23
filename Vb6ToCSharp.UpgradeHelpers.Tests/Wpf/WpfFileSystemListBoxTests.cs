using Vb6ToCSharp.UpgradeHelpers.Tests.Infrastructure;
using Vb6ToCSharp.UpgradeHelpers.Wpf;

namespace Vb6ToCSharp.UpgradeHelpers.Tests.Wpf;

public class WpfFileSystemListBoxTests
{
    [Fact]
    public void FileListBox_Filters()
    {
        using var dir = new TempDir();
        dir.File("a.txt");
        dir.File("b.log");
        dir.File("r.txt", FileAttributes.ReadOnly);
        Sta.Run(() =>
        {
            var flb = new FileListBox { Path = dir.Path, Pattern = "*.txt" };
            Assert.Equal(new[] { "a.txt", "r.txt" }, flb.Items.Cast<string>());
            flb.ReadOnly = false;
            Assert.Equal(new[] { "a.txt" }, flb.Items.Cast<string>());
            flb.Pattern = "*.log;*.txt";
            Assert.Equal(new[] { "a.txt", "b.log" }, flb.Items.Cast<string>());
            flb.FileName = "b.log";
            Assert.Equal("b.log", flb.FileName);
        });
    }

    [Fact]
    public void DirListBox_And_DriveListBox()
    {
        using var dir = new TempDir();
        var sub = dir.Dir("only");
        Sta.Run(() =>
        {
            var d = new DirListBox();
            var changes = 0;
            d.Change += (_, _) => changes++;
            d.Path = dir.Path;
            Assert.Equal(1, d.ListCount);
            Assert.Equal(sub, d.GetList(0));
            Assert.Equal(dir.Path, d.GetList(-1));
            Assert.Equal(-1, d.ListIndex);
            Assert.Equal(1, changes);

            var drv = new DriveListBox();
            var root = Path.GetPathRoot(dir.Path)!;
            drv.Drive = root;
            Assert.StartsWith(root.Substring(0, 2).ToLowerInvariant(), drv.Drive);
        });
    }
}
