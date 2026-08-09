// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform.Linux.Maps.Services;
using Microsoft.Maui.Platform.Linux.Maps.Views;
using Xunit;

namespace Microsoft.Maui.Platform.Tests.Maps;

public class TileSourceTests
{
    [Fact]
    public void BuildUrl_OsmSource_UsesXThenYAxisOrder()
    {
        var osm = new TileSource("https://tile.example/{z}/{x}/{y}.png", "attr");
        osm.BuildUrl(3, 4, 5).Should().Be("https://tile.example/3/4/5.png");
    }

    [Fact]
    public void BuildUrl_ArcGisSource_UsesYThenXAxisOrder()
    {
        // ArcGIS/Esri REST tiles are /{z}/{y}/{x} — the placeholder position in
        // the template is what flips the axis order, no separate flag needed.
        var esri = new TileSource("https://server.example/tile/{z}/{y}/{x}", "attr");
        esri.BuildUrl(3, 4, 5).Should().Be("https://server.example/tile/3/5/4");
    }

    [Fact]
    public void Key_DiffersPerTemplate_AndFollowsRedirects()
    {
        var a = new TileSource("https://a.example/{z}/{x}/{y}.png", "attr");
        var b = new TileSource("https://b.example/{z}/{x}/{y}.png", "attr");
        a.Key.Should().NotBe(b.Key);

        var originalKey = a.Key;
        a.UrlTemplate = "https://a2.example/{z}/{x}/{y}.png";
        a.Key.Should().NotBe(originalKey);
    }

    [Fact]
    public void DefaultLayers_HaveDistinctCacheKeys()
    {
        // Street / satellite / labels must occupy separate cache namespaces so
        // a layer switch can never serve the wrong style from cache.
        var keys = new[]
        {
            MapTileLayers.Street.Key,
            MapTileLayers.Satellite.Key,
            MapTileLayers.SatelliteReference.Key,
        };
        keys.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void ForLayer_Hybrid_StacksSatelliteBaseThenReferenceOverlay()
    {
        MapTileLayers.ForLayer(MapLayerType.Street).Should().ContainSingle()
            .Which.Should().BeSameAs(MapTileLayers.Street);

        MapTileLayers.ForLayer(MapLayerType.Satellite).Should().ContainSingle()
            .Which.Should().BeSameAs(MapTileLayers.Satellite);

        var hybrid = MapTileLayers.ForLayer(MapLayerType.Hybrid);
        hybrid.Should().HaveCount(2);
        hybrid[0].Should().BeSameAs(MapTileLayers.Satellite);   // opaque base first
        hybrid[1].Should().BeSameAs(MapTileLayers.SatelliteReference);
        hybrid[1].IsOverlay.Should().BeTrue();                  // transparent labels on top
    }
}
