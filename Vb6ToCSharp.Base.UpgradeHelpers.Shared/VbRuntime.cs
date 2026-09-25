using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Vb6ToCSharp.UpgradeHelpers.Arrays;
using Vb6ToCSharp.UpgradeHelpers.Interop;

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
        if (count1 < 0 || count2 < 0) throw new IndexOutOfRangeException("Subscript out of range"); // as the 1-D form (was an OverflowException)
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

    // ---------------------------------------------------------------- the UI stack (WinForms / WPF package)

    /// <summary>Assembly-qualified bridges of the UI packages, loaded on demand when none registered itself yet.</summary>
    private static readonly string[] UiBridgeTypes =
    {
        "Vb6ToCSharp.UpgradeHelpers.Wpf.Helpers.WpfUiBridge, Vb6ToCSharp.WPF.UpgradeHelpers",
        "Vb6ToCSharp.UpgradeHelpers.WinForms.Helpers.WinFormsUiBridge, Vb6ToCSharp.WinForms.UpgradeHelpers",
    };

    private static readonly List<(int Priority, IVbUiBridge Bridge)> UiBridges = new();
    private static bool uiProbed;

    /// <summary>
    /// Registers the UI stack of a converted program. The UI packages call this from a module initializer;
    /// <paramref name="priority"/> decides who answers when both are loaded (WPF before WinForms).
    /// </summary>
    public static void RegisterUi(IVbUiBridge bridge, int priority = 0)
    {
        if (bridge == null) throw new ArgumentNullException(nameof(bridge));
        lock (UiBridges)
        {
            if (UiBridges.Exists(b => b.Bridge.GetType() == bridge.GetType())) return;
            UiBridges.Add((priority, bridge));
            UiBridges.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
    }

    /// <summary>
    /// Loads the UI package deployed with the program, once: its module initializer registers the bridge and the
    /// OCX conversions. A module initializer runs only once the module is touched, and a program whose only use of
    /// the UI package is DoEvents / Load / Unload (or an OCX property) has not touched it yet.
    /// </summary>
    internal static void EnsureUiPackagesLoaded()
    {
        lock (UiBridges)
        {
            if (uiProbed) return;
            uiProbed = true;
            foreach (var name in UiBridgeTypes)
            {
                // loading the type is not enough: a module initializer runs on the first access to the module
                var bridge = Type.GetType(name, false);
                if (bridge != null) RuntimeHelpers.RunModuleConstructor(bridge.Module.ModuleHandle);
            }
        }
    }

    /// <summary>How many UI stacks are registered (tests).</summary>
    internal static int RegisteredUiCount
    {
        get { lock (UiBridges) return UiBridges.Count; }
    }

    /// <summary>Forgets the registered UI stacks (tests).</summary>
    internal static void ResetUi()
    {
        lock (UiBridges)
        {
            UiBridges.Clear();
            uiProbed = false;
        }
    }

    /// <summary>The registered bridges, highest priority first; loads the UI package when nothing registered yet.</summary>
    private static IVbUiBridge[] Ui()
    {
        lock (UiBridges)
        {
            if (UiBridges.Count == 0) EnsureUiPackagesLoaded();
            return UiBridges.Select(b => b.Bridge).ToArray();
        }
    }

    /// <summary>VB6 DoEvents: processes pending UI messages; returns the number of open forms (0 without a UI).</summary>
    public static int DoEvents()
    {
        foreach (var bridge in Ui())
        {
            if (bridge.IsActive) return bridge.DoEvents();
        }
        return 0;
    }

    /// <summary>VB6 Load form: creates the form without showing it.</summary>
    public static void Load(object form)
    {
        foreach (var bridge in Ui())
        {
            if (bridge.Load(form)) return;
        }
    }

    /// <summary>VB6 Unload form: closes it (its default instance is recreated on next use).</summary>
    public static void Unload(object form)
    {
        foreach (var bridge in Ui())
        {
            if (bridge.Unload(form)) return;
        }
    }

    /// <summary>
    /// The placeholder VB6 passes for an argument left out of a call (<c>cmd.Execute , , adExecuteNoRecords</c>).
    /// </summary>
    public static object Missing => Type.Missing;

    /// <summary>
    /// VB6 call with arguments left out. C# cannot omit an argument in the middle of a call, and cannot even write
    /// one for an <c>out</c> parameter of a COM method without knowing the signature, so the call is made late bound:
    /// <see cref="Missing"/> arguments reach the object as "not supplied", exactly as in VB6.
    /// </summary>
    /// <remarks>
    /// A COM object resolves an omitted argument itself, through IDispatch; a managed object - a type library
    /// replaced by a managed package, say - has to be bound by hand, because reflection accepts
    /// <see cref="Type.Missing"/> only for a parameter that declares a default value.
    /// </remarks>
    public static object ComInvoke(object target, string member, params object[] args)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        var a = args ?? new object[0];
        for (var i = 0; i < a.Length; i++)
        {
            if (a[i] is IVbLibraryConstant c) a[i] = c.Value; // a type library constant is a Long to COM
        }
        var type = target.GetType();
        if (!type.IsCOMObject)
        {
            return Internal.LateBinder.Invoke(target, member, a);
        }
        // OptionalParamBinding lets a Missing argument take the parameter's default, as VB6 does
        const System.Reflection.BindingFlags how = System.Reflection.BindingFlags.InvokeMethod
                                                   | System.Reflection.BindingFlags.OptionalParamBinding;
        return type.InvokeMember(member, how, null, target, a);
    }

    /// <summary>VB6 Option Compare Text string comparison (case-insensitive, current culture): -1, 0, 1.</summary>
    public static int TextCompare(object a, object b) =>
        Math.Sign(string.Compare(Convert.ToString(a) ?? "", Convert.ToString(b) ?? "", StringComparison.CurrentCultureIgnoreCase));
}
