// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
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

        handler.SourceLoader.UpdateImageSourceAsync();
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
                else if (source is FontImageSource fontSource)
                {
                    var bitmap = RenderFontImageSource(fontSource, _handler.PlatformView!.WidthRequest, _handler.PlatformView.HeightRequest);
                    if (bitmap != null)
                    {
                        _handler.PlatformView.LoadFromBitmap(bitmap);
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

        private static SKBitmap? RenderFontImageSource(FontImageSource fontSource, double requestedWidth, double requestedHeight)
        {
            string glyph = fontSource.Glyph;
            if (string.IsNullOrEmpty(glyph))
            {
                return null;
            }

            int size = (int)Math.Max(requestedWidth > 0 ? requestedWidth : 24.0, requestedHeight > 0 ? requestedHeight : 24.0);
            size = Math.Max(size, 16);

            SKColor color = fontSource.Color?.ToSKColor() ?? SKColors.Black;
            SKBitmap bitmap = new SKBitmap(size, size, false);
            using SKCanvas canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);

            SKTypeface? typeface = null;
            if (!string.IsNullOrEmpty(fontSource.FontFamily))
            {
                string[] fontPaths = new string[]
                {
                    "/usr/share/fonts/truetype/" + fontSource.FontFamily + ".ttf",
                    "/usr/share/fonts/opentype/" + fontSource.FontFamily + ".otf",
                    "/usr/local/share/fonts/" + fontSource.FontFamily + ".ttf",
                    Path.Combine(AppContext.BaseDirectory, fontSource.FontFamily + ".ttf")
                };

                foreach (string path in fontPaths)
                {
                    if (File.Exists(path))
                    {
                        typeface = SKTypeface.FromFile(path, 0);
                        if (typeface != null)
                        {
                            break;
                        }
                    }
                }

                if (typeface == null)
                {
                    typeface = SKTypeface.FromFamilyName(fontSource.FontFamily);
                }
            }

            if (typeface == null)
            {
                typeface = SKTypeface.Default;
            }

            float fontSize = size * 0.8f;
            using SKFont font = new SKFont(typeface, fontSize, 1f, 0f);
            using SKPaint paint = new SKPaint(font)
            {
                Color = color,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center
            };

            SKRect bounds = default;
            paint.MeasureText(glyph, ref bounds);
            float x = size / 2f;
            float y = (size - bounds.Top - bounds.Bottom) / 2f;
            canvas.DrawText(glyph, x, y, paint);

            return bitmap;
        }
    }
}
