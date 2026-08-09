// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Maps.Views;

namespace Microsoft.Maui.Platform.Linux.Maps.Services;

/// <summary>
/// The default <see cref="TileSource"/>s behind each <see cref="MapLayerType"/>,
/// and the base+overlay stacking for each. All providers here are keyless
/// (no API token required); each exposes a settable <see cref="TileSource.UrlTemplate"/>
/// so apps can redirect a layer at their own tile server.
///
/// <para><b>Providers.</b></para>
/// <list type="bullet">
///   <item><b>Street</b> — OpenStreetMap standard raster (unchanged default).</item>
///   <item><b>Satellite</b> — Esri "World Imagery", a widely used keyless aerial
///     basemap. NOTE the ArcGIS REST axis order is <c>/{z}/{y}/{x}</c>.</item>
///   <item><b>Hybrid</b> — the Satellite base plus Esri "World Boundaries and
///     Places", a transparent labels/boundaries reference overlay.</item>
/// </list>
///
/// <para><b>Usage terms.</b> The Esri layers are governed by Esri's terms of
/// use, not the OSM tile policy; production apps should confirm those terms or
/// point these sources at their own imagery. Attribution for the active layer
/// is rendered in the SkiaMap overlay.</para>
/// </summary>
public static class MapTileLayers
{
    /// <summary>OpenStreetMap standard raster — the Street layer.</summary>
    public static TileSource Street { get; } = new(
        "https://tile.openstreetmap.org/{z}/{x}/{y}.png",
        "© OpenStreetMap contributors",
        maxZoom: 19);

    /// <summary>Esri World Imagery — keyless satellite/aerial base raster (ArcGIS axis order z/y/x).</summary>
    public static TileSource Satellite { get; } = new(
        "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}",
        "Imagery © Esri, Maxar, Earthstar Geographics, and the GIS User Community",
        maxZoom: 19);

    /// <summary>Esri World Boundaries and Places — transparent labels/boundaries overlay for Hybrid.</summary>
    public static TileSource SatelliteReference { get; } = new(
        "https://server.arcgisonline.com/ArcGIS/rest/services/World_Boundaries_and_Places/MapServer/tile/{z}/{y}/{x}",
        "Labels © Esri",
        maxZoom: 19,
        isOverlay: true);

    // Pre-built stacks (bottom-to-top) — cached so the per-frame render path
    // allocates nothing. They hold references to the mutable sources above, so
    // a UrlTemplate redirect is reflected immediately.
    private static readonly TileSource[] s_street = { Street };
    private static readonly TileSource[] s_satellite = { Satellite };
    private static readonly TileSource[] s_hybrid = { Satellite, SatelliteReference };

    /// <summary>The tile source stack (base first, overlays after) for a layer type.</summary>
    public static IReadOnlyList<TileSource> ForLayer(MapLayerType layer) => layer switch
    {
        MapLayerType.Satellite => s_satellite,
        MapLayerType.Hybrid => s_hybrid,
        _ => s_street,
    };
}
