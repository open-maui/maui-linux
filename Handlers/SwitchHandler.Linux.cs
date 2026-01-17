// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Linux handler for Switch control.
/// </summary>
public class SwitchHandler : ViewHandler<ISwitch, SkiaSwitch>
{
    public static IPropertyMapper<ISwitch, SwitchHandler> Mapper = new PropertyMapper<ISwitch, SwitchHandler>(ViewHandler.ViewMapper)
    {
        ["IsOn"] = MapIsOn,
        ["TrackColor"] = MapTrackColor,
        ["ThumbColor"] = MapThumbColor,
        ["Background"] = MapBackground,
        ["IsEnabled"] = MapIsEnabled
    };

    public static CommandMapper<ISwitch, SwitchHandler> CommandMapper = new(ViewHandler.ViewCommandMapper);

    public SwitchHandler() : base(Mapper, CommandMapper)
    {
    }

    public SwitchHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaSwitch CreatePlatformView()
    {
        return new SkiaSwitch();
    }

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
        if (handler.PlatformView != null)
        {
            handler.PlatformView.IsOn = @switch.IsOn;
        }
    }

    public static void MapTrackColor(SwitchHandler handler, ISwitch @switch)
    {
        if (handler.PlatformView != null && @switch.TrackColor != null)
        {
            var onTrackColor = @switch.TrackColor.ToSKColor();
            handler.PlatformView.OnTrackColor = onTrackColor;
            handler.PlatformView.OffTrackColor = onTrackColor.WithAlpha(128);
        }
    }

    public static void MapThumbColor(SwitchHandler handler, ISwitch @switch)
    {
        if (handler.PlatformView != null && @switch.ThumbColor != null)
        {
            handler.PlatformView.ThumbColor = @switch.ThumbColor.ToSKColor();
        }
    }

    public static void MapBackground(SwitchHandler handler, ISwitch @switch)
    {
        if (handler.PlatformView != null)
        {
            if (@switch.Background is SolidPaint solidPaint && solidPaint.Color != null)
            {
                handler.PlatformView.BackgroundColor = solidPaint.Color;
            }
        }
    }

    public static void MapIsEnabled(SwitchHandler handler, ISwitch @switch)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.IsEnabled = @switch.IsEnabled;
        }
    }
}
