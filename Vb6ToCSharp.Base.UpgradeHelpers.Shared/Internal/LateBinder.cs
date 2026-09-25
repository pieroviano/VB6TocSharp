using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Vb6ToCSharp.UpgradeHelpers.Internal;

/// <summary>
/// Binds a VB6 late-bound call to a member of a managed object.
/// <para>
/// COM does this itself: an argument the caller leaves out arrives as DISP_E_PARAMNOTFOUND and IDispatch
/// substitutes whatever the type library declares, ByRef parameters included. Managed reflection does not -
/// <see cref="Type.Missing"/> is accepted only for a parameter that carries a default value in metadata, which a
/// <c>ref</c>/<c>out</c> parameter never does and a plain required one usually has not, so the call fails with
/// "Missing parameter does not have a default value". This resolves the overload and fills every omitted
/// argument the way VB6 would, so a managed replacement for a type library (ADO, say) behaves like the original.
/// </para>
/// </summary>
internal static class LateBinder
{
    private const BindingFlags Members = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
                                         | BindingFlags.FlattenHierarchy | BindingFlags.IgnoreCase;

    /// <summary>Calls <paramref name="member"/> of <paramref name="target"/> with VB6's argument semantics.</summary>
    /// <exception cref="MissingMemberException">No method or property of that name can take these arguments.</exception>
    public static object Invoke(object target, string member, object[] args)
    {
        var type = target.GetType();
        var method = Resolve(type.GetMethods(Members).Where(m => Named(m, member)), args)
                     ?? Resolve(Getters(type, member), args);
        if (method == null)
        {
            throw new MissingMemberException(type.FullName, member);
        }
        return method.Invoke(method.IsStatic ? null : target, Arguments(method, args));
    }

    private static bool Named(MemberInfo m, string member) =>
        string.Equals(m.Name, member, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A VB6 call with parentheses reads a parameterized property as readily as it calls a method
    /// (<c>rs.Fields("Text")</c>), so a property of the name counts as a candidate.
    /// </summary>
    private static IEnumerable<MethodInfo> Getters(Type type, string member) =>
        type.GetProperties(Members)
            .Where(p => Named(p, member) && p.CanRead)
            .Select(p => p.GetGetMethod())
            .Where(g => g != null);

    /// <summary>
    /// The overload these arguments fit: the one taking exactly as many parameters wins, then one whose extra
    /// tail is optional, then one whose <c>params</c> array swallows the rest - the order VB6's binder prefers.
    /// </summary>
    private static MethodInfo Resolve(IEnumerable<MethodInfo> candidates, object[] args)
    {
        MethodInfo optional = null;
        MethodInfo variadic = null;
        foreach (var m in candidates)
        {
            var p = m.GetParameters();
            if (p.Length == args.Length && !HasParamArray(p))
            {
                return m;
            }
            if (p.Length > args.Length && p.Skip(args.Length).All(x => x.IsOptional || IsParamArray(x)))
            {
                optional ??= m;
            }
            else if (HasParamArray(p) && args.Length >= p.Length - 1)
            {
                variadic ??= m;
            }
        }
        return optional ?? variadic;
    }

    private static bool IsParamArray(ParameterInfo p) => p.IsDefined(typeof(ParamArrayAttribute), false);

    private static bool HasParamArray(ParameterInfo[] p) => p.Length > 0 && IsParamArray(p[p.Length - 1]);

    /// <summary>The array to invoke with: one slot per parameter, each filled as VB6 fills it.</summary>
    private static object[] Arguments(MethodBase method, object[] args)
    {
        var parameters = method.GetParameters();
        var call = new object[parameters.Length];
        var variadic = HasParamArray(parameters) ? parameters.Length - 1 : -1;
        for (var i = 0; i < parameters.Length; i++)
        {
            if (i == variadic)
            {
                call[i] = ParamArray(parameters[i], args, i);
                break;
            }
            call[i] = i < args.Length && !IsOmitted(args[i])
                ? Coerce(args[i], parameters[i].ParameterType)
                : Omitted(parameters[i]);
        }
        return call;
    }

    /// <summary>The remaining arguments, as the <c>params</c> array the method declares.</summary>
    private static object ParamArray(ParameterInfo parameter, object[] args, int from)
    {
        var element = parameter.ParameterType.GetElementType() ?? typeof(object);
        var rest = Array.CreateInstance(element, Math.Max(0, args.Length - from));
        for (var i = from; i < args.Length; i++)
        {
            rest.SetValue(IsOmitted(args[i]) ? Default(element) : Coerce(args[i], element), i - from);
        }
        return rest;
    }

    /// <summary>What VB6 passes for an argument the caller left out.</summary>
    private static bool IsOmitted(object arg) => ReferenceEquals(arg, Type.Missing);

    /// <summary>
    /// The value an omitted argument becomes. VB6 discards what the callee writes to an omitted ByRef parameter,
    /// so it gets a slot of its own; a value parameter gets the default the metadata declares, if any.
    /// </summary>
    private static object Omitted(ParameterInfo parameter)
    {
        var type = parameter.ParameterType;
        if (type.IsByRef)
        {
            return Default(type.GetElementType());
        }
        // HasDefaultValue is false for [Optional] without a constant, where DefaultValue is DBNull
        return parameter.HasDefaultValue && parameter.DefaultValue is not DBNull
            ? parameter.DefaultValue
            : Default(type);
    }

    private static object Default(Type type) =>
        type != null && type.IsValueType ? Activator.CreateInstance(type) : null;

    /// <summary>
    /// VB6 passes every late-bound argument as a Variant and lets the callee's type decide, so a number reaching
    /// an enum or a narrower numeric parameter is converted rather than rejected, as reflection would reject it.
    /// </summary>
    private static object Coerce(object arg, Type type)
    {
        if (type.IsByRef)
        {
            type = type.GetElementType();
        }
        if (arg == null || type == null || type == typeof(object) || type.IsInstanceOfType(arg))
        {
            return arg;
        }
        var wanted = Nullable.GetUnderlyingType(type) ?? type;
        try
        {
            if (wanted.IsEnum)
            {
                return arg is string name
                    ? Enum.Parse(wanted, name, true)
                    : Enum.ToObject(wanted, Convert.ChangeType(arg, Enum.GetUnderlyingType(wanted)));
            }
            return arg is IConvertible ? Convert.ChangeType(arg, wanted) : arg;
        }
        catch (Exception e) when (e is InvalidCastException or FormatException or OverflowException or ArgumentException)
        {
            return arg; // let the invocation report the mismatch, as VB6's own type error would
        }
    }
}
