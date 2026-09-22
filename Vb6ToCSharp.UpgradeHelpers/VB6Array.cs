using System;
using System.Collections;
using System.Collections.Generic;

namespace Vb6ToCSharp.UpgradeHelpers;

/// <summary>
/// A one-dimensional VB6 array with any lower bound (Dim a(1 To 10), Option Base 1). The indexer returns the element
/// by reference, so members of UDT elements can be assigned in place (a[i].Name = x).
/// </summary>
public sealed class VB6Array<T> : IEnumerable<T>
{
    private T[] items;

    /// <summary>An array with bounds <paramref name="lbound"/> To <paramref name="ubound"/> (VB6-initialized elements).</summary>
    public VB6Array(int lbound, int ubound)
    {
        LBound = lbound;
        items = VbRuntime.NewArray<T>(Math.Max(0, ubound - lbound + 1));
    }

    /// <summary>An array not dimensioned yet (Dim a() under a non-zero lower bound): ReDim sizes it.</summary>
    public VB6Array(int lbound) : this(lbound, lbound - 1)
    {
    }

    public int LBound { get; private set; }

    public int UBound => LBound + items.Length - 1;

    public int Length => items.Length;

    public ref T this[int index]
    {
        get
        {
            if (index < LBound || index > UBound) throw new IndexOutOfRangeException("Subscript out of range");
            return ref items[index - LBound];
        }
    }

    /// <summary>ReDim [Preserve] a(lbound To ubound): Preserve keeps the elements whose index survives.</summary>
    public void ReDim(int lbound, int ubound, bool preserve = false)
    {
        var r = VbRuntime.NewArray<T>(Math.Max(0, ubound - lbound + 1));
        if (preserve)
        {
            for (var i = Math.Max(lbound, LBound); i <= Math.Min(ubound, UBound); i++) r[i - lbound] = items[i - LBound];
        }
        LBound = lbound;
        items = r;
    }

    /// <summary>Erase: a fixed array gets default elements again, a dynamic one is released.</summary>
    public void Erase(bool fixedSize)
    {
        if (fixedSize) ReDim(LBound, UBound);
        else ReDim(LBound, LBound - 1);
    }

    /// <summary>The elements as a zero-based .NET array (a copy).</summary>
    public T[] ToArray() => (T[])items.Clone();

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => items.GetEnumerator();
}
