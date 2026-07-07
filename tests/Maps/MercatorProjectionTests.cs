// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform.Linux.Maps.Native;
using Xunit;

namespace Microsoft.Maui.Platform.Tests.Maps;

public class MercatorProjectionTests
{
    [Fact]
    public void LatLonToTile_EquatorPrimeMeridian_ZoomZero()
    {
        // (0,0) at zoom 0 lands at the center of the single world tile.
        var (x, y) = MercatorProjection.LatLonToTile(0, 0, 0);
        x.Should().BeApproximately(0.5, 1e-6);
        y.Should().BeApproximately(0.5, 1e-6);
    }

    [Fact]
    public void LatLonToTile_TopLeftCorner_ZoomZero()
    {
        // (MaxLatitude, -180) is the top-left of the world tile.
        var (x, y) = MercatorProjection.LatLonToTile(MercatorProjection.MaxLatitude, -180, 0);
        x.Should().BeApproximately(0, 1e-6);
        y.Should().BeApproximately(0, 1e-4);
    }

    [Fact]
    public void LatLonToTile_ClampsLatitudeBeyondMaxToBoundary()
    {
        // Mercator diverges at the poles — OSM truncates at ±85.05112878°.
        var (_, yBeyond) = MercatorProjection.LatLonToTile(90, 0, 1);
        var (_, yAtMax) = MercatorProjection.LatLonToTile(MercatorProjection.MaxLatitude, 0, 1);
        yBeyond.Should().BeApproximately(yAtMax, 1e-6);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(40.7128, -74.0060, 5)]    // New York
    [InlineData(-33.8688, 151.2093, 12)]  // Sydney
    [InlineData(51.5074, -0.1278, 14)]    // London
    public void TileRoundTrip_PreservesCoordinates(double lat, double lon, int zoom)
    {
        var (tx, ty) = MercatorProjection.LatLonToTile(lat, lon, zoom);
        var (lat2, lon2) = MercatorProjection.TileToLatLon(tx, ty, zoom);
        lat2.Should().BeApproximately(lat, 1e-6);
        lon2.Should().BeApproximately(lon, 1e-6);
    }

    [Fact]
    public void GlobalPixel_AndTile_AreIn256thRatio()
    {
        // 256 px per tile by definition.
        var (px, py) = MercatorProjection.LatLonToGlobalPixel(40, -75, 4);
        var (tx, ty) = MercatorProjection.LatLonToTile(40, -75, 4);
        (px / tx).Should().BeApproximately(256.0, 1e-6);
        (py / ty).Should().BeApproximately(256.0, 1e-6);
    }

    [Fact]
    public void ZoomLevels_DoubleTheTileCount()
    {
        // At zoom z there are 2^z × 2^z tiles. Therefore a fixed lat/lon moves
        // by a 2x factor when zoom increases by 1.
        var (x1, _) = MercatorProjection.LatLonToTile(0, 90, 1);
        var (x2, _) = MercatorProjection.LatLonToTile(0, 90, 2);
        (x2 / x1).Should().BeApproximately(2.0, 1e-6);
    }

    [Fact]
    public void MetersPerPixel_EquatorZoomZero_IsCircumferenceOver256()
    {
        MercatorProjection.MetersPerPixel(0, 0)
            .Should().BeApproximately(MercatorProjection.EarthCircumferenceMeters / 256.0, 1e-6);
    }

    [Fact]
    public void MetersPerPixel_ShrinksWithCosLatitude_AndHalvesPerZoom()
    {
        // Mercator stretches toward the poles: at 60°N one pixel covers
        // cos(60°) = half the ground distance it covers at the equator.
        var atEquator = MercatorProjection.MetersPerPixel(0, 10);
        var at60 = MercatorProjection.MetersPerPixel(60, 10);
        (at60 / atEquator).Should().BeApproximately(0.5, 1e-9);

        // And each zoom level doubles the pixel density.
        var nextZoom = MercatorProjection.MetersPerPixel(0, 11);
        (atEquator / nextZoom).Should().BeApproximately(2.0, 1e-9);
    }
}
