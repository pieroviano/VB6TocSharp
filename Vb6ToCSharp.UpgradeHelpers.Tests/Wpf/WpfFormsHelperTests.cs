using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Vb6ToCSharp.UpgradeHelpers.Wpf.Helpers;
using Vb6ToCSharp.UpgradeHelpers.Tests.Infrastructure;

namespace Vb6ToCSharp.UpgradeHelpers.Tests.Wpf;

public class WpfFormsHelperTests
{
    [Theory]
    [InlineData(ModifierKeys.None, 0)]
    [InlineData(ModifierKeys.Shift, 1)]
    [InlineData(ModifierKeys.Control, 2)]
    [InlineData(ModifierKeys.Alt, 4)]
    [InlineData(ModifierKeys.Shift | ModifierKeys.Alt, 5)]
    [InlineData(ModifierKeys.Windows, 0)]
    public void ShiftFrom(ModifierKeys keys, int expected) => Assert.Equal(expected, FormsHelper.ShiftFrom(keys));

    [Fact]
    public void ButtonFrom_ChangedButton_And_PressedState()
    {
        Assert.Equal(1, FormsHelper.ButtonFrom(MouseButton.Left));
        Assert.Equal(2, FormsHelper.ButtonFrom(MouseButton.Right));
        Assert.Equal(4, FormsHelper.ButtonFrom(MouseButton.Middle));
        Assert.Equal(0, FormsHelper.ButtonFrom(MouseButton.XButton1));
        Assert.Equal(3, FormsHelper.ButtonFrom(true, true, false));
        Assert.Equal(0, FormsHelper.ButtonFrom((MouseEventArgs)null!));
        Sta.Run(() =>
        {
            var e = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Right);
            Assert.Equal(2, FormsHelper.ButtonFrom(e));
        });
    }

    [Fact]
    public void AllControls_NamedLogicalElements_Flat()
    {
        Sta.Run(() =>
        {
            var root = new Grid { Name = "root" };
            var group = new GroupBox { Name = "fra" };
            var inner = new StackPanel();
            var opt = new RadioButton { Name = "optA" };
            var unnamed = new Button();
            var ok = new Button { Name = "cmdOK", Content = new TextBlock { Name = "lblInside" } };
            inner.Children.Add(opt);
            inner.Children.Add(unnamed);
            group.Content = inner;
            root.Children.Add(group);
            root.Children.Add(ok);
            Assert.Equal(new[] { "fra", "optA", "cmdOK", "lblInside" }, FormsHelper.AllControls(root).Select(e => e.Name));
            Assert.Empty(FormsHelper.AllControls(null!));
        });
    }

    [Fact]
    public void ContextMenu_ProxiesItems_WithoutTouchingOriginal()
    {
        Sta.Run(() =>
        {
            var menu = new MenuItem { Header = "Edit" };
            var copy = new MenuItem { Header = "_Copy", InputGestureText = "Ctrl+C" };
            var sub = new MenuItem { Header = "More" };
            var deep = new MenuItem { Header = new TextBlock { Text = "Deep" }, IsCheckable = true };
            sub.Items.Add(deep);
            menu.Items.Add(copy);
            menu.Items.Add(new Separator());
            menu.Items.Add(sub);
            var clicks = new List<string>();
            copy.Click += (_, _) => clicks.Add("copy");
            deep.Click += (_, _) => clicks.Add("deep");
            menu.Click += (_, e) => clicks.Add("menu:" + ((MenuItem)e.Source).Header);

            var cm = FormsHelper.BuildContextMenu(menu);

            Assert.Equal(3, menu.Items.Count);
            Assert.Same(copy, menu.Items[0]);
            Assert.Equal(3, cm.Items.Count);
            var pCopy = (MenuItem)cm.Items[0];
            Assert.Equal("_Copy", pCopy.Header);
            Assert.Equal("Ctrl+C", pCopy.InputGestureText);
            Assert.IsType<Separator>(cm.Items[1]);
            var pDeep = (MenuItem)((MenuItem)cm.Items[2]).Items[0];
            Assert.Equal("Deep", pDeep.Header);

            pCopy.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, pCopy));
            pDeep.IsChecked = true;
            pDeep.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, pDeep));

            Assert.Equal(new[] { "copy", "menu:_Copy", "deep" }, clicks.Take(3));
            Assert.True(deep.IsChecked);
        });
    }

    [Fact]
    public void ActiveForm_NoApplication_IsNull()
    {
        Sta.Run(() =>
        {
            if (Application.Current == null) Assert.Null(FormsHelper.ActiveForm);
        });
    }
}
