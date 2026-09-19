// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Rendering;

/// <summary>
/// Central factory for all <see cref="SKFont"/> creation on the Linux platform.
///
/// WHY: The app scales the canvas by DpiScale on HiDPI displays, but SKFont's
/// defaults (Hinting=Normal, Subpixel=false, LinearMetrics=false) snap glyph
/// advances to integer values in FONT space. The canvas scale then magnifies
/// that rounding, producing irregular intra-word gaps (e.g. "SearchBar"
/// rendering as "Search Bar"). Setting <c>Subpixel = true</c> and
/// <c>LinearMetrics = true</c> keeps glyph advances fractional so they scale
/// cleanly with the canvas transform.
///
/// GUARD: All rendering/measurement code must create SKFonts through this
/// factory — mixed settings cause measure/draw mismatch (measured wrap points
/// and caret positions will disagree with drawn text).
/// </summary>
public static class SkiaFontFactory
{
    /// <summary>
    /// Creates an <see cref="SKFont"/> with the platform-required settings
    /// (<c>Subpixel = true</c>, <c>LinearMetrics = true</c>).
    /// </summary>
    /// <param name="typeface">The typeface to use, or null for the default typeface.</param>
    /// <param name="size">The font size in points.</param>
    public static SKFont Create(SKTypeface? typeface, float size)
        => new SKFont(typeface ?? SKTypeface.Default, size)
        {
            Subpixel = true,
            LinearMetrics = true,
        };

    /// <summary>
    /// Creates an <see cref="SKFont"/> using the default typeface with the
    /// platform-required settings (<c>Subpixel = true</c>, <c>LinearMetrics = true</c>).
    /// </summary>
    /// <param name="size">The font size in points.</param>
    public static SKFont Create(float size)
        => Create(SKTypeface.Default, size);
}
