// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Maps.Views;

/// <summary>
/// Visual style of a <see cref="SkiaMap"/>, mirroring MAUI's
/// <c>Microsoft.Maui.Maps.MapType</c>. The <c>LinuxMapHandler</c> maps
/// <c>Map.MapType</c> onto this.
/// </summary>
public enum MapLayerType
{
    /// <summary>Schematic road/street raster (OpenStreetMap).</summary>
    Street,

    /// <summary>Aerial / satellite imagery raster.</summary>
    Satellite,

    /// <summary>Satellite imagery with a transparent labels/roads reference layer on top.</summary>
    Hybrid,
}
