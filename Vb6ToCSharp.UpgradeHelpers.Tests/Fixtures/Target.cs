using System.Drawing;
using Brush = System.Windows.Media.Brush;

namespace Vb6ToCSharp.UpgradeHelpers.Tests.Fixtures;

internal sealed class Target
{
    public bool Enabled { get; set; }
    public int Count { get; set; }
    public short Small { get; set; }
    public double Ratio { get; set; }
    public string Caption { get; set; } = "";
    public DayOfWeek Day { get; set; }
    public Color BackColor { get; set; }
    public int? Optional { get; set; }
    public Brush? Fill { get; set; }
    public int ReadOnlyValue => 42;
}