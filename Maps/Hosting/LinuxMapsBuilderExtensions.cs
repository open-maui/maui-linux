// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Hosting;
using Microsoft.Maui.Maps;
using Microsoft.Maui.Maps.Handlers;
using Microsoft.Maui.Platform.Linux.Maps.Handlers;

namespace Microsoft.Maui.Platform.Linux.Maps.Hosting;

/// <summary>
/// Opt-in registration for the Linux Maps backend. Call AFTER
/// <c>UseMauiMaps()</c> and <c>UseLinux()</c>:
///
/// <code>
/// builder
///     .UseMauiApp&lt;App&gt;()
///     .UseMauiMaps()      // upstream MAUI Maps registration
///     .UseLinux()          // base Linux handlers
///     .UseLinuxMaps();     // this — opt-in Linux Maps backend
/// </code>
///
/// Safe to call on Windows / Android / iOS / macCatalyst — no-op there so the
/// same MauiProgram.cs works cross-platform. On Linux, registers
/// <see cref="LinuxMapHandler"/> as the <see cref="IMap"/> handler so
/// <c>&lt;maps:Map&gt;</c> in XAML renders via OpenStreetMap raster tiles in
/// SkiaSharp.
///
/// Runtime requirements (Linux only):
///   - Internet access to <c>tile.openstreetmap.org</c> on first view of each
///     tile. Cached afterward under <c>$XDG_CACHE_HOME/openmaui/osm-tiles</c>.
///   - No native libraries beyond what the main package already requires.
/// </summary>
public static class LinuxMapsBuilderExtensions
{
    public static MauiAppBuilder UseLinuxMaps(this MauiAppBuilder builder)
    {
        if (!OperatingSystem.IsLinux())
            return builder;

        builder.ConfigureMauiHandlers(handlers =>
        {
            handlers.AddHandler<Microsoft.Maui.Controls.Maps.Map, LinuxMapHandler>();
        });

        return builder;
    }
}
