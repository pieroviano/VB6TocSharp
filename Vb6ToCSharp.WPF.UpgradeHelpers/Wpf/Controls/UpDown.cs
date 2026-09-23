using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Vb6ToCSharp.UpgradeHelpers.Wpf.Controls;

/// <summary>
/// VB6 UpDown (ComCtl2) for WPF: a text box with up/down repeat buttons. "Up" moves towards <see cref="Max"/>
/// (also when Min &gt; Max); at a bound the value stops, or wraps when <see cref="Wrap"/> is set.
/// </summary>
public class UpDown : UserControl
{
    private int _value, _min, _max = 10, _increment = 1;

    public UpDown()
    {
        TextBox = new TextBox { VerticalContentAlignment = VerticalAlignment.Center, MinWidth = 24 };
        UpButton = new RepeatButton { Content = "▲", FontSize = 7, Padding = new Thickness(2, 0, 2, 0), Focusable = false };
        DownButton = new RepeatButton { Content = "▼", FontSize = 7, Padding = new Thickness(2, 0, 2, 0), Focusable = false };
        UpButton.Click += (_, _) => StepUp();
        DownButton.Click += (_, _) => StepDown();
        TextBox.LostFocus += (_, _) => CommitText();
        TextBox.PreviewKeyDown += OnTextKeyDown;

        var buttons = new Grid();
        buttons.RowDefinitions.Add(new RowDefinition());
        buttons.RowDefinitions.Add(new RowDefinition());
        Grid.SetRow(DownButton, 1);
        buttons.Children.Add(UpButton);
        buttons.Children.Add(DownButton);

        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition());
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(buttons, 1);
        root.Children.Add(TextBox);
        root.Children.Add(buttons);
        Content = root;
        UpdateText();
    }

    /// <summary>Raised when <see cref="Value"/> changes.</summary>
    public event EventHandler Change;

    internal TextBox TextBox { get; }
    internal RepeatButton UpButton { get; }
    internal RepeatButton DownButton { get; }

    /// <summary>Current value, clamped to the Min..Max range.</summary>
    public int Value
    {
        get => _value;
        set
        {
            var v = Clamp(value);
            if (v == _value)
            {
                UpdateText();
                return;
            }
            _value = v;
            UpdateText();
            Change?.Invoke(this, EventArgs.Empty);
        }
    }

    public int Min
    {
        get => _min;
        set { _min = value; Value = _value; }
    }

    public int Max
    {
        get => _max;
        set { _max = value; Value = _value; }
    }

    public int Increment
    {
        get => _increment;
        set => _increment = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), "Invalid property value");
    }

    public bool Wrap { get; set; }

    /// <summary>Up button: one <see cref="Increment"/> towards <see cref="Max"/>.</summary>
    internal void StepUp() => Step(+1);

    /// <summary>Down button: one <see cref="Increment"/> towards <see cref="Min"/>.</summary>
    internal void StepDown() => Step(-1);

    private void Step(int towardsMax)
    {
        var direction = _max >= _min ? 1 : -1;
        var next = (long)_value + (long)towardsMax * direction * _increment;
        if (next >= Math.Min(_min, _max) && next <= Math.Max(_min, _max)) Value = (int)next;
        else if (towardsMax > 0) Value = Wrap ? _min : _max;
        else Value = Wrap ? _max : _min;
    }

    private int Clamp(int v)
    {
        var lo = Math.Min(_min, _max);
        var hi = Math.Max(_min, _max);
        return Math.Max(lo, Math.Min(hi, v));
    }

    private void CommitText()
    {
        if (int.TryParse(TextBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var v)) Value = v;
        else UpdateText();
    }

    private void OnTextKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter: CommitText(); e.Handled = true; break;
            case Key.Up: StepUp(); e.Handled = true; break;
            case Key.Down: StepDown(); e.Handled = true; break;
        }
    }

    private void UpdateText() => TextBox.Text = _value.ToString(CultureInfo.CurrentCulture);
}
