using Vb6ToCSharp.ItemConversion;

namespace Vb6ToCSharp.Tests;

public class ModConvertUtilsTests
{
    public ModConvertUtilsTests()
    {
        ConverterUtils.ReComment(""); // flush any pending end-of-line comment
        ConverterUtils.InitDeString();
    }

    [Fact]
    public void DeComment_StripsCommentAndReCommentRestoresIt()
    {
        Assert.Equal("x = 1", ConverterUtils.DeComment("x = 1 ' hi"));
        Assert.Equal("y // hi", ConverterUtils.ReComment("y"));
        Assert.Equal("z", ConverterUtils.ReComment("z"));
    }

    [Fact] public void DeComment_IgnoresApostropheInString() => Assert.Equal("x = \"it's\"", ConverterUtils.DeComment("x = \"it's\""));
    [Fact] public void DeComment_AfterQuotedApostrophe() => Assert.Equal("x = \"it's\"", ConverterUtils.DeComment("x = \"it's\" ' c", true));

    [Fact]
    public void DeComment_Discard_DoesNotKeepComment()
    {
        ConverterUtils.DeComment("a ' b", true);
        Assert.Equal("q", ConverterUtils.ReComment("q"));
    }

    [Fact]
    public void ReComment_KeepVbComments_UsesApostrophe()
    {
        ConverterUtils.DeComment("a ' b");
        Assert.Equal("q ' b", ConverterUtils.ReComment("q", true));
    }

    [Fact]
    public void ReComment_MultiLine_AttachesToFirstLine()
    {
        ConverterUtils.DeComment("a ' b");
        Assert.Equal("l1// b\r\nl2", ConverterUtils.ReComment("l1\r\nl2"));
    }

    [Fact]
    public void DeString_ReString_RoundTrips()
    {
        const string src = "a = \"x\" & \"y\"\"z\"";
        var de = ConverterUtils.DeString(src);
        Assert.DoesNotContain("\"", de);
        Assert.Contains(ConverterUtils.deStringTokenBase, de);
        Assert.Equal(src, ConverterUtils.ReString(de));
    }

    [Fact]
    public void ReString_ConvertString_EscapesForCSharp()
    {
        var de = ConverterUtils.DeString("p = \"C:\\a\"\"b\"");
        Assert.Equal("p = \"C:\\\\a\\\"b\"", ConverterUtils.ReString(de, true));
    }

    [Fact] public void DeString_NoQuotes_Unchanged() => Assert.Equal("a = b", ConverterUtils.DeString("a = b"));
}