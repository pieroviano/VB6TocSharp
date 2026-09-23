using System;
using Vb6ToCSharp.Runtime.Model;
using static Vb6ToCSharp.Runtime.VbOperators;

namespace Vb6ToCSharp.Tests.Runtime;

/// <summary>
/// Expectations taken from Microsoft.VisualBasic.CompilerServices.LikeOperator, which VbOperators
/// replaces.
/// </summary>
public class VbOperatorsTests
{
    static bool Like(string s, string pattern) => LikeString(s, pattern, CompareMethod.Binary);

    [Theory]
    [InlineData("abc", "abc", true)]
    [InlineData("abc", "a*", true)]
    [InlineData("abc", "*c", true)]
    [InlineData("abcd", "a*d", true)]
    [InlineData("abcd", "a*x", false)]
    [InlineData("abc", "b*", false)]
    [InlineData("abc", "*", true)]
    [InlineData("", "*", true)]
    [InlineData("", "", true)]
    [InlineData("a", "", false)]
    public void LikeString_HandlesStarsAndLiterals(string s, string pattern, bool expected)
        => Assert.Equal(expected, Like(s, pattern));

    [Theory]
    [InlineData("abc", "a?c", true)]
    [InlineData("aXbc", "a?bc", true)]
    [InlineData("abc", "a?", false)]     // ? is exactly one character
    [InlineData("a1", "a#", true)]
    [InlineData("ab", "a#", false)]      // # is exactly one digit
    [InlineData("123", "###", true)]
    [InlineData("12a", "###", false)]
    public void LikeString_HandlesSingleCharacterWildcards(string s, string pattern, bool expected)
        => Assert.Equal(expected, Like(s, pattern));

    [Theory]
    [InlineData("b", "[a-c]", true)]
    [InlineData("d", "[a-c]", false)]
    [InlineData("b", "[!a-c]", false)]
    [InlineData("d", "[!a-c]", true)]
    [InlineData("abc", "[ab]bc", true)]
    [InlineData("a-c", "a[-]c", true)]   // a lone dash is itself
    public void LikeString_HandlesCharacterLists(string s, string pattern, bool expected)
        => Assert.Equal(expected, Like(s, pattern));

    [Fact]
    public void LikeString_IsCaseSensitiveUnlessAskedForText()
    {
        Assert.False(LikeString("abc", "A*", CompareMethod.Binary));
        Assert.True(LikeString("abc", "A*", CompareMethod.Text));
        Assert.True(LikeString("ABC", "[a-c]bc", CompareMethod.Text));
    }

    [Fact]
    public void LikeString_RejectsAnUnclosedCharacterList()
        => Assert.Throws<ArgumentException>(() => Like("a[b", "a[b"));

    [Fact]
    public void LikeString_ReadsNothingAsTheEmptyString()
    {
        Assert.True(LikeString(null, "", CompareMethod.Binary));
        Assert.True(LikeString(null, "*", CompareMethod.Binary));
        Assert.False(LikeString("a", null, CompareMethod.Binary));
    }
}
