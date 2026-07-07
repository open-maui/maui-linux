// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux;
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
        set { if (Math.Abs(_centerLatitude - value) > 1e-9) { _centerLatitude = value; Invalidate(); RaiseViewportChanged(); } }
    }

    /// <summary>Map center longitude in WGS84 degrees [-180, 180].</summary>
    public double CenterLongitude
    {
        get => _centerLongitude;
        set { if (Math.Abs(_centerLongitude - value) > 1e-9) { _centerLongitude = value; Invalidate(); RaiseViewportChanged(); } }
    }

    /// <summary>OSM zoom level [<see cref="MinZoom"/>, <see cref="MaxZoom"/>].</summary>
    public int ZoomLevel
    {
        get => _zoom;
        set
        {
            var clamped = Math.Clamp(value, MinZoom, MaxZoom);
            if (clamped != _zoom) { _zoom = clamped; Invalidate(); RaiseViewportChanged(); }
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
    private bool _panMoved;
    private float _panLastX;
    private float _panLastY;

    // Pin whose marker was tapped last; a second tap on it fires
    // InfoWindowClicked (see OnPointerPressed).
    private MapPin? _selectedPin;

    // Tiles that already have a redraw continuation attached to their fetch,
    // so each in-flight tile triggers at most one Invalidate no matter how
    // many frames observe it pending. Mutated on the main thread only.
    private readonly HashSet<(int Zoom, int X, int Y)> _pendingTileRedraws = new();

    /// <summary>
    /// Raised whenever the visible viewport changes: pan completes, zoom
    /// changes, <see cref="MoveTo"/> is called, or the view is (re)laid out.
    /// The MAUI handler uses this to keep <c>Map.VisibleRegion</c> current.
    /// </summary>
    public event EventHandler? ViewportChanged;

    private void RaiseViewportChanged() => ViewportChanged?.Invoke(this, EventArgs.Empty);

    protected override void OnBoundsChanged()
    {
        base.OnBoundsChanged();
        // Layout size determines the visible span; listeners (pending
        // MoveToRegion + VisibleRegion write-back) recompute from it.
        RaiseViewportChanged();
    }

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
        RaiseViewportChanged();
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

    // Linear filtering: tiles normally blit 1:1, but the HiDPI path (and any
    // fractional device scale) resamples — nearest-neighbor looks blocky there.
    private static readonly SKSamplingOptions s_tileSampling = new(SKFilterMode.Linear, SKMipmapMode.None);

    private void DrawTiles(SKCanvas canvas, SKRect bounds)
    {
        // Convert the map center to global pixel space, then figure out which
        // tile-grid coordinates cover the viewport.
        var (centerPx, centerPy) = MercatorProjection.LatLonToGlobalPixel(_centerLatitude, _centerLongitude, _zoom);

        // HiDPI: fetch tiles one zoom level deeper and draw each at half
        // logical size, so a 256px tile maps ~1:1 to device pixels instead of
        // being upscaled blurry. The logical pixel grid at _zoom is identical
        // to the (zoom+1) grid with 128-logical-px tiles, so all center/pin
        // math is unaffected.
        var deviceScale = LinuxApplication.Current?.DpiScale ?? 1.0f;
        var useDeepTiles = deviceScale >= 1.5f && _zoom < MaxZoom;
        var fetchZoom = useDeepTiles ? _zoom + 1 : _zoom;
        var tileSize = useDeepTiles ? 128.0 : 256.0;   // logical px per fetched tile

        var halfW = bounds.Width / 2f;
        var halfH = bounds.Height / 2f;

        var leftPx = centerPx - halfW;
        var topPy = centerPy - halfH;

        var firstTileX = (int)Math.Floor(leftPx / tileSize);
        var firstTileY = (int)Math.Floor(topPy / tileSize);
        var lastTileX = (int)Math.Floor((leftPx + bounds.Width) / tileSize);
        var lastTileY = (int)Math.Floor((topPy + bounds.Height) / tileSize);

        var tilesPerAxis = 1 << fetchZoom;

        for (int ty = firstTileY; ty <= lastTileY; ty++)
        {
            if (ty < 0 || ty >= tilesPerAxis) continue;   // no wrap vertically
            for (int tx = firstTileX; tx <= lastTileX; tx++)
            {
                // Horizontal wrap — globe is continuous east-west.
                var wrappedTx = ((tx % tilesPerAxis) + tilesPerAxis) % tilesPerAxis;

                var destX = bounds.Left + (float)(tx * tileSize - leftPx);
                var destY = bounds.Top + (float)(ty * tileSize - topPy);
                var dest = new SKRect(destX, destY, destX + (float)tileSize, destY + (float)tileSize);

                var image = TryGetCachedOrSchedule(fetchZoom, wrappedTx, ty);
                if (image != null)
                    canvas.DrawImage(image, dest, s_tileSampling);
            }
        }
    }

    private SKImage? TryGetCachedOrSchedule(int zoom, int x, int y)
    {
        var svc = OsmTileService.Default;
        // Quick path: if the tile is already in the in-memory cache (or is
        // negatively cached after a failed fetch), the task completes
        // synchronously — no frame-late draw cycle for a cache hit.
        var task = svc.GetTileAsync(zoom, x, y);
        if (task.IsCompletedSuccessfully) return task.Result;

        // Slow path: trigger fetch and schedule a redraw when it lands. Attach
        // at most one continuation per in-flight tile regardless of how many
        // frames observe it pending.
        var key = (zoom, x, y);
        if (!_pendingTileRedraws.Add(key))
            return null;

        task.ContinueWith(t =>
        {
            void Complete()
            {
                _pendingTileRedraws.Remove(key);
                if (t.IsCompletedSuccessfully && t.Result != null)
                    Invalidate();
            }

            // _pendingTileRedraws is main-thread state — marshal there.
            if (LinuxDispatcher.IsMainThread) Complete();
            else if (LinuxDispatcher.Main is { } main) main.Dispatch(Complete);
            else Complete();   // no main loop (tests/standalone)
        });
        return null;
    }

    /// <summary>
    /// Range of wrapped world copies [kMin, kMax] whose horizontal span
    /// overlaps the viewport — the same east-west wrapping the tile layer
    /// applies, so overlays follow the tiles across the antimeridian and at
    /// low zooms where the world repeats.
    /// </summary>
    private (int KMin, int KMax, double WorldWidth) VisibleWorldCopies(double originX, double viewWidth)
    {
        var worldWidth = 256.0 * (1 << _zoom);
        var kMin = (int)Math.Floor(originX / worldWidth);
        var kMax = (int)Math.Floor((originX + viewWidth) / worldWidth);
        return (kMin, kMax, worldWidth);
    }

    private void DrawPolylines(SKCanvas canvas, SKRect bounds)
    {
        if (Polylines.Count == 0) return;

        var (centerPx, centerPy) = MercatorProjection.LatLonToGlobalPixel(_centerLatitude, _centerLongitude, _zoom);
        var halfW = bounds.Width / 2f;
        var halfH = bounds.Height / 2f;
        var originX = centerPx - halfW;
        var originY = centerPy - halfH;
        var (kMin, kMax, worldWidth) = VisibleWorldCopies(originX, bounds.Width);

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

            // Project once, then emit one path per visible world copy. The
            // world offset is folded into the double-precision origin before
            // narrowing to float — world coordinates themselves overflow
            // float precision at high zooms.
            var projected = new List<(double Px, double Py)>(line.Points.Count);
            foreach (var (lat, lon) in line.Points)
                projected.Add(MercatorProjection.LatLonToGlobalPixel(lat, lon, _zoom));

            for (int k = kMin; k <= kMax; k++)
            {
                var copyOriginX = originX - k * worldWidth;
                using var path = new SKPath();
                bool started = false;
                foreach (var (px, py) in projected)
                {
                    var sx = bounds.Left + (float)(px - copyOriginX);
                    var sy = bounds.Top + (float)(py - originY);
                    if (!started) { path.MoveTo(sx, sy); started = true; }
                    else path.LineTo(sx, sy);
                }
                canvas.DrawPath(path, paint);
            }
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
        var (kMin, kMax, worldWidth) = VisibleWorldCopies(originX, bounds.Width);

        using var labelPaint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,
        };
        using var labelFont = new SKFont(SKTypeface.Default, 12);

        foreach (var pin in Pins)
        {
            var (px, py) = MercatorProjection.LatLonToGlobalPixel(pin.Latitude, pin.Longitude, _zoom);
            var sy = bounds.Top + (float)(py - originY);

            // One copy per visible wrapped world, matching the tile layer.
            for (int k = kMin; k <= kMax; k++)
            {
                var sx = bounds.Left + (float)(px + k * worldWidth - originX);
                if (sx < bounds.Left - pin.Size || sx > bounds.Right + pin.Size) continue;

                DrawTeardropPin(canvas, sx, sy, pin);

                if (!string.IsNullOrEmpty(pin.Label))
                {
                    // Label drawn just below the marker base.
                    canvas.DrawText(pin.Label, sx, sy + 8 + 14, SKTextAlign.Center, labelFont, labelPaint);
                }
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
        // around the head. If a pin is hit, fire its click events AND skip
        // pan start so dragging-on-pin doesn't accidentally scroll the map.
        //
        // Click semantics mirror the mobile platforms as closely as a desktop
        // map without an info bubble can: the first tap on a pin selects it
        // and fires Clicked (→ MarkerClicked); tapping the already-selected
        // pin fires InfoWindowClicked. Tapping empty map deselects.
        if (HitTestPin(e.X, e.Y) is { } hit)
        {
            if (ReferenceEquals(_selectedPin, hit))
            {
                hit.RaiseInfoWindowClicked();
            }
            else
            {
                _selectedPin = hit;
                hit.RaiseClicked();
            }
            return;
        }

        _selectedPin = null;

        if (!AllowPan) return;
        _isPanning = true;
        _panMoved = false;
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

        // Pointer events arrive in window coordinates even when the map sits
        // inside a scrolled container, so the pin positions must be computed
        // from ScreenBounds (Bounds minus ancestor scroll offsets) — the same
        // basis OnScroll uses.
        var bounds = ScreenBounds;
        var (centerPx, centerPy) = MercatorProjection.LatLonToGlobalPixel(_centerLatitude, _centerLongitude, _zoom);
        var halfW = bounds.Width / 2f;
        var halfH = bounds.Height / 2f;
        var originX = centerPx - halfW;
        var originY = centerPy - halfH;
        var (kMin, kMax, worldWidth) = VisibleWorldCopies(originX, bounds.Width);

        // The teardrop spans from the tip up to the head's top: the head
        // sits with center at tipY − 0.75*Size and radius Size/2. Hit-area
        // is the head circle plus the connecting triangle down to the tip.
        // Test every wrapped world copy DrawPins renders.
        for (int i = Pins.Count - 1; i >= 0; i--)
        {
            var pin = Pins[i];
            var (px, py) = MercatorProjection.LatLonToGlobalPixel(pin.Latitude, pin.Longitude, _zoom);
            var tipY = (float)bounds.Top + (float)(py - originY);

            var radius = pin.Size / 2f;
            var headCy = tipY - pin.Size * 0.75f;

            for (int k = kMin; k <= kMax; k++)
            {
                var tipX = (float)bounds.Left + (float)(px + k * worldWidth - originX);
                var headCx = tipX;

                // Head circle…
                var dx = clickX - headCx;
                var dy = clickY - headCy;
                if (dx * dx + dy * dy <= radius * radius) return pin;

                // …or anywhere in the tip extension box (head bottom → tip).
                if (clickX >= headCx - radius && clickX <= headCx + radius
                    && clickY >= headCy && clickY <= tipY) return pin;
            }
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
        // along with the cursor). Clamp latitude to the Mercator limit so the
        // center can't drift past the pole and leave the map "stuck" there.
        var (cx, cy) = MercatorProjection.LatLonToGlobalPixel(_centerLatitude, _centerLongitude, _zoom);
        var (lat, lon) = MercatorProjection.GlobalPixelToLatLon(cx - dx, cy - dy, _zoom);
        _centerLatitude = Math.Clamp(lat, -MercatorProjection.MaxLatitude, MercatorProjection.MaxLatitude);
        _centerLongitude = lon;
        _panMoved = true;
        Invalidate();
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        if (_isPanning && _panMoved)
            RaiseViewportChanged();   // pan settled
        _isPanning = false;
        _panMoved = false;
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
        RaiseViewportChanged();
        e.Handled = true;
    }
}
