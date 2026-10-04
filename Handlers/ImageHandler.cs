// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using Microsoft.Maui.Platform.Linux.Rendering;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for Image on Linux using Skia rendering.
/// Maps IImage interface to SkiaImage platform view.
/// IImage has: Aspect, IsOpaque (inherits from IImageSourcePart)
/// </summary>
public partial class ImageHandler : LinuxViewHandler<IImage, SkiaImage>
{
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

    public static void MapIsAnimationPlaying(ImageHandler handler, IImage image)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsAnimationPlaying = image.IsAnimationPlaying;
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

        private async Task<Exception?> LoadAsync(IImageSource source, CancellationToken token)
        {
            var view = _handler.PlatformView;
            if (view is null)
                return null;

            switch (source)
            {
                case IFileImageSource fileSource:
                    if (string.IsNullOrEmpty(fileSource.File))
                    {
                        view.ClearImage();
                        return null;
                    }
                    return await CaptureAsync(view, () => view.LoadFromFileAsync(fileSource.File));

                case IUriImageSource uriSource:
                    if (uriSource.Uri is null)
                    {
                        view.ClearImage();
                        return null;
                    }
                    return await CaptureAsync(view, () => view.LoadFromUriAsync(uriSource.Uri));

                case IStreamImageSource streamSource:
                    using (var stream = await streamSource.GetStreamAsync(token))
                    {
                        token.ThrowIfCancellationRequested();
                        if (stream is null)
                            return new InvalidOperationException("The stream image source returned no stream.");
                        return await CaptureAsync(view, () => view.LoadFromStreamAsync(stream));
                    }

                case FontImageSource fontSource:
                    var bitmap = RenderFontImageSource(fontSource, view.WidthRequest, view.HeightRequest);
                    if (bitmap is null)
                    {
                        view.ClearImage();
                        return null;
                    }
                    return await CaptureAsync(view, () =>
                    {
                        view.LoadFromBitmap(bitmap);
                        return Task.CompletedTask;
                    });

                default:
                    return new NotSupportedException($"Image source type {source.GetType().Name} is not supported on Linux.");
            }
        }

        private static Task<Exception?> CaptureAsync(SkiaImage view, Func<Task> load) =>
            ImageSourcePartLoading.CaptureErrorAsync(h => view.ImageLoadingError += h, h => view.ImageLoadingError -= h, load);

        internal static SKBitmap? RenderFontImageSource(FontImageSource fontSource, double requestedWidth, double requestedHeight)
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
                // Icon fonts registered through ConfigureFonts (AddFont("fa-solid.otf",
                // "FontAwesome")) resolve by alias or family name through the same
                // registrar the label renderer uses.
                typeface = LinuxFontRegistrar.Instance.TryGetTypeface(fontSource.FontFamily, SKFontStyle.Normal);
            }

            if (typeface == null && !string.IsNullOrEmpty(fontSource.FontFamily))
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
            using SKFont font = SkiaFontFactory.Create(typeface, fontSize);
            using SKPaint paint = new SKPaint
            {
                Color = color,
                IsAntialias = true
            };

            // symbol: ink-centering intentional (FontImageSource icon glyph is optically centered by its ink bounds)
            font.MeasureText(glyph, out SKRect bounds, paint);
            float x = size / 2f;
            float y = (size - bounds.Top - bounds.Bottom) / 2f;
            canvas.DrawText(glyph, x, y, SKTextAlign.Center, font, paint);

            return bitmap;
        }
    }
}
