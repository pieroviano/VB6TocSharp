using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using Vb6ToCSharp.UpgradeHelpers.Arrays;

namespace Vb6ToCSharp.UpgradeHelpers.Wpf.Controls;

/// <summary>
/// VB6 control array over WPF elements. <see cref="ControlArrayBase{T}.Load"/> clones the lowest-index element
/// (locally set layout, font, color, text and attached Canvas/Grid values; bindings are not copied) and inserts it
/// after the highest-index element in the parent <see cref="Panel"/> (hidden) or <see cref="ItemsControl"/>
/// (e.g. menu arrays; collapsed, so it takes no space).
/// </summary>
public class ControlArray<T> : ControlArrayBase<T> where T : FrameworkElement, new()
{
    private static readonly DependencyProperty[] Copied =
    {
        FrameworkElement.MarginProperty, FrameworkElement.WidthProperty, FrameworkElement.HeightProperty,
        FrameworkElement.MinWidthProperty, FrameworkElement.MinHeightProperty,
        FrameworkElement.MaxWidthProperty, FrameworkElement.MaxHeightProperty,
        FrameworkElement.HorizontalAlignmentProperty, FrameworkElement.VerticalAlignmentProperty,
        FrameworkElement.ToolTipProperty, FrameworkElement.TagProperty, FrameworkElement.CursorProperty,
        TextElement.FontFamilyProperty, TextElement.FontSizeProperty, TextElement.FontWeightProperty,
        TextElement.FontStyleProperty, TextElement.ForegroundProperty,
        Control.BackgroundProperty, Control.BorderBrushProperty, Control.BorderThicknessProperty, Control.PaddingProperty,
        Control.HorizontalContentAlignmentProperty, Control.VerticalContentAlignmentProperty,
        TextBlock.BackgroundProperty, TextBlock.TextProperty, TextBlock.TextWrappingProperty,
        Border.BackgroundProperty,
        UIElement.IsEnabledProperty, UIElement.OpacityProperty,
        ContentControl.ContentProperty, HeaderedItemsControl.HeaderProperty, HeaderedContentControl.HeaderProperty,
        TextBox.TextProperty, TextBox.TextAlignmentProperty, TextBoxBase.IsReadOnlyProperty,
        ToggleButton.IsCheckedProperty,
        MenuItem.IsCheckableProperty, MenuItem.IsCheckedProperty, MenuItem.InputGestureTextProperty,
        MenuItem.CommandProperty, MenuItem.CommandParameterProperty,
        RangeBase.MinimumProperty, RangeBase.MaximumProperty, RangeBase.ValueProperty,
        Canvas.LeftProperty, Canvas.TopProperty, Canvas.RightProperty, Canvas.BottomProperty,
        Grid.RowProperty, Grid.ColumnProperty, Grid.RowSpanProperty, Grid.ColumnSpanProperty,
        DockPanel.DockProperty, Panel.ZIndexProperty,
    };

    public ControlArray(string name) : base(name)
    {
    }

    protected override T CreateClone(T template, string name)
    {
        var clone = new T();
        foreach (var dp in Copied)
        {
            var value = template.ReadLocalValue(dp);
            if (value == DependencyProperty.UnsetValue || value is Expression || value is Visual || value is ContentElement)
                continue;
            try
            {
                clone.SetValue(dp, value);
            }
            catch (ArgumentException)
            {
                // Not applicable to this element type.
            }
        }
        clone.Name = name;
        clone.Visibility = Visibility.Hidden;
        return clone;
    }

    protected override void Attach(T template, T highest, T clone)
    {
        switch (template.Parent)
        {
            case null:
                return;
            case Panel panel:
                var p = panel.Children.IndexOf(highest);
                panel.Children.Insert(p < 0 ? panel.Children.Count : p + 1, clone);
                return;
            case ItemsControl items:
                if (items.ItemsSource != null)
                    throw new InvalidOperationException($"Control array '{Name}': parent items are data bound.");
                var i = items.Items.IndexOf(highest);
                items.Items.Insert(i < 0 ? items.Items.Count : i + 1, clone);
                clone.Visibility = Visibility.Collapsed;
                return;
            default:
                throw new NotSupportedException(
                    $"Control array '{Name}': cannot add elements to a {template.Parent.GetType().Name}.");
        }
    }

    protected override void Detach(T element)
    {
        switch (element.Parent)
        {
            case Panel panel: panel.Children.Remove(element); break;
            case ItemsControl items: items.Items.Remove(element); break;
        }
    }
}
