// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Maps;
using Microsoft.Maui.Platform.Linux.Maps.Handlers;
using Microsoft.Maui.Platform.Linux.Maps.Hosting;
using Microsoft.Maui.Platform.Linux.Maps.Services;
using Microsoft.Maui.Platform.Linux.Maps.Views;
using Xunit;
using Map = Microsoft.Maui.Controls.Maps.Map;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// Microsoft.Maui.Controls.Maps (10.0.110) through OpenMaui.Controls.Linux.Maps,
/// registered the documented way (<c>UseMauiMaps().UseLinuxMaps()</c>). Pass =
/// the stock Map control's pins, map elements, region and marker events work
/// against OpenMaui's Skia map. Tile downloads are redirected to an unroutable
/// address so the suite never touches the network (tiles are not asserted).
/// </summary>
[Collection(CompatHost.Collection)]
public sealed class MauiMapsCompatTests : IDisposable
{
    private readonly string _streetTemplate = MapTileLayers.Street.UrlTemplate;

    public MauiMapsCompatTests()
    {
        MapTileLayers.Street.UrlTemplate = "http://127.0.0.1:9/{z}/{x}/{y}.png";
    }

    public void Dispose() => MapTileLayers.Street.UrlTemplate = _streetTemplate;

    private static readonly Location Paris = new(48.8566, 2.3522);

    private static CompatHost Host(Map map)
        => new(new ContentPage { Content = map }, b => b.UseMauiMaps().UseLinuxMaps());

    [Fact]
    public void Map_resolves_to_the_linux_handler_and_moves_to_a_region()
    {
        var map = new Map(MapSpan.FromCenterAndRadius(Paris, Distance.FromKilometers(5)));
        using var host = Host(map);
        host.Render();

        map.Handler.Should().BeOfType<LinuxMapHandler>();
        var skia = (SkiaMap)CompatHost.PlatformOf(map);
        skia.CenterLatitude.Should().BeApproximately(Paris.Latitude, 0.01, "the initial MapSpan is applied once the view has a size");
        skia.CenterLongitude.Should().BeApproximately(Paris.Longitude, 0.01);
        int zoomNear = skia.ZoomLevel;

        map.MoveToRegion(MapSpan.FromCenterAndRadius(new Location(40.7128, -74.0060), Distance.FromKilometers(500)));

        skia.CenterLatitude.Should().BeApproximately(40.7128, 0.01);
        skia.CenterLongitude.Should().BeApproximately(-74.0060, 0.01);
        skia.ZoomLevel.Should().BeLessThan(zoomNear, "a wider span zooms out");
        map.VisibleRegion.Should().NotBeNull("the platform reports the visible region back to the Map");
        map.VisibleRegion!.Center.Latitude.Should().BeApproximately(40.7128, 0.05);
    }

    [Fact]
    public void Pins_and_map_elements_are_mapped_and_track_changes()
    {
        var pin = new Pin { Label = "Louvre", Location = Paris };
        var map = new Map(MapSpan.FromCenterAndRadius(Paris, Distance.FromKilometers(5)));
        map.Pins.Add(pin);
        map.MapElements.Add(new Polyline { Geopath = { Paris, new Location(48.86, 2.36) }, StrokeColor = Colors.Red });
        map.MapElements.Add(new Polygon { Geopath = { Paris, new Location(48.86, 2.36), new Location(48.85, 2.36) }, FillColor = Colors.Blue });
        map.MapElements.Add(new Circle { Center = Paris, Radius = Distance.FromMeters(300) });
        using var host = Host(map);
        host.Render();
        var skia = (SkiaMap)CompatHost.PlatformOf(map);

        skia.Pins.Should().ContainSingle().Which.Label.Should().Be("Louvre");
        skia.Polylines.Should().ContainSingle();
        skia.Polygons.Should().ContainSingle();
        skia.Circles.Should().ContainSingle();

        pin.Location = new Location(48.87, 2.30);
        pin.Label = "Moved";
        skia.Pins[0].Latitude.Should().Be(48.87);
        skia.Pins[0].Label.Should().Be("Moved");

        map.Pins.Add(new Pin { Label = "Second", Location = new Location(48.85, 2.34) });
        skia.Pins.Should().HaveCount(2);
    }

    [Fact]
    public void Tapping_a_pin_raises_MarkerClicked_then_InfoWindowClicked()
    {
        var pin = new Pin { Label = "Here", Location = Paris };
        var events = new List<string>();
        pin.MarkerClicked += (_, _) => events.Add("marker");
        pin.InfoWindowClicked += (_, _) => events.Add("info");
        var map = new Map(MapSpan.FromCenterAndRadius(Paris, Distance.FromKilometers(5)));
        map.Pins.Add(pin);
        using var host = Host(map);
        host.Render();

        // The marker's head sits above its tip (the pin location = map centre).
        float x = host.DisplayWindow.Width / 2f, y = host.DisplayWindow.Height / 2f - 20;
        host.DisplayWindow.RaisePointerPressed(x, y);
        host.DisplayWindow.RaisePointerReleased(x, y);
        host.DisplayWindow.RaisePointerPressed(x, y);
        host.DisplayWindow.RaisePointerReleased(x, y);

        events.Should().Equal("marker", "info");
    }

    [Theory]
    [InlineData(MapType.Street, MapLayerType.Street)]
    [InlineData(MapType.Satellite, MapLayerType.Satellite)]
    [InlineData(MapType.Hybrid, MapLayerType.Hybrid)]
    public void MapType_and_interaction_flags_map_to_the_skia_map(MapType type, MapLayerType expected)
    {
        var map = new Map { MapType = type, IsScrollEnabled = false, IsZoomEnabled = false };
        using var host = Host(map);
        var skia = (SkiaMap)CompatHost.PlatformOf(map);

        skia.LayerType.Should().Be(expected);
        skia.AllowPan.Should().BeFalse();
        skia.AllowZoom.Should().BeFalse();
    }
}
