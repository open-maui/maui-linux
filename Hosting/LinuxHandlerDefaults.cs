// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Hosting;

/// <summary>
/// The handlers OpenMaui registers in <c>UseLinux()</c>, kept as the platform's defaults.
/// On MAUI's own platforms the platform handlers are registered by <c>UseMauiApp</c>, before
/// anything the app or a library adds, so an app's <c>ConfigureMauiHandlers(h =&gt; h.AddHandler&lt;Label, MyHandler&gt;())</c>
/// always wins. <c>UseLinux()</c> can come after the app's registrations, so a default here never
/// replaces a registration the app or a library already made for the same type, and the handler
/// factory (<see cref="MauiHandlerExtensions"/>) can tell "OpenMaui registered this" from
/// "the app overrode it".
/// </summary>
internal static class LinuxHandlerDefaults
{
    private static readonly object s_lock = new();
    private static readonly Dictionary<Type, Type> s_defaults = new();
    private static readonly ConcurrentDictionary<Type, Type?> s_resolved = new();

    /// <summary>
    /// Registers OpenMaui's handler for <paramref name="viewType"/> as the platform default:
    /// recorded, and added unless the app or a library registered its own handler for that type
    /// before <c>UseLinux()</c> ran (MAUI's own portable registration is always replaced).
    /// </summary>
    internal static void Register(
        IMauiHandlersCollection handlers,
        Type viewType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType)
    {
        lock (s_lock)
        {
            s_defaults[viewType] = handlerType;
        }
        s_resolved.Clear();

        var existing = handlers.LastOrDefault(d => d.ServiceType == viewType);
        if (existing != null && IsAppRegistration(existing))
        {
            DiagnosticLog.Debug("LinuxHandlerDefaults",
                $"Keeping the handler registered for {viewType.Name} before UseLinux ({existing.ImplementationType?.Name ?? "factory"}) over OpenMaui's {handlerType.Name}");
            return;
        }
        handlers.AddHandler(viewType, handlerType);
    }

    /// <summary>
    /// The handler OpenMaui registered for <paramref name="elementType"/>, resolved the way MAUI's
    /// handler factory resolves a registration (the type itself, else the most derived registered
    /// base class, else a registered interface); null when UseLinux registered nothing for it.
    /// </summary>
    internal static Type? DefaultFor(Type elementType) => s_resolved.GetOrAdd(elementType, Resolve);

    private static Type? Resolve(Type elementType)
    {
        lock (s_lock)
        {
            if (s_defaults.TryGetValue(elementType, out var exact))
                return exact;
            Type? best = MostSpecific(elementType, s_defaults.Keys.Where(k => !k.IsInterface))
                ?? MostSpecific(elementType, s_defaults.Keys.Where(k => k.IsInterface));
            return best == null ? null : s_defaults[best];
        }
    }

    private static Type? MostSpecific(Type elementType, IEnumerable<Type> keys)
    {
        Type? best = null;
        foreach (var key in keys)
        {
            if (key.IsAssignableFrom(elementType) && (best == null || best.IsAssignableFrom(key)))
                best = key;
        }
        return best;
    }

    /// <summary>
    /// True when <paramref name="descriptor"/> is a registration the app or a library made:
    /// not MAUI's own portable handler and not one of OpenMaui's.
    /// </summary>
    internal static bool IsAppRegistration(ServiceDescriptor descriptor)
    {
        var implementation = descriptor.ImplementationType;
        if (implementation == null)
            return true; // a factory or instance registration is always the app's or a library's
        return !IsMauiHandler(implementation) && implementation.Assembly != typeof(LinuxHandlerDefaults).Assembly;
    }

    /// <summary>True when <paramref name="handlerType"/> is one of MAUI's own (portable) handlers.</summary>
    internal static bool IsMauiHandler(Type handlerType) => s_mauiAssemblies.GetOrAdd(handlerType.Assembly, assembly =>
    {
        var name = assembly.GetName().Name;
        return name != null && (name == "Microsoft.Maui" || name.StartsWith("Microsoft.Maui.", StringComparison.Ordinal));
    });

    private static readonly ConcurrentDictionary<System.Reflection.Assembly, bool> s_mauiAssemblies = new();
}
