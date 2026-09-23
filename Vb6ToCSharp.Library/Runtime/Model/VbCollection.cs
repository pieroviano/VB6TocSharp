using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Vb6ToCSharp.Runtime.Model;

/// <summary>
/// The VB6 <c>Collection</c> in plain C#: insertion-ordered, indexed from 1, with optional
/// case-insensitive keys. Replaces <c>Microsoft.VisualBasic.Collection</c> member for member,
/// including the exceptions callers depend on — <see cref="ArgumentException"/> for a duplicate or
/// unknown key (<c>ConversionUtility.CVal</c> catches it to return its default) and
/// <see cref="IndexOutOfRangeException"/> for an index outside 1..<see cref="Count"/>.
/// </summary>
public class VbCollection : ICollection
{
    private readonly List<object> items = new List<object>();
    private readonly List<string> itemKeys = new List<string>(); // null where the item was added without a key
    private readonly Dictionary<string, int> byKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    public int Count => items.Count;
    public bool IsSynchronized => false;
    public object SyncRoot => this;

    public void Add(object Item, string Key = null, object Before = null, object After = null)
    {
        if (Before != null && After != null)
            throw new ArgumentException("'Before' and 'After' cannot both be given.", nameof(After));
        if (Key != null && byKey.ContainsKey(Key))
            throw new ArgumentException("An item with the key '" + Key + "' is already in the collection.", nameof(Key));

        var at = Before != null ? PositionOf(Before) : After != null ? PositionOf(After) + 1 : items.Count;

        if (at == items.Count)
        {
            // Appending is the common case and stays O(1): only an insert has to renumber.
            items.Add(Item);
            itemKeys.Add(Key);
            if (Key != null) byKey[Key] = at;
            return;
        }

        items.Insert(at, Item);
        itemKeys.Insert(at, Key);
        Renumber();
    }

    public bool Contains(string Key) => Key != null && byKey.ContainsKey(Key);

    public void Remove(string Key)
    {
        RemoveAt(KeyPosition(Key));
    }

    public void Remove(int Index)
    {
        RemoveAt(IndexPosition(Index));
    }

    public dynamic Item(object Index) => items[PositionOf(Index)];

    // The indexers would be called Item in metadata, which the method above already takes.
    [IndexerName("Element")]
    public dynamic this[int Index] => items[IndexPosition(Index)];

    [IndexerName("Element")]
    public dynamic this[string Key] => items[KeyPosition(Key)];

    [IndexerName("Element")]
    public dynamic this[object Index] => items[PositionOf(Index)];

    public IEnumerator GetEnumerator() => items.GetEnumerator();

    public void CopyTo(Array array, int index) => ((ICollection)items).CopyTo(array, index);

    private void RemoveAt(int at)
    {
        items.RemoveAt(at);
        itemKeys.RemoveAt(at);
        Renumber();
    }

    private void Renumber()
    {
        byKey.Clear();
        for (var i = 0; i < itemKeys.Count; i++)
            if (itemKeys[i] != null) byKey[itemKeys[i]] = i;
    }

    private int PositionOf(object Index)
    {
        if (Index is string key) return KeyPosition(key);
        if (Index == null) throw new ArgumentException("An index or key is required.", nameof(Index));
        return IndexPosition(Convert.ToInt32(Index));
    }

    private int KeyPosition(string Key)
    {
        if (Key == null || !byKey.TryGetValue(Key, out var at))
            throw new ArgumentException("No item with the key '" + Key + "' is in the collection.", nameof(Key));
        return at;
    }

    private int IndexPosition(int Index)
    {
        if (Index < 1 || Index > items.Count)
            throw new IndexOutOfRangeException("Collection index " + Index + " is outside 1.." + items.Count + ".");
        return Index - 1;
    }
}
