// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// The Linux load method of an image-source service: what Android's
/// <c>GetDrawableAsync</c>, iOS's <c>GetImageAsync</c> and Windows's
/// <c>GetImageSourceAsync</c> are on the other platforms. MAUI's platform-neutral
/// <see cref="IImageSourceService"/> has no load method, so a service that should
/// load on Linux implements this interface as well as
/// <see cref="IImageSourceService{T}"/>, and is registered the MAUI way:
/// <code>
/// builder.ConfigureImageSources(services =&gt;
///     services.AddService&lt;IMyImageSource, MyImageSourceService&gt;());
/// </code>
/// The Image and ImageButton handlers resolve the service for the source's type
/// through <see cref="IImageSourceServiceProvider"/> and show the bitmap it returns.
/// </summary>
public interface ILinuxImageSourceService : IImageSourceService
{
    /// <summary>
    /// Loads <paramref name="imageSource"/> as a bitmap.
    /// </summary>
    /// <param name="imageSource">The source to load.</param>
    /// <param name="scale">Device scale of the window the image is shown in. A service
    /// that renders vector content (SVG, font glyphs) renders it at this scale and returns
    /// a result whose <see cref="IImageSourceServiceResult.IsResolutionDependent"/> is true;
    /// the view then shows the bitmap at its pixel size divided by <paramref name="scale"/>.</param>
    /// <param name="cancellationToken">Cancelled when a newer load replaces this one.</param>
    /// <returns>The bitmap, or null for an empty source (the view shows no picture).
    /// A failed load throws; the handler reports it through LoadingFailed.</returns>
    Task<IImageSourceServiceResult<SKBitmap>?> GetImageAsync(
        IImageSource imageSource,
        float scale = 1,
        CancellationToken cancellationToken = default);
}
