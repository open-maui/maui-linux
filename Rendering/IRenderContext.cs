// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Rendering;

/// <summary>
/// Per-view-tree rendering context. Replaces the prior <c>SkiaRenderingEngine.Current</c>
/// static so that views can be unit-tested in isolation against a mock context, and so
/// that future multi-window setups can give each window its own engine without crossing
/// streams.
///
/// Each <c>SkiaView</c> attached to a render tree gets its <c>RenderContext</c> set
/// when added; AddChild/InsertChild propagate the context down the tree automatically.
/// </summary>
public interface IRenderContext
{
    /// <summary>
    /// Cache of typefaces, raster images, and other Skia resources scoped to this
    /// render context. Views resolve fonts during draw via this cache rather than
    /// reaching for a global.
    /// </summary>
    ResourceCache Resources { get; }

    /// <summary>
    /// Display scale factor (e.g. 1.0 at 1x, 2.0 at HiDPI 2x). Views and shapes
    /// can use this to size text and stroke widths consistently across monitors.
    /// </summary>
    float DpiScale { get; }

    /// <summary>
    /// Mark the entire render surface as needing a redraw. Use sparingly — prefer
    /// <see cref="InvalidateRegion"/> when the dirty area is known so the engine
    /// can do partial repaints.
    /// </summary>
    void Invalidate();

    /// <summary>
    /// Mark a specific rectangle as dirty so the next frame only repaints that
    /// region. Coordinates are in absolute (window) space.
    /// </summary>
    void InvalidateRegion(SKRect rect);
}
