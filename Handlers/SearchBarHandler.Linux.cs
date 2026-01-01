// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Linux handler for SearchBar control.
/// </summary>
public class SearchBarHandler : ViewHandler<ISearchBar, SkiaSearchBar>
{
    public static IPropertyMapper<ISearchBar, SearchBarHandler> Mapper = new PropertyMapper<ISearchBar, SearchBarHandler>(ViewHandler.ViewMapper)
    {
        ["Text"] = MapText,
        ["TextColor"] = MapTextColor,
        ["Font"] = MapFont,
        ["Placeholder"] = MapPlaceholder,
        ["PlaceholderColor"] = MapPlaceholderColor,
        ["CancelButtonColor"] = MapCancelButtonColor,
        ["Background"] = MapBackground
    };

    public static CommandMapper<ISearchBar, SearchBarHandler> CommandMapper = new(ViewHandler.ViewCommandMapper);

    public SearchBarHandler() : base(Mapper, CommandMapper)
    {
    }

    public SearchBarHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaSearchBar CreatePlatformView()
    {
        return new SkiaSearchBar();
    }

    protected override void ConnectHandler(SkiaSearchBar platformView)
    {
        base.ConnectHandler(platformView);
        platformView.TextChanged += OnTextChanged;
        platformView.SearchButtonPressed += OnSearchButtonPressed;
    }

    protected override void DisconnectHandler(SkiaSearchBar platformView)
    {
        platformView.TextChanged -= OnTextChanged;
        platformView.SearchButtonPressed -= OnSearchButtonPressed;
        base.DisconnectHandler(platformView);
    }

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (VirtualView != null && PlatformView != null && VirtualView.Text != e.NewTextValue)
        {
            VirtualView.Text = e.NewTextValue ?? string.Empty;
        }
    }

    private void OnSearchButtonPressed(object? sender, EventArgs e)
    {
        VirtualView?.SearchButtonPressed();
    }

    public static void MapText(SearchBarHandler handler, ISearchBar searchBar)
    {
        if (handler.PlatformView != null && handler.PlatformView.Text != searchBar.Text)
        {
            handler.PlatformView.Text = searchBar.Text ?? string.Empty;
        }
    }

    public static void MapTextColor(SearchBarHandler handler, ISearchBar searchBar)
    {
        if (handler.PlatformView != null && searchBar.TextColor != null)
        {
            handler.PlatformView.TextColor = searchBar.TextColor.ToSKColor();
        }
    }

    public static void MapFont(SearchBarHandler handler, ISearchBar searchBar)
    {
        if (handler.PlatformView != null)
        {
            var font = searchBar.Font;
            if (font.Size > 0)
            {
                handler.PlatformView.FontSize = (float)font.Size;
            }
            if (!string.IsNullOrEmpty(font.Family))
            {
                handler.PlatformView.FontFamily = font.Family;
            }
        }
    }

    public static void MapPlaceholder(SearchBarHandler handler, ISearchBar searchBar)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.Placeholder = searchBar.Placeholder ?? string.Empty;
        }
    }

    public static void MapPlaceholderColor(SearchBarHandler handler, ISearchBar searchBar)
    {
        if (handler.PlatformView != null && searchBar.PlaceholderColor != null)
        {
            handler.PlatformView.PlaceholderColor = searchBar.PlaceholderColor.ToSKColor();
        }
    }

    public static void MapCancelButtonColor(SearchBarHandler handler, ISearchBar searchBar)
    {
        if (handler.PlatformView != null && searchBar.CancelButtonColor != null)
        {
            handler.PlatformView.ClearButtonColor = searchBar.CancelButtonColor.ToSKColor();
        }
    }

    public static void MapBackground(SearchBarHandler handler, ISearchBar searchBar)
    {
        if (handler.PlatformView != null)
        {
            if (searchBar.Background is SolidPaint solidPaint && solidPaint.Color != null)
            {
                handler.PlatformView.BackgroundColor = solidPaint.Color.ToSKColor();
            }
        }
    }
}
