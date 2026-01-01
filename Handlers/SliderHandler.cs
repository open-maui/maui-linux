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
public partial class SliderHandler : ViewHandler<ISlider, SkiaSlider>
{
    public static IPropertyMapper<ISlider, SliderHandler> Mapper = new PropertyMapper<ISlider, SliderHandler>(ViewHandler.ViewMapper)
    {
        [nameof(IRange.Minimum)] = MapMinimum,
        [nameof(IRange.Maximum)] = MapMaximum,
        [nameof(IRange.Value)] = MapValue,
        [nameof(ISlider.MinimumTrackColor)] = MapMinimumTrackColor,
        [nameof(ISlider.MaximumTrackColor)] = MapMaximumTrackColor,
        [nameof(ISlider.ThumbColor)] = MapThumbColor,
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
        base.DisconnectHandler(platformView);
    }

    private void OnValueChanged(object? sender, SliderValueChangedEventArgs e)
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

        // MinimumTrackColor maps to ActiveTrackColor (the filled portion)
        if (slider.MinimumTrackColor is not null)
            handler.PlatformView.ActiveTrackColor = slider.MinimumTrackColor.ToSKColor();
    }

    public static void MapMaximumTrackColor(SliderHandler handler, ISlider slider)
    {
        if (handler.PlatformView is null) return;

        // MaximumTrackColor maps to TrackColor (the unfilled portion)
        if (slider.MaximumTrackColor is not null)
            handler.PlatformView.TrackColor = slider.MaximumTrackColor.ToSKColor();
    }

    public static void MapThumbColor(SliderHandler handler, ISlider slider)
    {
        if (handler.PlatformView is null) return;

        if (slider.ThumbColor is not null)
            handler.PlatformView.ThumbColor = slider.ThumbColor.ToSKColor();
    }

    public static void MapBackground(SliderHandler handler, ISlider slider)
    {
        if (handler.PlatformView is null) return;

        if (slider.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.BackgroundColor = solidPaint.Color.ToSKColor();
        }
    }

    public static void MapIsEnabled(SliderHandler handler, ISlider slider)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsEnabled = slider.IsEnabled;
        handler.PlatformView.Invalidate();
    }
}
