using static Vb6ToCSharp.Runtime.VbConversion;

namespace Vb6ToCSharp.Tests.Runtime;

/// <summary>
/// Expectations taken from Microsoft.VisualBasic.Conversion, which VbConversion replaces.
/// </summary>
public class VbConversionTests
{
    [Theory]
    [InlineData("12ab", 12d)]       // stops at the first character that is not part of the number
    [InlineData("", 0d)]
    [InlineData("abc", 0d)]
    [InlineData("0", 0d)]
    [InlineData("007", 7d)]
    [InlineData("-3.5", -3.5d)]
    [InlineData("+3", 3d)]
    [InlineData(".5", 0.5d)]
    [InlineData("5.", 5d)]
    [InlineData("1.5e2", 150d)]
    [InlineData("1e+3", 1000d)]
    [InlineData("1d3", 1000d)]      // VB's double exponent
    [InlineData("1e", 1d)]          // a trailing exponent marker is not part of it
    [InlineData("1,5", 1d)]         // a comma ends the number
    [InlineData("&H10", 16d)]
    [InlineData("&HFF", 255d)]
    [InlineData("&O17", 15d)]
    public void Val_ReadsTheLeadingNumber(string input, double expected) => Assert.Equal(expected, Val(input));

    [Theory]
    [InlineData(" 1 2 ", 12d)]      // white space is ignored wherever it is
    [InlineData("- 3", -3d)]
    [InlineData("1\t2", 12d)]
    public void Val_IgnoresWhiteSpaceInsideTheNumber(string input, double expected)
        => Assert.Equal(expected, Val(input));

    [Fact]
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
    public void Val_ReadsNullAsZero() => Assert.Equal(0d, Val((string)null));
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.

    [Theory]
    [InlineData(5, " 5")]           // a space stands where the sign would be
    [InlineData(0, " 0")]
    [InlineData(-5, "-5")]
    public void Str_PutsASpaceWhereTheSignWouldBe(int number, string expected) => Assert.Equal(expected, Str(number));

    [Fact]
    public void Str_UsesTheInvariantDecimalPoint() => Assert.Equal(" 1.5", Str(1.5));

    [Fact]
    public void HexAndOct_AreUppercaseAndUnprefixed()
    {
        Assert.Equal("FF", Hex(255));
        Assert.Equal("10", Hex(16));
        Assert.Equal("17", Oct(15));
    }

    [Fact]
    public void FixAndInt_DifferOnNegativeNumbers()
    {
        Assert.Equal(1d, Fix(1.7));
        Assert.Equal(-1d, Fix(-1.7));   // Fix truncates towards zero
        Assert.Equal(1d, Int(1.7));
        Assert.Equal(-2d, Int(-1.7));   // Int rounds down
    }
}
