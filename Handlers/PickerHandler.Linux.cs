// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Linux handler for Picker control.
/// </summary>
public class PickerHandler : ViewHandler<IPicker, SkiaPicker>
{
    public static IPropertyMapper<IPicker, PickerHandler> Mapper = new PropertyMapper<IPicker, PickerHandler>(ViewHandler.ViewMapper)
    {
        ["Title"] = MapTitle,
        ["TitleColor"] = MapTitleColor,
        ["SelectedIndex"] = MapSelectedIndex,
        ["TextColor"] = MapTextColor,
        ["Font"] = MapFont,
        ["CharacterSpacing"] = MapCharacterSpacing,
        ["HorizontalTextAlignment"] = MapHorizontalTextAlignment,
        ["VerticalTextAlignment"] = MapVerticalTextAlignment,
        ["Background"] = MapBackground,
        ["ItemsSource"] = MapItemsSource
    };

    public static CommandMapper<IPicker, PickerHandler> CommandMapper = new(ViewHandler.ViewCommandMapper);

    private INotifyCollectionChanged? _itemsCollection;

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

        if (VirtualView is Picker picker && picker.Items is INotifyCollectionChanged itemsCollection)
        {
            _itemsCollection = itemsCollection;
            _itemsCollection.CollectionChanged += OnItemsCollectionChanged;
        }

        ReloadItems();
    }

    protected override void DisconnectHandler(SkiaPicker platformView)
    {
        platformView.SelectedIndexChanged -= OnSelectedIndexChanged;

        if (_itemsCollection != null)
        {
            _itemsCollection.CollectionChanged -= OnItemsCollectionChanged;
            _itemsCollection = null;
        }

        base.DisconnectHandler(platformView);
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ReloadItems();
    }

    private void OnSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (VirtualView != null && PlatformView != null)
        {
            VirtualView.SelectedIndex = PlatformView.SelectedIndex;
        }
    }

    private void ReloadItems()
    {
        if (PlatformView != null && VirtualView != null)
        {
            var items = VirtualView.GetItemsAsArray();
            PlatformView.SetItems(items.Select(i => i?.ToString() ?? ""));
        }
    }

    public static void MapTitle(PickerHandler handler, IPicker picker)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.Title = picker.Title ?? "";
        }
    }

    public static void MapTitleColor(PickerHandler handler, IPicker picker)
    {
        if (handler.PlatformView != null && picker.TitleColor != null)
        {
            handler.PlatformView.TitleColor = picker.TitleColor.ToSKColor();
        }
    }

    public static void MapSelectedIndex(PickerHandler handler, IPicker picker)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.SelectedIndex = picker.SelectedIndex;
        }
    }

    public static void MapTextColor(PickerHandler handler, IPicker picker)
    {
        if (handler.PlatformView != null && picker.TextColor != null)
        {
            handler.PlatformView.TextColor = picker.TextColor.ToSKColor();
        }
    }

    public static void MapFont(PickerHandler handler, IPicker picker)
    {
        if (handler.PlatformView != null)
        {
            var font = picker.Font;
            if (!string.IsNullOrEmpty(font.Family))
            {
                handler.PlatformView.FontFamily = font.Family;
            }
            if (font.Size > 0)
            {
                handler.PlatformView.FontSize = (float)font.Size;
            }
            handler.PlatformView.Invalidate();
        }
    }

    public static void MapCharacterSpacing(PickerHandler handler, IPicker picker)
    {
        // Character spacing not implemented
    }

    public static void MapHorizontalTextAlignment(PickerHandler handler, IPicker picker)
    {
        // Horizontal text alignment not implemented
    }

    public static void MapVerticalTextAlignment(PickerHandler handler, IPicker picker)
    {
        // Vertical text alignment not implemented
    }

    public static void MapBackground(PickerHandler handler, IPicker picker)
    {
        if (handler.PlatformView != null)
        {
            if (picker.Background is SolidPaint solidPaint && solidPaint.Color != null)
            {
                handler.PlatformView.BackgroundColor = solidPaint.Color;
            }
        }
    }

    public static void MapItemsSource(PickerHandler handler, IPicker picker)
    {
        handler.ReloadItems();
    }
}
