// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Maps;
using Microsoft.Maui.Maps.Handlers;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Maps.Native;
using Microsoft.Maui.Platform.Linux.Maps.Views;
// IMap is ambiguous between Microsoft.Maui.ApplicationModel (the launcher
// service) and Microsoft.Maui.Maps (the control contract). The handler always
// means the Maps one.
using IMap = Microsoft.Maui.Maps.IMap;
using IMapPin = Microsoft.Maui.Maps.IMapPin;
using IMapElement = Microsoft.Maui.Maps.IMapElement;
using IGeoPathMapElement = Microsoft.Maui.Maps.IGeoPathMapElement;
using IFilledMapElement = Microsoft.Maui.Maps.IFilledMapElement;
using ICircleMapElement = Microsoft.Maui.Maps.ICircleMapElement;

namespace Microsoft.Maui.Platform.Linux.Maps.Handlers;

/// <summary>
/// Linux <see cref="IMapHandler"/> that backs <c>Microsoft.Maui.Controls.Maps.Map</c>
/// with a <see cref="SkiaMap"/> render target. Property mappers wire MAUI's
/// IMap (IsScrollEnabled, IsZoomEnabled, Pins, Elements) onto SkiaMap and
/// trigger redraws. <c>VisibleRegion</c> is treated the way the other
/// platforms treat it — as platform-reported OUTPUT: after every pan/zoom/
/// layout the handler recomputes the visible span and pushes it to the Map;
/// <c>MoveToRegion</c> (a handler command) is the input path.
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
        [nameof(IMap.IsScrollEnabled)] = MapIsScrollEnabled,
        [nameof(IMap.IsZoomEnabled)] = MapIsZoomEnabled,
        [nameof(IMap.Pins)] = MapPins,
        [nameof(IMap.Elements)] = MapElements,
        // VisibleRegion is deliberately NOT mapped: it is platform-reported
        // state this handler writes back, and treating it as an input would
        // let the write-back re-enter the mapper. MoveToRegion is the input.
        // MapType / IsShowingUser / IsTrafficEnabled: see class doc — no-op.
    };

    public static CommandMapper<IMap, IMapHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
        [nameof(IMapHandler.UpdateMapElement)] = MapUpdateMapElement,
        // MoveToRegion arrives as a handler command (not a property change) when
        // app code calls Map.MoveToRegion(span). Without this entry, button-
        // driven jumps silently no-op while drag-pan still works.
        ["MoveToRegion"] = MapMoveToRegion,
    };

    public LinuxMapHandler() : base(Mapper, CommandMapper) { }
    public LinuxMapHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper) { }

    // MoveToRegion received before the view has a size — a zoom level can't
    // be computed without a viewport width, so it's applied on first layout.
    private MapSpan? _pendingRegion;

    // Guards the VisibleRegion write-back against re-entering this handler.
    private bool _pushingVisibleRegion;

    // Pins whose PropertyChanged we're subscribed to (mirrors PlatformView.Pins).
    private readonly List<INotifyPropertyChanged> _observedPins = new();

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
        platformView.ViewportChanged += OnViewportChanged;
        if (VirtualView == null) return;

        MapIsScrollEnabled(this, VirtualView);
        MapIsZoomEnabled(this, VirtualView);
        MapPins(this, VirtualView);
        MapElements(this, VirtualView);
    }

    protected override void DisconnectHandler(SkiaMap platformView)
    {
        platformView.ViewportChanged -= OnViewportChanged;
        UnsubscribePins();
        _pendingRegion = null;
        base.DisconnectHandler(platformView);
    }

    // --- Property mappers ---

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
        self.RebindPins(map);
    }

    private void RebindPins(IMap map)
    {
        var platformView = PlatformView!;
        UnsubscribePins();
        platformView.Pins.Clear();
        foreach (var virtualPin in map.Pins)
        {
            var skiaPin = new MapPin
            {
                Latitude = virtualPin.Location.Latitude,
                Longitude = virtualPin.Location.Longitude,
                Label = virtualPin.Label,
                Tag = virtualPin,
            };
            // Bubble SkiaMap's per-pin clicks into MAUI's events with the
            // platform-standard split: the first tap selects the marker and
            // fires MarkerClicked; a tap on the already-selected pin fires
            // InfoWindowClicked (SkiaMap tracks the selection).
            skiaPin.Clicked += (_, _) => virtualPin.SendMarkerClick();
            skiaPin.InfoWindowClicked += (_, _) => virtualPin.SendInfoWindowClick();
            platformView.Pins.Add(skiaPin);

            // Track per-pin mutations (pin.Location = …, pin.Label = …) —
            // the Pins mapper only re-runs on collection changes.
            if (virtualPin is INotifyPropertyChanged observable)
            {
                observable.PropertyChanged += OnPinPropertyChanged;
                _observedPins.Add(observable);
            }
        }
        platformView.Invalidate();
    }

    private void UnsubscribePins()
    {
        foreach (var pin in _observedPins)
            pin.PropertyChanged -= OnPinPropertyChanged;
        _observedPins.Clear();
    }

    private void OnPinPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not IMapPin virtualPin || PlatformView is null) return;
        foreach (var skiaPin in PlatformView.Pins)
        {
            if (!ReferenceEquals(skiaPin.Tag, virtualPin)) continue;
            skiaPin.Latitude = virtualPin.Location.Latitude;
            skiaPin.Longitude = virtualPin.Location.Longitude;
            skiaPin.Label = virtualPin.Label;
            PlatformView.Invalidate();
            return;
        }
    }

    public static void MapElements(IMapHandler handler, IMap map)
    {
        if (handler is not LinuxMapHandler self || self.PlatformView is null) return;
        // Route every IMapElement flavor MAUI Maps ships: circles, filled
        // geo-paths (polygons), and plain geo-paths (polylines). The property
        // mapper re-runs whenever the Elements collection mutates.
        // Order matters: Controls.Maps.Polygon is BOTH IGeoPathMapElement and
        // IFilledMapElement, so the filled check must precede the plain one.
        var platformView = self.PlatformView;
        platformView.Polylines.Clear();
        platformView.Polygons.Clear();
        platformView.Circles.Clear();
        foreach (var element in map.Elements)
        {
            if (element is ICircleMapElement circleElement)
            {
                var circle = new MapCircle
                {
                    Latitude = circleElement.Center.Latitude,
                    Longitude = circleElement.Center.Longitude,
                    RadiusMeters = circleElement.Radius.Meters,
                };
                if (element is IFilledMapElement filledCircle
                    && filledCircle.Fill.ToSKColorOrNull() is { } circleFill)
                    circle.FillColor = circleFill;
                if (element.Stroke.ToSKColorOrNull() is { } circleStroke)
                    circle.StrokeColor = circleStroke;
                if (element.StrokeThickness > 0)
                    circle.StrokeWidth = (float)element.StrokeThickness;
                platformView.Circles.Add(circle);
            }
            else if (element is IGeoPathMapElement filledGeo && element is IFilledMapElement filled)
            {
                var polygon = new MapPolygon();
                foreach (var location in filledGeo)
                    polygon.Points.Add((location.Latitude, location.Longitude));
                if (filled.Fill.ToSKColorOrNull() is { } polygonFill)
                    polygon.FillColor = polygonFill;
                if (element.Stroke.ToSKColorOrNull() is { } polygonStroke)
                    polygon.StrokeColor = polygonStroke;
                if (element.StrokeThickness > 0)
                    polygon.StrokeWidth = (float)element.StrokeThickness;
                platformView.Polygons.Add(polygon);
            }
            else if (element is IGeoPathMapElement geo)
            {
                // IGeoPathMapElement extends IList<Location> directly; iterate it.
                var line = new MapPolyline();
                foreach (var location in geo)
                    line.Points.Add((location.Latitude, location.Longitude));
                if (element.Stroke.ToSKColorOrNull() is { } lineStroke)
                    line.StrokeColor = lineStroke;
                if (element.StrokeThickness > 0)
                    line.StrokeWidth = (float)element.StrokeThickness;
                if (element.StrokeDashPattern is { Length: > 0 } dash)
                    line.DashPattern = dash;
                platformView.Polylines.Add(line);
            }
        }
        platformView.Invalidate();
    }

    public static void MapUpdateMapElement(IMapHandler handler, IMap map, object? arg)
    {
        // Recompute all overlay collections; simplest implementation, fine
        // because real-world map element counts are small.
        MapElements(handler, map);
    }

    /// <summary>
    /// Handle <c>Map.MoveToRegion(MapSpan)</c> command. MAUI invokes this via
    /// the command mapper when app code calls <c>Map.MoveToRegion(span)</c>;
    /// the <paramref name="arg"/> is the <see cref="MapSpan"/>. Translates the
    /// span's center + visible degrees into a center coordinate + integer zoom
    /// for <see cref="SkiaMap"/>. Before first layout the span is stored and
    /// applied once the view has a width.
    /// </summary>
    public static void MapMoveToRegion(IMapHandler handler, IMap map, object? arg)
    {
        if (handler is not LinuxMapHandler self || self.PlatformView is null) return;
        if (arg is not MapSpan span) return;

        if (self.PlatformView.Bounds.Width <= 0)
        {
            self._pendingRegion = span;
            return;
        }
        self.ApplyRegion(span);
    }

    private void ApplyRegion(MapSpan span)
    {
        var platformView = PlatformView!;
        var zoom = ZoomLevelForSpan(span, platformView.Bounds.Width);
        platformView.MoveTo(span.Center.Latitude, span.Center.Longitude, zoom);
    }

    /// <summary>
    /// SkiaMap viewport changed (pan settled, zoom step, MoveTo, or layout).
    /// Applies a deferred MoveToRegion once the view has a size, then reports
    /// the now-visible region back to the Map.
    /// </summary>
    private void OnViewportChanged(object? sender, EventArgs e)
    {
        if (VirtualView == null || PlatformView == null) return;
        if (_pushingVisibleRegion) return;

        if (_pendingRegion != null && PlatformView.Bounds.Width > 0)
        {
            var pending = _pendingRegion;
            _pendingRegion = null;
            // MoveTo re-raises ViewportChanged, which lands in the
            // write-back branch below.
            ApplyRegion(pending);
            return;
        }

        PushVisibleRegion();
    }

    /// <summary>
    /// Compute the MapSpan currently covered by the SkiaMap viewport and
    /// report it via <see cref="IMap.VisibleRegion"/> — the same write-back
    /// the mobile handlers perform after camera movement.
    /// </summary>
    private void PushVisibleRegion()
    {
        var platformView = PlatformView;
        var map = VirtualView;
        if (platformView == null || map == null) return;

        var width = platformView.Bounds.Width;
        var height = platformView.Bounds.Height;
        if (width <= 0 || height <= 0) return;

        var zoom = platformView.ZoomLevel;
        var lat = platformView.CenterLatitude;
        var lon = platformView.CenterLongitude;

        // Longitude span is linear in pixels; latitude span must be projected
        // through Mercator from the top/bottom edge pixels.
        var lonDegrees = Math.Min(360.0, 360.0 * width / (256.0 * (1 << zoom)));
        var (centerPx, centerPy) = MercatorProjection.LatLonToGlobalPixel(lat, lon, zoom);
        var (topLat, _) = MercatorProjection.GlobalPixelToLatLon(centerPx, centerPy - height / 2.0, zoom);
        var (bottomLat, _) = MercatorProjection.GlobalPixelToLatLon(centerPx, centerPy + height / 2.0, zoom);
        var latDegrees = Math.Abs(topLat - bottomLat);

        var current = map.VisibleRegion;
        if (current != null
            && Math.Abs(current.Center.Latitude - lat) < 1e-9
            && Math.Abs(current.Center.Longitude - lon) < 1e-9
            && Math.Abs(current.LatitudeDegrees - latDegrees) < 1e-9
            && Math.Abs(current.LongitudeDegrees - lonDegrees) < 1e-9)
            return;   // unchanged — avoid property-changed churn

        _pushingVisibleRegion = true;
        try
        {
            map.VisibleRegion = new MapSpan(new Location(lat, lon), latDegrees, lonDegrees);
        }
        finally
        {
            _pushingVisibleRegion = false;
        }
    }

    /// <summary>
    /// Convert a <see cref="MapSpan"/> (center + lat/lon span) to the nearest
    /// OSM integer zoom level for a viewport of the given logical pixel width:
    /// the world is 256·2^z px wide, so the zoom where <c>lonDegrees</c> fills
    /// <c>viewWidthPx</c> satisfies 2^z = 360·width / (256·lonDegrees).
    /// </summary>
    private static int ZoomLevelForSpan(MapSpan span, double viewWidthPx)
    {
        var lonRange = Math.Clamp(span.LongitudeDegrees, 1e-6, 360.0);
        var z = Math.Log2(360.0 * viewWidthPx / (256.0 * lonRange));
        return Math.Clamp((int)Math.Round(z), SkiaMap.MinZoom, SkiaMap.MaxZoom);
    }
}
