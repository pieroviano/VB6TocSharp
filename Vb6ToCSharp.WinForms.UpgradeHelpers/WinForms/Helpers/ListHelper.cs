using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace Vb6ToCSharp.UpgradeHelpers.WinForms.Helpers;

/// <summary>
/// VB6 ListBox / ComboBox list API (<c>AddItem</c>, <c>ItemData</c>, <c>NewIndex</c>, <c>List</c>, …) for
/// <see cref="ListBox"/>, <see cref="CheckedListBox"/> and <see cref="ComboBox"/>. ItemData is kept per control,
/// aligned with the items on insert/remove.
/// </summary>
public static class ListHelper
{
    private sealed class ListState
    {
        public readonly List<int> ItemData = new();
        public int NewIndex = -1;
    }

    private static readonly ConditionalWeakTable<Control, ListState> States = new();

    /// <summary>VB6 <c>AddItem item, [index]</c>; returns the index of the new item (sorted lists ignore <paramref name="index"/>).</summary>
    public static int AddItem(this Control list, object item, int? index = null)
    {
        var items = Items(list);
        var state = State(list, items);
        int at;
        if (index.HasValue && !IsSorted(list))
        {
            if (index.Value < 0 || index.Value > items.Count)
                throw new ArgumentOutOfRangeException(nameof(index), "Invalid procedure call or argument");
            items.Insert(index.Value, item);
            at = index.Value;
        }
        else
        {
            at = items.Add(item);
        }
        state.ItemData.Insert(Math.Min(at, state.ItemData.Count), 0);
        state.NewIndex = at;
        return at;
    }

    public static void RemoveItem(this Control list, int index)
    {
        var items = Items(list);
        var state = State(list, items);
        if (index < 0 || index >= items.Count)
            throw new ArgumentOutOfRangeException(nameof(index), "Invalid procedure call or argument");
        items.RemoveAt(index);
        state.ItemData.RemoveAt(index);
        if (state.NewIndex == index) state.NewIndex = -1;
        else if (state.NewIndex > index) state.NewIndex--;
    }

    public static void Clear(this Control list)
    {
        var items = Items(list);
        var state = State(list, items);
        items.Clear();
        state.ItemData.Clear();
        state.NewIndex = -1;
    }

    /// <summary>VB6 <c>NewIndex</c>: index of the last added item (-1 if none).</summary>
    public static int GetNewIndex(this Control list) => State(list, Items(list)).NewIndex;

    public static int GetItemData(this Control list, int index)
    {
        var items = Items(list);
        var state = State(list, items);
        Check(index, items.Count);
        return state.ItemData[index];
    }

    public static void SetItemData(this Control list, int index, int value)
    {
        var items = Items(list);
        var state = State(list, items);
        Check(index, items.Count);
        state.ItemData[index] = value;
    }

    /// <summary>VB6 <c>List(index)</c>: item text, "" when out of range (no error, like VB6).</summary>
    public static string GetList(this Control list, int index)
    {
        var items = Items(list);
        if (index < 0 || index >= items.Count) return "";
        return list is ListControl lc ? lc.GetItemText(items[index]) : Convert.ToString(items[index]);
    }

    /// <summary>VB6 <c>List(index) = value</c>; ItemData of the item is kept.</summary>
    public static void SetList(this Control list, int index, string value)
    {
        var items = Items(list);
        Check(index, items.Count);
        items[index] = value;
    }

    public static int GetListCount(this Control list) => Items(list).Count;

    /// <summary>VB6 <c>Selected(index)</c>; for a check-box list (Style = 1) it is the checked state.</summary>
    /// <remarks>
    /// The check-box list is matched through <see cref="object"/>: WinForms derives CheckedListBox from ListBox,
    /// the Gtk stack does not, and a direct pattern would not compile there. A stack where the two are unrelated
    /// simply never reaches the check-box branch, which is right - it cannot pass one here in the first place.
    /// </remarks>
    public static bool GetSelected(this ListBox list, int index)
    {
        Check(index, list.Items.Count);
        return (object)list is CheckedListBox cl ? cl.GetItemChecked(index) : list.GetSelected(index);
    }

    public static void SetSelected(this ListBox list, int index, bool value)
    {
        Check(index, list.Items.Count);
        if ((object)list is CheckedListBox cl) cl.SetItemChecked(index, value);
        else list.SetSelected(index, value);
    }

    private static IList Items(Control list) => list switch
    {
        ListBox lb => lb.Items,
        ComboBox cb => cb.Items,
        null => throw new ArgumentNullException(nameof(list)),
        _ => throw new ArgumentException($"{list.GetType().Name} is not a ListBox or ComboBox.", nameof(list)),
    };

    private static bool IsSorted(Control list) => list switch
    {
        ListBox lb => lb.Sorted,
        ComboBox cb => cb.Sorted,
        _ => false,
    };

    /// <summary>State of <paramref name="list"/>, re-aligned if items were changed without the helper.</summary>
    private static ListState State(Control list, IList items)
    {
        var state = States.GetValue(list, _ => new ListState());
        var data = state.ItemData;
        if (data.Count > items.Count) data.RemoveRange(items.Count, data.Count - items.Count);
        while (data.Count < items.Count) data.Add(0);
        return state;
    }

    private static void Check(int index, int count)
    {
        if (index < 0 || index >= count)
            throw new ArgumentOutOfRangeException(nameof(index), "Invalid procedure call or argument");
    }
}
