// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for ImageButton on Linux using Skia rendering.
/// Maps IImageButton interface to SkiaImageButton platform view.
/// IImageButton extends: IImage, IView, IButtonStroke, IPadding
/// </summary>
public partial class ImageButtonHandler : LinuxViewHandler<IImageButton, SkiaImageButton>, IImageButtonHandler
{
    IImageButton IImageButtonHandler.VirtualView => VirtualView;
    object IImageButtonHandler.PlatformView => PlatformView;
    IImage IImageHandler.VirtualView => VirtualView;
    object IImageHandler.PlatformView => PlatformView;

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
        _ = handler.ImageLoad.UpdateImageSourceAsync();
    }

    /// <summary>MAUI's <c>MapStrokeColor</c> signature; maps the property on a Linux <see cref="ImageButtonHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapStrokeColor(IImageButtonHandler handler, IButtonStroke buttonStroke)
    {
        if (handler is ImageButtonHandler linuxHandler && buttonStroke is IImageButton linuxView)
            MapStrokeColorCore(linuxHandler, linuxView);
    }

    [Obsolete("Use the IImageButtonHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapStrokeColor(ImageButtonHandler handler, IImageButton imageButton) => MapStrokeColorCore(handler, imageButton);

    private static void MapStrokeColorCore(ImageButtonHandler handler, IImageButton imageButton)
    {
        if (handler.PlatformView is null) return;

        if (imageButton.StrokeColor is not null)
            handler.PlatformView.StrokeColor = imageButton.StrokeColor;
    }

    /// <summary>MAUI's <c>MapStrokeThickness</c> signature; maps the property on a Linux <see cref="ImageButtonHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapStrokeThickness(IImageButtonHandler handler, IButtonStroke buttonStroke)
    {
        if (handler is ImageButtonHandler linuxHandler && buttonStroke is IImageButton linuxView)
            MapStrokeThicknessCore(linuxHandler, linuxView);
    }

    [Obsolete("Use the IImageButtonHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapStrokeThickness(ImageButtonHandler handler, IImageButton imageButton) => MapStrokeThicknessCore(handler, imageButton);

    private static void MapStrokeThicknessCore(ImageButtonHandler handler, IImageButton imageButton)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.StrokeThickness = imageButton.StrokeThickness;
    }

    /// <summary>MAUI's <c>MapCornerRadius</c> signature; maps the property on a Linux <see cref="ImageButtonHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapCornerRadius(IImageButtonHandler handler, IButtonStroke buttonStroke)
    {
        if (handler is ImageButtonHandler linuxHandler && buttonStroke is IImageButton linuxView)
            MapCornerRadiusCore(linuxHandler, linuxView);
    }

    [Obsolete("Use the IImageButtonHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapCornerRadius(ImageButtonHandler handler, IImageButton imageButton) => MapCornerRadiusCore(handler, imageButton);

    private static void MapCornerRadiusCore(ImageButtonHandler handler, IImageButton imageButton)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.CornerRadius = imageButton.CornerRadius;
    }

    /// <summary>MAUI's <c>MapPadding</c> signature; maps the property on a Linux <see cref="ImageButtonHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapPadding(IImageButtonHandler handler, IImageButton imageButton)
    {
        if (handler is ImageButtonHandler linuxHandler)
            MapPaddingCore(linuxHandler, imageButton);
    }

    [Obsolete("Use the IImageButtonHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapPadding(ImageButtonHandler handler, IImageButton imageButton) => MapPaddingCore(handler, imageButton);

    private static void MapPaddingCore(ImageButtonHandler handler, IImageButton imageButton)
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
    private ImageSourceServiceResultManager? _imageLoad;
    private ImageSourcePartLoader? _imageSourcePartLoader;

    private ImageSourceServiceResultManager ImageLoad =>
        _imageLoad ??= new ImageSourceServiceResultManager(this);

    /// <summary>
    /// MAUI's <c>IImageHandler.SourceLoader</c>: its <see cref="ImageSourcePartLoader.UpdateImageSourceAsync"/>
    /// loads the virtual view's source into the SkiaImageButton (through the image source service, with
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
        private readonly ImageButtonHandler _handler;
        private CancellationTokenSource? _cts;

        public ImageSourceServiceResultManager(ImageButtonHandler handler)
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
