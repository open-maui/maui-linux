// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Threading;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Linux handler for Image control.
/// </summary>
public class ImageHandler : ViewHandler<IImage, SkiaImage>
{
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

                if (_handler.VirtualView is IImageSourcePart imagePart)
                {
                    imagePart.UpdateIsLoading(true);
                }

                if (source is IFileImageSource fileSource)
                {
                    var file = fileSource.File;
                    if (!string.IsNullOrEmpty(file))
                    {
                        await _handler.PlatformView.LoadFromFileAsync(file);
                    }
                    return;
                }

                if (source is IUriImageSource uriSource)
                {
                    var uri = uriSource.Uri;
                    if (uri != null)
                    {
                        await _handler.PlatformView.LoadFromUriAsync(uri);
                    }
                    return;
                }

                if (source is IStreamImageSource streamSource)
                {
                    var stream = await streamSource.GetStreamAsync(token);
                    if (stream != null)
                    {
                        await _handler.PlatformView.LoadFromStreamAsync(stream);
                    }
                    return;
                }

                if (source is FontImageSource fontSource)
                {
                    var bitmap = RenderFontImageSource(fontSource, _handler.PlatformView.WidthRequest, _handler.PlatformView.HeightRequest);
                    if (bitmap != null)
                    {
                        _handler.PlatformView.LoadFromBitmap(bitmap);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Cancelled - ignore
            }
            catch (Exception)
            {
                if (_handler.VirtualView is IImageSourcePart imagePart)
                {
                    imagePart.UpdateIsLoading(false);
                }
            }
        }

        private static SKBitmap? RenderFontImageSource(FontImageSource fontSource, double requestedWidth, double requestedHeight)
        {
            var glyph = fontSource.Glyph;
            if (string.IsNullOrEmpty(glyph))
            {
                return null;
            }

            int size = (int)Math.Max(
                requestedWidth > 0 ? requestedWidth : 24.0,
                requestedHeight > 0 ? requestedHeight : 24.0);
            size = Math.Max(size, 16);

            var color = fontSource.Color?.ToSKColor() ?? SKColors.Black;
            var bitmap = new SKBitmap(size, size, false);

            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);

            SKTypeface? typeface = null;
            if (!string.IsNullOrEmpty(fontSource.FontFamily))
            {
                var fontPaths = new[]
                {
                    "/usr/share/fonts/truetype/" + fontSource.FontFamily + ".ttf",
                    "/usr/share/fonts/opentype/" + fontSource.FontFamily + ".otf",
                    "/usr/local/share/fonts/" + fontSource.FontFamily + ".ttf",
                    Path.Combine(AppContext.BaseDirectory, fontSource.FontFamily + ".ttf")
                };

                foreach (var path in fontPaths)
                {
                    if (File.Exists(path))
                    {
                        typeface = SKTypeface.FromFile(path);
                        if (typeface != null)
                            break;
                    }
                }

                if (typeface == null)
                {
                    typeface = SKTypeface.FromFamilyName(fontSource.FontFamily);
                }
            }

            typeface ??= SKTypeface.Default;

            float fontSize = size * 0.8f;
            using var font = new SKFont(typeface, fontSize);
            using var paint = new SKPaint(font)
            {
                Color = color,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center
            };

            var bounds = new SKRect();
            paint.MeasureText(glyph, ref bounds);

            float x = size / 2f;
            float y = (size - bounds.Top - bounds.Bottom) / 2f;
            canvas.DrawText(glyph, x, y, paint);

            return bitmap;
        }
    }

    public static IPropertyMapper<IImage, ImageHandler> Mapper = new PropertyMapper<IImage, ImageHandler>(ViewHandler.ViewMapper)
    {
        ["Aspect"] = MapAspect,
        ["IsOpaque"] = MapIsOpaque,
        ["Source"] = MapSource,
        ["Background"] = MapBackground,
        ["Width"] = MapWidth,
        ["Height"] = MapHeight
    };

    public static CommandMapper<IImage, ImageHandler> CommandMapper = new(ViewHandler.ViewCommandMapper);

    private ImageSourceServiceResultManager? _sourceLoader;
    private ImageSourceServiceResultManager SourceLoader => _sourceLoader ??= new ImageSourceServiceResultManager(this);

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
        if (VirtualView is IImageSourcePart imagePart)
        {
            imagePart.UpdateIsLoading(false);
        }
    }

    private void OnImageLoadingError(object? sender, ImageLoadingErrorEventArgs e)
    {
        if (VirtualView is IImageSourcePart imagePart)
        {
            imagePart.UpdateIsLoading(false);
        }
    }

    public static void MapAspect(ImageHandler handler, IImage image)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.Aspect = image.Aspect;
        }
    }

    public static void MapIsOpaque(ImageHandler handler, IImage image)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.IsOpaque = image.IsOpaque;
        }
    }

    public static void MapSource(ImageHandler handler, IImage image)
    {
        if (handler.PlatformView == null)
        {
            return;
        }

        if (image is Image mauiImage)
        {
            if (mauiImage.WidthRequest > 0)
            {
                handler.PlatformView.WidthRequest = mauiImage.WidthRequest;
            }
            if (mauiImage.HeightRequest > 0)
            {
                handler.PlatformView.HeightRequest = mauiImage.HeightRequest;
            }
        }

        handler.SourceLoader.UpdateImageSourceAsync();
    }

    public static void MapBackground(ImageHandler handler, IImage image)
    {
        if (handler.PlatformView != null)
        {
            if (image.Background is SolidPaint solidPaint && solidPaint.Color != null)
            {
                handler.PlatformView.BackgroundColor = solidPaint.Color.ToSKColor();
            }
        }
    }

    public static void MapWidth(ImageHandler handler, IImage image)
    {
        if (handler.PlatformView != null)
        {
            if (image is Image mauiImage && mauiImage.WidthRequest > 0)
            {
                handler.PlatformView.WidthRequest = mauiImage.WidthRequest;
                Console.WriteLine($"[ImageHandler] MapWidth: {mauiImage.WidthRequest}");
            }
            else if (image.Width > 0)
            {
                handler.PlatformView.WidthRequest = image.Width;
            }
        }
    }

    public static void MapHeight(ImageHandler handler, IImage image)
    {
        if (handler.PlatformView != null)
        {
            if (image is Image mauiImage && mauiImage.HeightRequest > 0)
            {
                handler.PlatformView.HeightRequest = mauiImage.HeightRequest;
                Console.WriteLine($"[ImageHandler] MapHeight: {mauiImage.HeightRequest}");
            }
            else if (image.Height > 0)
            {
                handler.PlatformView.HeightRequest = image.Height;
            }
        }
    }
}
