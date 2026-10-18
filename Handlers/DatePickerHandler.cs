// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using SkiaSharp;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for DatePicker on Linux using Skia rendering.
/// </summary>
public partial class DatePickerHandler : LinuxViewHandler<IDatePicker, SkiaDatePicker>, IDatePickerHandler
{
    IDatePicker IDatePickerHandler.VirtualView => VirtualView;
    object IDatePickerHandler.PlatformView => PlatformView;

    public static IPropertyMapper<IDatePicker, DatePickerHandler> Mapper =
        new PropertyMapper<IDatePicker, DatePickerHandler>(ViewHandler.ViewMapper)
        {
            [nameof(IDatePicker.Date)] = MapDate,
            [nameof(IDatePicker.MinimumDate)] = MapMinimumDate,
            [nameof(IDatePicker.MaximumDate)] = MapMaximumDate,
            [nameof(IDatePicker.Format)] = MapFormat,
            [nameof(IDatePicker.TextColor)] = MapTextColor,
            [nameof(IDatePicker.CharacterSpacing)] = MapCharacterSpacing,
            [nameof(ITextStyle.Font)] = MapFont,
            [nameof(IView.Background)] = MapBackground,
            [nameof(IDatePicker.IsOpen)] = MapIsOpen,
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
        VisualStateBridge.Attach(VirtualView, platformView);
        platformView.DateSelected += OnDateSelected;
        platformView.IsOpenChanged += OnPlatformIsOpenChanged;
    }

    protected override void DisconnectHandler(SkiaDatePicker platformView)
    {
        platformView.IsOpenChanged -= OnPlatformIsOpenChanged;
        platformView.DateSelected -= OnDateSelected;
        VisualStateBridge.Detach(platformView);
        base.DisconnectHandler(platformView);
    }

    // True while MapDate pushes the view's Date to the platform: that change is not
    // the user's pick and must not be written back (a null Date would become today).
    private bool _mappingDate;

    private void OnDateSelected(object? sender, DateChangedEventArgs e)
    {
        if (VirtualView is null || PlatformView is null || _mappingDate) return;

        VirtualView.Date = e.NewDate;
    }

    /// <summary>MAUI's <c>MapDate</c> signature; maps the property on a Linux <see cref="DatePickerHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapDate(IDatePickerHandler handler, IDatePicker datePicker)
    {
        if (handler is DatePickerHandler linuxHandler)
            MapDateCore(linuxHandler, datePicker);
    }

    [Obsolete("Use the IDatePickerHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapDate(DatePickerHandler handler, IDatePicker datePicker) => MapDateCore(handler, datePicker);

    private static void MapDateCore(DatePickerHandler handler, IDatePicker datePicker)
    {
        if (handler.PlatformView is null) return;
        handler._mappingDate = true;
        try
        {
            handler.PlatformView.Date = datePicker.Date ?? DateTime.Today;
        }
        finally
        {
            handler._mappingDate = false;
        }
        // A null Date shows no date (the calendar still opens on today).
        handler.PlatformView.HasDate = datePicker.Date.HasValue;
    }

    /// <summary>MAUI's <c>MapMinimumDate</c> signature; maps the property on a Linux <see cref="DatePickerHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapMinimumDate(IDatePickerHandler handler, IDatePicker datePicker)
    {
        if (handler is DatePickerHandler linuxHandler)
            MapMinimumDateCore(linuxHandler, datePicker);
    }

    [Obsolete("Use the IDatePickerHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapMinimumDate(DatePickerHandler handler, IDatePicker datePicker) => MapMinimumDateCore(handler, datePicker);

    private static void MapMinimumDateCore(DatePickerHandler handler, IDatePicker datePicker)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.MinimumDate = datePicker.MinimumDate ?? DateTime.MinValue;
    }

    /// <summary>MAUI's <c>MapMaximumDate</c> signature; maps the property on a Linux <see cref="DatePickerHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapMaximumDate(IDatePickerHandler handler, IDatePicker datePicker)
    {
        if (handler is DatePickerHandler linuxHandler)
            MapMaximumDateCore(linuxHandler, datePicker);
    }

    [Obsolete("Use the IDatePickerHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapMaximumDate(DatePickerHandler handler, IDatePicker datePicker) => MapMaximumDateCore(handler, datePicker);

    private static void MapMaximumDateCore(DatePickerHandler handler, IDatePicker datePicker)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.MaximumDate = datePicker.MaximumDate ?? DateTime.MaxValue;
    }

    /// <summary>MAUI's <c>MapFormat</c> signature; maps the property on a Linux <see cref="DatePickerHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapFormat(IDatePickerHandler handler, IDatePicker datePicker)
    {
        if (handler is DatePickerHandler linuxHandler)
            MapFormatCore(linuxHandler, datePicker);
    }

    [Obsolete("Use the IDatePickerHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapFormat(DatePickerHandler handler, IDatePicker datePicker) => MapFormatCore(handler, datePicker);

    private static void MapFormatCore(DatePickerHandler handler, IDatePicker datePicker)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Format = datePicker.Format ?? "d";
    }

    /// <summary>MAUI's <c>MapTextColor</c> signature; maps the property on a Linux <see cref="DatePickerHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapTextColor(IDatePickerHandler handler, IDatePicker datePicker)
    {
        if (handler is DatePickerHandler linuxHandler)
            MapTextColorCore(linuxHandler, datePicker);
    }

    [Obsolete("Use the IDatePickerHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapTextColor(DatePickerHandler handler, IDatePicker datePicker) => MapTextColorCore(handler, datePicker);

    private static void MapTextColorCore(DatePickerHandler handler, IDatePicker datePicker)
    {
        if (handler.PlatformView is null) return;
        if (datePicker.TextColor is not null)
        {
            handler.PlatformView.TextColor = datePicker.TextColor;
        }
    }

    /// <summary>MAUI's <c>MapCharacterSpacing</c> signature; maps the property on a Linux <see cref="DatePickerHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapCharacterSpacing(IDatePickerHandler handler, IDatePicker datePicker)
    {
        if (handler is DatePickerHandler linuxHandler)
            MapCharacterSpacingCore(linuxHandler, datePicker);
    }

    [Obsolete("Use the IDatePickerHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapCharacterSpacing(DatePickerHandler handler, IDatePicker datePicker) => MapCharacterSpacingCore(handler, datePicker);

    private static void MapCharacterSpacingCore(DatePickerHandler handler, IDatePicker datePicker)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.CharacterSpacing = datePicker.CharacterSpacing;
    }

    /// <summary>MAUI's <c>MapFont</c> signature; maps the property on a Linux <see cref="DatePickerHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapFont(IDatePickerHandler handler, IDatePicker datePicker)
    {
        if (handler is DatePickerHandler linuxHandler)
            MapFontCore(linuxHandler, datePicker);
    }

    [Obsolete("Use the IDatePickerHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapFont(DatePickerHandler handler, IDatePicker datePicker) => MapFontCore(handler, datePicker);

    private static void MapFontCore(DatePickerHandler handler, IDatePicker datePicker)
    {
        if (handler.PlatformView is null) return;

        var font = datePicker.Font;
        if (font.Size > 0)
            handler.PlatformView.FontSize = font.Size;

        if (!string.IsNullOrEmpty(font.Family))
            handler.PlatformView.FontFamily = font.Family;

        // Map FontAttributes from the Font weight/slant
        var attrs = FontAttributes.None;
        if (font.Weight >= FontWeight.Bold)
            attrs |= FontAttributes.Bold;
        if (font.Slant == FontSlant.Italic || font.Slant == FontSlant.Oblique)
            attrs |= FontAttributes.Italic;
        handler.PlatformView.FontAttributes = attrs;
    }

    public static void MapBackground(DatePickerHandler handler, IDatePicker datePicker)
    {
        if (handler.PlatformView is null) return;

        if (datePicker.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.BackgroundColor = solidPaint.Color;
        }
    }

    /// <summary>
    /// Opens or closes the drop-down from code (MAUI 10's <c>IDatePicker.IsOpen</c>), as MAUI's
    /// Windows handler opens its platform picker.
    /// </summary>
    public static void MapIsOpen(DatePickerHandler handler, IDatePicker datePicker)
    {
        if (handler.PlatformView is { } platform && platform.IsOpen != datePicker.IsOpen)
            platform.IsOpen = datePicker.IsOpen;
    }

    /// <summary>The user opened or closed the drop-down: <c>IsOpen</c> follows (MAUI raises Opened/Closed).</summary>
    private void OnPlatformIsOpenChanged(object? sender, EventArgs e)
    {
        if (VirtualView is { } view && PlatformView is { } platform && view.IsOpen != platform.IsOpen)
            view.IsOpen = platform.IsOpen;
    }
}
