using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Vb6ToCSharp.UpgradeHelpers.Wpf.Controls;
using Vb6ToCSharp.UpgradeHelpers.Tests.Fixtures;
using Vb6ToCSharp.UpgradeHelpers.Arrays;

namespace Vb6ToCSharp.UpgradeHelpers.Tests.Wpf.Controls;

public class ControlArrayTests
{
    [Fact]
    public void Load_ClonesLocalValues_IntoPanelAfterHighest()
    {
        Sta.Run(() =>
        {
            var grid = new Grid();
            var b0 = new Button { Name = "cmd_0", Content = "Zero", Width = 50, Height = 20, Margin = new Thickness(5), FontSize = 15, Foreground = Brushes.Red, ToolTip = "tip", HorizontalAlignment = HorizontalAlignment.Left };
            Grid.SetRow(b0, 1);
            Grid.SetColumn(b0, 2);
            var other = new TextBlock();
            var b1 = new Button { Name = "cmd_1" };
            grid.Children.Add(b0);
            grid.Children.Add(b1);
            grid.Children.Add(other);
            var a = new ControlArray<Button>("cmd");
            a.SetIndex(b0, 0);
            a.SetIndex(b1, 1);

            var c = a.Load(2);

            Assert.Equal("cmd_2", c.Name);
            Assert.Equal("Zero", c.Content);
            Assert.Equal((50.0, 20.0), (c.Width, c.Height));
            Assert.Equal(new Thickness(5), c.Margin);
            Assert.Equal(15, c.FontSize);
            Assert.Same(Brushes.Red, c.Foreground);
            Assert.Equal("tip", c.ToolTip);
            Assert.Equal(HorizontalAlignment.Left, c.HorizontalAlignment);
            Assert.Equal((1, 2), (Grid.GetRow(c), Grid.GetColumn(c)));
            Assert.Equal(Visibility.Hidden, c.Visibility);
            Assert.Equal(2, grid.Children.IndexOf(c)); // right after cmd(1)
            Assert.Same(grid, c.Parent);
        });
    }

    [Fact]
    public void Load_CopiesCanvasPosition_TextBoxText_NotBindingsOrElements()
    {
        Sta.Run(() =>
        {
            var canvas = new Canvas();
            var t0 = new TextBox { Text = "hello" };
            Canvas.SetLeft(t0, 12);
            Canvas.SetTop(t0, 34);
            canvas.Children.Add(t0);
            var b0 = new Button { Content = new TextBlock { Text = "element" } };
            b0.SetBinding(FrameworkElement.WidthProperty, new Binding("Missing"));
            canvas.Children.Add(b0);

            var ta = new ControlArray<TextBox>("txt");
            ta.SetIndex(t0, 0);
            var t1 = ta.Load(1);
            Assert.Equal("hello", t1.Text);
            Assert.Equal((12.0, 34.0), (Canvas.GetLeft(t1), Canvas.GetTop(t1)));

            var ba = new ControlArray<Button>("cmd");
            ba.SetIndex(b0, 0);
            var b1 = ba.Load(1);
            Assert.Null(b1.Content);
            Assert.Null(BindingOperations.GetBinding(b1, FrameworkElement.WidthProperty));
        });
    }

    [Fact]
    public void MenuArray_InsertsIntoItemsControl_AndUnloadRemoves()
    {
        Sta.Run(() =>
        {
            var menu = new MenuItem { Header = "File" };
            var m0 = new MenuItem { Header = "Recent 0", IsCheckable = true, IsChecked = true, InputGestureText = "Ctrl+1" };
            var m1 = new MenuItem { Header = "Recent 1" };
            var exit = new MenuItem { Header = "Exit" };
            menu.Items.Add(m0);
            menu.Items.Add(m1);
            menu.Items.Add(new Separator());
            menu.Items.Add(exit);
            var a = new ControlArray<MenuItem>("mnuRecent");
            a.SetIndex(m0, 0);
            a.SetIndex(m1, 1);

            var m2 = a.Load(2);

            Assert.Equal(2, menu.Items.IndexOf(m2));
            Assert.Equal("Recent 0", m2.Header);
            Assert.True(m2.IsCheckable && m2.IsChecked);
            Assert.Equal("Ctrl+1", m2.InputGestureText);
            Assert.Equal(Visibility.Collapsed, m2.Visibility);

            a.Unload(2);
            Assert.Equal(-1, menu.Items.IndexOf(m2));
            Assert.Equal(362, Assert.Throws<ControlArrayException>(() => a.Unload(1)).Number);
            Assert.Equal(360, Assert.Throws<ControlArrayException>(() => a.Load(0)).Number);
        });
    }

    [Fact]
    public void Wire_AndEnumerationOrder()
    {
        Sta.Run(() =>
        {
            var panel = new StackPanel();
            var b5 = new Button();
            var b1 = new Button();
            panel.Children.Add(b5);
            panel.Children.Add(b1);
            var a = new ControlArray<Button>("b");
            a.SetIndex(b5, 5);
            a.SetIndex(b1, 1);
            var wired = new List<int>();
            a.Wire(b => wired.Add(a.GetIndex(b)));
            var b3 = a.Load(3);
            Assert.Equal(new[] { 1, 5, 3 }, wired);
            Assert.Equal(new[] { b1, b3, b5 }, a.ToArray());
            Assert.Equal((1, 5, 3), (a.LBound(), a.UBound(), a.Count));
        });
    }

    [Fact]
    public void UnsupportedParent_Throws()
    {
        Sta.Run(() =>
        {
            var border = new Border();
            var b = new Button();
            border.Child = b;
            var a = new ControlArray<Button>("b");
            a.SetIndex(b, 0);
            Assert.Throws<NotSupportedException>(() => a.Load(1));
            Assert.False(a.Exists(1));
        });
    }
}
