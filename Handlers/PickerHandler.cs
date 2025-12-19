// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for Picker on Linux using Skia rendering.
/// </summary>
public partial class PickerHandler : ViewHandler<IPicker, SkiaPicker>
{
    public static IPropertyMapper<IPicker, PickerHandler> Mapper =
        new PropertyMapper<IPicker, PickerHandler>(ViewHandler.ViewMapper)
        {
            [nameof(IPicker.Title)] = MapTitle,
            [nameof(IPicker.TitleColor)] = MapTitleColor,
            [nameof(IPicker.SelectedIndex)] = MapSelectedIndex,
            [nameof(IPicker.TextColor)] = MapTextColor,
            [nameof(IPicker.CharacterSpacing)] = MapCharacterSpacing,
            [nameof(IPicker.HorizontalTextAlignment)] = MapHorizontalTextAlignment,
            [nameof(IPicker.VerticalTextAlignment)] = MapVerticalTextAlignment,
            [nameof(IView.Background)] = MapBackground,
        };

    public static CommandMapper<IPicker, PickerHandler> CommandMapper =
        new(ViewHandler.ViewCommandMapper)
        {
        };

    public PickerHandler() : base(Mapper, CommandMapper)
    {
    }

    public PickerHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaPicker CreatePlatformView()
    {
        return new SkiaPicker();
    }

    protected override void ConnectHandler(SkiaPicker platformView)
    {
        base.ConnectHandler(platformView);
        platformView.SelectedIndexChanged += OnSelectedIndexChanged;

        // Load items
        ReloadItems();
    }

    protected override void DisconnectHandler(SkiaPicker platformView)
    {
        platformView.SelectedIndexChanged -= OnSelectedIndexChanged;
        base.DisconnectHandler(platformView);
    }

    private void OnSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (VirtualView is null || PlatformView is null) return;

        VirtualView.SelectedIndex = PlatformView.SelectedIndex;
    }

    private void ReloadItems()
    {
        if (PlatformView is null || VirtualView is null) return;

        var items = VirtualView.GetItemsAsArray();
        PlatformView.SetItems(items.Select(i => i?.ToString() ?? ""));
    }

    public static void MapTitle(PickerHandler handler, IPicker picker)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Title = picker.Title ?? "";
    }

    public static void MapTitleColor(PickerHandler handler, IPicker picker)
    {
        if (handler.PlatformView is null) return;
        if (picker.TitleColor is not null)
        {
            handler.PlatformView.TitleColor = picker.TitleColor.ToSKColor();
        }
    }

    public static void MapSelectedIndex(PickerHandler handler, IPicker picker)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.SelectedIndex = picker.SelectedIndex;
    }

    public static void MapTextColor(PickerHandler handler, IPicker picker)
    {
        if (handler.PlatformView is null) return;
        if (picker.TextColor is not null)
        {
            handler.PlatformView.TextColor = picker.TextColor.ToSKColor();
        }
    }

    public static void MapCharacterSpacing(PickerHandler handler, IPicker picker)
    {
        // Character spacing could be implemented with custom text rendering
    }

    public static void MapHorizontalTextAlignment(PickerHandler handler, IPicker picker)
    {
        // Text alignment would require changes to SkiaPicker drawing
    }

    public static void MapVerticalTextAlignment(PickerHandler handler, IPicker picker)
    {
        // Text alignment would require changes to SkiaPicker drawing
    }

    public static void MapBackground(PickerHandler handler, IPicker picker)
    {
        if (handler.PlatformView is null) return;

        if (picker.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.BackgroundColor = solidPaint.Color.ToSKColor();
        }
    }
}
