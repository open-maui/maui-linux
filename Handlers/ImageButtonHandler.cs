// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for ImageButton on Linux using Skia rendering.
/// Maps IImageButton interface to SkiaImageButton platform view.
/// IImageButton extends: IImage, IView, IButtonStroke, IPadding
/// </summary>
public partial class ImageButtonHandler : ViewHandler<IImageButton, SkiaImageButton>
{
    public static IPropertyMapper<IImageButton, ImageButtonHandler> Mapper = new PropertyMapper<IImageButton, ImageButtonHandler>(ViewHandler.ViewMapper)
    {
        [nameof(IImage.Aspect)] = MapAspect,
        [nameof(IImage.IsOpaque)] = MapIsOpaque,
        [nameof(IImageSourcePart.Source)] = MapSource,
        [nameof(IButtonStroke.StrokeColor)] = MapStrokeColor,
        [nameof(IButtonStroke.StrokeThickness)] = MapStrokeThickness,
        [nameof(IButtonStroke.CornerRadius)] = MapCornerRadius,
        [nameof(IPadding.Padding)] = MapPadding,
        [nameof(IView.Background)] = MapBackground,
    };

    public static CommandMapper<IImageButton, ImageButtonHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
    };

    public ImageButtonHandler() : base(Mapper, CommandMapper)
    {
    }

    public ImageButtonHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaImageButton CreatePlatformView()
    {
        return new SkiaImageButton();
    }

    protected override void ConnectHandler(SkiaImageButton platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Clicked += OnClicked;
        platformView.Pressed += OnPressed;
        platformView.Released += OnReleased;
        platformView.ImageLoaded += OnImageLoaded;
        platformView.ImageLoadingError += OnImageLoadingError;
    }

    protected override void DisconnectHandler(SkiaImageButton platformView)
    {
        platformView.Clicked -= OnClicked;
        platformView.Pressed -= OnPressed;
        platformView.Released -= OnReleased;
        platformView.ImageLoaded -= OnImageLoaded;
        platformView.ImageLoadingError -= OnImageLoadingError;
        base.DisconnectHandler(platformView);
    }

    private void OnClicked(object? sender, EventArgs e)
    {
        VirtualView?.Clicked();
    }

    private void OnPressed(object? sender, EventArgs e)
    {
        VirtualView?.Pressed();
    }

    private void OnReleased(object? sender, EventArgs e)
    {
        VirtualView?.Released();
    }

    private void OnImageLoaded(object? sender, EventArgs e)
    {
        if (VirtualView is IImageSourcePart imageSourcePart)
        {
            imageSourcePart.UpdateIsLoading(false);
        }
    }

    private void OnImageLoadingError(object? sender, ImageLoadingErrorEventArgs e)
    {
        if (VirtualView is IImageSourcePart imageSourcePart)
        {
            imageSourcePart.UpdateIsLoading(false);
        }
    }

    public static void MapAspect(ImageButtonHandler handler, IImageButton imageButton)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Aspect = imageButton.Aspect;
    }

    public static void MapIsOpaque(ImageButtonHandler handler, IImageButton imageButton)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsOpaque = imageButton.IsOpaque;
    }

    public static void MapSource(ImageButtonHandler handler, IImageButton imageButton)
    {
        if (handler.PlatformView is null) return;
        handler.SourceLoader.UpdateImageSourceAsync();
    }

    public static void MapStrokeColor(ImageButtonHandler handler, IImageButton imageButton)
    {
        if (handler.PlatformView is null) return;

        if (imageButton.StrokeColor is not null)
            handler.PlatformView.StrokeColor = imageButton.StrokeColor.ToSKColor();
    }

    public static void MapStrokeThickness(ImageButtonHandler handler, IImageButton imageButton)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.StrokeThickness = (float)imageButton.StrokeThickness;
    }

    public static void MapCornerRadius(ImageButtonHandler handler, IImageButton imageButton)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.CornerRadius = imageButton.CornerRadius;
    }

    public static void MapPadding(ImageButtonHandler handler, IImageButton imageButton)
    {
        if (handler.PlatformView is null) return;

        var padding = imageButton.Padding;
        handler.PlatformView.PaddingLeft = (float)padding.Left;
        handler.PlatformView.PaddingTop = (float)padding.Top;
        handler.PlatformView.PaddingRight = (float)padding.Right;
        handler.PlatformView.PaddingBottom = (float)padding.Bottom;
    }

    public static void MapBackground(ImageButtonHandler handler, IImageButton imageButton)
    {
        if (handler.PlatformView is null) return;

        if (imageButton.Background is SolidPaint solidPaint && solidPaint.Color is not null)
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
        private readonly ImageButtonHandler _handler;
        private CancellationTokenSource? _cts;

        public ImageSourceServiceResultManager(ImageButtonHandler handler)
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
