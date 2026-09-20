// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Rendering;

/// <summary>
/// Owns how rendered pixels reach the display. <see cref="SkiaRenderingEngine"/>
/// decides <em>what</em> to draw (layout, dirty regions, overlays) and draws it
/// on the canvas a target hands out; the target decides <em>where</em> that
/// canvas lives (CPU bitmap or GPU framebuffer) and how a finished frame is
/// submitted (wl_shm / XPutImage copy, or eglSwapBuffers). <see cref="Window.IDisplayWindow"/>
/// stays responsible for window, input and lifecycle semantics only.
/// </summary>
public interface IRenderTarget : IDisposable
{
    /// <summary>Short diagnostic name, e.g. "raster", "egl-wayland", "egl-x11".</summary>
    string Name { get; }

    /// <summary>True when frames are rasterised on the GPU.</summary>
    bool IsGpuAccelerated { get; }

    /// <summary>
    /// True when the canvas returned by <see cref="BeginFrame"/> still holds the
    /// previous frame's pixels, so the engine may repaint only dirty regions.
    /// A double-buffered GPU swapchain does not preserve contents; the engine
    /// then repaints the whole surface every frame.
    /// </summary>
    bool PreservesContents { get; }

    /// <summary>Surface width in physical pixels.</summary>
    int Width { get; }

    /// <summary>Surface height in physical pixels.</summary>
    int Height { get; }

    /// <summary>
    /// Resizes the surface (physical pixels). Implementations may keep the old
    /// contents where that avoids a blank flash during interactive resize.
    /// </summary>
    void Resize(int width, int height);

    /// <summary>
    /// Makes the target current and returns the canvas to draw this frame on,
    /// or null when the target cannot render (lost context, zero size).
    /// </summary>
    SKCanvas? BeginFrame();

    /// <summary>Flushes drawing and submits the frame to the display.</summary>
    void EndFrame();
}
