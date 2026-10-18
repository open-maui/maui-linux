// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using FluentAssertions;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Hosting;

/// <summary>
/// No public OpenMaui type in a namespace app code imports next to MAUI's
/// (<c>Microsoft.Maui.Platform</c>, which handler customizations need for the Skia views, and
/// <c>Microsoft.Maui.Platform.Linux.Services</c>) shares its simple name with a public MAUI type
/// in a namespace apps import (<c>Microsoft.Maui</c>, <c>Microsoft.Maui.Controls</c>,
/// <c>Microsoft.Maui.Primitives</c>, <c>Microsoft.Maui.Layouts</c>, ...): a file importing both
/// gets CS0104 (ambiguous reference). The types that did were marked [Obsolete] in 10.0.110.x,
/// each naming its replacement, and are removed in OpenMaui 11.0. The test fails when a new
/// non-obsolete clash appears, when a listed exception no longer clashes (take it off), and when
/// an obsolete clashing type names a replacement that does not exist.
/// </summary>
public class PlatformTypeNameClashTests
{
    /// <summary>The MAUI namespaces app code imports.</summary>
    private static readonly HashSet<string> AppNamespaces = new()
    {
        "Microsoft.Maui",
        "Microsoft.Maui.Accessibility",
        "Microsoft.Maui.Animations",
        "Microsoft.Maui.ApplicationModel",
        "Microsoft.Maui.ApplicationModel.Communication",
        "Microsoft.Maui.ApplicationModel.DataTransfer",
        "Microsoft.Maui.Authentication",
        "Microsoft.Maui.Controls",
        "Microsoft.Maui.Controls.Shapes",
        "Microsoft.Maui.Controls.Xaml",
        "Microsoft.Maui.Converters",
        "Microsoft.Maui.Devices",
        "Microsoft.Maui.Devices.Sensors",
        "Microsoft.Maui.Dispatching",
        "Microsoft.Maui.Graphics",
        "Microsoft.Maui.Handlers",
        "Microsoft.Maui.Hosting",
        "Microsoft.Maui.Layouts",
        "Microsoft.Maui.Media",
        "Microsoft.Maui.Networking",
        "Microsoft.Maui.Primitives",
        "Microsoft.Maui.Storage",
    };

    /// <summary>The OpenMaui namespaces app code imports together with MAUI's.</summary>
    private static readonly HashSet<string> OpenMauiNamespaces = new()
    {
        "Microsoft.Maui.Platform",
        "Microsoft.Maui.Platform.Linux.Services",
    };

    /// <summary>Clashing types that stay usable without a warning in 10.0.110.x, and why.</summary>
    private static readonly Dictionary<string, string> KnownExceptions = new()
    {
        // The parameter type of SkiaView's virtual OnPointer* methods: obsoleting it would warn on
        // every override with no replacement until 11.0 changes the signatures. Code that handles
        // the pointer events can name its base, SkiaPointerEventArgs.
        ["Microsoft.Maui.Platform.PointerEventArgs"] = "virtual OnPointer* parameter type",
    };

    private static readonly string[] OpenMauiAssemblyNames =
    {
        "OpenMaui.Controls.Linux",
        "OpenMaui.Controls.Linux.Maps",
        "OpenMaui.Controls.Linux.MediaElement",
        "OpenMaui.Controls.Linux.Pdf",
        "OpenMaui.Hosting",
    };

    private static IEnumerable<Assembly> MauiAssemblies() => new[]
    {
        typeof(Microsoft.Maui.IView).Assembly,
        typeof(Microsoft.Maui.Controls.Element).Assembly,
        typeof(Microsoft.Maui.Graphics.Color).Assembly,
        typeof(Microsoft.Maui.ApplicationModel.AppInfo).Assembly,
    }.Distinct();

    private static IEnumerable<Assembly> OpenMauiAssemblies()
    {
        yield return typeof(SkiaView).Assembly;
        foreach (var name in OpenMauiAssemblyNames.Skip(1))
            yield return Assembly.Load(new AssemblyName(name));
    }

    private static string SimpleName(Type type)
    {
        var tick = type.Name.IndexOf('`');
        return tick < 0 ? type.Name : type.Name[..tick];
    }

    private static bool IsObsolete(Type type) => type.GetCustomAttribute<ObsoleteAttribute>() != null;

    /// <summary>MAUI's public top-level types in the app namespaces, by simple name.</summary>
    private static Dictionary<string, List<string>> MauiTypesByName() =>
        MauiAssemblies()
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => t.DeclaringType == null && t.Namespace != null && AppNamespaces.Contains(t.Namespace))
            .GroupBy(SimpleName)
            .ToDictionary(g => g.Key, g => g.Select(t => t.FullName!).Distinct().OrderBy(n => n).ToList());

    /// <summary>OpenMaui's public top-level types in the checked namespaces that share a name with a MAUI type.</summary>
    private static List<(Type Type, List<string> Maui)> Clashes()
    {
        var maui = MauiTypesByName();
        return OpenMauiAssemblies()
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => t.DeclaringType == null && t.Namespace != null && OpenMauiNamespaces.Contains(t.Namespace))
            .Where(t => maui.ContainsKey(SimpleName(t)))
            .Select(t => (t, maui[SimpleName(t)]))
            .OrderBy(c => c.t.FullName)
            .ToList();
    }

    [Fact]
    public void No_active_OpenMaui_type_shares_a_name_with_a_MAUI_type()
    {
        var active = Clashes()
            .Where(c => !IsObsolete(c.Type) && !KnownExceptions.ContainsKey(c.Type.FullName!))
            .Select(c => $"{c.Type.FullName} (MAUI: {string.Join(", ", c.Maui)})")
            .ToList();

        active.Should().BeEmpty(
            "app code importing Microsoft.Maui.Platform with MAUI's namespaces gets CS0104 for these; give the type a name of its own (a Skia/Linux prefix) or use MAUI's type");
    }

    [Fact]
    public void Known_exceptions_still_clash_and_are_not_obsolete()
    {
        var clashing = Clashes().Where(c => !IsObsolete(c.Type)).Select(c => c.Type.FullName!).ToHashSet();

        KnownExceptions.Keys.Where(k => !clashing.Contains(k)).Should().BeEmpty(
            "a listed exception that is obsolete, gone or no longer clashing should be taken off the list");
    }

    [Fact]
    public void Obsolete_clashing_types_name_a_replacement_that_exists()
    {
        var known = MauiAssemblies().Concat(OpenMauiAssemblies())
            .SelectMany(a => a.GetExportedTypes())
            .SelectMany(t => new[] { t.FullName!, $"{t.Namespace}.{SimpleName(t)}", SimpleName(t) })
            .ToHashSet();

        var wrong = new List<string>();
        foreach (var (type, _) in Clashes().Where(c => IsObsolete(c.Type)))
        {
            var message = type.GetCustomAttribute<ObsoleteAttribute>()!.Message ?? "";
            if (!message.StartsWith("Use ", StringComparison.Ordinal) || !message.Contains("11.0", StringComparison.Ordinal))
            {
                wrong.Add($"{type.FullName}: '{message}' does not say 'Use X' and the 11.0 removal");
                continue;
            }

            var replacement = message[4..].Split(';', ' ', ',')[0];
            if (!known.Contains(replacement))
                wrong.Add($"{type.FullName}: replacement '{replacement}' is not a public type");
            else if (SimpleName(type) == replacement)
                wrong.Add($"{type.FullName}: replacement '{replacement}' is the clashing name itself");
        }

        wrong.Should().BeEmpty();
    }
}
