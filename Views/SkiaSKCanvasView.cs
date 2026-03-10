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
    private float _surfaceScale = 1f;

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
        InvalidateCachedSurface();
        Invalidate();
    }

    private void InvalidateCachedSurface()
    {
        _cachedSurface?.Dispose();
        _cachedSurface = null;
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

        // Create the offscreen surface at physical pixel dimensions (matching Android/iOS behavior).
        // MotionCanvas applies Canvas.Scale(density) to convert logical→physical coordinates,
        // so the surface must be in physical pixels for the scaling to work correctly.
        float density = (float)DeviceDisplayService.Instance.MainDisplayInfo.Density;
        if (density <= 0) density = 1f;
        _surfaceScale = density;

        int physWidth = Math.Max(1, (int)(bounds.Width * density));
        int physHeight = Math.Max(1, (int)(bounds.Height * density));

        var info = new SKImageInfo(physWidth, physHeight, SKColorType.Rgba8888, SKAlphaType.Premul);

        // Reuse surface if dimensions match
        if (_cachedSurface == null || _cachedInfo.Width != physWidth || _cachedInfo.Height != physHeight)
        {
            _cachedSurface?.Dispose();
            _cachedSurface = SKSurface.Create(info);
            _cachedInfo = info;

            if (_cachedSurface == null)
            {
                DiagnosticLog.Error("SkiaSKCanvasView", $"Failed to create SKSurface ({physWidth}x{physHeight})");
                return;
            }

            // Notify the canvas view of the physical pixel size
            _canvasView.OnCanvasSizeChanged(new SKSizeI(physWidth, physHeight));
        }

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

        // Composite the offscreen surface onto our canvas
        _cachedSurface.Canvas.Flush();

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

                DiagnosticLog.Error("SkiaSKCanvasView", $"Draw #{_drawCount}: {physWidth}x{physHeight} (scale={_surfaceScale}), pixels={nonT}/{totalPx}, {coreInfo}");
            }
        }

        // Composite the physical-pixel surface back to our logical-pixel canvas
        if (_surfaceScale != 1f)
        {
            canvas.Save();
            canvas.Translate(bounds.Left, bounds.Top);
            canvas.Scale(1f / _surfaceScale, 1f / _surfaceScale);
            canvas.DrawSurface(_cachedSurface, 0, 0);
            canvas.Restore();
        }
        else
        {
            canvas.DrawSurface(_cachedSurface, bounds.Left, bounds.Top);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            InvalidateCachedSurface();
        }
        base.Dispose(disposing);
    }
}
