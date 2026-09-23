using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Microsoft.VisualBasic;
using Vb6ToCSharp.Runtime;
using Vb6ToCSharp.Tests.Fixtures;
using static Vb6ToCSharp.Runtime.RuntimeExtension;
using Timer = Vb6ToCSharp.Runtime.Timer;

namespace Vb6ToCSharp.Tests.Runtime;

public class RuntimeExtensionTests
{
    [Fact] public void CDate_ParsesValidDateString() => Assert.Equal(new DateTime(2020, 1, 2), CDate("2020-01-02"));
    [Fact] public void CDate_InvalidString_ReturnsMinValue() => Assert.Equal(DateTime.MinValue, CDate("not a date"));
    [Fact] public void CDate_DateTime_PassesThrough() => Assert.Equal(new DateTime(2001, 2, 3), CDate(new DateTime(2001, 2, 3)));

    [Fact] public void DateValue_InvalidString_ReturnsMinValue() => Assert.Equal(DateTime.MinValue, DateValue("xx"));

    [Fact]
    public void IsDate_Works()
    {
        Assert.True(IsDate("2020-01-01"));
        Assert.False(IsDate("hello"));
    }

    [Theory]
    [InlineData("12abc", 12)]
    [InlineData("-3.5", -3.5)]
    [InlineData("", 0)]
    [InlineData("abc", 0)]
    [InlineData("true", 1)]
    [InlineData("false", 0)]
    [InlineData("1.2.3", 1.2)]
    [InlineData("  7", 7)]
    [InlineData("-", 0)]
    [InlineData(".5", 0.5)]
    public void ValDouble_ParsesLeadingNumber(string s, double expected) => Assert.Equal(expected, ValDouble(s), 10);

    [Fact] public void ValDouble_Null_ReturnsZero() => Assert.Equal(0, ValDouble(null!));

    [Fact]
    public void ValDouble_IsCultureInvariant()
    {
        var old = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("it-IT");
            Assert.Equal(1.5, ValDouble("1.5"));
        }
        finally { Thread.CurrentThread.CurrentCulture = old; }
    }

    [Fact] public void ValD_StripsThousandsSeparators() => Assert.Equal(1234.5m, ValD("1,234.5"));
    [Fact] public void ValD_Null_ReturnsZero() => Assert.Equal(0m, ValD((string)null!));
    [Fact] public void ValI_TruncatesToInt() => Assert.Equal(42, ValI("42.9x"));

    [Fact]
    public void ValI_Bool()
    {
        Assert.Equal(1, ValI(true));
        Assert.Equal(0, ValI(false));
    }

    [Fact] public void ValL_String() => Assert.Equal(-8, ValL("-8"));
    [Fact] public void ValF_String() => Assert.Equal(2.5f, ValF("2.5"));

    [Fact] public void LBound_EmptyList_IsZero() => Assert.Equal(0, LBound(new List<int>()));

    [Fact]
    public void LBound_EmptyList_LoopToUBoundDoesNotRun()
    {
        var l = new List<int>();
        var runs = 0;
        for (var i = LBound(l); i <= UBound(l); i++) runs++;
        Assert.Equal(0, runs);
    }

    [Fact]
    public void UBound_ListIsCountMinusOne()
    {
        Assert.Equal(2, UBound(new[] { 1, 2, 3 }));
        Assert.Equal(-1, UBound(new int[0]));
    }

    [Fact] public void UBound_NonList_IsZero() => Assert.Equal(0, UBound(5));

    [Theory]
    [InlineData("cmd_3", 3)]
    [InlineData("a_b_12", 12)]
    [InlineData("cmd_0", 0)]
    [InlineData("cmd", -1)]
    public void ControlIndex_ReadsSuffixAfterLastUnderscore(string name, int expected) => Assert.Equal(expected, controlIndex(name));

    [Fact] public void SenderIndex_ReadsSuffix() => Assert.Equal(5, SenderIndex("opt_5"));

    [Fact] public void VBSwitch_ReturnsFirstTrueValue() => Assert.Equal(2, (int)VBSwitch(false, 1, true, 2, true, 3));
    [Fact] public void VBSwitch_OddCount_ReturnsDefault() => Assert.Equal("def", (string)VBSwitch(false, 1, "def"));
    [Fact] public void VBSwitch_NoMatch_ReturnsNull() => Assert.Null(VBSwitch(false, 1));

    [Fact]
    public void CBool_Conversions()
    {
        Assert.True(CBool(true));
        Assert.True(CBool("True"));
        Assert.True(CBool(1));
        Assert.False(CBool(0));
        Assert.False(CBool(null!));
        Assert.False(CBool(new object()));
    }

    [Fact]
    public void CInt_BankersRounding()
    {
        Assert.Equal(4, CInt(3.6));
        Assert.Equal(2, CInt(2.5));
    }

    [Fact] public void CStr_Null_IsEmpty() => Assert.Equal("", CStr(null!));
    [Fact] public void CDbl_NonConvertible_IsZero() => Assert.Equal(0, CDbl(new object()));

    [Fact]
    public void IIf_Overloads()
    {
        Assert.Equal("a", IIf(true, "a", "b"));
        Assert.Equal(2, IIf(false, 1, 2));
        Assert.Equal(1.5m, IIf(true, 1.5m, 2m));
    }

    [Fact]
    public void IsNull_TreatsDbNullAsNull()
    {
        Assert.True(IsNull(null!));
        Assert.True(IsNull(DBNull.Value));
        Assert.False(IsNull(1));
        Assert.True(IsObject(1));
        Assert.True(IsNothing(null!));
    }

    [Fact]
    public void IsList_DetectsLists()
    {
        Assert.True(IsList(new List<int>()));
        Assert.False(IsList("x"));
        Assert.False(IsList(null!));
    }

    [Fact]
    public void IsLike_UsesVbLikeSemantics()
    {
        Assert.True(IsLike("abc", "a*"));
        Assert.True(IsLike("a1", "a#"));
        Assert.False(IsLike("abc", "b*"));
    }

    [Fact]
    public void IsInStr_Works()
    {
        Assert.True(IsInStr("hello", "ll"));
        Assert.False(IsInStr("hello", "z"));
    }

    [Theory]
    [InlineData("a\r\nb", "\n", "a\nb")]
    [InlineData("a\rb", "\n", "a\nb")]
    [InlineData("a\nb", "\r\n", "a\r\nb")]
    [InlineData("ab", "\n", "ab")]
    public void SanitizeNls_NormalisesLineEndings(string s, string desired, string expected) => Assert.Equal(expected, SanitizeNls(s, desired));

    [Fact] public void Spc_ReturnsSpaces() => Assert.Equal("   ", Spc(3));
    [Fact] public void Tab_ReturnsMarker() => Assert.Equal("::TABSTOP:4", Tab(4));

    [Theory]
    [InlineData("y", DateInterval.Year)]
    [InlineData("m", DateInterval.Month)]
    [InlineData("n", DateInterval.Minute)]
    [InlineData("s", DateInterval.Second)]
    [InlineData("?", DateInterval.Day)]
    public void GetDateInterval_MapsVbCodes(string s, DateInterval expected) => Assert.Equal(expected, getDateInterval(s));

    [Fact] public void TextHeight_IsTenPerLine() => Assert.Equal(20m, TextHeight("a\r\nb"));

    [Fact]
    public void Timer_Interval_IsMilliseconds()
    {
        var t = new Timer { Interval = 500 };
        Assert.Equal(TimeSpan.FromMilliseconds(500), t.getInterval());
        Assert.Equal(500, t.Interval);
        t.IntervalSeconds = 3;
        Assert.Equal(3000, t.Interval);
    }

    [Fact]
    public void Timer_StartTimerSeconds_UsesSeconds()
    {
        var t = new Timer();
        t.startTimerSeconds(2);
        try { Assert.Equal(2, t.IntervalSeconds); }
        finally { t.stopTimer(); }
        Assert.False(t.Enabled);
    }

    [Fact]
    public void Timer_StartTimer_SetsTagAndEnables()
    {
        var t = new Timer();
        t.startTimer(250, "tag");
        try
        {
            Assert.True(t.Enabled);
            Assert.Equal(250, t.Interval);
            Assert.Equal("tag", (string)t.Tag);
        }
        finally { t.stopTimer(); }
    }




    [Fact] public void DoEvents_WithoutWindow_DoesNotThrow() => Assert.True(TestUtil.WithTimeout(() => DoEvents()));
}
