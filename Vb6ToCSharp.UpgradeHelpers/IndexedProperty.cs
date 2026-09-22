using System;

namespace Vb6ToCSharp.UpgradeHelpers;

/// <summary>VB6 parameterized property with one index (e.g. <c>ColWidth(col)</c>).</summary>
public sealed class IndexedProperty<TValue>
{
    private readonly Func<int, TValue> _get;
    private readonly Action<int, TValue> _set;

    internal IndexedProperty(Func<int, TValue> get, Action<int, TValue> set)
    {
        _get = get;
        _set = set;
    }

    public TValue this[int index]
    {
        get => _get(index);
        set => _set(index, value);
    }
}

/// <summary>VB6 parameterized property with two indexes (e.g. <c>TextMatrix(row, col)</c>).</summary>
public sealed class MatrixProperty<TValue>
{
    private readonly Func<int, int, TValue> _get;
    private readonly Action<int, int, TValue> _set;

    internal MatrixProperty(Func<int, int, TValue> get, Action<int, int, TValue> set)
    {
        _get = get;
        _set = set;
    }

    public TValue this[int row, int col]
    {
        get => _get(row, col);
        set => _set(row, col, value);
    }
}
