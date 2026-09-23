using Microsoft.VisualBasic;
using Vb6ToCSharp.Tests.Infrastructure;
using static Vb6ToCSharp.Modules.ModUtils;

namespace Vb6ToCSharp.Tests;

public class ModUtilsTests
{
    [Fact] public void TFileName_ReturnsNamePart() => Assert.Equal("b.txt", TFileName(@"C:\a\b.txt"));
    [Fact] public void TFileName_NoFolder_ReturnsInput() => Assert.Equal("b.txt", TFileName("b.txt"));
    [Fact] public void FilePath_ReturnsFolderWithSlash() => Assert.Equal(@"C:\a\", FilePath(@"C:\a\b.txt"));

    [Theory]
    [InlineData(@"C:\a\b.txt", "b")]
    [InlineData(@"C:\a\b.c.txt", "b.c")]
    [InlineData(@"C:\a\README", "README")]
    [InlineData(@"C:\a.b\c", "c")]
    public void FileBaseName_StripsOnlyTheFileExtension(string fn, string expected) => Assert.Equal(expected, FileBaseName(fn));

    [Theory]
    [InlineData("a.txt", ".cs", "a.cs")]
    [InlineData(@"C:\d\a.b.txt", ".cs", @"C:\d\a.b.cs")]
    [InlineData(@"C:\d.x\file", ".cs", @"C:\d.x\file.cs")]
    public void ChgExt_ReplacesOrAppendsExtension(string fn, string ext, string expected) => Assert.Equal(expected, ChgExt(fn, ext));

    [Theory]
    [InlineData("a.TXT", true, ".txt")]
    [InlineData("a.TXT", false, ".TXT")]
    [InlineData("", true, "")]
    [InlineData("noext", true, "")]
    [InlineData(@"C:\d.x\file", true, "")]
    public void FileExt_ReturnsExtensionOfFileNameOnly(string fn, bool lcase, string expected) => Assert.Equal(expected, FileExt(fn, lcase));

    [Fact] public void TLeft_TrimsFirst() => Assert.Equal("ab", TLeft("  abcd", 2));

    [Fact]
    public void TMid_TrimsFirst()
    {
        Assert.Equal("bcd", TMid("  abcd ", 2));
        Assert.Equal("bc", TMid("  abcd", 2, 2));
    }

    [Fact] public void StrCnt_CountsOccurrences() => Assert.Equal(2, StrCnt("a,b,c", ","));
    [Fact] public void StrCnt_MultiCharNeedle() => Assert.Equal(2, StrCnt("a<>b<>c", "<>"));

    [Fact]
    public void LMatch_And_TLMatch()
    {
        Assert.True(LMatch("Private Sub", "Private"));
        Assert.False(LMatch("  Private Sub", "Private"));
        Assert.True(TLMatch("  Private Sub", "Private"));
    }

    [Theory]
    [InlineData(140, 10)]
    [InlineData(21, 2)]
    [InlineData(0, 0)]
    public void Px_ConvertsTwipsToPixels(double twips, int px) => Assert.Equal(px, Px(twips));

    [Fact] public void Px_String() => Assert.Equal(2, Px("28"));

    [Fact] public void Quote_WrapsInQuotes() => Assert.Equal("\"x\"", Quote("x"));

    [Fact]
    public void AlignString_PadsOrTruncates()
    {
        Assert.Equal("ab  ", AlignString("ab", 4));
        Assert.Equal("abc", AlignString("abcdef", 3));
    }

    [Fact]
    public void Capitalize_UppercasesFirstChar()
    {
        Assert.Equal("Hello", Capitalize("hello"));
        Assert.Equal("", Capitalize(""));
    }

    [Fact]
    public void IsIn_MatchesAnyCandidate()
    {
        Assert.True(IsIn("b", "a", "b"));
        Assert.False(IsIn("c", "a", "b"));
        Assert.False(IsIn("c"));
    }

    [Fact] public void IsNotInStr_Negates() => Assert.True(IsNotInStr("abc", "z"));

    [Theory]
    [InlineData("\"x\"", "x")]
    [InlineData("x", "x")]
    [InlineData("\"x", "x")]
    [InlineData("", "")]
    public void DeQuote_StripsSurroundingQuotes(string s, string expected) => Assert.Equal(expected, DeQuote(s));

    [Theory]
    [InlineData("\r\n  x \t\r\n", "x")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("a b", "a b")]
    public void NlTrim_TrimsWhitespaceAndNewlines(string s, string expected) => Assert.Equal(expected, NlTrim(s));

    [Fact] public void SSpace_ReturnsSpaces() => Assert.Equal("  ", SSpace(2));

    [Theory]
    [InlineData("a,b,c", 1, "a")]
    [InlineData("a,b,c", 2, "b")]
    [InlineData("a,b,c", 3, "c")]
    [InlineData("a,b,c", 4, "")]
    [InlineData("abc", 1, "abc")]
    public void NextBy_ReturnsIndexedPart(string src, int ind, string expected)
        => Assert.Equal(expected, TestUtil.WithTimeout(() => NextBy(src, ",", ind)));

    [Fact] public void NextByP_RespectsParentheses() => Assert.Equal("f(a,b)", NextByP("f(a,b),c", ","));
    [Fact] public void NextByP_SecondPart() => Assert.Equal("c", NextByP("f(a,b),c", ",", 2));

    [Theory]
    [InlineData("a,b,c", 3)]
    [InlineData("f(a,b),c", 2)]
    [InlineData("a", 1)]
    public void NextByPCt_CountsTopLevelParts(string src, int expected)
        => Assert.Equal(expected, TestUtil.WithTimeout(() => NextByPCt(src, ",")));

    [Fact] public void StrQCnt_IgnoresQuotedText() => Assert.Equal(1, StrQCnt("a(\"(\")", "("));

    [Fact]
    public void NextByOp_SplitsOnFirstOperator()
    {
        string op = null!;
        var first = NextByOp("a + b", 1, ref op);
        Assert.Equal("a", first);
        Assert.Equal(" + ", op);
    }

    [Fact] public void ReplaceToken_ReplacesWholeTokensOnly() => Assert.Equal(" y = x1 + y ", ReplaceToken(" x = x1 + x ", "x", "y"));
    [Fact] public void ReplaceToken_ReplacesAdjacentOccurrences() => Assert.Equal("(y,y)", ReplaceToken("(x,x)", "x", "y"));
    [Fact] public void ReplaceToken_ReplacesAtStringBoundaries() => Assert.Equal("y = y", ReplaceToken("x = x", "x", "y"));

    [Fact] public void SplitWord_Index() => Assert.Equal("Spain", SplitWord("The Rain In Spain Falls Mostly", 4));
    [Fact] public void SplitWord_IncludeRest() => Assert.Equal("Spain Falls Mostly", SplitWord("The Rain In Spain Falls Mostly", 4, includeRest: true));
    [Fact] public void SplitWord_NegativeIndexCountsFromEnd() => Assert.Equal("d", SplitWord("a:b:c:d", -1, ":"));
    [Fact] public void SplitWord_OutOfRange_IsEmpty() { Assert.Equal("", SplitWord("a b", 3)); Assert.Equal("", SplitWord("", 1)); }

    [Fact] public void CountWords_IgnoresBlanks() => Assert.Equal(6, CountWords("  The Rain In  Spain Falls Mostly "));
    [Fact] public void CountWords_CustomSeparator() => Assert.Equal(4, CountWords("The Rain In Spain Falls Mostly", "n"));

    [Fact]
    public void ArrSlice_ReturnsInclusiveRange()
    {
        Assert.Equal(new[] { 2, 3 }, (int[])ArrSlice(new[] { 1, 2, 3, 4 }, 1, 2));
        Assert.Equal(new[] { 3, 4 }, (int[])ArrSlice(new[] { 1, 2, 3, 4 }, 2, 99));
        Assert.Null(ArrSlice("x", 0, 1));
    }

    [Fact] public void SubArr_UsesLength() => Assert.Equal(new[] { 2, 3 }, (int[])SubArr(new[] { 1, 2, 3, 4 }, 1, 2));

    [Fact]
    public void ArrAdd_AppendsAndCreates()
    {
        dynamic[] arr = null!;
        ArrAdd(ref arr, 1);
        ArrAdd(ref arr, 2);
        Assert.Equal(2, arr.Length);
        Assert.Equal(2, (int)arr[1]);
    }

    [Fact]
    public void InRange_And_FitRange()
    {
        Assert.True(InRange(1, 2, 3));
        Assert.True(InRange(1, 1, 3));
        Assert.False(InRange(1, 1, 3, false));
        Assert.Equal(3, (int)FitRange(1, 5, 3));
        Assert.Equal(1, (int)FitRange(1, -5, 3));
        Assert.Equal(2, (int)FitRange(1, 2, 3));
    }

    [Fact]
    public void CodeSectionLoc_PointsPastAllAttributeLines()
    {
        var s = "VERSION 1.0 CLASS\r\nAttribute VB_Name = \"X\"\r\nAttribute VB_Exposed = False\r\nOption Explicit\r\n";
        Assert.Equal(s.IndexOf("Option", StringComparison.Ordinal) + 1, TestUtil.WithTimeout(() => CodeSectionLoc(s)));
    }

    [Fact] public void CodeSectionLoc_NoAttribute_IsZero() => Assert.Equal(0, CodeSectionLoc("Option Explicit"));

    [Fact]
    public void CodeSectionGlobalEndLoc_SkipsDeclaresAndStopsBeforeFirstProcedure()
    {
        var s = "Option Explicit\r\nPrivate Declare Function GetTickCount Lib \"kernel32\" () As Long\r\nDim x As Long\r\n\r\nPublic Function Foo()\r\nEnd Function\r\n";
        var end = TestUtil.WithTimeout(() => CodeSectionGlobalEndLoc(s));
        Assert.Equal(s.IndexOf("Public Function", StringComparison.Ordinal), end);
    }

    [Fact]
    public void CodeSectionGlobalEndLoc_NoProcedures_IsWholeString()
    {
        var s = "Option Explicit\r\nDim x As Long\r\n";
        Assert.Equal(s.Length, TestUtil.WithTimeout(() => CodeSectionGlobalEndLoc(s)));
    }

    [Theory]
    [InlineData("+")]
    [InlineData(" - ")]
    [InlineData("<>")]
    [InlineData("Mod")]
    [InlineData("And")]
    [InlineData("Xor")]
    public void IsOperator_RecognisesVbOperators(string s) => Assert.True(IsOperator(s));

    [Fact] public void IsOperator_RejectsTokens() => Assert.False(IsOperator("x"));

    [Fact]
    public void CVal_ReturnsValueOrDefault()
    {
        var c = new Collection();
        c.Add("v", "key");
        Assert.Equal("v", CVal(ref c, "KEY"));
        Assert.Equal("def", CVal(ref c, "missing", "def"));
    }

    [Fact]
    public void CValP_DeQuotesAndEscapes()
    {
        var c = new Collection();
        c.Add("\"a<b\"", "k");
        Assert.Equal("a&lt;b", CValP(ref c, "k"));
    }

    [Fact] public void P_EscapesXml() => Assert.Equal("&lt;a &amp; b&gt;", P("<a & b>"));
    [Fact] public void ModuleName_ReadsVbName() => Assert.Equal("modX", ModuleName("Attribute VB_Name = \"modX\"\r\n"));

    [Fact]
    public void IsInCode_DetectsComments()
    {
        var s = "x = 1 ' comment";
        Assert.False(IsInCode(s, s.IndexOf("comment", StringComparison.Ordinal) + 1));
        var t = "x = \"'\" + y";
        Assert.True(IsInCode(t, t.Length));
        Assert.True(IsInCode("a\r\nb", 4));
    }

    [Fact] public void TokenList_ListsIdentifiers() => Assert.Equal(",a,b1", TokenList("a + b1"));

    [Fact]
    public void Stack_PushPopIsLifo()
    {
        var src = "";
        Stack(ref src, "a");
        Stack(ref src, "b");
        Assert.Equal("b", Stack(ref src, "##REM##", true));
        Assert.Equal("b", Stack(ref src));
        Assert.Equal("a", Stack(ref src));
        Assert.Equal("", src);
    }

    [Fact]
    public void Stack_ValuesWithCommasAndQuotesSurvive()
    {
        var src = "";
        Stack(ref src, "outer");
        Stack(ref src, "Foo(1, \"x\")");
        Assert.Equal("Foo(1, \"x\")", Stack(ref src));
        Assert.Equal("outer", Stack(ref src));
    }

    [Fact] public void QuoteXml_EscapesAndQuotes() => Assert.Equal("\"a&quot;b\"", QuoteXml("a\"b"));

    [Fact] public void ReduceString_Slugifies() => Assert.Equal("something-to-be-slugified", ReduceString("   Something To be 'slugified'!!!****"));
    [Fact] public void ReduceString_MaxLenAndCase() => Assert.Equal("AB", ReduceString("A B C", "ABC", "", 2, false));

    [Fact]
    public void DeWs_CollapsesBlankLines()
    {
        var s = "a  \r\n\r\n\r\n\r\n\r\nb";
        Assert.Equal("a\r\n\r\n\r\nb", DeWs(s));
    }

    [Fact] public void Random_IsInRange() { var r = Random(5); Assert.InRange(r, 1, 5); }
}
