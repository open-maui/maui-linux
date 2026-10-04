// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for Slider on Linux using Skia rendering.
/// Maps ISlider interface to SkiaSlider platform view.
/// </summary>
public partial class SliderHandler : LinuxViewHandler<ISlider, SkiaSlider>
{
    public static IPropertyMapper<ISlider, SliderHandler> Mapper = new PropertyMapper<ISlider, SliderHandler>(ViewHandler.ViewMapper)
    {
        [nameof(IRange.Minimum)] = MapMinimum,
        [nameof(IRange.Maximum)] = MapMaximum,
        [nameof(IRange.Value)] = MapValue,
        [nameof(ISlider.MinimumTrackColor)] = MapMinimumTrackColor,
        [nameof(ISlider.MaximumTrackColor)] = MapMaximumTrackColor,
        [nameof(ISlider.ThumbColor)] = MapThumbColor,
        [nameof(ISlider.ThumbImageSource)] = MapThumbImageSource,
        [nameof(IView.Background)] = MapBackground,
        [nameof(IView.IsEnabled)] = MapIsEnabled,
    };

    public static CommandMapper<ISlider, SliderHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
    };

    public SliderHandler() : base(Mapper, CommandMapper)
    {
    }

    public SliderHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaSlider CreatePlatformView()
    {
        return new SkiaSlider();
    }

    protected override void ConnectHandler(SkiaSlider platformView)
    {
        base.ConnectHandler(platformView);
        VisualStateBridge.Attach(VirtualView, platformView);
        platformView.ValueChanged += OnValueChanged;
        platformView.DragStarted += OnDragStarted;
        platformView.DragCompleted += OnDragCompleted;

        // Sync properties that may have been set before handler connection
        if (VirtualView != null)
        {
            MapMinimum(this, VirtualView);
            MapMaximum(this, VirtualView);
            MapValue(this, VirtualView);
            MapIsEnabled(this, VirtualView);
        }
    }

    protected override void DisconnectHandler(SkiaSlider platformView)
    {
        platformView.ValueChanged -= OnValueChanged;
        platformView.DragStarted -= OnDragStarted;
        platformView.DragCompleted -= OnDragCompleted;
        ReleaseThumbImage(platformView);
        VisualStateBridge.Detach(platformView);
        base.DisconnectHandler(platformView);
    }

    private void OnValueChanged(object? sender, ValueChangedEventArgs e)
    {
        if (VirtualView is null || PlatformView is null) return;

        if (Math.Abs(VirtualView.Value - e.NewValue) > 0.0001)
        {
            VirtualView.Value = e.NewValue;
        }
    }

    private void OnDragStarted(object? sender, EventArgs e)
    {
        VirtualView?.DragStarted();
    }

    private void OnDragCompleted(object? sender, EventArgs e)
    {
        VirtualView?.DragCompleted();
    }

    public static void MapMinimum(SliderHandler handler, ISlider slider)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Minimum = slider.Minimum;
    }

    public static void MapMaximum(SliderHandler handler, ISlider slider)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Maximum = slider.Maximum;
    }

    public static void MapValue(SliderHandler handler, ISlider slider)
    {
        if (handler.PlatformView is null) return;

        if (Math.Abs(handler.PlatformView.Value - slider.Value) > 0.0001)
            handler.PlatformView.Value = slider.Value;
    }

    public static void MapMinimumTrackColor(SliderHandler handler, ISlider slider)
    {
        if (handler.PlatformView is null) return;

        if (slider.MinimumTrackColor is not null)
            handler.PlatformView.MinimumTrackColor = slider.MinimumTrackColor;
    }

    public static void MapMaximumTrackColor(SliderHandler handler, ISlider slider)
    {
        if (handler.PlatformView is null) return;

        if (slider.MaximumTrackColor is not null)
            handler.PlatformView.MaximumTrackColor = slider.MaximumTrackColor;
    }

    public static void MapThumbColor(SliderHandler handler, ISlider slider)
    {
        if (handler.PlatformView is null) return;

        if (slider.ThumbColor is not null)
            handler.PlatformView.ThumbColor = slider.ThumbColor;
    }

    public static void MapBackground(SliderHandler handler, ISlider slider)
    {
        if (handler.PlatformView is null) return;

        if (slider.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.BackgroundColor = solidPaint.Color;
        }
    }

    public static void MapIsEnabled(SliderHandler handler, ISlider slider)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsEnabled = slider.IsEnabled;
        handler.PlatformView.Invalidate();
    }

    private CancellationTokenSource? _thumbImageLoad;
    private IImageSourceServiceResult<SKBitmap>? _thumbImageResult;

    /// <summary>
    /// The thumb's image (Slider.ThumbImageSource), loaded through its image-source service (file,
    /// font, URI, stream, or an app's own source) as MAUI's Windows handler loads it; none draws
    /// the circle thumb.
    /// </summary>
    public static void MapThumbImageSource(SliderHandler handler, ISlider slider) =>
        _ = handler.LoadThumbImageAsync(slider.ThumbImageSource);

    private async Task LoadThumbImageAsync(IImageSource? source)
    {
        ReleaseThumbImage(PlatformView);
        if (PlatformView is not { } platform || source == null || (source is Microsoft.Maui.Controls.ImageSource { IsEmpty: true }))
            return;
        var load = new CancellationTokenSource();
        _thumbImageLoad = load;
        try
        {
            var result = await Microsoft.Maui.Platform.Linux.Services.LinuxImageSourceServices.LoadAsync(
                MauiContext?.Services ?? Microsoft.Maui.Platform.Linux.Services.LinuxImageSourceServices.AppServices, source, Math.Max(1f, platform.DeviceScale), Size.Zero, load.Token);
            if (load.IsCancellationRequested || !ReferenceEquals(_thumbImageLoad, load))
            {
                result?.Dispose();
                return;
            }
            _thumbImageResult = result;
            platform.ThumbImage = result?.Value;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Microsoft.Maui.Platform.Linux.Services.DiagnosticLog.Error("SliderHandler", "Loading the thumb image failed", ex);
        }
    }

    private void ReleaseThumbImage(SkiaSlider? platform)
    {
        _thumbImageLoad?.Cancel();
        _thumbImageLoad = null;
        if (platform != null)
            platform.ThumbImage = null;
        _thumbImageResult?.Dispose();
        _thumbImageResult = null;
    }

}
