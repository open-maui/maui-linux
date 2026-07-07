// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Maps.Views;
using Xunit;

namespace Microsoft.Maui.Platform.Tests.Maps;

public class SkiaMapTests
{
    // Drawing (Skia surface, tile fetcher hitting the network) isn't unit-
    // tested here — those need a real render surface and reachable
    // tile.openstreetmap.org. These cover property mutation behavior.

    [Fact]
    public void ZoomLevel_ClampedToValidRange()
    {
        var map = new SkiaMap { ZoomLevel = 100 };
        map.ZoomLevel.Should().Be(SkiaMap.MaxZoom);

        map.ZoomLevel = -5;
        map.ZoomLevel.Should().Be(SkiaMap.MinZoom);
    }

    [Fact]
    public void MoveTo_NormalizesLongitudeAndClampsLatitude()
    {
        var map = new SkiaMap();
        map.MoveTo(latitude: 200, longitude: 540, zoomLevel: 8);
        map.CenterLatitude.Should().BeLessOrEqualTo(85.05112878);
        map.CenterLongitude.Should().BeInRange(-180, 180);
        map.ZoomLevel.Should().Be(8);
    }

    [Fact]
    public void Pins_AreMutableCollection()
    {
        var map = new SkiaMap();
        map.Pins.Add(new MapPin { Latitude = 1, Longitude = 2, Label = "A" });
        map.Pins.Add(new MapPin { Latitude = 3, Longitude = 4 });
        map.Pins.Should().HaveCount(2);
    }

    [Fact]
    public void Polylines_AreMutableCollection()
    {
        var map = new SkiaMap();
        var line = new MapPolyline();
        line.Points.Add((1, 2));
        line.Points.Add((3, 4));
        map.Polylines.Add(line);
        map.Polylines.Should().HaveCount(1);
        map.Polylines[0].Points.Should().HaveCount(2);
    }

    [Fact]
    public void ViewportChanged_RaisedByMoveTo()
    {
        var map = new SkiaMap();
        int raised = 0;
        map.ViewportChanged += (_, _) => raised++;
        map.MoveTo(10, 20, 5);
        raised.Should().Be(1);
    }

    [Fact]
    public void PinClick_FirstTapFiresClicked_TapOnSelectedFiresInfoWindow()
    {
        var map = new SkiaMap();
        map.Bounds = new Rect(0, 0, 512, 512);
        map.MoveTo(0, 0, 2);

        var pin = new MapPin { Latitude = 0, Longitude = 0 };
        int clicked = 0, infoClicked = 0;
        pin.Clicked += (_, _) => clicked++;
        pin.InfoWindowClicked += (_, _) => infoClicked++;
        map.Pins.Add(pin);

        // Pin tip sits at the viewport center (256, 256); the marker head
        // center is 0.75 * Size above the tip.
        var headY = 256f - pin.Size * 0.75f;

        map.OnPointerPressed(new PointerEventArgs(256f, headY, PointerButton.Left));
        clicked.Should().Be(1);
        infoClicked.Should().Be(0);

        map.OnPointerPressed(new PointerEventArgs(256f, headY, PointerButton.Left));
        clicked.Should().Be(1);
        infoClicked.Should().Be(1);

        // Clicking empty map deselects; the next pin tap is a marker click again.
        map.OnPointerPressed(new PointerEventArgs(10f, 10f, PointerButton.Left));
        map.OnPointerPressed(new PointerEventArgs(256f, headY, PointerButton.Left));
        clicked.Should().Be(2);
        infoClicked.Should().Be(1);
    }
}
