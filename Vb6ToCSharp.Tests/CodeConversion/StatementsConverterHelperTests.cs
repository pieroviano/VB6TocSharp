using System.Linq;
using Vb6ToCSharp.CodeConversion;

namespace Vb6ToCSharp.Tests.CodeConversion;

/// <summary>Pure helpers of the statement layer (no project configuration needed).</summary>
public class StatementsConverterHelperTests
{
    [Theory]
    [InlineData("&HFF", "0xFF")]
    [InlineData("&H8000", "-32768")]
    [InlineData("&H8000&", "0x8000")]
    [InlineData("&HFFFFFFFF", "-1")]
    [InlineData("&H7FFFFFFF", "0x7FFFFFFF")]
    [InlineData("&O777", "511")]
    [InlineData("12", null)]
    public void ConvertRadixLiteral_FollowsVbTyping(string vb, string? cs) => Assert.Equal(cs, StatementsConverter.ConvertRadixLiteral(vb));

    [Theory]
    [InlineData("Left$(s$, 2)", "Left(s, 2)")]
    [InlineData("n% = a& + b! * c# - d@", "n = a + b * c - d")]
    [InlineData("x = 5& + 1.5#", "x = 5 + 1.5")]
    [InlineData("v = RS!Name", "v = RS!Name")]
    [InlineData("n = &HFF&", "n = &HFF&")]
    [InlineData("d = #1/2/2003#", "d = #1/2/2003#")]
    [InlineData("Print #1, a", "Print #1, a")]
    public void StripTypeSuffixes(string vb, string expected) => Assert.Equal(expected, StatementsConverter.StripTypeSuffixes(vb));

    [Fact]
    public void SplitTopLevel_IgnoresNestedCommas() => Assert.Equal(new[] { "a(1, 2)", "b", "c(d(e, f))" }, StatementsConverter.SplitTopLevel("a(1, 2), b, c(d(e, f))"));

    [Theory]
    [InlineData("a Else b", "a", "b")]
    [InlineData("If x Then a Else b", "If x Then a Else b", null)]
    [InlineData("If x Then a Else b Else c", "If x Then a Else b", "c")]
    [InlineData("a", "a", null)]
    public void SplitSingleLineIf_BindsElseToInnermostIf(string rest, string thenPart, string? elsePart)
    {
        CodeConverter.SplitSingleLineIf(rest, out var t, out var e);
        Assert.Equal(thenPart, t);
        Assert.Equal(elsePart, e);
    }

    [Theory]
    [InlineData("x = 1", 3)]
    [InlineData("  g(1, 2) = x", 11)]
    [InlineData("Set o = New C", 7)]
    [InlineData("RS!F = 2", 6)]
    [InlineData("Foo a, b", 0)]
    [InlineData("Debug.Print a = b", 0)]
    [InlineData("If a = b Then", 0)]
    public void AssignmentPos_FindsTheTopLevelEquals(string s, int pos) => Assert.Equal(pos, CodeConverter.AssignmentPos(s));
}
