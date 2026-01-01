// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for DatePicker on Linux using Skia rendering.
/// </summary>
public partial class DatePickerHandler : ViewHandler<IDatePicker, SkiaDatePicker>
{
    public static IPropertyMapper<IDatePicker, DatePickerHandler> Mapper =
        new PropertyMapper<IDatePicker, DatePickerHandler>(ViewHandler.ViewMapper)
        {
            [nameof(IDatePicker.Date)] = MapDate,
            [nameof(IDatePicker.MinimumDate)] = MapMinimumDate,
            [nameof(IDatePicker.MaximumDate)] = MapMaximumDate,
            [nameof(IDatePicker.Format)] = MapFormat,
            [nameof(IDatePicker.TextColor)] = MapTextColor,
            [nameof(IDatePicker.CharacterSpacing)] = MapCharacterSpacing,
            [nameof(IView.Background)] = MapBackground,
        };

    public static CommandMapper<IDatePicker, DatePickerHandler> CommandMapper =
        new(ViewHandler.ViewCommandMapper)
        {
        };

    public DatePickerHandler() : base(Mapper, CommandMapper)
    {
    }

    public DatePickerHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaDatePicker CreatePlatformView()
    {
        return new SkiaDatePicker();
    }

    protected override void ConnectHandler(SkiaDatePicker platformView)
    {
        base.ConnectHandler(platformView);
        platformView.DateSelected += OnDateSelected;
    }

    protected override void DisconnectHandler(SkiaDatePicker platformView)
    {
        platformView.DateSelected -= OnDateSelected;
        base.DisconnectHandler(platformView);
    }

    private void OnDateSelected(object? sender, EventArgs e)
    {
        if (VirtualView is null || PlatformView is null) return;

        VirtualView.Date = PlatformView.Date;
    }

    public static void MapDate(DatePickerHandler handler, IDatePicker datePicker)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Date = datePicker.Date;
    }

    public static void MapMinimumDate(DatePickerHandler handler, IDatePicker datePicker)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.MinimumDate = datePicker.MinimumDate;
    }

    public static void MapMaximumDate(DatePickerHandler handler, IDatePicker datePicker)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.MaximumDate = datePicker.MaximumDate;
    }

    public static void MapFormat(DatePickerHandler handler, IDatePicker datePicker)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Format = datePicker.Format ?? "d";
    }

    public static void MapTextColor(DatePickerHandler handler, IDatePicker datePicker)
    {
        if (handler.PlatformView is null) return;
        if (datePicker.TextColor is not null)
        {
            handler.PlatformView.TextColor = datePicker.TextColor.ToSKColor();
        }
    }

    public static void MapCharacterSpacing(DatePickerHandler handler, IDatePicker datePicker)
    {
        // Character spacing would require custom text rendering
    }

    public static void MapBackground(DatePickerHandler handler, IDatePicker datePicker)
    {
        if (handler.PlatformView is null) return;

        if (datePicker.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.BackgroundColor = solidPaint.Color.ToSKColor();
        }
    }
}
