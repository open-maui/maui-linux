// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Maps;
using Microsoft.Maui.Maps.Handlers;
using Microsoft.Maui.Platform.Linux.Maps.Views;
// IMap is ambiguous between Microsoft.Maui.ApplicationModel (the launcher
// service) and Microsoft.Maui.Maps (the control contract). The handler always
// means the Maps one.
using IMap = Microsoft.Maui.Maps.IMap;
using IMapElement = Microsoft.Maui.Maps.IMapElement;
using IGeoPathMapElement = Microsoft.Maui.Maps.IGeoPathMapElement;

namespace Microsoft.Maui.Platform.Linux.Maps.Handlers;

/// <summary>
/// Linux <see cref="IMapHandler"/> that backs <c>Microsoft.Maui.Controls.Maps.Map</c>
/// with a <see cref="SkiaMap"/> render target. Property mappers wire MAUI's
/// IMap (VisibleRegion, MapType, IsScrollEnabled, IsZoomEnabled, Pins,
/// Elements) onto SkiaMap and trigger redraws.
///
/// Three MAUI-Map features have no clean Linux backend yet and are documented
/// as no-ops:
///   - <c>MapType</c> (Street / Satellite / Hybrid) — OSM raster has only one
///     style; consumers can swap tile URL via <c>OsmTileService.Default.UrlTemplate</c>.
///   - <c>IsShowingUser</c> — would need GeolocationService wiring + a "blue dot"
///     overlay; deferred.
///   - <c>IsTrafficEnabled</c> — needs a traffic data source we don't ship.
/// </summary>
public partial class LinuxMapHandler : ViewHandler<IMap, SkiaMap>, IMapHandler
{
    public static IPropertyMapper<IMap, IMapHandler> Mapper = new PropertyMapper<IMap, IMapHandler>(ViewHandler.ViewMapper)
    {
        [nameof(IMap.VisibleRegion)] = MapVisibleRegion,
        [nameof(IMap.IsScrollEnabled)] = MapIsScrollEnabled,
        [nameof(IMap.IsZoomEnabled)] = MapIsZoomEnabled,
        [nameof(IMap.Pins)] = MapPins,
        [nameof(IMap.Elements)] = MapElements,
        // MapType / IsShowingUser / IsTrafficEnabled: see class doc — no-op.
    };

    public static CommandMapper<IMap, IMapHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
        [nameof(IMapHandler.UpdateMapElement)] = MapUpdateMapElement,
    };

    public LinuxMapHandler() : base(Mapper, CommandMapper) { }
    public LinuxMapHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper) { }

    protected override SkiaMap CreatePlatformView() => new SkiaMap();

    /// <summary>
    /// <see cref="IMapHandler.UpdateMapElement"/> is on the interface itself
    /// (not just the command mapper). MAUI calls it when an individual
    /// <see cref="IMapElement"/> mutates — we just re-bind the full element
    /// collection because real-world counts are small.
    /// </summary>
    void IMapHandler.UpdateMapElement(IMapElement element)
    {
        if (VirtualView == null) return;
        MapElements(this, VirtualView);
    }

    protected override void ConnectHandler(SkiaMap platformView)
    {
        base.ConnectHandler(platformView);
        if (VirtualView == null) return;

        MapVisibleRegion(this, VirtualView);
        MapIsScrollEnabled(this, VirtualView);
        MapIsZoomEnabled(this, VirtualView);
        MapPins(this, VirtualView);
        MapElements(this, VirtualView);
    }

    // --- Property mappers ---

    public static void MapVisibleRegion(IMapHandler handler, IMap map)
    {
        if (handler is not LinuxMapHandler self || self.PlatformView is null) return;
        var region = map.VisibleRegion;
        if (region == null) return;
        var center = region.Center;
        var zoom = ZoomLevelFromMapSpan(region);
        self.PlatformView.MoveTo(center.Latitude, center.Longitude, zoom);
    }

    public static void MapIsScrollEnabled(IMapHandler handler, IMap map)
    {
        if (handler is not LinuxMapHandler self || self.PlatformView is null) return;
        self.PlatformView.AllowPan = map.IsScrollEnabled;
    }

    public static void MapIsZoomEnabled(IMapHandler handler, IMap map)
    {
        if (handler is not LinuxMapHandler self || self.PlatformView is null) return;
        self.PlatformView.AllowZoom = map.IsZoomEnabled;
    }

    public static void MapPins(IMapHandler handler, IMap map)
    {
        if (handler is not LinuxMapHandler self || self.PlatformView is null) return;
        self.PlatformView.Pins.Clear();
        foreach (var pin in map.Pins)
        {
            self.PlatformView.Pins.Add(new MapPin
            {
                Latitude = pin.Location.Latitude,
                Longitude = pin.Location.Longitude,
                Label = pin.Label,
            });
        }
        self.PlatformView.Invalidate();
    }

    public static void MapElements(IMapHandler handler, IMap map)
    {
        if (handler is not LinuxMapHandler self || self.PlatformView is null) return;
        // For v1 we render IGeoPathMapElement entries as polylines. Circles /
        // polygons / filled shapes can be added as needed; the property mapper
        // re-runs whenever the Elements collection mutates.
        self.PlatformView.Polylines.Clear();
        foreach (var element in map.Elements)
        {
            // IGeoPathMapElement extends IList<Location> directly; iterate it.
            if (element is IGeoPathMapElement geo)
            {
                var line = new MapPolyline();
                foreach (var location in geo)
                    line.Points.Add((location.Latitude, location.Longitude));
                self.PlatformView.Polylines.Add(line);
            }
        }
        self.PlatformView.Invalidate();
    }

    public static void MapUpdateMapElement(IMapHandler handler, IMap map, object? arg)
    {
        // Recompute the polyline collection; simplest implementation, fine
        // because real-world map element counts are small.
        MapElements(handler, map);
    }

    /// <summary>
    /// Convert a <see cref="MapSpan"/> (center + lat/lon span) to the nearest
    /// OSM integer zoom level. Uses the standard formula: zoom is determined
    /// by how many tile widths the visible longitude range spans.
    /// </summary>
    private static int ZoomLevelFromMapSpan(MapSpan span)
    {
        // longitudeDegrees per tile at zoom z = 360 / 2^z
        // → z = log2(360 / longitudeDegrees)
        var lonRange = Math.Max(span.LongitudeDegrees, 1e-6);
        var z = Math.Log2(360.0 / lonRange);
        return Math.Clamp((int)Math.Round(z), SkiaMap.MinZoom, SkiaMap.MaxZoom);
    }
}
