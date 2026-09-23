using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Vb6ToCSharp.UpgradeHelpers.Internal;
using Vb6ToCSharp.UpgradeHelpers.Model;
using DrawingColor = System.Drawing.Color;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;

namespace Vb6ToCSharp.UpgradeHelpers.Interop;

/// <summary>
/// Late-bound property access for OCX wrappers (AxHost) and other objects whose design-time properties
/// the converter can only emit by name. Names are matched case-insensitively (VB semantics).
/// </summary>
public static class OcxHelper
{
    /// <summary>Sets public property <paramref name="name"/>, converting <paramref name="value"/> to its type.</summary>
    /// <exception cref="MissingMemberException">No public read/write property with that name.</exception>
    public static void SetProperty(object target, string name, object value)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        var p = Find(target, name);
        if (!p.CanWrite || p.GetSetMethod() == null)
            throw new MissingMemberException(target.GetType().FullName, name);
        p.SetValue(target, ConvertTo(value, p.PropertyType), null);
    }

    /// <summary>Value of public property <paramref name="name"/>.</summary>
    /// <exception cref="MissingMemberException">No public readable property with that name.</exception>
    public static object GetProperty(object target, string name)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        var p = Find(target, name);
        if (!p.CanRead || p.GetGetMethod() == null)
            throw new MissingMemberException(target.GetType().FullName, name);
        return p.GetValue(target, null);
    }

    /// <summary>VB-aware conversion used by <see cref="SetProperty"/>.</summary>
    internal static object ConvertTo(object value, Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (value == null || value is DBNull)
            return underlying != null || !type.IsValueType ? null : Activator.CreateInstance(type);
        type = underlying ?? type;
        if (type.IsInstanceOfType(value)) return value;

        if (type == typeof(bool)) return ToBoolean(value);
        if (type.IsEnum)
        {
            if (value is string es && !VbCompare.TryParseLong(es, out _))
                return Enum.Parse(type, es.Trim(), ignoreCase: true);
            return Enum.ToObject(type, ToLong(value));
        }
        if (type == typeof(DrawingColor)) return Vb6Color.ToColor(ToOle(value));
        if (type == typeof(MediaColor)) return Vb6ToCSharp.UpgradeHelpers.Wpf.Helpers.Vb6Color.ToColor(ToOle(value));
        if (typeof(MediaBrush).IsAssignableFrom(type)) return Vb6ToCSharp.UpgradeHelpers.Wpf.Helpers.Vb6Color.ToBrush(ToOle(value));
        if (type == typeof(string)) return Convert.ToString(value, CultureInfo.InvariantCulture);
        if (value is bool b && IsNumericType(type)) return Convert.ChangeType(b ? -1 : 0, type, CultureInfo.InvariantCulture);
        if (value is string s && IsNumericType(type) && VbCompare.TryParseLong(s, out var l))
            return Convert.ChangeType(l, type, CultureInfo.InvariantCulture);
        return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
    }

    private static PropertyInfo Find(object target, string name)
    {
        if (string.IsNullOrEmpty(name)) throw new ArgumentException("Property name required.", nameof(name));
        var p = target.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(x => x.GetIndexParameters().Length == 0 && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Name == name ? 0 : 1)
            .ThenByDescending(x => Depth(x.DeclaringType))
            .FirstOrDefault();
        return p ?? throw new MissingMemberException(target.GetType().FullName, name);
    }

    private static bool ToBoolean(object value) => value switch
    {
        string s when bool.TryParse(s.Trim(), out var b) => b,
        string s when VbCompare.TryParseLong(s, out var n) => n != 0,
        string s => throw new FormatException($"'{s}' is not a Boolean."),
        _ => Convert.ToDouble(value, CultureInfo.InvariantCulture) != 0,
    };

    private static long ToLong(object value) => value switch
    {
        string s when VbCompare.TryParseLong(s, out var n) => n,
        bool b => b ? -1 : 0,
        _ => Convert.ToInt64(value, CultureInfo.InvariantCulture),
    };

    private static int ToOle(object value)
    {
        var n = ToLong(value);
        return unchecked((int)n);
    }

    private static bool IsNumericType(Type t) => Type.GetTypeCode(t) is >= TypeCode.SByte and <= TypeCode.Decimal;

    private static int Depth(Type t)
    {
        var d = 0;
        for (; t != null; t = t.BaseType) d++;
        return d;
    }
}
