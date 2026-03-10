// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace Microsoft.Maui.Platform;

/// <summary>
/// SkiaView that hosts an SKCanvasView by providing the native rendering surface.
/// When OnDraw is called, creates an offscreen SKSurface and fires the SKCanvasView's
/// PaintSurface event, then composites the result onto our canvas.
/// This enables all SkiaSharp-based MAUI controls (LiveCharts, custom drawings, etc.)
/// to work on Linux without platform-specific native hosts.
/// </summary>
public class SkiaSKCanvasView : SkiaView
{
    private ISKCanvasView? _canvasView;
    private SKSurface? _cachedSurface;
    private SKImageInfo _cachedInfo;
    private bool _needsPaint = true;

    /// <summary>
    /// Sets the ISKCanvasView virtual view that will receive PaintSurface callbacks.
    /// </summary>
    public ISKCanvasView? CanvasView
    {
        get => _canvasView;
        set
        {
            _canvasView = value;
            InvalidateCachedSurface();
            Invalidate();
        }
    }

    /// <summary>
    /// Called by the handler when InvalidateSurface() is requested.
    /// </summary>
    public void InvalidateCanvas()
    {
        _needsPaint = true;
        Invalidate();
    }

    private void InvalidateCachedSurface()
    {
        _cachedSurface?.Dispose();
        _cachedSurface = null;
        _needsPaint = true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var w = WidthRequest >= 0 ? WidthRequest : availableSize.Width;
        var h = HeightRequest >= 0 ? HeightRequest : availableSize.Height;

        // Don't return infinity
        if (double.IsInfinity(w) || double.IsNaN(w)) w = 300;
        if (double.IsInfinity(h) || double.IsNaN(h)) h = 200;

        return new Size(w, h);
    }

    private int _drawCount;

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        if (_canvasView == null)
        {
            return;
        }

        _drawCount++;

        int width = Math.Max(1, (int)bounds.Width);
        int height = Math.Max(1, (int)bounds.Height);

        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);

        // Recreate surface if dimensions changed
        if (_cachedSurface == null || _cachedInfo.Width != width || _cachedInfo.Height != height)
        {
            _cachedSurface?.Dispose();
            _cachedSurface = SKSurface.Create(info);
            _cachedInfo = info;
            _needsPaint = true;

            if (_cachedSurface == null)
            {
                DiagnosticLog.Error("SkiaSKCanvasView", $"Failed to create SKSurface ({width}x{height})");
                return;
            }

            // Notify the canvas view of the size change
            _canvasView.OnCanvasSizeChanged(new SKSizeI(width, height));
        }

        // Only repaint the offscreen surface when explicitly invalidated.
        // This prevents partial dirty-region redraws from advancing animation
        // state and creating visual discontinuities.
        if (_needsPaint)
        {
            _needsPaint = false;

            // Clear the offscreen surface
            _cachedSurface.Canvas.Clear(SKColors.Transparent);

            // Fire the PaintSurface event on the SKCanvasView
            try
            {
                var args = new SKPaintSurfaceEventArgs(_cachedSurface, info);
                _canvasView.OnPaintSurface(args);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("SkiaSKCanvasView", $"PaintSurface failed: {ex.Message}");
                return;
            }

            _cachedSurface.Canvas.Flush();
        }

        if (_drawCount <= 30 && (_drawCount <= 3 || _drawCount % 5 == 0))
        {
            using var snap = _cachedSurface.Snapshot();
            using var px = snap.PeekPixels();
            if (px != null)
            {
                var span = px.GetPixelSpan();
                int nonT = 0;
                // Scan the entire canvas for non-transparent pixels
                for (int i = 3; i < span.Length; i += 4)
                    if (span[i] > 0) nonT++;
                int totalPx = span.Length / 4;

                // Check MotionCanvas state via reflection
                string coreInfo = "n/a";
                try
                {
                    if (_canvasView is Microsoft.Maui.Controls.VisualElement ve)
                    {
                        // SKCanvasView's parent should be MotionCanvas
                        var parent = (ve as Microsoft.Maui.Controls.Element)?.Parent;
                        coreInfo = $"parent={parent?.GetType().Name ?? "null"}";

                        if (parent != null)
                        {
                            // Check MotionCanvas.CanvasCore paint task count
                            var canvasCoreP = parent.GetType().GetProperty("CanvasCore");
                            var canvasCore = canvasCoreP?.GetValue(parent);
                            if (canvasCore != null)
                            {
                                var paintTasksProp = canvasCore.GetType().GetMethod("GetPaintTasks");
                                if (paintTasksProp != null)
                                {
                                    var tasks = paintTasksProp.Invoke(canvasCore, null) as System.Collections.IEnumerable;
                                    int count = 0;
                                    if (tasks != null) foreach (var _ in tasks) count++;
                                    coreInfo += $", tasks={count}";
                                }
                                else
                                {
                                    // Try _paintTasks field
                                    var ptField = canvasCore.GetType().GetField("_paintTasks",
                                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                    if (ptField != null)
                                    {
                                        var tasks = ptField.GetValue(canvasCore) as System.Collections.IEnumerable;
                                        int count = 0;
                                        if (tasks != null) foreach (var _ in tasks) count++;
                                        coreInfo += $", _paintTasks={count}";
                                    }
                                }
                            }

                            // Check the chart's _core
                            var grandparent = (parent as Microsoft.Maui.Controls.Element)?.Parent;
                            if (grandparent != null)
                            {
                                coreInfo += $", chart={grandparent.GetType().Name}";
                                var coreField = grandparent.GetType().GetField("_core",
                                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                var coreObj = coreField?.GetValue(grandparent);
                                coreInfo += $", _core={coreObj != null}";

                                if (coreObj != null)
                                {
                                    var ctrlSizeProp = coreObj.GetType().GetProperty("ControlSize");
                                    var ctrlSize = ctrlSizeProp?.GetValue(coreObj);
                                    coreInfo += $", ctrlSize={ctrlSize}";

                                    var isLoadedProp = coreObj.GetType().GetProperty("IsLoaded");
                                    var isLoaded = isLoadedProp?.GetValue(coreObj);
                                    coreInfo += $", isLoaded={isLoaded}";
                                }

                                // MotionCanvas W/H and _density
                                if (parent is Microsoft.Maui.Controls.VisualElement mcVe)
                                    coreInfo += $", mcW={mcVe.Width}, mcH={mcVe.Height}";

                                // Check _density field on MotionCanvas
                                var densityField = parent.GetType().GetField("_density",
                                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                if (densityField != null)
                                {
                                    var mcDensity = densityField.GetValue(parent);
                                    coreInfo += $", _density={mcDensity}";
                                }

                                // Check ControlSize Width/Height
                                if (coreObj != null)
                                {
                                    var csProp = coreObj.GetType().GetProperty("ControlSize");
                                    var ctrlSizeVal = csProp?.GetValue(coreObj);
                                    if (ctrlSizeVal != null)
                                    {
                                        var wProp = ctrlSizeVal.GetType().GetProperty("Width");
                                        var hProp = ctrlSizeVal.GetType().GetProperty("Height");
                                        coreInfo += $", csW={wProp?.GetValue(ctrlSizeVal)}, csH={hProp?.GetValue(ctrlSizeVal)}";
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    coreInfo = $"err: {ex.Message}";
                }

                DiagnosticLog.Error("SkiaSKCanvasView", $"Draw #{_drawCount}: {width}x{height}, pixels={nonT}/{totalPx}, {coreInfo}");
            }
        }

        canvas.DrawSurface(_cachedSurface, bounds.Left, bounds.Top);
    }

    // Temporarily suppress pointer events to test if they cause chart movement
    public override void OnPointerMoved(PointerEventArgs e) { }
    public override void OnPointerEntered(PointerEventArgs e) { }
    public override void OnPointerExited(PointerEventArgs e) { }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            InvalidateCachedSurface();
        }
        base.Dispose(disposing);
    }
}
