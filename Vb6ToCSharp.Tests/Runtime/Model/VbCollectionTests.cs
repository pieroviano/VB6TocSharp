using System;
using System.Collections;
using System.Collections.Generic;
using Vb6ToCSharp.Runtime.Model;

namespace Vb6ToCSharp.Tests.Runtime.Model;

/// <summary>
/// Expectations taken from Microsoft.VisualBasic.Collection, which VbCollection replaces: keys are
/// case-insensitive, indexes start at 1, and the exceptions are the ones the converter's own code
/// catches (ConversionUtility.CVal relies on the ArgumentException for an unknown key).
/// </summary>
public class VbCollectionTests
{
    static VbCollection Keyed()
    {
        var c = new VbCollection();
        c.Add("v1", "KeyA");
        c.Add("v2", "keyb");
        return c;
    }

    [Fact]
    public void Count_CountsTheItems()
    {
        Assert.Empty(new VbCollection());
        Assert.Equal(2, Keyed().Count);
    }

    [Fact]
    public void Keys_AreCaseInsensitive()
    {
        var c = Keyed();
        Assert.True(c.Contains("keya"));
        Assert.True(c.Contains("KEYB"));
        Assert.Equal("v1", c["keya"]);
        Assert.Equal("v2", c["KeyB"]);
    }

    [Fact]
    public void Contains_IsFalseForAnItemAddedWithoutAKey()
    {
        var c = new VbCollection();
        c.Add("a");
        Assert.False(c.Contains("a"));
        Assert.Single(c);
    }

    [Fact]
    public void Index_StartsAtOne()
    {
        var c = Keyed();
        Assert.Equal("v1", c[1]);
        Assert.Equal("v2", c[2]);
    }

    [Fact]
    public void Item_TakesAKeyOrAnIndex()
    {
        var c = Keyed();
        Assert.Equal("v1", c.Item("keya"));
        Assert.Equal("v2", c.Item(2));
    }

    [Fact]
    public void Index_OutsideTheCollection_Throws()
    {
        var c = Keyed();
        Assert.Throws<IndexOutOfRangeException>(() => c[0]);
        Assert.Throws<IndexOutOfRangeException>(() => c[3]);
        Assert.Throws<IndexOutOfRangeException>(() => c.Item(-1));
    }

    [Fact]
    public void UnknownKey_ThrowsArgumentException()
    {
        var c = Keyed();
        Assert.Throws<ArgumentException>(() => c["nope"]);
        Assert.Throws<ArgumentException>(() => c.Item("nope"));
    }

    [Fact]
    public void DuplicateKey_ThrowsArgumentException()
    {
        var c = Keyed();
        Assert.Throws<ArgumentException>(() => c.Add("v3", "KEYA"));
        Assert.Equal(2, c.Count);
    }

    [Fact]
    public void Remove_ByKey_DropsTheItemAndRenumbers()
    {
        var c = Keyed();
        c.Remove("keya");
        Assert.Single(c);
        Assert.False(c.Contains("keya"));
        Assert.Equal("v2", c[1]);
    }

    [Fact]
    public void Remove_ByIndex_DropsTheItem()
    {
        var c = Keyed();
        c.Remove(1);
        Assert.Single(c);
        Assert.Equal("v2", c[1]);
        Assert.False(c.Contains("keya"));
    }

    [Fact]
    public void Remove_OfSomethingAbsent_Throws()
    {
        var c = Keyed();
        Assert.Throws<ArgumentException>(() => c.Remove("nope"));
        Assert.Throws<IndexOutOfRangeException>(() => c.Remove(9));
    }

    [Fact]
    public void Enumeration_FollowsInsertionOrder()
    {
        var c = new VbCollection();
        c.Add("a");
        c.Add("b");
        c.Add("c");

        var seen = new List<object>();
        foreach (var item in c) seen.Add(item);
        Assert.Equal(new object[] { "a", "b", "c" }, seen);
    }

    [Fact]
    public void Add_Before_InsertsAheadOfThatItem()
    {
        var c = Keyed();
        c.Add("v0", "first", "KeyA");
        Assert.Equal("v0", c[1]);
        Assert.Equal("v1", c[2]);
        Assert.Equal("v0", c["first"]);
        Assert.Equal("v1", c["keya"]);
    }

    [Fact]
    public void Add_After_InsertsBehindThatItem()
    {
        var c = Keyed();
        c.Add("mid", "between", null, 1);
        Assert.Equal("v1", c[1]);
        Assert.Equal("mid", c[2]);
        Assert.Equal("v2", c[3]);
        Assert.Equal("v2", c["keyb"]);
    }

    [Fact]
    public void Add_WithBothBeforeAndAfter_Throws()
        => Assert.Throws<ArgumentException>(() => new VbCollection().Add("x", "k", 1, 1));

    [Fact]
    public void ItCanBeUsedAsAnICollection()
    {
        ICollection c = Keyed();
        Assert.Equal(2, c.Count);
        Assert.False(c.IsSynchronized);
        Assert.NotNull(c.SyncRoot);

        var target = new object[2];
        c.CopyTo(target, 0);
        Assert.Equal(new object[] { "v1", "v2" }, target);
    }
}
