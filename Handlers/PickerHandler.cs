// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using System.Collections.Specialized;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for Picker on Linux using Skia rendering.
/// Maps IPicker interface to SkiaPicker platform view.
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
            [nameof(ITextStyle.Font)] = MapFont,
            [nameof(IPicker.CharacterSpacing)] = MapCharacterSpacing,
            [nameof(IPicker.HorizontalTextAlignment)] = MapHorizontalTextAlignment,
            [nameof(IPicker.VerticalTextAlignment)] = MapVerticalTextAlignment,
            [nameof(IView.Background)] = MapBackground,
            [nameof(IView.IsEnabled)] = MapIsEnabled,
            [nameof(Picker.ItemsSource)] = MapItemsSource,
        };

    public static CommandMapper<IPicker, PickerHandler> CommandMapper =
        new(ViewHandler.ViewCommandMapper)
        {
        };

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

        // Subscribe to items collection changes
        if (VirtualView is Picker picker && picker.Items is INotifyCollectionChanged items)
        {
            _itemsCollection = items;
            _itemsCollection.CollectionChanged += OnItemsCollectionChanged;
        }

        // Load items and sync properties
        ReloadItems();

        if (VirtualView != null)
        {
            MapTitle(this, VirtualView);
            MapTitleColor(this, VirtualView);
            MapTextColor(this, VirtualView);
            MapSelectedIndex(this, VirtualView);
            MapIsEnabled(this, VirtualView);
        }
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

    private void OnSelectedIndexChanged(object? sender, SelectedIndexChangedEventArgs e)
    {
        if (VirtualView is null || PlatformView is null) return;

        if (VirtualView.SelectedIndex != e.NewIndex)
        {
            VirtualView.SelectedIndex = e.NewIndex;
        }
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
            handler.PlatformView.TitleColor = picker.TitleColor;
        }
    }

    public static void MapSelectedIndex(PickerHandler handler, IPicker picker)
    {
        if (handler.PlatformView is null) return;

        if (handler.PlatformView.SelectedIndex != picker.SelectedIndex)
        {
            handler.PlatformView.SelectedIndex = picker.SelectedIndex;
        }
    }

    public static void MapTextColor(PickerHandler handler, IPicker picker)
    {
        if (handler.PlatformView is null) return;
        if (picker.TextColor is not null)
        {
            handler.PlatformView.TextColor = picker.TextColor;
        }
    }

    public static void MapFont(PickerHandler handler, IPicker picker)
    {
        if (handler.PlatformView is null) return;

        var font = picker.Font;
        if (!string.IsNullOrEmpty(font.Family))
        {
            handler.PlatformView.FontFamily = font.Family;
        }
        if (font.Size > 0)
        {
            handler.PlatformView.FontSize = font.Size;
        }

        // Map FontAttributes from the Font weight
        var attrs = FontAttributes.None;
        if (font.Weight >= FontWeight.Bold)
            attrs |= FontAttributes.Bold;
        handler.PlatformView.FontAttributes = attrs;

        handler.PlatformView.Invalidate();
    }

    public static void MapCharacterSpacing(PickerHandler handler, IPicker picker)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.CharacterSpacing = picker.CharacterSpacing;
    }

    public static void MapHorizontalTextAlignment(PickerHandler handler, IPicker picker)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.HorizontalTextAlignment = picker.HorizontalTextAlignment;
    }

    public static void MapVerticalTextAlignment(PickerHandler handler, IPicker picker)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.VerticalTextAlignment = picker.VerticalTextAlignment;
    }

    public static void MapBackground(PickerHandler handler, IPicker picker)
    {
        if (handler.PlatformView is null) return;

        if (picker.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.BackgroundColor = solidPaint.Color.ToSKColor();
        }
    }

    public static void MapIsEnabled(PickerHandler handler, IPicker picker)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsEnabled = picker.IsEnabled;
        handler.PlatformView.Invalidate();
    }

    public static void MapItemsSource(PickerHandler handler, IPicker picker)
    {
        handler.ReloadItems();
    }
}
