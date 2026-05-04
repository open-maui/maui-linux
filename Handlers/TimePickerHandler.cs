// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for TimePicker on Linux using Skia rendering.
/// </summary>
public partial class TimePickerHandler : ViewHandler<ITimePicker, SkiaTimePicker>
{
    public static IPropertyMapper<ITimePicker, TimePickerHandler> Mapper =
        new PropertyMapper<ITimePicker, TimePickerHandler>(ViewHandler.ViewMapper)
        {
            [nameof(ITimePicker.Time)] = MapTime,
            [nameof(ITimePicker.Format)] = MapFormat,
            [nameof(ITimePicker.TextColor)] = MapTextColor,
            [nameof(ITimePicker.CharacterSpacing)] = MapCharacterSpacing,
            [nameof(ITextStyle.Font)] = MapFont,
            [nameof(IView.Background)] = MapBackground,
        };

    public static CommandMapper<ITimePicker, TimePickerHandler> CommandMapper =
        new(ViewHandler.ViewCommandMapper)
        {
        };

    public TimePickerHandler() : base(Mapper, CommandMapper)
    {
    }

    public TimePickerHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaTimePicker CreatePlatformView()
    {
        return new SkiaTimePicker();
    }

    protected override void ConnectHandler(SkiaTimePicker platformView)
    {
        base.ConnectHandler(platformView);
        platformView.TimeSelected += OnTimeSelected;
    }

    protected override void DisconnectHandler(SkiaTimePicker platformView)
    {
        platformView.TimeSelected -= OnTimeSelected;
        base.DisconnectHandler(platformView);
    }

    private void OnTimeSelected(object? sender, TimeChangedEventArgs e)
    {
        if (VirtualView is null || PlatformView is null) return;

        VirtualView.Time = e.NewTime;
    }

    public static void MapTime(TimePickerHandler handler, ITimePicker timePicker)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Time = timePicker.Time ?? TimeSpan.Zero;
    }

    public static void MapFormat(TimePickerHandler handler, ITimePicker timePicker)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Format = timePicker.Format ?? "t";
    }

    public static void MapTextColor(TimePickerHandler handler, ITimePicker timePicker)
    {
        if (handler.PlatformView is null) return;
        if (timePicker.TextColor is not null)
        {
            handler.PlatformView.TextColor = timePicker.TextColor;
        }
    }

    public static void MapCharacterSpacing(TimePickerHandler handler, ITimePicker timePicker)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.CharacterSpacing = timePicker.CharacterSpacing;
    }

    public static void MapFont(TimePickerHandler handler, ITimePicker timePicker)
    {
        if (handler.PlatformView is null) return;

        var font = timePicker.Font;
        if (font.Size > 0)
            handler.PlatformView.FontSize = font.Size;

        if (!string.IsNullOrEmpty(font.Family))
            handler.PlatformView.FontFamily = font.Family;

        // Map FontAttributes from the Font weight/slant
        var attrs = FontAttributes.None;
        if (font.Weight >= FontWeight.Bold)
            attrs |= FontAttributes.Bold;
        handler.PlatformView.FontAttributes = attrs;
    }

    public static void MapBackground(TimePickerHandler handler, ITimePicker timePicker)
    {
        if (handler.PlatformView is null) return;

        if (timePicker.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.BackgroundColor = solidPaint.Color;
        }
    }
}
