// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for Switch on Linux using Skia rendering.
/// Maps ISwitch interface to SkiaSwitch platform view.
/// </summary>
public partial class SwitchHandler : ViewHandler<ISwitch, SkiaSwitch>
{
    public static IPropertyMapper<ISwitch, SwitchHandler> Mapper = new PropertyMapper<ISwitch, SwitchHandler>(ViewHandler.ViewMapper)
    {
        [nameof(ISwitch.IsOn)] = MapIsOn,
        [nameof(ISwitch.TrackColor)] = MapTrackColor,
        [nameof(ISwitch.ThumbColor)] = MapThumbColor,
        [nameof(IView.Background)] = MapBackground,
        [nameof(IView.IsEnabled)] = MapIsEnabled,
    };

    public static CommandMapper<ISwitch, SwitchHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
    };

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

    private void OnToggled(object? sender, Platform.ToggledEventArgs e)
    {
        if (VirtualView is not null && VirtualView.IsOn != e.Value)
        {
            VirtualView.IsOn = e.Value;
        }
    }

    public static void MapIsOn(SwitchHandler handler, ISwitch @switch)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsOn = @switch.IsOn;
    }

    public static void MapTrackColor(SwitchHandler handler, ISwitch @switch)
    {
        if (handler.PlatformView is null) return;

        // TrackColor sets the On track color (MAUI's OnColor)
        if (@switch.TrackColor is not null)
        {
            handler.PlatformView.OnTrackColor = @switch.TrackColor;
            // Off track is a lighter/desaturated version
            handler.PlatformView.OffTrackColor = @switch.TrackColor.WithAlpha(0.5f);
        }
    }

    public static void MapThumbColor(SwitchHandler handler, ISwitch @switch)
    {
        if (handler.PlatformView is null) return;

        if (@switch.ThumbColor is not null)
            handler.PlatformView.ThumbColor = @switch.ThumbColor;
    }

    public static void MapBackground(SwitchHandler handler, ISwitch @switch)
    {
        if (handler.PlatformView is null) return;

        if (@switch.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            // Background color for the switch container (not the track)
            handler.PlatformView.BackgroundColor = solidPaint.Color.ToSKColor();
        }
    }

    public static void MapIsEnabled(SwitchHandler handler, ISwitch @switch)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsEnabled = @switch.IsEnabled;
    }
}
