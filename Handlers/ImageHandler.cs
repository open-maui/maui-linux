// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
