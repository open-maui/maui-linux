// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Maps;
using Microsoft.Maui.Platform.Linux.Maps.Handlers;
using Microsoft.Maui.Platform.Linux.Maps.Views;
using Xunit;

namespace Microsoft.Maui.Platform.Tests.Maps;

public class MapTypeRoutingTests
{
    [Theory]
    [InlineData(MapType.Street, MapLayerType.Street)]
    [InlineData(MapType.Satellite, MapLayerType.Satellite)]
    [InlineData(MapType.Hybrid, MapLayerType.Hybrid)]
    public void ToLayerType_MapsEachMauiMapType(MapType input, MapLayerType expected)
    {
        LinuxMapHandler.ToLayerType(input).Should().Be(expected);
    }

    [Fact]
    public void SkiaMap_LayerType_DefaultsToStreet_AndDrivesAttribution()
    {
        var map = new SkiaMap();
        map.LayerType.Should().Be(MapLayerType.Street);
        map.AttributionText.Should().Contain("OpenStreetMap");

        map.LayerType = MapLayerType.Satellite;
        map.AttributionText.Should().Contain("Esri");

        map.LayerType = MapLayerType.Hybrid;
        map.AttributionText.Should().Contain("Esri");
    }

    [Fact]
    public void SkiaMap_AttributionOverride_WinsUntilCleared()
    {
        var map = new SkiaMap { AttributionText = "My tiles" };
        map.AttributionText.Should().Be("My tiles");

        // Layer changes don't clobber an explicit override.
        map.LayerType = MapLayerType.Satellite;
        map.AttributionText.Should().Be("My tiles");

        // Assigning null restores the automatic per-layer credit.
        map.AttributionText = null!;
        map.AttributionText.Should().Contain("Esri");
    }
}
