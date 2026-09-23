using System.Collections.Generic;
using Extras.Model;

namespace Extras.Tests.Model;

public class PropIndexerTests
{
    [Fact]
    public void Indexer_RoutesGetAndSetToTheDelegates()
    {
        var store = new Dictionary<string, int>();
        var idx = new PropIndexer<string, int>(k => store.TryGetValue(k, out var v) ? v : -1, (k, v) => store[k] = v);

        idx["a"] = 7;
        Assert.Equal(7, store["a"]);
        Assert.Equal(7, idx["a"]);
        Assert.Equal(-1, idx["missing"]);
    }

    [Fact]
    public void Indexer_WithoutASetter_IgnoresWrites()
    {
        var idx = new PropIndexer<string, int>(k => 3);
        idx["a"] = 9;
        Assert.Equal(3, idx["a"]);
    }

    [Fact]
    public void Indexer_WithoutDelegates_ReadsTheDefaultValue()
    {
        Assert.Equal(0, new PropIndexer<string, int>()["a"]);
        Assert.Null(new PropIndexer<string, string>()["a"]);
    }

    [Fact]
    public void TwoIndexIndexer_RoutesGetAndSetToTheDelegates()
    {
        var store = new Dictionary<string, string>();
        var idx = new PropIndexer<int, int, string>(
            (r, c) => store.TryGetValue(r + ":" + c, out var v) ? v : "",
            (r, c, v) => store[r + ":" + c] = v);

        idx[1, 2] = "x";
        Assert.Equal("x", idx[1, 2]);
        Assert.Equal("", idx[2, 1]);
    }

    [Fact]
    public void TwoIndexIndexer_WithoutASetter_IgnoresWrites()
    {
        var idx = new PropIndexer<int, int, string>((r, c) => "fixed");
        idx[1, 2] = "x";
        Assert.Equal("fixed", idx[1, 2]);
    }

    [Fact]
    public void TwoIndexIndexer_WithoutDelegates_ReadsTheDefaultValue()
        => Assert.Null(new PropIndexer<int, int, string>()[0, 0]);
}
