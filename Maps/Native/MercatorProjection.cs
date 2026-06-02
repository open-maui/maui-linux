// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Maps.Native;

/// <summary>
/// Web-Mercator (EPSG:3857) projection — the projection every OSM-style raster
/// tile server uses. We only need the slice that converts between geographic
/// coordinates (WGS84 lat/lon in degrees), tile-grid coordinates at a zoom
/// level, and per-tile pixel offsets.
///
/// Latitude is clamped to ±85.05112878° because Mercator diverges at the
/// poles — that's the standard OSM truncation. Longitude wraps modulo 360°.
/// </summary>
public static class MercatorProjection
{
    public const double MaxLatitude = 85.05112878;

    /// <summary>
    /// Continuous tile-grid coordinate (fractional X, Y) at the given zoom.
    /// Integer parts identify the tile; fractional parts give the position
    /// inside that tile in the [0,1] range.
    /// </summary>
    public static (double X, double Y) LatLonToTile(double latitude, double longitude, int zoom)
    {
        latitude = Math.Max(-MaxLatitude, Math.Min(MaxLatitude, latitude));
        // OSM tile servers don't follow a wrap; clamp to the canonical [-180,180].
        longitude = ((longitude + 180.0) % 360.0 + 360.0) % 360.0 - 180.0;

        var n = Math.Pow(2.0, zoom);
        var x = (longitude + 180.0) / 360.0 * n;
        var latRad = latitude * Math.PI / 180.0;
        var y = (1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * n;
        return (x, y);
    }

    /// <summary>
    /// Inverse — convert a fractional tile-grid coordinate back into lat/lon.
    /// </summary>
    public static (double Latitude, double Longitude) TileToLatLon(double tileX, double tileY, int zoom)
    {
        var n = Math.Pow(2.0, zoom);
        var longitude = tileX / n * 360.0 - 180.0;
        var latRad = Math.Atan(Math.Sinh(Math.PI * (1.0 - 2.0 * tileY / n)));
        var latitude = latRad * 180.0 / Math.PI;
        return (latitude, longitude);
    }

    /// <summary>
    /// Pixel offset of a coordinate inside the global Mercator pixel grid at
    /// the given zoom. Each tile is 256×256, so the global grid is
    /// (2^zoom × 256) pixels wide and tall. Useful for placing pin overlays
    /// and computing pan deltas in pixel space.
    /// </summary>
    public static (double PixelX, double PixelY) LatLonToGlobalPixel(double latitude, double longitude, int zoom)
    {
        var (tx, ty) = LatLonToTile(latitude, longitude, zoom);
        return (tx * 256.0, ty * 256.0);
    }

    /// <summary>Inverse of <see cref="LatLonToGlobalPixel"/>.</summary>
    public static (double Latitude, double Longitude) GlobalPixelToLatLon(double pixelX, double pixelY, int zoom)
        => TileToLatLon(pixelX / 256.0, pixelY / 256.0, zoom);
}
