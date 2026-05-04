// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Linux handler for SearchBar control.
/// </summary>
public partial class SearchBarHandler : ViewHandler<ISearchBar, SkiaSearchBar>
{
    public static IPropertyMapper<ISearchBar, SearchBarHandler> Mapper = new PropertyMapper<ISearchBar, SearchBarHandler>(ViewHandler.ViewMapper)
    {
        [nameof(ISearchBar.Text)] = MapText,
        [nameof(ISearchBar.Placeholder)] = MapPlaceholder,
        [nameof(ISearchBar.PlaceholderColor)] = MapPlaceholderColor,
        [nameof(ISearchBar.TextColor)] = MapTextColor,
        [nameof(ISearchBar.Font)] = MapFont,
        [nameof(IView.IsEnabled)] = MapIsEnabled,
        [nameof(IView.Background)] = MapBackground,
    };

    public static CommandMapper<ISearchBar, SearchBarHandler> CommandMapper = new(ViewHandler.ViewCommandMapper);

    public SearchBarHandler() : base(Mapper, CommandMapper) { }

    protected override SkiaSearchBar CreatePlatformView() => new SkiaSearchBar();

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
        if (VirtualView != null && VirtualView.Text != e.NewText)
        {
            VirtualView.Text = e.NewText;
        }
    }

    private void OnSearchButtonPressed(object? sender, EventArgs e)
    {
        VirtualView?.SearchButtonPressed();
    }

    public static void MapText(SearchBarHandler handler, ISearchBar searchBar)
    {
        if (handler.PlatformView.Text != searchBar.Text)
        {
            handler.PlatformView.Text = searchBar.Text ?? "";
        }
    }

    public static void MapPlaceholder(SearchBarHandler handler, ISearchBar searchBar)
    {
        handler.PlatformView.Placeholder = searchBar.Placeholder ?? "";
        handler.PlatformView.Invalidate();
    }

    public static void MapPlaceholderColor(SearchBarHandler handler, ISearchBar searchBar)
    {
        if (searchBar.PlaceholderColor != null)
            handler.PlatformView.PlaceholderColor = searchBar.PlaceholderColor.ToSKColor();
        handler.PlatformView.Invalidate();
    }

    public static void MapTextColor(SearchBarHandler handler, ISearchBar searchBar)
    {
        if (searchBar.TextColor != null)
            handler.PlatformView.TextColor = searchBar.TextColor.ToSKColor();
        handler.PlatformView.Invalidate();
    }

    public static void MapFont(SearchBarHandler handler, ISearchBar searchBar)
    {
        var font = searchBar.Font;
        if (font.Family != null)
            handler.PlatformView.FontFamily = font.Family;
        handler.PlatformView.FontSize = (float)font.Size;
        handler.PlatformView.Invalidate();
    }

    public static void MapIsEnabled(SearchBarHandler handler, ISearchBar searchBar)
    {
        handler.PlatformView.IsEnabled = searchBar.IsEnabled;
        handler.PlatformView.Invalidate();
    }

    public static void MapBackground(SearchBarHandler handler, ISearchBar searchBar)
    {
        if (searchBar.Background is SolidColorBrush solidBrush && solidBrush.Color != null)
            handler.PlatformView.BackgroundColor = solidBrush.Color.ToSKColor();
        handler.PlatformView.Invalidate();
    }
}
