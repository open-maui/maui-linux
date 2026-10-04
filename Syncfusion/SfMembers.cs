// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// Cached access to the private members of Syncfusion's controls that the
/// bridges drive in place of the Windows build's platform code (the shared
/// handlers the platform-neutral build ships but never calls). Members are
/// found by name on the type and its bases; one that a Syncfusion release
/// renamed makes the call a no-op and is reported once, never thrown.
/// </summary>
internal static class SfMembers
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static readonly ConcurrentDictionary<(Type, string, int), MethodInfo?> s_methods = new();
    private static readonly ConcurrentDictionary<(Type, string), MemberInfo?> s_values = new();
    private static readonly ConcurrentDictionary<string, byte> s_reported = new();

    /// <summary>True when <paramref name="type"/> is, or derives from, the type with this full name.</summary>
    internal static bool Is(Type? type, string fullName)
    {
        for (; type != null; type = type.BaseType)
        {
            if (type.FullName == fullName)
                return true;
        }
        return false;
    }

    /// <summary>The method <paramref name="name"/> with <paramref name="arity"/> parameters (any when negative).</summary>
    internal static MethodInfo? Method(Type type, string name, int arity = -1) =>
        s_methods.GetOrAdd((type, name, arity), key =>
        {
            for (var t = key.Item1; t != null; t = t.BaseType)
            {
                var found = t.GetMethods(Instance | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == key.Item2 && (key.Item3 < 0 || m.GetParameters().Length == key.Item3));
                if (found != null)
                    return found;
            }
            return null;
        });

    /// <summary>Calls a (possibly private or static) method; null when it is missing or failed.</summary>
    internal static object? Call(object target, string name, params object?[] args)
    {
        var method = Method(target.GetType(), name, args.Length);
        if (method == null)
        {
            ReportMissing(target.GetType(), name);
            return null;
        }
        try
        {
            return method.Invoke(method.IsStatic ? null : target, args);
        }
        catch (TargetInvocationException ex)
        {
            DiagnosticLog.Error("Syncfusion", $"{target.GetType().Name}.{name} failed", ex.InnerException ?? ex);
            return null;
        }
    }

    /// <summary>Reads a field or property (private, internal or public).</summary>
    internal static object? Get(object? target, string name)
    {
        if (target == null)
            return null;
        try
        {
            return Value(target.GetType(), name) switch
            {
                FieldInfo field => field.GetValue(field.IsStatic ? null : target),
                PropertyInfo property => property.GetValue(target),
                _ => null,
            };
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", $"Reading {target.GetType().Name}.{name} failed", ex);
            return null;
        }
    }

    /// <summary>Writes a field or property; false when it is missing.</summary>
    internal static bool Set(object? target, string name, object? value)
    {
        if (target == null)
            return false;
        try
        {
            switch (Value(target.GetType(), name))
            {
                case FieldInfo field:
                    field.SetValue(field.IsStatic ? null : target, value);
                    return true;
                case PropertyInfo { CanWrite: true } property:
                    property.SetValue(target, value);
                    return true;
                default:
                    ReportMissing(target.GetType(), name);
                    return false;
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", $"Writing {target.GetType().Name}.{name} failed", ex);
            return false;
        }
    }

    private static MemberInfo? Value(Type type, string name) =>
        s_values.GetOrAdd((type, name), key =>
        {
            for (var t = key.Item1; t != null; t = t.BaseType)
            {
                if (t.GetField(key.Item2, Instance | BindingFlags.Static) is { } field)
                    return field;
                if (t.GetProperty(key.Item2, Instance | BindingFlags.Static) is { } property && property.GetIndexParameters().Length == 0)
                    return property;
            }
            // An explicitly implemented interface property (an internal interface's member).
            foreach (var face in key.Item1.GetInterfaces())
            {
                if (face.GetProperty(key.Item2, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) is { } property && property.GetIndexParameters().Length == 0)
                    return property;
            }
            return null;
        });

    private static void ReportMissing(Type type, string name)
    {
        if (s_reported.TryAdd(type.FullName + "." + name, 0))
            DiagnosticLog.Warn("Syncfusion", $"This Syncfusion release lacks {type.Name}.{name}; the Linux bridge skips it.");
    }
}
