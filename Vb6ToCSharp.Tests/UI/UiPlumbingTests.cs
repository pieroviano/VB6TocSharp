using System.Collections.Generic;
using Vb6ToCSharp.UI;

namespace Vb6ToCSharp.Tests.UI;

/// <summary>WPF/MVVM plumbing the front end binds to.</summary>
public class UiPlumbingTests
{
    [Fact]
    public void PropIndexer_ForwardsToDelegates()
    {
        var store = new Dictionary<int, string>();
        var p = new PropertyIndexer<int, string>(i => store[i], (i, v) => store[i] = v);
        p[1] = "x";
        Assert.Equal("x", p[1]);
        Assert.Null(new PropertyIndexer<int, string>()[5]);
    }

    [Fact]
    public void CommandBase_DefaultsToExecutable()
    {
        object? got = null;
        var c = new CommandBase(o => got = o);
        Assert.True(c.CanExecute(null));
        c.Execute(7);
        Assert.Equal(7, got);
        Assert.False(new CommandBase(_ => { }, () => false).CanExecute(null));
    }

    [Fact] public void ComboboxItem_ToStringIsText() => Assert.Equal("t", new ComboboxItem("t", 3).ToString());
}
