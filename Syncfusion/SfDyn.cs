// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Reflection;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// Late-bound access to the internal members of Syncfusion's optional control
/// assemblies (DataGrid, Scheduler, ImageEditor), which the bridge does not
/// reference at compile time. The patches written with it port a Windows
/// method body member by member, so they read like the original. Lookups walk
/// the base types (private fields included) and are cached per type and name;
/// a member that is missing returns null (or is skipped) rather than throwing,
/// and the patch that needed it is expected to fall back to the stock body.
/// </summary>
internal static class SfDyn
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static readonly ConcurrentDictionary<(Type, string), MemberInfo?> s_members = new();
    private static readonly ConcurrentDictionary<(Type, string, int, bool), MethodInfo[]> s_methods = new();

    /// <summary>A type by its full name, from an assembly that may not be loaded.</summary>
    internal static Type? Type(string fullName, string assembly) => System.Type.GetType($"{fullName}, {assembly}");

    /// <summary>The value of a property or field (any visibility, declared on the type or a base).</summary>
    internal static object? Get(object? target, string name)
    {
        if (target == null)
            return null;
        try
        {
            return Member(target.GetType(), name) switch
            {
                PropertyInfo p => p.GetValue(target),
                FieldInfo f => f.GetValue(target),
                _ => null,
            };
        }
        catch (TargetInvocationException)
        {
            return null;
        }
    }

    internal static T? Get<T>(object? target, string name) => Get(target, name) is T value ? value : default;

    internal static bool Is(object? target, string name) => Get(target, name) is true;

    /// <summary>Sets a property or field (any visibility).</summary>
    internal static bool Set(object? target, string name, object? value)
    {
        if (target == null)
            return false;
        switch (Member(target.GetType(), name))
        {
            case PropertyInfo { CanWrite: true } p:
                p.SetValue(target, value);
                return true;
            case FieldInfo f:
                f.SetValue(target, value);
                return true;
            default:
                return false;
        }
    }

    /// <summary>True when the type (or a base) declares a method of that name.</summary>
    internal static bool HasMethod(Type type, string name) => Methods(type, name, -1, isStatic: false).Length > 0;

    /// <summary>Calls an instance method chosen by name and the arguments' types.</summary>
    internal static object? Call(object? target, string name, params object?[] args)
    {
        if (target == null)
            return null;
        var method = Resolve(Methods(target.GetType(), name, args.Length, isStatic: false), args)
            ?? throw new MissingMethodException(target.GetType().FullName, name);
        return Invoke(method, target, args);
    }

    /// <summary>Calls an instance method when it exists; returns false when it does not.</summary>
    internal static bool TryCall(object? target, string name, out object? result, params object?[] args)
    {
        result = null;
        if (target == null)
            return false;
        var method = Resolve(Methods(target.GetType(), name, args.Length, isStatic: false), args);
        if (method == null)
            return false;
        result = Invoke(method, target, args);
        return true;
    }

    /// <summary>Calls a static method chosen by name and the arguments' types.</summary>
    internal static object? CallStatic(Type type, string name, params object?[] args)
    {
        var method = Resolve(Methods(type, name, args.Length, isStatic: true), args)
            ?? throw new MissingMethodException(type.FullName, name);
        return Invoke(method, null, args);
    }

    /// <summary>Calls a static method when it exists; returns false when it does not.</summary>
    internal static bool TryCallStatic(Type type, string name, out object? result, params object?[] args)
    {
        result = null;
        var method = Resolve(Methods(type, name, args.Length, isStatic: true), args);
        if (method == null)
            return false;
        result = Invoke(method, null, args);
        return true;
    }

    /// <summary>Creates an instance through a constructor of any visibility.</summary>
    internal static object? New(Type type, params object?[] args)
        => Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, args, null);

    /// <summary>An enum value of <paramref name="enumType"/> by name.</summary>
    internal static object EnumValue(Type enumType, string name) => Enum.Parse(enumType, name);

    /// <summary>True when the value is the named member of its enum.</summary>
    internal static bool IsEnum(object? value, string name) => value is Enum e && e.ToString() == name;

    internal static MethodInfo? FindMethod(Type type, string name, params Type[] parameters)
    {
        for (var t = type; t != null; t = t.BaseType)
        {
            var m = t.GetMethod(name, Instance | Static, null, parameters, null);
            if (m != null)
                return m;
        }
        return null;
    }

    /// <summary>The IL of a method body (empty when it has none).</summary>
    internal static byte[] Il(MethodBase? method) => method?.GetMethodBody()?.GetILAsByteArray() ?? Array.Empty<byte>();

    /// <summary>True when <paramref name="method"/>'s IL calls a method named <paramref name="name"/>.</summary>
    internal static bool Calls(MethodBase? method, string name)
    {
        var il = Il(method);
        for (int i = 0; i + 4 < il.Length; i++)
        {
            if (il[i] != 0x28 && il[i] != 0x6F && il[i] != 0x73) // call, callvirt, newobj
                continue;
            try
            {
                if (method!.Module.ResolveMethod(BitConverter.ToInt32(il, i + 1))?.Name == name)
                    return true;
            }
            catch (ArgumentException)
            {
                // an operand byte that looked like an opcode
            }
        }
        return false;
    }

    private static object? Invoke(MethodInfo method, object? target, object?[] args)
    {
        try
        {
            return method.Invoke(target, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static MemberInfo? Member(Type type, string name) => s_members.GetOrAdd((type, name), static key =>
    {
        for (var t = key.Item1; t != null; t = t.BaseType)
        {
            var p = t.GetProperty(key.Item2, Instance | Static);
            if (p != null && p.GetIndexParameters().Length == 0)
                return p;
            var f = t.GetField(key.Item2, Instance | Static);
            if (f != null)
                return f;
        }
        // An explicit interface implementation: read it through the interface.
        foreach (var i in key.Item1.GetInterfaces())
        {
            var p = i.GetProperty(key.Item2);
            if (p != null && p.GetIndexParameters().Length == 0)
                return p;
        }
        return null;
    });

    private static MethodInfo[] Methods(Type type, string name, int arity, bool isStatic) =>
        s_methods.GetOrAdd((type, name, arity, isStatic), static key =>
        {
            var list = new List<MethodInfo>();
            for (var t = key.Item1; t != null; t = t.BaseType)
            {
                foreach (var m in t.GetMethods(key.Item4 ? Static : Instance))
                {
                    if (m.Name == key.Item2 && (key.Item3 < 0 || m.GetParameters().Length == key.Item3) && !m.IsGenericMethodDefinition)
                        list.Add(m);
                }
            }
            if (list.Count == 0 && !key.Item4)
            {
                // An explicit interface implementation: call it through the interface.
                foreach (var i in key.Item1.GetInterfaces())
                {
                    foreach (var m in i.GetMethods())
                    {
                        if (m.Name == key.Item2 && (key.Item3 < 0 || m.GetParameters().Length == key.Item3))
                            list.Add(m);
                    }
                }
            }
            return list.ToArray();
        });

    private static MethodInfo? Resolve(MethodInfo[] candidates, object?[] args)
    {
        if (candidates.Length == 1)
            return candidates[0];
        foreach (var m in candidates)
        {
            var ps = m.GetParameters();
            bool ok = true;
            for (int i = 0; i < ps.Length && ok; i++)
            {
                var pt = ps[i].ParameterType;
                if (pt.IsByRef)
                    pt = pt.GetElementType()!;
                ok = args[i] == null
                    ? !pt.IsValueType || Nullable.GetUnderlyingType(pt) != null
                    : pt.IsInstanceOfType(args[i]) || (Nullable.GetUnderlyingType(pt)?.IsInstanceOfType(args[i]) ?? false);
            }
            if (ok)
                return m;
        }
        return null;
    }
}
