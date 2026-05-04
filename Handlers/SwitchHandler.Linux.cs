// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Linux handler for Switch control.
/// </summary>
public partial class SwitchHandler : ViewHandler<ISwitch, SkiaSwitch>
{
    public static IPropertyMapper<ISwitch, SwitchHandler> Mapper = new PropertyMapper<ISwitch, SwitchHandler>(ViewHandler.ViewMapper)
    {
        [nameof(ISwitch.IsOn)] = MapIsOn,
        [nameof(ISwitch.TrackColor)] = MapTrackColor,
        [nameof(ISwitch.ThumbColor)] = MapThumbColor,
        [nameof(IView.IsEnabled)] = MapIsEnabled,
        [nameof(IView.Background)] = MapBackground,
        ["BackgroundColor"] = MapBackgroundColor,
    };

    public static CommandMapper<ISwitch, SwitchHandler> CommandMapper = new(ViewHandler.ViewCommandMapper);

    public SwitchHandler() : base(Mapper, CommandMapper) { }

    protected override SkiaSwitch CreatePlatformView() => new SkiaSwitch();

    protected override void ConnectHandler(SkiaSwitch platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Toggled += OnToggled;
    }

    protected override void DisconnectHandler(SkiaSwitch platformView)
    {
        platformView.Toggled -= OnToggled;
        base.DisconnectHandler(platformView);
    }

    private void OnToggled(object? sender, ToggledEventArgs e)
    {
        if (VirtualView != null && VirtualView.IsOn != e.Value)
        {
            VirtualView.IsOn = e.Value;
        }
    }

    public static void MapIsOn(SwitchHandler handler, ISwitch @switch)
    {
        if (handler.PlatformView.IsOn != @switch.IsOn)
        {
            handler.PlatformView.IsOn = @switch.IsOn;
        }
    }

    public static void MapTrackColor(SwitchHandler handler, ISwitch @switch)
    {
        if (@switch.TrackColor != null)
            handler.PlatformView.OnTrackColor = @switch.TrackColor.ToSKColor();
        handler.PlatformView.Invalidate();
    }

    public static void MapThumbColor(SwitchHandler handler, ISwitch @switch)
    {
        if (@switch.ThumbColor != null)
            handler.PlatformView.ThumbColor = @switch.ThumbColor.ToSKColor();
        handler.PlatformView.Invalidate();
    }

    public static void MapIsEnabled(SwitchHandler handler, ISwitch @switch)
    {
        handler.PlatformView.IsEnabled = @switch.IsEnabled;
        handler.PlatformView.Invalidate();
    }

    public static void MapBackground(SwitchHandler handler, ISwitch @switch)
    {
        if (@switch.Background is SolidColorBrush solidBrush && solidBrush.Color != null)
        {
            handler.PlatformView.BackgroundColor = solidBrush.Color.ToSKColor();
            handler.PlatformView.Invalidate();
        }
    }

    public static void MapBackgroundColor(SwitchHandler handler, ISwitch @switch)
    {
        if (@switch is Microsoft.Maui.Controls.VisualElement ve && ve.BackgroundColor != null)
        {
            handler.PlatformView.BackgroundColor = ve.BackgroundColor.ToSKColor();
            handler.PlatformView.Invalidate();
        }
    }
}
