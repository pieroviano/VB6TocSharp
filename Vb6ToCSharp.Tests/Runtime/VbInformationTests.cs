using System;
using static Vb6ToCSharp.Runtime.VbInformation;

namespace Vb6ToCSharp.Tests.Runtime;

/// <summary>
/// Expectations taken from Microsoft.VisualBasic.Information, which VbInformation replaces.
/// </summary>
public class VbInformationTests
{
    [Theory]
    [InlineData("1", true)]
    [InlineData("0", true)]
    [InlineData("1.5", true)]
    [InlineData("-1", true)]
    [InlineData("1e3", true)]
    [InlineData(" 1 ", true)]
    [InlineData("1,500", true)]   // a group separator is allowed
    [InlineData("(1)", true)]     // so are accounting parentheses
    [InlineData("&H10", true)]
    [InlineData("&O17", true)]
    [InlineData("", false)]
    [InlineData("abc", false)]
    [InlineData("1a", false)]
    [InlineData("1.5.5", false)]
    [InlineData("+", false)]
    [InlineData("1 2", false)]
    [InlineData("&HZZ", false)]
    [InlineData("1d3", false)]    // Val("1d3") is 1000, but IsNumeric says no
    public void IsNumeric_OnStrings(string input, bool expected) => Assert.Equal(expected, IsNumeric(input));

    [Fact]
    public void IsNumeric_OnValues()
    {
        Assert.True(IsNumeric(5));
        Assert.True(IsNumeric(1.5));
        Assert.True(IsNumeric(true));
        Assert.False(IsNumeric(null));
        Assert.False(IsNumeric(new object()));
    }

    [Fact]
    public void IsNothing_IsANullCheck()
    {
        Assert.True(IsNothing(null));
        Assert.False(IsNothing("a"));
        Assert.False(IsNothing(""));
    }

    [Fact]
    public void IsArray_AnswersForArraysOnly()
    {
        Assert.True(IsArray(new[] { "a" }));
        Assert.False(IsArray("a"));
        Assert.False(IsArray(null));
    }

    [Fact]
    public void IsDate_ParsesWithTheCurrentCulture()
    {
        Assert.True(IsDate("2020-01-02"));
        Assert.True(IsDate(new DateTime(2020, 1, 2)));
        Assert.False(IsDate("abc"));
        Assert.False(IsDate(null));
    }

    [Fact]
    public void LBoundAndUBound_AreTheArrayBounds()
    {
        var a = new[] { "a", "b" };
        Assert.Equal(0, LBound(a));
        Assert.Equal(1, UBound(a));
        Assert.Equal(0, UBound(new[] { "only" }));
        Assert.Equal(-1, UBound(new string[0]));
    }

    [Fact]
    public void UBound_OnNothing_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => UBound(null));
        Assert.Throws<ArgumentNullException>(() => LBound(null));
    }

    [Fact]
    public void UBound_RejectsARankTheArrayDoesNotHave()
    {
        Assert.Throws<ArgumentException>(() => UBound(new[] { "a" }, 2));
        Assert.Throws<ArgumentException>(() => UBound(new[] { "a" }, 0));
    }

    [Fact]
    public void UBound_OfASecondDimension()
    {
        var grid = new string[2, 5];
        Assert.Equal(1, UBound(grid));
        Assert.Equal(4, UBound(grid, 2));
    }
}
