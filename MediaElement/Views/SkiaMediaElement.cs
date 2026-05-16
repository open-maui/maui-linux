// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.MediaElement.Views;

/// <summary>
/// Skia-rendered video surface backing CommunityToolkit.Maui.MediaElement on Linux.
/// Owns the GStreamer pipeline (created in S4.2) and holds the most recent
/// decoded BGRA frame as an <see cref="SKImage"/>. OnDraw blits the image into
/// the view's bounds with the requested aspect mode.
///
/// Construction is cheap — the pipeline starts when LinuxMediaElementHandler
/// calls SetSource() in response to MAUI's MapSource. Frames flow from the
/// appsink callback via LinuxDispatcher to the main thread, where this view
/// replaces _latestFrame and Invalidate()s.
/// </summary>
public class SkiaMediaElement : SkiaView
{
    private SKImage? _latestFrame;
    private string? _currentUri;
    private bool _isPlaying;

    public string? CurrentUri => _currentUri;
    public bool IsPlaying => _isPlaying;

    /// <summary>
    /// Apply the requested media source. Tearing down and rebuilding the
    /// pipeline lives in S4.2; this stub records the URI so handler tests
    /// can verify the mapper routed through.
    /// </summary>
    public void SetSource(string? uri)
    {
        _currentUri = uri;
        // S4.2: build playbin pipeline and seek to Ready state here.
    }

    public void Play()
    {
        _isPlaying = true;
        // S4.2: gst_element_set_state(pipeline, GST_STATE_PLAYING)
    }

    public void Pause()
    {
        _isPlaying = false;
        // S4.2: gst_element_set_state(pipeline, GST_STATE_PAUSED)
    }

    public void Stop()
    {
        _isPlaying = false;
        // S4.2: gst_element_set_state(pipeline, GST_STATE_NULL) + seek to 0
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        // Background: paint a solid black so empty video frames don't show
        // through to whatever was underneath.
        using (var bg = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Fill })
            canvas.DrawRect(bounds, bg);

        if (_latestFrame == null) return;

        // Stretch-to-fit for the placeholder; aspect-correct blit lands in S4.3
        // when the Aspect property is wired through.
        canvas.DrawImage(_latestFrame, bounds);
    }

    protected override Size MeasureOverride(Size availableSize) => availableSize;
}
