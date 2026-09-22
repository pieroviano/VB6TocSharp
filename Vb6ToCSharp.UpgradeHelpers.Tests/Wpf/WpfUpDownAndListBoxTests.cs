using Vb6ToCSharp.UpgradeHelpers.Wpf;

namespace Vb6ToCSharp.UpgradeHelpers.Tests.Wpf;

public class UpDownTests
{
    [Fact]
    public void Defaults_And_Clamping()
    {
        Sta.Run(() =>
        {
            var u = new UpDown();
            Assert.Equal((0, 0, 10, 1, false), (u.Value, u.Min, u.Max, u.Increment, u.Wrap));
            Assert.Equal("0", u.TextBox.Text);
            u.Value = 50;
            Assert.Equal(10, u.Value);
            Assert.Equal("10", u.TextBox.Text);
            u.Max = 5;
            Assert.Equal(5, u.Value);
            Assert.Throws<ArgumentOutOfRangeException>(() => u.Increment = 0);
        });
    }

    [Fact]
    public void Step_StopsAtBounds_OrWraps()
    {
        Sta.Run(() =>
        {
            var u = new UpDown { Max = 10, Increment = 4 };
            var changes = 0;
            u.Change += (_, _) => changes++;
            u.StepUp();
            u.StepUp();
            Assert.Equal(8, u.Value);
            u.StepUp();
            Assert.Equal(10, u.Value);
            u.StepUp();
            Assert.Equal(10, u.Value);
            Assert.Equal(3, changes);

            u.Wrap = true;
            u.StepUp();
            Assert.Equal(0, u.Value);
            u.StepDown();
            Assert.Equal(10, u.Value);
        });
    }

    [Fact]
    public void MinGreaterThanMax_UpMovesTowardsMax()
    {
        Sta.Run(() =>
        {
            var u = new UpDown { Min = 10, Max = 0, Value = 10 };
            u.StepUp();
            Assert.Equal(9, u.Value);
            u.StepDown();
            u.StepDown();
            Assert.Equal(10, u.Value);
        });
    }

    [Fact]
    public void ButtonsAndText_DriveTheValue()
    {
        Sta.Run(() =>
        {
            var u = new UpDown();
            u.UpButton.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.Equal(1, u.Value);
            u.DownButton.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.Equal(0, u.Value);
            u.TextBox.Text = "7";
            u.TextBox.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.UIElement.LostFocusEvent));
            Assert.Equal(7, u.Value);
            u.TextBox.Text = "abc";
            u.TextBox.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.UIElement.LostFocusEvent));
            Assert.Equal("7", u.TextBox.Text);
        });
    }
}

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
