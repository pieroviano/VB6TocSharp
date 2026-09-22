using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Vb6ToCSharp.UpgradeHelpers.Internal;

namespace Vb6ToCSharp.UpgradeHelpers.WinForms;

/// <summary>
/// VB6 (ComCtl) TreeView <c>Nodes</c> API. Keys map to <see cref="TreeNode.Name"/>; image indexes are VB6
/// 1-based ImageList indexes (strings are ImageList keys). A 1-based node index follows the VB6 <c>Nodes</c>
/// collection: add order, renumbered on removal (nodes added without the helper follow, in tree order).
/// </summary>
public static class TreeViewHelper
{
    public const int tvwFirst = 0;
    public const int tvwLast = 1;
    public const int tvwNext = 2;
    public const int tvwPrevious = 3;
    public const int tvwChild = 4;

    private static readonly ConditionalWeakTable<TreeView, List<TreeNode>> AddOrder = new();

    /// <summary>VB6 <c>Nodes.Add([relative], [relationship], [key], [text], [image], [selectedImage])</c>.</summary>
    /// <param name="relative">Key, 1-based index or <see cref="TreeNode"/>; null → last root node.</param>
    public static TreeNode AddNode(TreeView tv, object relative = null, int relationship = tvwLast, string key = null,
        string text = null, object image = null, object selectedImage = null)
    {
        if (tv == null) throw new ArgumentNullException(nameof(tv));
        if (!string.IsNullOrEmpty(key) && tv.Nodes.Find(key, true).Length > 0)
            throw new ArgumentException("Key is not unique in collection", nameof(key));
        var node = new TreeNode(text ?? "") { Name = key ?? "" };
        ApplyImage(image, i => node.ImageIndex = i, k => node.ImageKey = k);
        ApplyImage(selectedImage, i => node.SelectedImageIndex = i, k => node.SelectedImageKey = k);

        var rel = relative == null ? null : ResolveNode(tv, relative);
        if (rel == null)
        {
            tv.Nodes.Add(node);
        }
        else
        {
            var siblings = rel.Parent?.Nodes ?? tv.Nodes;
            switch (relationship)
            {
                case tvwFirst: siblings.Insert(0, node); break;
                case tvwNext: siblings.Insert(rel.Index + 1, node); break;
                case tvwPrevious: siblings.Insert(rel.Index, node); break;
                case tvwChild: rel.Nodes.Add(node); break;
                default: siblings.Add(node); break;
            }
        }
        AddOrder.GetOrCreateValue(tv).Add(node);
        return node;
    }

    /// <summary>VB6 <c>Nodes(keyOrIndex)</c>: key string or 1-based index.</summary>
    /// <exception cref="KeyNotFoundException">Unknown key (VB6 35601 "Element not found").</exception>
    /// <exception cref="ArgumentOutOfRangeException">Bad index (VB6 35600 "Index out of bounds").</exception>
    public static TreeNode GetNode(TreeView tv, object keyOrIndex)
    {
        if (tv == null) throw new ArgumentNullException(nameof(tv));
        return ResolveNode(tv, keyOrIndex);
    }

    /// <summary>All nodes in VB6 <c>Nodes</c> collection order.</summary>
    internal static List<TreeNode> OrderedNodes(TreeView tv)
    {
        var order = AddOrder.GetOrCreateValue(tv);
        order.RemoveAll(n => n.TreeView != tv);
        var known = new HashSet<TreeNode>(order);
        var result = new List<TreeNode>(order);
        result.AddRange(Walk(tv.Nodes).Where(n => !known.Contains(n)));
        return result;
    }

    private static TreeNode ResolveNode(TreeView tv, object keyOrIndex)
    {
        switch (keyOrIndex)
        {
            case TreeNode n:
                return n;
            case string key:
                return tv.Nodes.Find(key, true).FirstOrDefault() ?? throw new KeyNotFoundException("Element not found");
            default:
                if (!VbCompare.TryGetIndex(keyOrIndex, out var index))
                    throw new ArgumentException("Invalid key or index", nameof(keyOrIndex));
                var all = OrderedNodes(tv);
                if (index < 1 || index > all.Count) throw new ArgumentOutOfRangeException(nameof(keyOrIndex), "Index out of bounds");
                return all[index - 1];
        }
    }

    private static IEnumerable<TreeNode> Walk(TreeNodeCollection nodes)
    {
        foreach (TreeNode n in nodes)
        {
            yield return n;
            foreach (var c in Walk(n.Nodes)) yield return c;
        }
    }

    internal static void ApplyImage(object image, Action<int> setIndex, Action<string> setKey)
    {
        switch (image)
        {
            case null: return;
            case string k when k.Length > 0: setKey(k); return;
            case string: return;
            default:
                if (VbCompare.TryGetIndex(image, out var i) && i > 0) setIndex(i - 1);
                return;
        }
    }
}
