using System;
using Vb6ToCSharp.Runtime.Model;
using static Vb6ToCSharp.Runtime.VbStrings;

namespace Vb6ToCSharp.Tests.Runtime;

/// <summary>
/// VbStrings replaces Microsoft.VisualBasic.Strings for the converter's own code, so every
/// expectation here was taken from the VB runtime itself — these are its answers, not a guess at
/// what they should be.
/// </summary>
public class VbStringsTests
{
    [Fact]
    public void Mid_IsOneBasedAndClamps()
    {
        Assert.Equal("cdef", Mid("abcdef", 3));
        Assert.Equal("abcdef", Mid("abcdef", 1));
        Assert.Equal("f", Mid("abcdef", 6));
        Assert.Equal("", Mid("abcdef", 7));
        Assert.Equal("", Mid("abcdef", 20));
        Assert.Equal("bcd", Mid("abcdef", 2, 3));
        Assert.Equal("bcdef", Mid("abcdef", 2, 99));
        Assert.Equal("f", Mid("abcdef", 6, 3));
        Assert.Equal("", Mid("abcdef", 3, 0));
        Assert.Equal("", Mid("", 1));
        Assert.Equal("", Mid(null, 1));
    }

    [Fact]
    public void Mid_RejectsAStartBeforeOneAndANegativeLength()
    {
        Assert.Throws<ArgumentException>(() => Mid("abc", 0));
        Assert.Throws<ArgumentException>(() => Mid("abc", 1, -1));
    }

    [Fact]
    public void LeftAndRight_ClampInsteadOfThrowing()
    {
        Assert.Equal("", Left("abcdef", 0));
        Assert.Equal("ab", Left("abcdef", 2));
        Assert.Equal("abcdef", Left("abcdef", 6));
        Assert.Equal("abcdef", Left("abcdef", 99));
        Assert.Equal("", Left("", 3));
        Assert.Equal("", Left(null, 1));

        Assert.Equal("", Right("abcdef", 0));
        Assert.Equal("ef", Right("abcdef", 2));
        Assert.Equal("abcdef", Right("abcdef", 99));
        Assert.Equal("", Right("", 3));
        Assert.Equal("", Right(null, 1));

        Assert.Throws<ArgumentException>(() => Left("abc", -1));
        Assert.Throws<ArgumentException>(() => Right("abc", -1));
    }

    [Fact]
    public void Len_ReadsNullAsZero()
    {
        Assert.Equal(3, Len("abc"));
        Assert.Equal(0, Len(""));
        Assert.Equal(0, Len(null));
    }

    [Fact]
    public void Trim_TakesSpacesOnly_NotTabs()
    {
        // The difference that matters: the converter reads tab-indented VB6 source, and VB's Trim
        // leaves a tab where string.Trim() would take it.
        Assert.Equal("\ta\t", Trim("\ta\t"));
        Assert.Equal("a\t", Trim(" a\t "));
        Assert.Equal("a b", Trim("  a b  "));
        Assert.Equal("abc", Trim("abc"));
        Assert.Equal("", Trim("   "));
        Assert.Equal("", Trim(null));
        // The other space VB trims.
        Assert.Equal("a", Trim("　a　"));
    }

    [Fact]
    public void LTrimAndRTrim_TakeOneEndEach()
    {
        Assert.Equal("a  ", LTrim("  a  "));
        Assert.Equal("  a", RTrim("  a  "));
        Assert.Equal("\ta  ", LTrim("  \ta  "));
        Assert.Equal("", LTrim(null));
        Assert.Equal("", RTrim(null));
    }

    [Fact]
    public void UCaseAndLCase_ReadNullAsEmpty()
    {
        Assert.Equal("ABC", UCase("aBc"));
        Assert.Equal("abc", LCase("AbC"));
        Assert.Equal("", UCase(""));
        Assert.Equal("", UCase(null));
        Assert.Equal("", LCase(null));
    }

    [Fact]
    public void SpaceAndStrDup_BuildRuns()
    {
        Assert.Equal("", Space(0));
        Assert.Equal("   ", Space(3));
        Assert.Equal("xxx", StrDup(3, "xy"));
        Assert.Equal("---", StrDup(3, '-'));
        Assert.Throws<ArgumentException>(() => Space(-1));
        Assert.Throws<ArgumentException>(() => StrDup(3, ""));
    }

    [Fact]
    public void LSetAndRSet_PadOrTruncateToWidth()
    {
        Assert.Equal("ab   ", LSet("ab", 5));
        Assert.Equal("abc", LSet("abcdef", 3));
        Assert.Equal("   ab", RSet("ab", 5));
        Assert.Equal("abc", RSet("abcdef", 3));
        Assert.Equal("   ", LSet(null, 3));
    }

    [Fact]
    public void InStr_IsOneBasedAndZeroWhenMissing()
    {
        Assert.Equal(2, InStr("abcabc", "b"));
        Assert.Equal(0, InStr("abcabc", "z"));
        Assert.Equal(2, InStr("abcabc", "bc"));
        Assert.Equal(1, InStr("abc", ""));
        Assert.Equal(0, InStr("", "a"));
        Assert.Equal(0, InStr("", ""));
        Assert.Equal(0, InStr("ABC", "b"));
        Assert.Equal(2, InStr("ABC", "b", CompareMethod.Text));
    }

    [Fact]
    public void InStr_WithAStart_SearchesFromThere()
    {
        Assert.Equal(5, InStr(3, "abcabc", "b"));
        Assert.Equal(5, InStr(4, "abcabc", "b"));
        Assert.Equal(0, InStr(7, "abcabc", "b"));
        Assert.Equal(3, InStr(3, "abcabc", ""));
        Assert.Throws<ArgumentException>(() => InStr(0, "abc", "b"));
    }

    [Fact]
    public void InStrRev_SearchesInsideTheFirstStartCharacters()
    {
        Assert.Equal(5, InStrRev("abcabc", "b"));
        Assert.Equal(2, InStrRev("abcabc", "b", 4));
        Assert.Equal(5, InStrRev("abcabc", "bc"));
        // The match has to end inside the first Start characters, so this is 2 and not 5.
        Assert.Equal(2, InStrRev("abcabc", "bc", 5));
        Assert.Equal(2, InStrRev("abcabc", "bc", 4));
        Assert.Equal(0, InStrRev("abcabc", "z"));
        Assert.Equal(3, InStrRev("abc", ""));
        Assert.Equal(1, InStrRev("abc", "a", 1));
        Assert.Equal(0, InStrRev("", "a"));
        Assert.Equal(0, InStrRev("abc", "c", 9));
        Assert.Equal(0, InStrRev(null, "a"));
    }

    [Fact]
    public void InStrRev_RejectsAStartOfZeroOrBelowMinusOne()
    {
        Assert.Throws<ArgumentException>(() => InStrRev("abc", "b", 0));
        Assert.Throws<ArgumentException>(() => InStrRev("abc", "b", -2));
    }

    [Fact]
    public void Replace_ReplacesEveryOccurrenceByDefault()
    {
        Assert.Equal("aXcaXc", Replace("abcabc", "b", "X"));
        Assert.Equal("acac", Replace("abcabc", "b", ""));
        Assert.Equal("zz", Replace("abcabc", "abc", "z"));
        Assert.Equal("abcabc", Replace("abcabc", "z", "X"));
        Assert.Equal("ac", Replace("abc", "b", null));
        Assert.Equal("abc", Replace("abc", "", "X"));
        Assert.Equal("AXC", Replace("ABC", "b", "X", 1, -1, CompareMethod.Text));
    }

    [Fact]
    public void Replace_AnswersNothingForAnEmptyExpression()
    {
        // VB really does return Nothing here; RuntimeExtension.Replace coalesces it to "".
        Assert.Null(Replace("", "a", "X"));
        Assert.Null(Replace(null, "a", "X"));
        Assert.Null(Replace("abc", "b", "X", 9));
    }

    [Fact]
    public void Replace_WithStartAndCount_ReturnsTheTailFromStart()
    {
        Assert.Equal("bba", Replace("aaa", "a", "b", 1, 2));
        Assert.Equal("cXbc", Replace("abcabc", "a", "X", 3));
        Assert.Equal("cXbc", Replace("abcabc", "a", "X", 3, 1));
        Assert.Equal("aa", Replace("aaa", "a", "b", 2, 0));
        Assert.Throws<ArgumentException>(() => Replace("abc", "b", "X", 0));
        Assert.Throws<ArgumentException>(() => Replace("abc", "b", "X", 1, -2));
    }

    [Fact]
    public void Split_KeepsEmptyFieldsAndAnswersOneElementForNothing()
    {
        Assert.Equal(new[] { "a", "b" }, Split("a,b", ","));
        Assert.Equal(new[] { "a", "", "b" }, Split("a,,b", ","));
        Assert.Equal(new[] { "", "a" }, Split(",a", ","));
        Assert.Equal(new[] { "a", "" }, Split("a,", ","));
        Assert.Equal(new[] { "" }, Split("", ","));
        Assert.Equal(new[] { "" }, Split(null, ","));
        Assert.Equal(new[] { "abc" }, Split("abc", ""));
        Assert.Equal(new[] { "a", "b" }, Split("a b"));
    }

    [Fact]
    public void Split_HonoursLimitAndCompare()
    {
        Assert.Equal(new[] { "a", "b,c" }, Split("a,b,c", ",", 2));
        Assert.Equal(new[] { "a,b,c" }, Split("a,b,c", ",", 1));
        Assert.Equal(new[] { "a", "b", "c" }, Split("aXbxc", "x", -1, CompareMethod.Text));
    }

    [Fact]
    public void Join_AnswersNothingForAnEmptyArray()
    {
        Assert.Null(Join(new string[0], ","));
        Assert.Equal("a", Join(new[] { "a" }, ","));
        Assert.Equal("a,,b", Join(new[] { "a", null, "b" }, ","));
        Assert.Equal("a b", Join(new[] { "a", "b" }));
    }

    [Fact]
    public void Filter_KeepsOrDropsWhatMatches()
    {
        Assert.Equal(new[] { "ab", "ax" }, Filter(new[] { "ab", "cd", "ax" }, "a"));
        Assert.Equal(new[] { "cd" }, Filter(new[] { "ab", "cd", "ax" }, "a", false));
        Assert.Equal(new[] { "ab" }, Filter(new[] { "ab", "cd" }, "A", true, CompareMethod.Text));
        Assert.Null(Filter(new[] { "ab" }, ""));
    }

    [Fact]
    public void StrComp_IsOrdinalUnlessAskedForText()
    {
        Assert.Equal(-1, StrComp("A", "a"));
        Assert.Equal(1, StrComp("a", "A"));
        Assert.Equal(0, StrComp("a", "a"));
        Assert.Equal(0, StrComp("A", "a", CompareMethod.Text));
        Assert.Equal(-1, StrComp("", "a"));
        Assert.Equal(-1, StrComp(null, "a"));
    }

    [Fact]
    public void AscAndChr_RoundTripAscii()
    {
        Assert.Equal(97, Asc("a"));
        Assert.Equal(65, Asc("A"));
        Assert.Equal(97, Asc("abc"));
        Assert.Equal(97, AscW("a"));
        Assert.Equal('A', Chr(65));
        Assert.Equal('\0', Chr(0));
        Assert.Equal('A', ChrW(65));
        Assert.Throws<ArgumentException>(() => Asc(""));
        Assert.Throws<ArgumentException>(() => Chr(256));
    }
}
