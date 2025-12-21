// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Linux handler for Slider control.
/// </summary>
public partial class SliderHandler : ViewHandler<ISlider, SkiaSlider>
{
    public static IPropertyMapper<ISlider, SliderHandler> Mapper = new PropertyMapper<ISlider, SliderHandler>(ViewHandler.ViewMapper)
    {
        [nameof(ISlider.Minimum)] = MapMinimum,
        [nameof(ISlider.Maximum)] = MapMaximum,
        [nameof(ISlider.Value)] = MapValue,
        [nameof(ISlider.MinimumTrackColor)] = MapMinimumTrackColor,
        [nameof(ISlider.MaximumTrackColor)] = MapMaximumTrackColor,
        [nameof(ISlider.ThumbColor)] = MapThumbColor,
        [nameof(IView.IsEnabled)] = MapIsEnabled,
        [nameof(IView.Background)] = MapBackground,
        ["BackgroundColor"] = MapBackgroundColor,
    };

    public static CommandMapper<ISlider, SliderHandler> CommandMapper = new(ViewHandler.ViewCommandMapper);

    public SliderHandler() : base(Mapper, CommandMapper) { }

    protected override SkiaSlider CreatePlatformView() => new SkiaSlider();

    protected override void ConnectHandler(SkiaSlider platformView)
    {
        base.ConnectHandler(platformView);
        platformView.ValueChanged += OnValueChanged;
        platformView.DragStarted += OnDragStarted;
        platformView.DragCompleted += OnDragCompleted;
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
        if (VirtualView != null && Math.Abs(VirtualView.Value - e.NewValue) > 0.001)
        {
            VirtualView.Value = e.NewValue;
        }
    }

    private void OnDragStarted(object? sender, EventArgs e) => VirtualView?.DragStarted();
    private void OnDragCompleted(object? sender, EventArgs e) => VirtualView?.DragCompleted();

    public static void MapMinimum(SliderHandler handler, ISlider slider)
    {
        handler.PlatformView.Minimum = slider.Minimum;
        handler.PlatformView.Invalidate();
    }

    public static void MapMaximum(SliderHandler handler, ISlider slider)
    {
        handler.PlatformView.Maximum = slider.Maximum;
        handler.PlatformView.Invalidate();
    }

    public static void MapValue(SliderHandler handler, ISlider slider)
    {
        if (Math.Abs(handler.PlatformView.Value - slider.Value) > 0.001)
        {
            handler.PlatformView.Value = slider.Value;
        }
    }

    public static void MapMinimumTrackColor(SliderHandler handler, ISlider slider)
    {
        if (slider.MinimumTrackColor != null)
            handler.PlatformView.ActiveTrackColor = slider.MinimumTrackColor.ToSKColor();
        handler.PlatformView.Invalidate();
    }

    public static void MapMaximumTrackColor(SliderHandler handler, ISlider slider)
    {
        if (slider.MaximumTrackColor != null)
            handler.PlatformView.TrackColor = slider.MaximumTrackColor.ToSKColor();
        handler.PlatformView.Invalidate();
    }

    public static void MapThumbColor(SliderHandler handler, ISlider slider)
    {
        if (slider.ThumbColor != null)
            handler.PlatformView.ThumbColor = slider.ThumbColor.ToSKColor();
        handler.PlatformView.Invalidate();
    }

    public static void MapIsEnabled(SliderHandler handler, ISlider slider)
    {
        handler.PlatformView.IsEnabled = slider.IsEnabled;
        handler.PlatformView.Invalidate();
    }

    public static void MapBackground(SliderHandler handler, ISlider slider)
    {
        if (slider.Background is SolidColorBrush solidBrush && solidBrush.Color != null)
        {
            handler.PlatformView.BackgroundColor = solidBrush.Color.ToSKColor();
            handler.PlatformView.Invalidate();
        }
    }

    public static void MapBackgroundColor(SliderHandler handler, ISlider slider)
    {
        if (slider is Microsoft.Maui.Controls.VisualElement ve && ve.BackgroundColor != null)
        {
            handler.PlatformView.BackgroundColor = ve.BackgroundColor.ToSKColor();
            handler.PlatformView.Invalidate();
        }
    }
}
