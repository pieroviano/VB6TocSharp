using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Vb6ToCSharp.UpgradeHelpers.Internal;

/// <summary>Copies public read/write properties between two objects of the same type (VB6 <c>Load</c> semantics).</summary>
internal static class PropertyCloner
{
    /// <param name="excluded">Property names never copied.</param>
    /// <param name="isCopyable">Filter on the property (type/name).</param>
    /// <remarks>
    /// Two passes so order-dependent setters (e.g. Value vs Maximum) settle; a property is only set when the value
    /// differs from the target's (avoids setters with side effects such as ImageIndex clearing Image); setters that
    /// throw are skipped.
    /// </remarks>
    internal static void Copy(object source, object target, ISet<string> excluded, Func<PropertyInfo, bool> isCopyable)
    {
        var props = Properties(target.GetType())
            .Where(p => !excluded.Contains(p.Name) && isCopyable(p))
            .ToList();
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var p in props)
            {
                try
                {
                    var value = p.GetValue(source, null);
                    if (Equals(value, p.GetValue(target, null))) continue;
                    p.SetValue(target, value, null);
                }
                catch (Exception)
                {
                    // Property not copyable for this instance: VB6 would not have it either.
                }
            }
        }
    }

    /// <summary>Public, non-indexed, read/write instance properties; most-derived declaration per name.</summary>
    internal static IEnumerable<PropertyInfo> Properties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0
                        && p.GetGetMethod() != null && p.GetSetMethod() != null)
            .GroupBy(p => p.Name)
            .Select(g => g.OrderByDescending(p => Depth(p.DeclaringType)).First());

    internal static bool IsSimple(Type t) =>
        t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime);

    private static int Depth(Type t)
    {
        var d = 0;
        for (; t != null; t = t.BaseType) d++;
        return d;
    }
}
