// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for Image on Linux using Skia rendering.
/// Maps IImage interface to SkiaImage platform view.
/// IImage has: Aspect, IsOpaque (inherits from IImageSourcePart)
/// </summary>
public partial class ImageHandler : ViewHandler<IImage, SkiaImage>
{
    public static IPropertyMapper<IImage, ImageHandler> Mapper = new PropertyMapper<IImage, ImageHandler>(ViewHandler.ViewMapper)
    {
        [nameof(IImage.Aspect)] = MapAspect,
        [nameof(IImage.IsOpaque)] = MapIsOpaque,
        [nameof(IImageSourcePart.Source)] = MapSource,
        [nameof(IView.Background)] = MapBackground,
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
        platformView.ImageLoaded += OnImageLoaded;
        platformView.ImageLoadingError += OnImageLoadingError;
    }

    protected override void DisconnectHandler(SkiaImage platformView)
    {
        platformView.ImageLoaded -= OnImageLoaded;
        platformView.ImageLoadingError -= OnImageLoadingError;
        base.DisconnectHandler(platformView);
    }

    private void OnImageLoaded(object? sender, EventArgs e)
    {
        // Notify that the image has been loaded
        if (VirtualView is IImageSourcePart imageSourcePart)
        {
            imageSourcePart.UpdateIsLoading(false);
        }
    }

    private void OnImageLoadingError(object? sender, ImageLoadingErrorEventArgs e)
    {
        // Handle loading error
        if (VirtualView is IImageSourcePart imageSourcePart)
        {
            imageSourcePart.UpdateIsLoading(false);
        }
    }

    public static void MapAspect(ImageHandler handler, IImage image)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Aspect = image.Aspect;
    }

    public static void MapIsOpaque(ImageHandler handler, IImage image)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsOpaque = image.IsOpaque;
    }

    public static void MapSource(ImageHandler handler, IImage image)
    {
        if (handler.PlatformView is null) return;

        handler.SourceLoader.UpdateImageSourceAsync();
    }

    public static void MapBackground(ImageHandler handler, IImage image)
    {
        if (handler.PlatformView is null) return;

        if (image.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.BackgroundColor = solidPaint.Color.ToSKColor();
        }
    }

    // Image source loading helper
    private ImageSourceServiceResultManager _sourceLoader = null!;

    private ImageSourceServiceResultManager SourceLoader =>
        _sourceLoader ??= new ImageSourceServiceResultManager(this);

    internal class ImageSourceServiceResultManager
    {
        private readonly ImageHandler _handler;
        private CancellationTokenSource? _cts;

        public ImageSourceServiceResultManager(ImageHandler handler)
        {
            _handler = handler;
        }

        public async void UpdateImageSourceAsync()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                var source = _handler.VirtualView?.Source;
                if (source == null)
                {
                    _handler.PlatformView?.LoadFromData(Array.Empty<byte>());
                    return;
                }

                if (_handler.VirtualView is IImageSourcePart imageSourcePart)
                {
                    imageSourcePart.UpdateIsLoading(true);
                }

                // Handle different image source types
                if (source is IFileImageSource fileSource)
                {
                    var file = fileSource.File;
                    if (!string.IsNullOrEmpty(file))
                    {
                        await _handler.PlatformView!.LoadFromFileAsync(file);
                    }
                }
                else if (source is IUriImageSource uriSource)
                {
                    var uri = uriSource.Uri;
                    if (uri != null)
                    {
                        await _handler.PlatformView!.LoadFromUriAsync(uri);
                    }
                }
                else if (source is IStreamImageSource streamSource)
                {
                    var stream = await streamSource.GetStreamAsync(token);
                    if (stream != null)
                    {
                        await _handler.PlatformView!.LoadFromStreamAsync(stream);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Loading was cancelled
            }
            catch (Exception)
            {
                // Handle error
                if (_handler.VirtualView is IImageSourcePart imageSourcePart)
                {
                    imageSourcePart.UpdateIsLoading(false);
                }
            }
        }
    }
}
