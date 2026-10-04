// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for ImageButton on Linux using Skia rendering.
/// Maps IImageButton interface to SkiaImageButton platform view.
/// IImageButton extends: IImage, IView, IButtonStroke, IPadding
/// </summary>
public partial class ImageButtonHandler : LinuxViewHandler<IImageButton, SkiaImageButton>
{
    public static IPropertyMapper<IImageButton, ImageButtonHandler> Mapper = new PropertyMapper<IImageButton, ImageButtonHandler>(ViewHandler.ViewMapper)
    {
        [nameof(IImage.Aspect)] = MapAspect,
        [nameof(IImage.IsOpaque)] = MapIsOpaque,
        [nameof(IImageSourcePart.Source)] = MapSource,
        [nameof(IImageSourcePart.IsAnimationPlaying)] = MapIsAnimationPlaying,
        [nameof(IButtonStroke.StrokeColor)] = MapStrokeColor,
        [nameof(IButtonStroke.StrokeThickness)] = MapStrokeThickness,
        [nameof(IButtonStroke.CornerRadius)] = MapCornerRadius,
        [nameof(IPadding.Padding)] = MapPadding,
        [nameof(IView.Background)] = MapBackground,
        ["BackgroundColor"] = MapBackgroundColor,
        [nameof(IView.Width)] = MapWidth,
        [nameof(IView.Height)] = MapHeight,
        ["VerticalOptions"] = MapVerticalOptions,
        ["HorizontalOptions"] = MapHorizontalOptions,
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
        VisualStateBridge.Attach(VirtualView, platformView);
        platformView.Clicked += OnClicked;
        platformView.Pressed += OnPressed;
        platformView.Released += OnReleased;
        if (VirtualView is Microsoft.Maui.Controls.View view)
            ToolkitIconTint.Attach(view, color => platformView.TintColor = color);
    }

    protected override void DisconnectHandler(SkiaImageButton platformView)
    {
        platformView.Clicked -= OnClicked;
        platformView.Pressed -= OnPressed;
        platformView.Released -= OnReleased;
        if (VirtualView is Microsoft.Maui.Controls.View view)
            ToolkitIconTint.Detach(view);
        VisualStateBridge.Detach(platformView);
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

    public static void MapIsAnimationPlaying(ImageButtonHandler handler, IImageButton imageButton)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsAnimationPlaying = imageButton.IsAnimationPlaying;
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
            handler.PlatformView.StrokeColor = imageButton.StrokeColor;
    }

    public static void MapStrokeThickness(ImageButtonHandler handler, IImageButton imageButton)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.StrokeThickness = imageButton.StrokeThickness;
    }

    public static void MapCornerRadius(ImageButtonHandler handler, IImageButton imageButton)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.CornerRadius = imageButton.CornerRadius;
    }

    public static void MapPadding(ImageButtonHandler handler, IImageButton imageButton)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Padding = imageButton.Padding;
    }

    public static void MapBackground(ImageButtonHandler handler, IImageButton imageButton)
    {
        if (handler.PlatformView is null) return;

        if (imageButton.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.ImageBackgroundColor = solidPaint.Color;
        }
    }

    public static void MapBackgroundColor(ImageButtonHandler handler, IImageButton imageButton)
    {
        if (handler.PlatformView is null) return;

        if (imageButton is Microsoft.Maui.Controls.ImageButton imgBtn && imgBtn.BackgroundColor is not null)
        {
            handler.PlatformView.ImageBackgroundColor = imgBtn.BackgroundColor;
        }
    }

    public static void MapWidth(ImageButtonHandler handler, IImageButton imageButton)
    {
        if (handler.PlatformView is null) return;

        // Map WidthRequest from the MAUI ImageButton to the platform view
        if (imageButton is Microsoft.Maui.Controls.ImageButton imgBtn && imgBtn.WidthRequest > 0)
        {
            handler.PlatformView.WidthRequest = imgBtn.WidthRequest;
        }
    }

    public static void MapHeight(ImageButtonHandler handler, IImageButton imageButton)
    {
        if (handler.PlatformView is null) return;

        // Map HeightRequest from the MAUI ImageButton to the platform view
        if (imageButton is Microsoft.Maui.Controls.ImageButton imgBtn && imgBtn.HeightRequest > 0)
        {
            handler.PlatformView.HeightRequest = imgBtn.HeightRequest;
        }
    }

    public static void MapVerticalOptions(ImageButtonHandler handler, IImageButton imageButton)
    {
        if (handler.PlatformView is null) return;

        if (imageButton is Microsoft.Maui.Controls.ImageButton imgBtn)
        {
            handler.PlatformView.VerticalOptions = imgBtn.VerticalOptions;
        }
    }

    public static void MapHorizontalOptions(ImageButtonHandler handler, IImageButton imageButton)
    {
        if (handler.PlatformView is null) return;

        if (imageButton is Microsoft.Maui.Controls.ImageButton imgBtn)
        {
            handler.PlatformView.HorizontalOptions = imgBtn.HorizontalOptions;
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

            try
            {
                await ImageSourcePartLoading.RunAsync(_handler.VirtualView, _cts.Token, LoadAsync, Clear);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("ImageButtonHandler", "Image source load failed", ex);
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
    }
}
