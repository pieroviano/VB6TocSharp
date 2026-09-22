using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Vb6ToCSharp.UpgradeHelpers.Internal;

namespace Vb6ToCSharp.UpgradeHelpers.WinForms;

/// <summary>
/// VB6 (ComCtl) ListView <c>ListItems</c> / <c>ColumnHeaders</c> API: 1-based indexes, keys map to
/// <c>Name</c>, icons are VB6 1-based ImageList indexes or keys.
/// </summary>
public static class ListViewHelper
{
    public const int lvwColumnLeft = 0;
    public const int lvwColumnRight = 1;
    public const int lvwColumnCenter = 2;

    /// <summary>VB6 <c>ListItems.Add([index], [key], [text], [icon], [smallIcon])</c>.</summary>
    /// <remarks>WinForms items have a single image slot shared by both image lists: <paramref name="icon"/> wins.</remarks>
    public static ListViewItem AddItem(ListView lv, int? index = null, string key = null, string text = null,
        object icon = null, object smallIcon = null)
    {
        if (lv == null) throw new ArgumentNullException(nameof(lv));
        if (!string.IsNullOrEmpty(key) && lv.Items.ContainsKey(key))
            throw new ArgumentException("Key is not unique in collection", nameof(key));
        var item = new ListViewItem(text ?? "") { Name = key ?? "" };
        TreeViewHelper.ApplyImage(icon ?? smallIcon, i => item.ImageIndex = i, k => item.ImageKey = k);
        if (index.HasValue)
        {
            if (index.Value < 1 || index.Value > lv.Items.Count + 1)
                throw new ArgumentOutOfRangeException(nameof(index), "Index out of bounds");
            lv.Items.Insert(index.Value - 1, item);
        }
        else
        {
            lv.Items.Add(item);
        }
        return item;
    }

    /// <summary>VB6 <c>ListItems(keyOrIndex)</c> (1-based).</summary>
    public static ListViewItem GetItem(ListView lv, object keyOrIndex)
    {
        if (lv == null) throw new ArgumentNullException(nameof(lv));
        if (keyOrIndex is string key)
            return lv.Items[key] ?? throw new KeyNotFoundException("Element not found");
        if (!VbCompare.TryGetIndex(keyOrIndex, out var index))
            throw new ArgumentException("Invalid key or index", nameof(keyOrIndex));
        if (index < 1 || index > lv.Items.Count) throw new ArgumentOutOfRangeException(nameof(keyOrIndex), "Index out of bounds");
        return lv.Items[index - 1];
    }

    /// <summary>VB6 <c>ColumnHeaders.Add([index], [key], [text], [width], [alignment])</c>; width in twips.</summary>
    public static ColumnHeader AddColumn(ListView lv, int? index = null, string key = null, string text = null,
        double? widthTwips = null, int alignment = lvwColumnLeft)
    {
        if (lv == null) throw new ArgumentNullException(nameof(lv));
        var column = new ColumnHeader
        {
            Name = key ?? "",
            Text = text ?? "",
            TextAlign = alignment switch
            {
                lvwColumnRight => HorizontalAlignment.Right,
                lvwColumnCenter => HorizontalAlignment.Center,
                _ => HorizontalAlignment.Left,
            },
        };
        if (widthTwips.HasValue) column.Width = Twips.ToPixelsX(widthTwips.Value);
        if (index.HasValue)
        {
            if (index.Value < 1 || index.Value > lv.Columns.Count + 1)
                throw new ArgumentOutOfRangeException(nameof(index), "Index out of bounds");
            lv.Columns.Insert(index.Value - 1, column);
        }
        else
        {
            lv.Columns.Add(column);
        }
        return column;
    }
}
