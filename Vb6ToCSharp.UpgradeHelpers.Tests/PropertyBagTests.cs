namespace Vb6ToCSharp.UpgradeHelpers.Tests;

public class PropertyBagTests
{
    [Fact]
    public void ReadProperty_Missing_ReturnsDefault()
    {
        var bag = new PropertyBag();
        Assert.Equal("dflt", bag.ReadProperty("Caption", "dflt"));
        Assert.Null(bag.ReadProperty("Caption"));
    }

    [Fact]
    public void WriteProperty_NamesAreCaseInsensitive()
    {
        var bag = new PropertyBag();
        bag.WriteProperty("Caption", "Hello");
        Assert.Equal("Hello", bag.ReadProperty("CAPTION"));
    }

    [Fact]
    public void WriteProperty_ValueEqualToDefault_IsNotStored()
    {
        var bag = new PropertyBag();
        bag.WriteProperty("Size", 5, 5.0); // VB compares by value
        Assert.Equal(99, bag.ReadProperty("Size", 99));

        bag.WriteProperty("Size", 7, 5);
        Assert.Equal(7, bag.ReadProperty("Size", 99));

        bag.WriteProperty("Size", 5, 5); // writing the default again removes the stored value
        Assert.Equal(99, bag.ReadProperty("Size", 99));
    }

    [Fact]
    public void Contents_RoundTrip_AllSupportedTypes()
    {
        var date = new DateTime(2024, 2, 29, 13, 45, 10, DateTimeKind.Local);
        var bag = new PropertyBag();
        bag.WriteProperty("s", "text");
        bag.WriteProperty("b", true);
        bag.WriteProperty("by", (byte)200);
        bag.WriteProperty("sb", (sbyte)-5);
        bag.WriteProperty("i16", (short)-1234);
        bag.WriteProperty("u16", (ushort)65000);
        bag.WriteProperty("i32", -123456);
        bag.WriteProperty("u32", 4000000000u);
        bag.WriteProperty("i64", long.MinValue);
        bag.WriteProperty("u64", ulong.MaxValue);
        bag.WriteProperty("f", 1.5f);
        bag.WriteProperty("d", Math.PI);
        bag.WriteProperty("m", 12.345m);
        bag.WriteProperty("dt", date);
        bag.WriteProperty("c", 'x');
        bag.WriteProperty("bytes", new byte[] { 1, 2, 3 });
        bag.WriteProperty("nul", null, "not null default");

        var copy = new PropertyBag { Contents = bag.Contents };

        Assert.Equal("text", copy.ReadProperty("s"));
        Assert.Equal(true, copy.ReadProperty("b"));
        Assert.Equal((byte)200, copy.ReadProperty("by"));
        Assert.Equal((sbyte)-5, copy.ReadProperty("sb"));
        Assert.Equal((short)-1234, copy.ReadProperty("i16"));
        Assert.Equal((ushort)65000, copy.ReadProperty("u16"));
        Assert.Equal(-123456, copy.ReadProperty("i32"));
        Assert.Equal(4000000000u, copy.ReadProperty("u32"));
        Assert.Equal(long.MinValue, copy.ReadProperty("i64"));
        Assert.Equal(ulong.MaxValue, copy.ReadProperty("u64"));
        Assert.Equal(1.5f, copy.ReadProperty("f"));
        Assert.Equal(Math.PI, copy.ReadProperty("d"));
        Assert.Equal(12.345m, copy.ReadProperty("m"));
        Assert.Equal(date, copy.ReadProperty("dt"));
        Assert.Equal('x', copy.ReadProperty("c"));
        Assert.Equal(new byte[] { 1, 2, 3 }, (byte[])copy.ReadProperty("bytes"));
        Assert.Null(copy.ReadProperty("nul", "fallback"));
    }

    [Fact]
    public void Contents_Enum_IsStoredAsUnderlyingValue()
    {
        var bag = new PropertyBag();
        bag.WriteProperty("day", DayOfWeek.Friday);
        var copy = new PropertyBag { Contents = bag.Contents };
        Assert.Equal((int)DayOfWeek.Friday, copy.ReadProperty("day"));
    }

    [Fact]
    public void Contents_Set_ReplacesValues_AndEmptyClears()
    {
        var bag = new PropertyBag();
        bag.WriteProperty("a", 1);
        bag.Contents = new PropertyBag().Contents;
        Assert.Null(bag.ReadProperty("a"));

        bag.WriteProperty("a", 1);
        bag.Contents = null!;
        Assert.Null(bag.ReadProperty("a"));
    }

    [Fact]
    public void Contents_InvalidData_Throws()
    {
        var bag = new PropertyBag();
        Assert.Throws<InvalidDataException>(() => bag.Contents = new byte[] { 1, 2, 3, 4, 5 });

        var good = new PropertyBag();
        good.WriteProperty("x", "value");
        var truncated = good.Contents.Take(good.Contents.Length - 2).ToArray();
        Assert.Throws<InvalidDataException>(() => bag.Contents = truncated);
    }

    [Fact]
    public void Contents_UnsupportedType_Throws()
    {
        var bag = new PropertyBag();
        bag.WriteProperty("obj", new object());
        var e = Assert.Throws<NotSupportedException>(() => bag.Contents);
        Assert.Contains("obj", e.Message);
    }

    [Fact]
    public void FromPairs_FillsBag()
    {
        var bag = PropertyBag.FromPairs("Caption", "OK", "Value", 3, "Default", 0);
        Assert.Equal("OK", bag.ReadProperty("caption"));
        Assert.Equal(3, bag.ReadProperty("Value"));
        Assert.Equal(0, bag.ReadProperty("Default", 9)); // pairs are stored verbatim
        Assert.Throws<ArgumentException>(() => PropertyBag.FromPairs("odd"));
        Assert.Throws<ArgumentException>(() => PropertyBag.FromPairs(1, 2));
    }
}
