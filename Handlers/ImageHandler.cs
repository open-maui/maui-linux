// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for Image on Linux using Skia rendering.
/// Maps IImage interface to SkiaImage platform view.
/// IImage has: Aspect, IsOpaque (inherits from IImageSourcePart)
/// </summary>
public partial class ImageHandler : LinuxViewHandler<IImage, SkiaImage>, IImageHandler
{
    IImage IImageHandler.VirtualView => VirtualView;
    object IImageHandler.PlatformView => PlatformView;

    public static IPropertyMapper<IImage, ImageHandler> Mapper = new PropertyMapper<IImage, ImageHandler>(ViewHandler.ViewMapper)
    {
        [nameof(IImage.Aspect)] = MapAspect,
        [nameof(IImage.IsOpaque)] = MapIsOpaque,
        [nameof(IImageSourcePart.Source)] = MapSource,
        [nameof(IImageSourcePart.IsAnimationPlaying)] = MapIsAnimationPlaying,
        [nameof(IView.Background)] = MapBackground,
        ["Width"] = MapWidth,
        ["Height"] = MapHeight,
        ["HorizontalOptions"] = MapHorizontalOptions,
        ["VerticalOptions"] = MapVerticalOptions,
    };

    public static CommandMapper<IImage, ImageHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
    };

    public ImageHandler() : base(Mapper, CommandMapper)
    {
    }

    public ImageHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaImage CreatePlatformView()
    {
        return new SkiaImage();
    }

    protected override void ConnectHandler(SkiaImage platformView)
    {
        base.ConnectHandler(platformView);
        if (VirtualView is Microsoft.Maui.Controls.View view)
            ToolkitIconTint.Attach(view, color => platformView.TintColor = color);
    }

    protected override void DisconnectHandler(SkiaImage platformView)
    {
        if (VirtualView is Microsoft.Maui.Controls.View view)
            ToolkitIconTint.Detach(view);
        base.DisconnectHandler(platformView);
    }

    /// <summary>MAUI's <c>MapAspect</c> signature; maps the property on a Linux <see cref="ImageHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapAspect(IImageHandler handler, IImage image)
    {
        if (handler is ImageHandler linuxHandler)
            MapAspectCore(linuxHandler, image);
    }

    [Obsolete("Use the IImageHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapAspect(ImageHandler handler, IImage image) => MapAspectCore(handler, image);

    private static void MapAspectCore(ImageHandler handler, IImage image)
    {
        handler.PlatformView?.UpdateAspect(image);
    }

    public static void MapIsOpaque(ImageHandler handler, IImage image)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsOpaque = image.IsOpaque;
    }

    /// <summary>MAUI's <c>MapIsAnimationPlaying</c> signature; maps the property on a Linux <see cref="ImageHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapIsAnimationPlaying(IImageHandler handler, IImage image)
    {
        if (handler is ImageHandler linuxHandler)
            MapIsAnimationPlayingCore(linuxHandler, image);
    }

    [Obsolete("Use the IImageHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapIsAnimationPlaying(ImageHandler handler, IImage image) => MapIsAnimationPlayingCore(handler, image);

    private static void MapIsAnimationPlayingCore(ImageHandler handler, IImage image)
    {
        handler.PlatformView?.UpdateIsAnimationPlaying(image);
    }

    /// <summary>MAUI's <c>MapSource</c> signature; maps the property on a Linux <see cref="ImageHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapSource(IImageHandler handler, IImage image)
    {
        if (handler is ImageHandler linuxHandler)
            MapSourceCore(linuxHandler, image);
    }

    [Obsolete("Use the IImageHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapSource(ImageHandler handler, IImage image) => MapSourceCore(handler, image);

    private static void MapSourceCore(ImageHandler handler, IImage image)
    {
        if (handler.PlatformView is null) return;

        // Extract width/height requests from Image control
        if (image is Image img)
        {
            if (img.WidthRequest > 0)
            {
                handler.PlatformView.WidthRequest = img.WidthRequest;
            }
            if (img.HeightRequest > 0)
            {
                handler.PlatformView.HeightRequest = img.HeightRequest;
            }
        }

        _ = handler.ImageLoad.UpdateImageSourceAsync();
    }

    public static void MapBackground(ImageHandler handler, IImage image)
    {
        if (handler.PlatformView is null) return;

        if (image.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.ImageBackgroundColor = solidPaint.Color;
        }
    }

    public static void MapWidth(ImageHandler handler, IImage image)
    {
        if (handler.PlatformView is null) return;

        if (image is Image img && img.WidthRequest > 0)
        {
            handler.PlatformView.WidthRequest = img.WidthRequest;
            DiagnosticLog.Debug("ImageHandler", $"MapWidth: {img.WidthRequest}");
        }
        else if (image.Width > 0)
        {
            handler.PlatformView.WidthRequest = image.Width;
        }
    }

    public static void MapHeight(ImageHandler handler, IImage image)
    {
        if (handler.PlatformView is null) return;

        if (image is Image img && img.HeightRequest > 0)
        {
            handler.PlatformView.HeightRequest = img.HeightRequest;
            DiagnosticLog.Debug("ImageHandler", $"MapHeight: {img.HeightRequest}");
        }
        else if (image.Height > 0)
        {
            handler.PlatformView.HeightRequest = image.Height;
        }
    }

    public static void MapHorizontalOptions(ImageHandler handler, IImage image)
    {
        if (handler.PlatformView is null) return;

        if (image is Image img)
        {
            handler.PlatformView.HorizontalOptions = img.HorizontalOptions;
        }
    }

    public static void MapVerticalOptions(ImageHandler handler, IImage image)
    {
        if (handler.PlatformView is null) return;

        if (image is Image img)
        {
            handler.PlatformView.VerticalOptions = img.VerticalOptions;
        }
    }

    // Image source loading helper
    private ImageSourceServiceResultManager? _imageLoad;
    private ImageSourcePartLoader? _imageSourcePartLoader;

    private ImageSourceServiceResultManager ImageLoad =>
        _imageLoad ??= new ImageSourceServiceResultManager(this);

    /// <summary>
    /// MAUI's <c>IImageHandler.SourceLoader</c>: its <see cref="ImageSourcePartLoader.UpdateImageSourceAsync"/>
    /// loads the virtual view's source into the SkiaImage (through the image source service, with
    /// IsLoading and the loading events), as the handler's own Source mapping does.
    /// </summary>
    public virtual ImageSourcePartLoader SourceLoader =>
        _imageSourcePartLoader ??= new LinuxImageSourcePartLoader(
            new LinuxImageSourcePartSetter(this, SetPlatformImage),
            () => ImageLoad.UpdateImageSourceAsync());

    // A platform image handed to the part setter directly (MAUI's loader sets a null image
    // for a null source): a bitmap is shown, anything else clears the picture.
    private void SetPlatformImage(object? platformImage)
    {
        if (PlatformView is null)
            return;
        if (LinuxImageSourcePartSetter.ToBitmap(platformImage) is { } bitmap)
            PlatformView.LoadFromBitmap(bitmap);
        else
            PlatformView.ClearImage();
    }

    internal class ImageSourceServiceResultManager
    {
        private readonly ImageHandler _handler;
        private CancellationTokenSource? _cts;

        public ImageSourceServiceResultManager(ImageHandler handler)
        {
            _handler = handler;
        }

        public async Task UpdateImageSourceAsync()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            try
            {
                await ImageSourcePartLoading.RunAsync(_handler.VirtualView, _cts.Token, LoadAsync, Clear);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("ImageHandler", "Image source load failed", ex);
            }
        }

        private void Clear() => _handler.PlatformView?.ClearImage();

        private Task<Exception?> LoadAsync(IImageSource source, CancellationToken token)
        {
            var view = _handler.PlatformView;
            var part = _handler.VirtualView;
            if (view is null || part is null)
                return Task.FromResult<Exception?>(null);

            float scale = view.DeviceScale;
            return ImageSourcePartLoading.LoadThroughServiceAsync(
                _handler, part, source, scale, new Size(view.WidthRequest, view.HeightRequest), token,
                result => view.ApplyResult(result, scale), view.ClearImage);
        }

        /// <summary>The glyph of a FontImageSource as a bitmap (toolbar icons and tests use it directly).</summary>
        internal static SKBitmap? RenderFontImageSource(FontImageSource fontSource, double requestedWidth, double requestedHeight) =>
            LinuxFontImageSourceService.RenderGlyph(fontSource, requestedWidth, requestedHeight);
    }
}
