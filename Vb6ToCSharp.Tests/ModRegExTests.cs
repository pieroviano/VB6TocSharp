using static Vb6ToCSharp.Modules.ModRegEx;

namespace Vb6ToCSharp.Tests;

public class ModRegExTests
{
    [Fact] public void RegExTest_Matches() { Assert.True(RegExTest("abc", "b")); Assert.False(RegExTest("abc", "z")); }
    [Fact] public void RegExTest_IsCaseSensitive() => Assert.False(RegExTest("ABC", "abc"));
    [Fact] public void RegExCount_CountsAllMatches() => Assert.Equal(2, RegExCount("a1b2", "[0-9]"));
    [Fact] public void RegExCount_NoMatch_IsZero() => Assert.Equal(0, RegExCount("ab", "[0-9]"));
    [Fact] public void RegExNPos_IsOneBased() => Assert.Equal(3, RegExNPos("abc", "c"));
    [Fact] public void RegExNPos_NthMatch() => Assert.Equal(4, RegExNPos("a1b2", "[0-9]", 1));
    [Fact] public void RegExNPos_NoMatch_IsZero() => Assert.Equal(0, RegExNPos("abc", "z"));
    [Fact] public void RegExNPos_IndexBeyondMatches_IsZero() => Assert.Equal(0, RegExNPos("a1", "[0-9]", 3));
    [Fact] public void RegExNMatch_ReturnsMatch() => Assert.Equal("12", RegExNMatch("a12b", "[0-9]+"));
    [Fact] public void RegExNMatch_NoMatch_IsEmpty() => Assert.Equal("", RegExNMatch("ab", "[0-9]+"));
    [Fact] public void RegExReplace_ReplacesAll() => Assert.Equal("aXbX", RegExReplace("a1b2", "[0-9]", "X"));
    [Fact] public void RegExReplace_SupportsGroups() => Assert.Equal("b-a", RegExReplace("a-b", "(a)-(b)", "$2-$1"));

    [Fact]
    public void RegExSplit_SplitsOnPattern()
    {
        object[] parts = RegExSplit("a,b,c", ",");
        Assert.Equal(new object[] { "a", "b", "c" }, parts);
    }

    [Fact] public void RegExSplitCount_CountsParts() => Assert.Equal(3, RegExSplitCount("a,b,c", ","));

    [Fact]
    public void RegExSplit_DoesNotLeakIgnoreCaseIntoLaterCalls()
    {
        RegExSplit("a,b", ",");
        Assert.False(RegExTest("ABC", "abc"));
    }
}
