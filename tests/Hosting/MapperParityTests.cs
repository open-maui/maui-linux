// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using FluentAssertions;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform.Linux.Hosting;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Hosting;

/// <summary>
/// Every property and command MAUI's own handler maps for a control is mapped by OpenMaui's
/// handler for it, or is listed below as a known gap. The handler mappers are MAUI's contract
/// with a platform: a key nobody maps is a property that silently does nothing on Linux
/// (MediaElement's ShouldLoopPlayback, CollectionView's EmptyViewTemplate were). The test fails
/// when MAUI gains a key OpenMaui does not map, and when a listed gap is mapped (take it off).
/// </summary>
[Collection("LinuxApplication.Current")]
public class MapperParityTests
{
    /// <summary>The open gaps, per control: MAUI maps these and OpenMaui does not yet.</summary>
    private static readonly Dictionary<string, string[]> KnownGaps = new()
    {
        ["ContentPage"] = new[] { "HideSoftInputOnTapped" },
        ["FlyoutPage"] = new[] { "Content", "HideSoftInputOnTapped", "Title" },
        ["NavigationPage"] = new[] { "Content", "HideSoftInputOnTapped", "Title" },
        ["Page"] = new[] { "Content", "HideSoftInputOnTapped" },
        ["Shell"] = new[] { "Content", "HideSoftInputOnTapped" },
        ["TabbedPage"] = new[] { "Content", "HideSoftInputOnTapped", "Title" },
    };

    private static IEnumerable<string> Keys(object? mapper)
    {
        if (mapper == null) return Array.Empty<string>();
        var getKeys = mapper.GetType().GetMethod("GetKeys", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return getKeys?.Invoke(mapper, null) as IEnumerable<string> ?? Array.Empty<string>();
    }

    private static object? StaticMember(Type type, string name)
    {
        for (var t = type; t != null; t = t.BaseType)
        {
            if (t.GetField(name, BindingFlags.Static | BindingFlags.Public) is { } field) return field.GetValue(null);
            if (t.GetProperty(name, BindingFlags.Static | BindingFlags.Public) is { } property) return property.GetValue(null);
        }
        return null;
    }

    private static object? InstanceField(object instance, string name)
    {
        for (var t = instance.GetType(); t != null; t = t.BaseType)
            if (t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic) is { } field)
                return field.GetValue(instance);
        return null;
    }

    /// <summary>Per control: the keys MAUI's handler maps and OpenMaui's does not.</summary>
    private static Dictionary<string, SortedSet<string>> Unmapped()
    {
        // The Controls remaps (RemapForControls) run when a MAUI app is built.
        var mauiFactory = (IMauiHandlersFactory)MauiApp.CreateBuilder().UseMauiApp<Application>().Build()
            .Services.GetService(typeof(IMauiHandlersFactory))!;
        var map = (System.Collections.IDictionary)typeof(MauiHandlerExtensions)
            .GetField("LinuxHandlerMap", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var result = new Dictionary<string, SortedSet<string>>();
        // MAUI controls add their mappings in their static constructors (CheckBox maps Color
        // there): run them all first, or the keys depend on which controls earlier tests made.
        foreach (System.Collections.DictionaryEntry entry in map)
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(((Type)entry.Key).TypeHandle);

        foreach (System.Collections.DictionaryEntry entry in map)
        {
            var control = (Type)entry.Key;
            Type? mauiHandler = null;
            try { mauiHandler = mauiFactory.GetHandlerType(control); } catch (Exception) { }
            if (mauiHandler == null)
                continue;
            var linux = (IElementHandler)((Delegate)entry.Value!).DynamicInvoke()!;
            var ours = Keys(InstanceField(linux, "_mapper")).Concat(Keys(InstanceField(linux, "_commandMapper"))).ToHashSet();
            var theirs = Keys(StaticMember(mauiHandler, "Mapper")).Concat(Keys(StaticMember(mauiHandler, "CommandMapper")));
            var missing = new SortedSet<string>(theirs.Where(k => !ours.Contains(k)));
            if (missing.Count > 0)
                result[control.Name] = missing;
        }
        return result;
    }

    [Fact]
    public void Every_property_MAUI_maps_is_mapped_or_a_known_gap()
    {
        using var host = new HeadlessMauiHost(new ContentPage(), withEngine: false);
        var unmapped = Unmapped();

        var newGaps = unmapped.SelectMany(kv => kv.Value
                .Where(key => !(KnownGaps.TryGetValue(kv.Key, out var known) && known.Contains(key)))
                .Select(key => $"{kv.Key}.{key}"))
            .ToList();
        newGaps.Should().BeEmpty("MAUI maps these and OpenMaui does not: map them in the Linux handler (or, if they cannot apply on Linux, list them with the reason)");

        var closed = KnownGaps.SelectMany(kv => kv.Value
                .Where(key => !(unmapped.TryGetValue(kv.Key, out var open) && open.Contains(key)))
                .Select(key => $"{kv.Key}.{key}"))
            .ToList();
        closed.Should().BeEmpty("these gaps are mapped now: take them off KnownGaps");
    }
}
