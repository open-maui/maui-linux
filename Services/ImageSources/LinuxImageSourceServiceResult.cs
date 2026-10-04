// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// A bitmap loaded by an <see cref="ILinuxImageSourceService"/>: the Linux
/// counterpart of MAUI's platform <c>ImageSourceServiceResult</c>.
/// </summary>
/// <remarks>
/// The result owns the bitmap's lifetime, not the view showing it: the view keeps the
/// result while it shows <see cref="Value"/> and disposes the result when it shows
/// something else, which runs the dispose action. Pass <c>bitmap.Dispose</c> for a
/// bitmap made for this load; pass no action for a bitmap the service shares (a cache
/// entry other views may be drawing), which must never be disposed by a view.
/// </remarks>
public class LinuxImageSourceServiceResult : IImageSourceServiceResult<SKBitmap>
{
    private Action? _dispose;

    /// <summary>Creates a result that is not resolution dependent.</summary>
    /// <param name="bitmap">The loaded bitmap.</param>
    /// <param name="dispose">Releases the bitmap when the view no longer shows it; null for a shared bitmap.</param>
    public LinuxImageSourceServiceResult(SKBitmap bitmap, Action? dispose = null)
        : this(bitmap, false, dispose)
    {
    }

    /// <summary>Creates a result.</summary>
    /// <param name="bitmap">The loaded bitmap.</param>
    /// <param name="resolutionDependent">True when the bitmap was rendered for the scale it
    /// was requested at (its logical size is its pixel size divided by that scale).</param>
    /// <param name="dispose">Releases the bitmap when the view no longer shows it; null for a shared bitmap.</param>
    public LinuxImageSourceServiceResult(SKBitmap bitmap, bool resolutionDependent, Action? dispose = null)
    {
        Value = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
        IsResolutionDependent = resolutionDependent;
        _dispose = dispose;
    }

    /// <inheritdoc/>
    public SKBitmap Value { get; }

    /// <inheritdoc/>
    public bool IsResolutionDependent { get; }

    /// <inheritdoc/>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Frames of an animated image (GIF), all owned like <see cref="Value"/> (the first frame).
    /// Only the built-in services set it.
    /// </summary>
    internal IReadOnlyList<ImageFrame>? Frames { get; init; }

    /// <summary>
    /// The SVG file the bitmap was rendered from, so a view can render it again at the
    /// size it ends up shown at. Only the built-in file service sets it.
    /// </summary>
    internal string? SvgPath { get; init; }

    /// <summary>Runs the dispose action once.</summary>
    public void Dispose()
    {
        if (IsDisposed)
            return;

        IsDisposed = true;
        _dispose?.Invoke();
        _dispose = null;
    }
}

/// <summary>One frame of an animated image and how long it shows, in milliseconds.</summary>
internal sealed record ImageFrame(SKBitmap Bitmap, int Duration);
