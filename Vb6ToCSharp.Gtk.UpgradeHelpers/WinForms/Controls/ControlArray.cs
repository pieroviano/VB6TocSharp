using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Vb6ToCSharp.UpgradeHelpers.Arrays;
using Vb6ToCSharp.UpgradeHelpers.Internal;

namespace Vb6ToCSharp.UpgradeHelpers.WinForms.Controls;

/// <summary>
/// VB6 control array over WinForms components. <see cref="ControlArrayBase{T}.Load"/> clones
/// <see cref="Control"/>s (added to the template's parent, just above it in z-order) and
/// <see cref="ToolStripItem"/>s (menu arrays: inserted in the owner's items after the highest-index element);
/// other components are cloned without a container.
/// </summary>
public class ControlArray<T> : ControlArrayBase<T> where T : Component, new()
{
    private static readonly HashSet<string> ControlExcluded = new(StringComparer.Ordinal)
    {
        "Name", "Parent", "Visible", "Handle", "Controls", "WindowTarget", "Site", "BindingContext", "DataBindings",
        "Capture", "TabIndex", "TopLevel", "TopMost", "WindowState", "AutoScrollPosition",
    };

    private static readonly HashSet<string> ItemExcluded = new(StringComparer.Ordinal)
    {
        "Name", "Owner", "OwnerItem", "Parent", "Visible", "Available", "Site", "MergeAction", "MergeIndex",
    };

    public ControlArray(string name) : base(name)
    {
    }

    protected override T CreateClone(T template, string name)
    {
        var clone = new T();
        var excluded = template is ToolStripItem ? ItemExcluded : ControlExcluded;
        PropertyCloner.Copy(template, clone, excluded, IsCopyable);
        switch (clone)
        {
            case Control c:
                c.Name = name;
                c.Visible = false;
                break;
            case ToolStripItem i:
                i.Name = name;
                i.Visible = false;
                break;
        }
        return clone;
    }

    protected override void Attach(T template, T highest, T clone)
    {
        switch (clone)
        {
            case Control c when template is Control t && t.Parent != null:
                var parent = t.Parent;
                var z = parent.Controls.GetChildIndex(t);
                parent.Controls.Add(c);
                parent.Controls.SetChildIndex(c, z);
                break;
            case ToolStripItem i:
                var anchor = (ToolStripItem)(object)highest;
                var owner = anchor.Owner ?? ((ToolStripItem)(object)template).Owner;
                if (owner == null) break;
                var pos = owner.Items.IndexOf(anchor);
                owner.Items.Insert(pos < 0 ? owner.Items.Count : pos + 1, i);
                break;
        }
    }

    protected override void Detach(T element)
    {
        switch (element)
        {
            case Control c:
                c.Parent?.Controls.Remove(c);
                break;
            case ToolStripItem i:
                i.Owner?.Items.Remove(i);
                break;
        }
        element.Dispose();
    }

    private static bool IsCopyable(PropertyInfo p)
    {
        if (p.Name == "Tag") return true;
        if (p.Name.StartsWith("Selected", StringComparison.Ordinal)
            || (p.Name.StartsWith("Selection", StringComparison.Ordinal) && p.Name != "SelectionMode")) return false;
        var t = p.PropertyType;
        return PropertyCloner.IsSimple(t)
               || t == typeof(Font) || t == typeof(Color) || typeof(Image).IsAssignableFrom(t)
               || t == typeof(Size) || t == typeof(Point) || t == typeof(Padding) || t == typeof(Cursor);
    }
}
