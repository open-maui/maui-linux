// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using HarmonyLib;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// The <see cref="ImageSourcePartLoader"/> the Linux image handlers expose as MAUI's
/// <c>IImageHandler.SourceLoader</c> / <c>IButtonHandler.ImageSourceLoader</c>.
/// On the platform-neutral build MAUI's <see cref="ImageSourcePartLoader.UpdateImageSourceAsync"/>
/// loads nothing (only a null source reaches the setter); for this loader it runs the handler's
/// own Linux load instead (the image source service, IsLoading and the loading events), so
/// MAUI code that calls <c>handler.SourceLoader.UpdateImageSourceAsync()</c> (MAUI's own
/// <c>MapSource</c> does) shows the image.
/// </summary>
internal sealed class LinuxImageSourcePartLoader : ImageSourcePartLoader
{
    private readonly Func<Task> _load;

    /// <param name="setter">The part setter (the handler and the image part it loads).</param>
    /// <param name="load">Loads the part's current source into the handler's platform view.</param>
    public LinuxImageSourcePartLoader(IImageSourcePartSetter setter, Func<Task> load)
        : base(setter)
    {
        _load = load ?? throw new ArgumentNullException(nameof(load));
        ImageSourcePartLoaderPatches.Install();
    }

    /// <summary>
    /// Loads the image part's current source into the platform view: what
    /// <see cref="ImageSourcePartLoader.UpdateImageSourceAsync"/> does for this loader.
    /// </summary>
    public Task LoadAsync() => _load();
}

/// <summary>
/// The <see cref="IImageSourcePartSetter"/> of a Linux image handler: the image part is the
/// handler's virtual view, and a loaded platform image (an <see cref="SKBitmap"/> or
/// <see cref="SKImage"/>; null clears) is given to the platform view.
/// </summary>
internal sealed class LinuxImageSourcePartSetter : IImageSourcePartSetter
{
    private readonly WeakReference<IElementHandler> _handler;
    private readonly Action<object?> _setImage;

    public LinuxImageSourcePartSetter(IElementHandler handler, Action<object?> setImage)
    {
        _handler = new WeakReference<IElementHandler>(handler);
        _setImage = setImage;
    }

    public IElementHandler? Handler => _handler.TryGetTarget(out var handler) ? handler : null;

    public IImageSourcePart? ImageSourcePart => Handler?.VirtualView as IImageSourcePart;

    public void SetImageSource(object? platformImage) => _setImage(platformImage);

    /// <summary>A platform image as a bitmap: an <see cref="SKBitmap"/> as is, an <see cref="SKImage"/> converted.</summary>
    internal static SKBitmap? ToBitmap(object? platformImage) => platformImage switch
    {
        SKBitmap bitmap => bitmap,
        SKImage image => SKBitmap.FromImage(image),
        _ => null,
    };
}

/// <summary>
/// Routes <see cref="ImageSourcePartLoader.UpdateImageSourceAsync"/> to the Linux load for a
/// <see cref="LinuxImageSourcePartLoader"/> (MAUI's method is not virtual; on the
/// platform-neutral build it does not load). Other loaders keep MAUI's behaviour.
/// </summary>
internal static class ImageSourcePartLoaderPatches
{
    private static int s_installed;

    internal static void Install()
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
            return;
        try
        {
            var target = typeof(ImageSourcePartLoader).GetMethod(nameof(ImageSourcePartLoader.UpdateImageSourceAsync),
                BindingFlags.Instance | BindingFlags.Public, Type.EmptyTypes);
            if (target is null)
            {
                DiagnosticLog.Warn("ImageSourcePartLoaderPatches", "ImageSourcePartLoader.UpdateImageSourceAsync not found; SourceLoader will not load images");
                return;
            }
            new Harmony("com.openmaui.imagesourcepartloader").Patch(target,
                new HarmonyMethod(typeof(ImageSourcePartLoaderPatches).GetMethod(nameof(UpdateImageSourceAsync_Prefix), BindingFlags.Static | BindingFlags.NonPublic)));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("ImageSourcePartLoaderPatches", "Patching ImageSourcePartLoader failed", ex);
        }
    }

    private static bool UpdateImageSourceAsync_Prefix(ImageSourcePartLoader __instance, ref Task __result)
    {
        if (__instance is not LinuxImageSourcePartLoader linux)
            return true;
        __result = linux.LoadAsync();
        return false;
    }
}
