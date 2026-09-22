using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Vb6ToCSharp.UpgradeHelpers;

/// <summary>
/// UI-agnostic VB6 control array: sparse integer indexes, design-time vs runtime-loaded elements,
/// event hookups replayed on every element. UI stacks derive and supply clone/attach/detach.
/// </summary>
public abstract class ControlArrayBase<T> : IEnumerable<T> where T : class
{
    private readonly SortedDictionary<int, T> _items = new();
    private readonly HashSet<int> _designTime = new();
    private readonly List<Action<T>> _hookups = new();

    protected ControlArrayBase(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));

    /// <summary>VB6 array name (base of the names given to loaded elements: <c>Name_index</c>).</summary>
    public string Name { get; }

    /// <summary>Element at <paramref name="index"/>; VB6 error 340 when missing.</summary>
    public T this[int index] => _items.TryGetValue(index, out var c) ? c : throw ControlArrayException.ElementNotFound(index);

    public int Count => _items.Count;

    public bool Exists(int index) => _items.ContainsKey(index);

    /// <summary>Lowest index (0 when empty).</summary>
    public int LBound() => _items.Count == 0 ? 0 : _items.Keys.First();

    /// <summary>Highest index (-1 when empty, so <c>For i = LBound To UBound</c> does not run).</summary>
    public int UBound() => _items.Count == 0 ? -1 : _items.Keys.Last();

    /// <summary>VB6 <c>Index</c> of <paramref name="control"/>; -1 when it is not an element.</summary>
    public int GetIndex(object control)
    {
        foreach (var kv in _items)
            if (ReferenceEquals(kv.Value, control)) return kv.Key;
        return -1;
    }

    /// <summary>Registers a design-time element (emitted by the designer); existing hookups are applied to it.</summary>
    public void SetIndex(T control, int index)
    {
        if (control == null) throw new ArgumentNullException(nameof(control));
        if (_items.TryGetValue(index, out var existing))
        {
            if (ReferenceEquals(existing, control)) return;
            throw ControlArrayException.AlreadyLoaded();
        }
        var previous = GetIndex(control);
        if (previous >= 0)
        {
            _items.Remove(previous);
            _designTime.Remove(previous);
        }
        _items.Add(index, control);
        _designTime.Add(index);
        foreach (var hookup in _hookups) hookup(control);
    }

    /// <summary>Applies <paramref name="hookup"/> to every current element and to every element added later.</summary>
    public void Wire(Action<T> hookup)
    {
        if (hookup == null) throw new ArgumentNullException(nameof(hookup));
        _hookups.Add(hookup);
        foreach (var c in _items.Values.ToList()) hookup(c);
    }

    /// <summary>VB6 <c>Load arr(index)</c>: clones the lowest-index element; the clone starts invisible.</summary>
    public T Load(int index)
    {
        if (_items.ContainsKey(index)) throw ControlArrayException.AlreadyLoaded();
        if (_items.Count == 0)
            throw new InvalidOperationException($"Control array '{Name}' has no element to clone.");
        var template = _items.First().Value;
        var highest = _items.Last().Value;
        var clone = CreateClone(template, $"{Name}_{index}");
        Attach(template, highest, clone);
        _items.Add(index, clone);
        foreach (var hookup in _hookups) hookup(clone);
        return clone;
    }

    /// <summary>VB6 <c>Unload arr(index)</c>: only runtime-loaded elements (error 362 otherwise).</summary>
    public void Unload(int index)
    {
        if (!_items.TryGetValue(index, out var element)) throw ControlArrayException.ElementNotFound(index);
        if (_designTime.Contains(index)) throw ControlArrayException.DesignTimeUnload();
        _items.Remove(index);
        Detach(element);
    }

    /// <summary>Elements in ascending index order.</summary>
    public IEnumerator<T> GetEnumerator() => _items.Values.ToList().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Creates an invisible copy of <paramref name="template"/> named <paramref name="name"/>.</summary>
    protected abstract T CreateClone(T template, string name);

    /// <summary>Puts <paramref name="clone"/> in the container of the array (<paramref name="highest"/> = current highest-index element).</summary>
    protected abstract void Attach(T template, T highest, T clone);

    /// <summary>Removes <paramref name="element"/> from its container and releases it.</summary>
    protected abstract void Detach(T element);
}
