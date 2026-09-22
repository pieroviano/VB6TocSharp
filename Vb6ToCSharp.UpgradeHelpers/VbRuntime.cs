using System;

namespace Vb6ToCSharp.UpgradeHelpers;

/// <summary>A converted VB6 user-defined type (Type ... End Type): fills its strings and fixed arrays as VB6 does.</summary>
public interface IVbStruct
{
    /// <summary>Sets strings to "" (fixed-length ones to spaces) and allocates fixed-size arrays.</summary>
    void Initialize();
}

/// <summary>VB6 language semantics used by converted code (arrays, fixed-length strings, statements without a .NET counterpart).</summary>
public static class VbRuntime
{
    /// <summary>The value a VB6 variable of type <typeparamref name="T"/> starts with ("" for String, an initialized UDT).</summary>
    public static T DefaultOf<T>()
    {
        if (typeof(T) == typeof(string)) return (T)(object)"";
        if (typeof(T).IsValueType && typeof(IVbStruct).IsAssignableFrom(typeof(T)))
        {
            object boxed = default(T);
            ((IVbStruct)boxed).Initialize();
            return (T)boxed;
        }
        return default;
    }

    /// <summary>A UDT variable as VB6 declares it (Dim r As MyType).</summary>
    public static T NewStruct<T>() where T : struct, IVbStruct => DefaultOf<T>();

    /// <summary>A zero-based array of <paramref name="count"/> VB6-initialized elements (Dim a(count - 1)).</summary>
    public static T[] NewArray<T>(int count)
    {
        if (count < 0) throw new IndexOutOfRangeException("Subscript out of range");
        var r = new T[count];
        if (NeedsInit<T>())
        {
            for (var i = 0; i < count; i++) r[i] = DefaultOf<T>();
        }
        return r;
    }

    /// <summary>A two-dimensional array of VB6-initialized elements.</summary>
    public static T[,] NewArray<T>(int count1, int count2)
    {
        var r = new T[count1, count2];
        if (NeedsInit<T>())
        {
            for (var i = 0; i < count1; i++)
            for (var j = 0; j < count2; j++)
                r[i, j] = DefaultOf<T>();
        }
        return r;
    }

    private static bool NeedsInit<T>() => typeof(T) == typeof(string) || typeof(T).IsValueType && typeof(IVbStruct).IsAssignableFrom(typeof(T));

    /// <summary>VB6 ReDim [Preserve] of a zero-based array: <paramref name="count"/> elements.</summary>
    public static T[] ReDim<T>(T[] array, int count, bool preserve = false)
    {
        var r = NewArray<T>(count);
        if (preserve && array != null) Array.Copy(array, r, Math.Min(array.Length, count));
        return r;
    }

    /// <summary>VB6 ReDim [Preserve] of a two-dimensional array (Preserve keeps the overlapping elements).</summary>
    public static T[,] ReDim<T>(T[,] array, int count1, int count2, bool preserve = false)
    {
        var r = NewArray<T>(count1, count2);
        if (preserve && array != null)
        {
            for (var i = 0; i < Math.Min(count1, array.GetLength(0)); i++)
            for (var j = 0; j < Math.Min(count2, array.GetLength(1)); j++)
                r[i, j] = array[i, j];
        }
        return r;
    }

    /// <summary>LBound of an array with a VB6 lower bound.</summary>
    public static int LBound<T>(VB6Array<T> array, int dimension = 1) => array.LBound;

    /// <summary>UBound of an array with a VB6 lower bound.</summary>
    public static int UBound<T>(VB6Array<T> array, int dimension = 1) => array.UBound;

    /// <summary>The value of a VB6 fixed-length string (String * length): padded with spaces or truncated.</summary>
    public static string FixedLen(string value, int length)
    {
        value = value ?? "";
        return value.Length >= length ? value.Substring(0, length) : value + new string(' ', length - value.Length);
    }

    /// <summary>VB6 Mid statement: overwrites characters of <paramref name="target"/> in place; its length never changes.</summary>
    public static void MidStmt(ref string target, int start, int length, string value)
    {
        target = target ?? "";
        value = value ?? "";
        if (start < 1 || start > target.Length || length < 0) throw new ArgumentException("Invalid procedure call or argument");
        var n = Math.Min(Math.Min(length, value.Length), target.Length - start + 1);
        target = target.Substring(0, start - 1) + value.Substring(0, n) + target.Substring(start - 1 + n);
    }

    /// <summary>VB6 Mid statement without a length: replaces as many characters as <paramref name="value"/> has.</summary>
    public static void MidStmt(ref string target, int start, string value) => MidStmt(ref target, start, int.MaxValue, value);

    /// <summary>VB6 Option Compare Text string comparison (case-insensitive, current culture): -1, 0, 1.</summary>
    public static int TextCompare(object a, object b) =>
        Math.Sign(string.Compare(Convert.ToString(a) ?? "", Convert.ToString(b) ?? "", StringComparison.CurrentCultureIgnoreCase));
}
