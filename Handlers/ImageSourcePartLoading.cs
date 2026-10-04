// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Runs one image-source load of the Image and ImageButton handlers with the
/// notifications MAUI's ImageSourcePartLoader gives the virtual view:
/// <see cref="IImageSourcePart.UpdateIsLoading"/> around the load and, through
/// <see cref="IImageSourcePartEvents"/>, LoadingStarted followed by
/// LoadingCompleted(true), LoadingCompleted(false) for a load that a newer one
/// replaced, or LoadingFailed. A null source, or a source that fails to load,
/// clears the picture (MAUI sets a null image); a replaced load changes nothing.
/// </summary>
internal static class ImageSourcePartLoading
{
    /// <param name="part">The virtual view.</param>
    /// <param name="token">Cancelled when a newer load starts.</param>
    /// <param name="load">Loads the source into the platform view; returns the error the
    /// view reported, or null when the picture was set.</param>
    /// <param name="clear">Removes the platform view's picture.</param>
    public static async Task RunAsync(
        IImageSourcePart? part,
        CancellationToken token,
        Func<IImageSource, CancellationToken, Task<Exception?>> load,
        Action clear)
    {
        var source = part?.Source;
        part?.UpdateIsLoading(false);

        if (source is null)
        {
            clear();
            return;
        }

        var events = part as IImageSourcePartEvents;
        events?.LoadingStarted();
        part!.UpdateIsLoading(true);

        Exception? failure = null;
        var cancelled = false;
        try
        {
            failure = await load(source, token);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }
        catch (Exception ex)
        {
            failure = ex;
        }

        var current = !cancelled && !token.IsCancellationRequested && ReferenceEquals(part.Source, source);
        try
        {
            if (!current)
            {
                events?.LoadingCompleted(false);
            }
            else if (failure is not null)
            {
                clear();
                events?.LoadingFailed(failure);
            }
            else
            {
                events?.LoadingCompleted(true);
            }
        }
        finally
        {
            if (current)
                part.UpdateIsLoading(false);
        }
    }

    /// <summary>
    /// Loads <paramref name="source"/> through the image-source service registered for its
    /// type (MAUI's IImageSourceServiceProvider; the built-in Linux service for MAUI's own
    /// source kinds when none with a Linux load method is registered) and shows the result.
    /// The load method passed to <see cref="RunAsync"/>.
    /// </summary>
    /// <param name="handler">The handler, whose MauiContext supplies the services.</param>
    /// <param name="part">The virtual view; a load whose source it no longer shows is dropped.</param>
    /// <param name="source">The source to load.</param>
    /// <param name="scale">The device scale of the view's window.</param>
    /// <param name="requestedSize">The view's requested logical size (0 when unset), which the
    /// built-in services render SVGs and font glyphs for.</param>
    /// <param name="token">Cancelled when a newer load starts.</param>
    /// <param name="apply">Shows a loaded image in the platform view.</param>
    /// <param name="clear">Removes the platform view's picture (the service returned no image).</param>
    /// <returns>Null: a failed load throws, which <see cref="RunAsync"/> reports.</returns>
    public static async Task<Exception?> LoadThroughServiceAsync(
        IElementHandler handler,
        IImageSourcePart part,
        IImageSource source,
        float scale,
        Size requestedSize,
        CancellationToken token,
        Action<IImageSourceServiceResult<SKBitmap>> apply,
        Action clear)
    {
        var result = await LinuxImageSourceServices.LoadAsync(handler.MauiContext?.Services, source, scale, requestedSize, token);

        // A load that a newer one replaced is not shown, and nothing else would release it.
        if (token.IsCancellationRequested || !ReferenceEquals(part.Source, source))
        {
            result?.Dispose();
            throw new OperationCanceledException(token);
        }

        if (result is null)
            clear();
        else
            apply(result);
        return null;
    }
}
