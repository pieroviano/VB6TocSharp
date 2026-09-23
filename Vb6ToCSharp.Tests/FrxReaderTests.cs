using System.Text;
using Vb6ToCSharp.Parsing;
using Vb6ToCSharp.Parsing.Model;

namespace Vb6ToCSharp.Tests;

public class FrxReaderTests
{
    private static byte[] Bytes(params byte[] b) => b;

    [Fact]
    public void ReadBlob_LtHeader()
    {
        var img = new byte[] { 0x42, 0x4D, 1, 2, 3 };
        var data = new byte[] { (byte)'l', (byte)'t', 0, 0, 5, 0, 0, 0 }.Concat(img).ToArray();
        var blob = new FrxReader(data).ReadBlob(0);
        Assert.Equal(img, blob);
        Assert.Equal(FrxBlobKind.Bmp, FrxReader.Sniff(blob));
    }

    [Fact]
    public void ReadBlob_SizeHeaderAtOffset_SkipsPaddingBeforeMagic()
    {
        var data = new byte[] { 9, 9, 9, 10, 0, 0, 0, 0xAA, 0xBB, 0x00, 0x00, 0x01, 0x00, 7, 7, 7, 7 };
        var blob = new FrxReader(data).ReadBlob(3);
        Assert.Equal(FrxBlobKind.Icon, FrxReader.Sniff(blob));
        Assert.Equal(8, blob.Length);
    }

    [Fact]
    public void ReadString_Variants()
    {
        var enc = Encoding.Default;
        var u8 = new byte[] { 3 }.Concat(enc.GetBytes("abc")).ToArray();
        Assert.Equal("abc", new FrxReader(u8).ReadString(0));
        var ff = new byte[] { 0xFF, 4, 0 }.Concat(enc.GetBytes("wxyz")).ToArray();
        Assert.Equal("wxyz", new FrxReader(ff).ReadString(0));
        var u32 = new byte[] { 5, 0, 0, 0 }.Concat(enc.GetBytes("a\r\nbc")).ToArray();
        Assert.Equal("a\r\nbc", new FrxReader(u32).ReadString(0));
        var one = new byte[] { 1, (byte)'Z', 0, 0 };
        Assert.Equal("Z", new FrxReader(one).ReadString(0));
    }

    [Fact]
    public void ReadListAndItemData()
    {
        var enc = Encoding.Default;
        var list = new byte[] { 2, 0, 3, 0 }.Concat(enc.GetBytes("One")).Concat(new byte[] { 3, 0 }).Concat(enc.GetBytes("Two")).ToArray();
        Assert.Equal(new[] { "One", "Two" }, new FrxReader(list).ReadList(0));
        var data = new byte[] { 2, 0, 10, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0xFF };
        Assert.Equal(new[] { 10, -1 }, new FrxReader(data).ReadItemData(0));
    }

    [Fact]
    public void Reads_AreBoundsSafe()
    {
        var r = new FrxReader(Bytes(0xFF));
        Assert.Equal("", r.ReadString(5));
        Assert.Empty(r.ReadList(0));
        Assert.Empty(r.ReadBlob(10));
    }

    [Theory]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38 }, FrxBlobKind.Gif, ".gif")]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, FrxBlobKind.Jpeg, ".jpg")]
    [InlineData(new byte[] { 0xD7, 0xCD, 0xC6, 0x9A }, FrxBlobKind.Wmf, ".wmf")]
    [InlineData(new byte[] { 0x00, 0x00, 0x02, 0x00 }, FrxBlobKind.Cursor, ".cur")]
    [InlineData(new byte[] { 1, 2, 3, 4 }, FrxBlobKind.Unknown, ".bin")]
    public void Sniff_KnownFormats(byte[] b, FrxBlobKind kind, string ext)
    {
        Assert.Equal(kind, FrxReader.Sniff(b));
        Assert.Equal(ext, FrxReader.Extension(kind));
    }
}