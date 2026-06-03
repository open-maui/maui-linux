// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Dispatching;
using Microsoft.Maui.Platform.Linux.Maps.Native;
using Microsoft.Maui.Platform.Linux.Maps.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Maps.Views;

/// <summary>
/// Standalone SkiaSharp-rendered map view backed by OpenStreetMap raster
/// tiles. Can be used directly (drop into any SkiaView hierarchy) or behind
/// <c>Microsoft.Maui.Controls.Maps.Map</c> via <c>LinuxMapHandler</c>.
///
/// Behavior:
///   - Pan with left-mouse drag.
///   - Zoom with scroll wheel (toward cursor).
///   - Pin overlays in <see cref="Pins"/>.
///   - Polyline overlays in <see cref="Polylines"/>.
///   - Attribution overlay (lower-right) when <see cref="ShowAttribution"/> is true.
///
/// Tiles are fetched and cached by <see cref="OsmTileService.Default"/> — see
/// that class to override the URL template (e.g. for a self-hosted tile server)
/// or the cache root.
/// </summary>
public class SkiaMap : SkiaView
{
    public const int MinZoom = 0;
    public const int MaxZoom = 19;

    private double _centerLatitude;
    private double _centerLongitude;
    private int _zoom = 2;
    private string _attributionText = "© OpenStreetMap contributors";

    /// <summary>Map center latitude in WGS84 degrees [-85.05, 85.05].</summary>
    public double CenterLatitude
    {
        get => _centerLatitude;
        set { if (Math.Abs(_centerLatitude - value) > 1e-9) { _centerLatitude = value; Invalidate(); } }
    }

    /// <summary>Map center longitude in WGS84 degrees [-180, 180].</summary>
    public double CenterLongitude
    {
        get => _centerLongitude;
        set { if (Math.Abs(_centerLongitude - value) > 1e-9) { _centerLongitude = value; Invalidate(); } }
    }

    /// <summary>OSM zoom level [<see cref="MinZoom"/>, <see cref="MaxZoom"/>].</summary>
    public int ZoomLevel
    {
        get => _zoom;
        set
        {
            var clamped = Math.Clamp(value, MinZoom, MaxZoom);
            if (clamped != _zoom) { _zoom = clamped; Invalidate(); }
        }
    }

    /// <summary>Mutable pin collection. Add/remove freely, then call <see cref="Invalidate"/>.</summary>
    public List<MapPin> Pins { get; } = new();

    /// <summary>Mutable polyline collection.</summary>
    public List<MapPolyline> Polylines { get; } = new();

    public bool ShowAttribution { get; set; } = true;
    public bool AllowPan { get; set; } = true;
    public bool AllowZoom { get; set; } = true;

    /// <summary>
    /// Attribution credit drawn in the lower-right when
    /// <see cref="ShowAttribution"/> is true. Override when using a non-OSM
    /// tile source so the appropriate credit appears.
    /// </summary>
    public string AttributionText
    {
        get => _attributionText;
        set { _attributionText = value ?? string.Empty; Invalidate(); }
    }

    // --- Pan/zoom state ---
    private bool _isPanning;
    private float _panLastX;
    private float _panLastY;

    /// <summary>
    /// Center the map on a coordinate and (optionally) jump to a zoom level
    /// in one go — avoids two separate property assignments triggering two
    /// invalidations.
    /// </summary>
    public void MoveTo(double latitude, double longitude, int? zoomLevel = null)
    {
        _centerLatitude = Math.Clamp(latitude, -MercatorProjection.MaxLatitude, MercatorProjection.MaxLatitude);
        _centerLongitude = ((longitude + 180.0) % 360.0 + 360.0) % 360.0 - 180.0;
        if (zoomLevel.HasValue)
            _zoom = Math.Clamp(zoomLevel.Value, MinZoom, MaxZoom);
        Invalidate();
    }

    // --- Drawing ---

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        // Background fill — visible where tiles haven't arrived yet (and at
        // the world's edges when zoomed all the way out).
        using (var bg = new SKPaint { Color = new SKColor(0xE6, 0xE6, 0xE6), Style = SKPaintStyle.Fill })
            canvas.DrawRect(bounds, bg);

        canvas.Save();
        canvas.ClipRect(bounds);

        DrawTiles(canvas, bounds);
        DrawPolylines(canvas, bounds);
        DrawPins(canvas, bounds);

        canvas.Restore();

        if (ShowAttribution && !string.IsNullOrEmpty(_attributionText))
            DrawAttribution(canvas, bounds);
    }

    private void DrawTiles(SKCanvas canvas, SKRect bounds)
    {
        // Convert the map center to global pixel space, then figure out which
        // tile-grid coordinates cover the viewport.
        var (centerPx, centerPy) = MercatorProjection.LatLonToGlobalPixel(_centerLatitude, _centerLongitude, _zoom);

        var halfW = bounds.Width / 2f;
        var halfH = bounds.Height / 2f;

        var leftPx = centerPx - halfW;
        var topPy = centerPy - halfH;

        var firstTileX = (int)Math.Floor(leftPx / 256.0);
        var firstTileY = (int)Math.Floor(topPy / 256.0);
        var lastTileX = (int)Math.Floor((leftPx + bounds.Width) / 256.0);
        var lastTileY = (int)Math.Floor((topPy + bounds.Height) / 256.0);

        var tilesPerAxis = 1 << _zoom;

        for (int ty = firstTileY; ty <= lastTileY; ty++)
        {
            if (ty < 0 || ty >= tilesPerAxis) continue;   // no wrap vertically
            for (int tx = firstTileX; tx <= lastTileX; tx++)
            {
                // Horizontal wrap — globe is continuous east-west.
                var wrappedTx = ((tx % tilesPerAxis) + tilesPerAxis) % tilesPerAxis;

                var destX = bounds.Left + (float)(tx * 256 - leftPx);
                var destY = bounds.Top + (float)(ty * 256 - topPy);
                var dest = new SKRect(destX, destY, destX + 256, destY + 256);

                var image = TryGetCachedOrSchedule(_zoom, wrappedTx, ty);
                if (image != null)
                    canvas.DrawImage(image, dest);
            }
        }
    }

    private SKImage? TryGetCachedOrSchedule(int zoom, int x, int y)
    {
        var svc = OsmTileService.Default;
        // Quick path: if the tile is already in the in-memory cache, return it
        // synchronously so we don't introduce a frame-late draw cycle just for
        // a cache hit.
        var task = svc.GetTileAsync(zoom, x, y);
        if (task.IsCompletedSuccessfully) return task.Result;

        // Slow path: trigger fetch and schedule a redraw when it lands.
        task.ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully && t.Result != null)
            {
                if (LinuxDispatcher.IsMainThread) Invalidate();
                else LinuxDispatcher.Main?.Dispatch(Invalidate);
            }
        });
        return null;
    }

    private void DrawPolylines(SKCanvas canvas, SKRect bounds)
    {
        if (Polylines.Count == 0) return;

        var (centerPx, centerPy) = MercatorProjection.LatLonToGlobalPixel(_centerLatitude, _centerLongitude, _zoom);
        var halfW = bounds.Width / 2f;
        var halfH = bounds.Height / 2f;
        var originX = centerPx - halfW;
        var originY = centerPy - halfH;

        foreach (var line in Polylines)
        {
            if (line.Points.Count < 2) continue;

            using var paint = new SKPaint
            {
                Color = line.StrokeColor,
                StrokeWidth = line.StrokeWidth,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round,
            };
            if (line.DashPattern is { Length: > 0 } dp)
                paint.PathEffect = SKPathEffect.CreateDash(dp, 0);

            using var path = new SKPath();
            bool started = false;
            foreach (var (lat, lon) in line.Points)
            {
                var (px, py) = MercatorProjection.LatLonToGlobalPixel(lat, lon, _zoom);
                var sx = bounds.Left + (float)(px - originX);
                var sy = bounds.Top + (float)(py - originY);
                if (!started) { path.MoveTo(sx, sy); started = true; }
                else path.LineTo(sx, sy);
            }
            canvas.DrawPath(path, paint);
        }
    }

    private void DrawPins(SKCanvas canvas, SKRect bounds)
    {
        if (Pins.Count == 0) return;

        var (centerPx, centerPy) = MercatorProjection.LatLonToGlobalPixel(_centerLatitude, _centerLongitude, _zoom);
        var halfW = bounds.Width / 2f;
        var halfH = bounds.Height / 2f;
        var originX = centerPx - halfW;
        var originY = centerPy - halfH;

        foreach (var pin in Pins)
        {
            var (px, py) = MercatorProjection.LatLonToGlobalPixel(pin.Latitude, pin.Longitude, _zoom);
            var sx = bounds.Left + (float)(px - originX);
            var sy = bounds.Top + (float)(py - originY);

            DrawTeardropPin(canvas, sx, sy, pin);

            if (!string.IsNullOrEmpty(pin.Label))
            {
                using var labelPaint = new SKPaint
                {
                    Color = SKColors.Black,
                    IsAntialias = true,
                };
                using var labelFont = new SKFont(SKTypeface.Default, 12);
                // Label drawn just below the marker base.
                canvas.DrawText(pin.Label, sx, sy + 8 + 14, SKTextAlign.Center, labelFont, labelPaint);
            }
        }
    }

    private static void DrawTeardropPin(SKCanvas canvas, float tipX, float tipY, MapPin pin)
    {
        // Teardrop: circle on top, downward triangle to the tip.
        var radius = pin.Size / 2f;
        var circleCenterY = tipY - pin.Size * 0.75f;

        using var fill = new SKPaint { Color = pin.FillColor, IsAntialias = true, Style = SKPaintStyle.Fill };
        using var border = new SKPaint { Color = pin.BorderColor, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
        using var path = new SKPath();
        path.MoveTo(tipX, tipY);
        path.LineTo(tipX - radius * 0.7f, circleCenterY + radius * 0.35f);
        path.ArcTo(new SKRect(tipX - radius, circleCenterY - radius, tipX + radius, circleCenterY + radius),
            startAngle: 135, sweepAngle: 270, forceMoveTo: false);
        path.LineTo(tipX, tipY);
        path.Close();
        canvas.DrawPath(path, fill);
        canvas.DrawPath(path, border);

        // White inner dot for contrast.
        using var dot = new SKPaint { Color = pin.BorderColor, IsAntialias = true, Style = SKPaintStyle.Fill };
        canvas.DrawCircle(tipX, circleCenterY, radius * 0.32f, dot);
    }

    private void DrawAttribution(SKCanvas canvas, SKRect bounds)
    {
        using var font = new SKFont(SKTypeface.Default, 11);
        using var textPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        var textWidth = font.MeasureText(_attributionText);
        var pad = 6f;
        var boxRect = new SKRect(
            bounds.Right - textWidth - pad * 2,
            bounds.Bottom - 18 - pad,
            bounds.Right - 1,
            bounds.Bottom - 1);

        using var bgPaint = new SKPaint { Color = new SKColor(0xFF, 0xFF, 0xFF, 0xCC), Style = SKPaintStyle.Fill };
        canvas.DrawRect(boxRect, bgPaint);
        canvas.DrawText(_attributionText, boxRect.Right - pad, boxRect.Bottom - 5, SKTextAlign.Right, font, textPaint);
    }

    // --- Input ---

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled) return;
        if (e.Button != PointerButton.Left) return;

        // Hit-test pins first (frontmost wins). Each pin's hit-box covers the
        // teardrop's circle head — we approximate with the bounding square
        // around the head. If a pin is hit, fire its Clicked event AND skip
        // pan start so dragging-on-pin doesn't accidentally scroll the map.
        if (HitTestPin(e.X, e.Y) is { } hit)
        {
            hit.RaiseClicked();
            return;
        }

        if (!AllowPan) return;
        _isPanning = true;
        _panLastX = e.X;
        _panLastY = e.Y;
    }

    /// <summary>
    /// Convert screen coords to pin-relative test. Returns the topmost pin
    /// whose head bounding-box contains the point, or null. Iterates pins in
    /// reverse so later-added pins (drawn last → on top) win hit-testing.
    /// </summary>
    private MapPin? HitTestPin(float clickX, float clickY)
    {
        if (Pins.Count == 0) return null;

        // DrawPins puts each pin at `bounds.Left + (px - originX)`; the
        // SkiaView render path runs OnDraw without translating the canvas, so
        // that's an absolute on-screen position. Pointer events reach
        // OnPointerPressed in screen coords too, so we compare them directly.
        var bounds = Bounds;
        var (centerPx, centerPy) = MercatorProjection.LatLonToGlobalPixel(_centerLatitude, _centerLongitude, _zoom);
        var halfW = bounds.Width / 2f;
        var halfH = bounds.Height / 2f;
        var originX = centerPx - halfW;
        var originY = centerPy - halfH;

        // The teardrop spans from the tip up to the head's top: the head
        // sits with center at tipY − 0.75*Size and radius Size/2. Hit-area
        // is the head circle plus the connecting triangle down to the tip.
        for (int i = Pins.Count - 1; i >= 0; i--)
        {
            var pin = Pins[i];
            var (px, py) = MercatorProjection.LatLonToGlobalPixel(pin.Latitude, pin.Longitude, _zoom);
            var tipX = (float)bounds.Left + (float)(px - originX);
            var tipY = (float)bounds.Top + (float)(py - originY);

            var radius = pin.Size / 2f;
            var headCx = tipX;
            var headCy = tipY - pin.Size * 0.75f;

            // Head circle…
            var dx = clickX - headCx;
            var dy = clickY - headCy;
            if (dx * dx + dy * dy <= radius * radius) return pin;

            // …or anywhere in the tip extension box (head bottom → tip).
            if (clickX >= headCx - radius && clickX <= headCx + radius
                && clickY >= headCy && clickY <= tipY) return pin;
        }
        return null;
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        if (!_isPanning) return;
        var dx = e.X - _panLastX;
        var dy = e.Y - _panLastY;
        _panLastX = e.X;
        _panLastY = e.Y;
        if (dx == 0 && dy == 0) return;

        // Translate the center by (-dx, -dy) in pixel space (drag map content
        // along with the cursor).
        var (cx, cy) = MercatorProjection.LatLonToGlobalPixel(_centerLatitude, _centerLongitude, _zoom);
        var (lat, lon) = MercatorProjection.GlobalPixelToLatLon(cx - dx, cy - dy, _zoom);
        _centerLatitude = lat;
        _centerLongitude = lon;
        Invalidate();
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        _isPanning = false;
    }

    public override void OnScroll(ScrollEventArgs e)
    {
        if (!IsEnabled || !AllowZoom) return;
        if (e.DeltaY == 0) return;

        var step = e.DeltaY > 0 ? 1 : -1;
        var newZoom = Math.Clamp(_zoom + step, MinZoom, MaxZoom);
        if (newZoom == _zoom) return;

        // Zoom toward the cursor: keep the geographic point under (e.X, e.Y)
        // pinned to that screen coordinate after the zoom change.
        var bounds = Bounds;
        var screenBounds = ScreenBounds;
        var localX = e.X - (float)screenBounds.Left;
        var localY = e.Y - (float)screenBounds.Top;

        // Coordinate under cursor BEFORE zooming.
        var (centerPx, centerPy) = MercatorProjection.LatLonToGlobalPixel(_centerLatitude, _centerLongitude, _zoom);
        var cursorPx = centerPx - (bounds.Width / 2f - localX);
        var cursorPy = centerPy - (bounds.Height / 2f - localY);
        var (cursorLat, cursorLon) = MercatorProjection.GlobalPixelToLatLon(cursorPx, cursorPy, _zoom);

        _zoom = newZoom;

        // Re-center so that cursorLat/cursorLon land on (localX, localY) again
        // at the new zoom.
        var (cursorPxNew, cursorPyNew) = MercatorProjection.LatLonToGlobalPixel(cursorLat, cursorLon, _zoom);
        var newCenterPx = cursorPxNew + (bounds.Width / 2f - localX);
        var newCenterPy = cursorPyNew + (bounds.Height / 2f - localY);
        var (newLat, newLon) = MercatorProjection.GlobalPixelToLatLon(newCenterPx, newCenterPy, _zoom);
        _centerLatitude = Math.Clamp(newLat, -MercatorProjection.MaxLatitude, MercatorProjection.MaxLatitude);
        _centerLongitude = newLon;

        Invalidate();
        e.Handled = true;
    }
}
