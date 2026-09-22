using System.Windows.Forms;
using Vb6ToCSharp.UpgradeHelpers.WinForms;

namespace Vb6ToCSharp.UpgradeHelpers.Tests.WinForms;

public class ScrollBarHelperTests
{
    [Fact]
    public void Max_Maps_ToMaximumPlusLargeChangeMinusOne()
    {
        Sta.Run(() =>
        {
            var sb = new HScrollBar { LargeChange = 10 };
            ScrollBarHelper.SetMax(sb, 100);
            Assert.Equal(109, sb.Maximum);
            Assert.Equal(100, ScrollBarHelper.GetMax(sb));
        });
    }

    [Fact]
    public void SetLargeChange_KeepsVbMax()
    {
        Sta.Run(() =>
        {
            var sb = new VScrollBar { LargeChange = 10 };
            ScrollBarHelper.SetMax(sb, 100);
            ScrollBarHelper.SetLargeChange(sb, 25);
            Assert.Equal(25, sb.LargeChange);
            Assert.Equal(124, sb.Maximum);
            Assert.Equal(100, ScrollBarHelper.GetMax(sb));
            Assert.Throws<ArgumentOutOfRangeException>(() => ScrollBarHelper.SetLargeChange(sb, 0));
        });
    }

    [Fact]
    public void SetMax_UsesLargeChangeClampedBySmallRange()
    {
        Sta.Run(() =>
        {
            var sb = new HScrollBar { Maximum = 5, LargeChange = 10 }; // getter clamps LargeChange to 6
            ScrollBarHelper.SetMax(sb, 100);
            Assert.Equal(109, sb.Maximum);
            Assert.Equal(100, ScrollBarHelper.GetMax(sb));
        });
    }

    [Fact]
    public void SetMax_BelowMinimum_LowersMinimum_AndKeepsValueInRange()
    {
        Sta.Run(() =>
        {
            var sb = new HScrollBar { Minimum = 10, Maximum = 200, LargeChange = 1, Value = 150 };
            ScrollBarHelper.SetMax(sb, 5);
            Assert.Equal(5, sb.Minimum);
            Assert.Equal(5, ScrollBarHelper.GetMax(sb));
            Assert.True(sb.Value <= sb.Maximum);
        });
    }
}
