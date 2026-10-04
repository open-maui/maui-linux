// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
    /// Awaits a platform-view load and returns the error the view raised while it ran
    /// (the views report failures through their ImageLoadingError event, not by throwing).
    /// </summary>
    public static async Task<Exception?> CaptureErrorAsync(
        Action<EventHandler<ImageLoadingErrorEventArgs>> subscribe,
        Action<EventHandler<ImageLoadingErrorEventArgs>> unsubscribe,
        Func<Task> load)
    {
        Exception? error = null;
        EventHandler<ImageLoadingErrorEventArgs> onError = (_, e) => error ??= e.Exception;
        subscribe(onError);
        try
        {
            await load();
        }
        finally
        {
            unsubscribe(onError);
        }
        return error;
    }
}
