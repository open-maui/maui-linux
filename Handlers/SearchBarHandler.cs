// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for SearchBar on Linux using Skia rendering.
/// Maps ISearchBar interface to SkiaSearchBar platform view.
/// </summary>
public partial class SearchBarHandler : LinuxViewHandler<ISearchBar, SkiaSearchBar>, ISearchBarHandler
{
    ISearchBar ISearchBarHandler.VirtualView => VirtualView;
    object ISearchBarHandler.PlatformView => PlatformView;
    // MAUI exposes the search field as a platform editor; the Skia search bar
    // draws its own field and has no child editor view.
    object? ISearchBarHandler.QueryEditor => null;

    public static IPropertyMapper<ISearchBar, SearchBarHandler> Mapper = new PropertyMapper<ISearchBar, SearchBarHandler>(ViewHandler.ViewMapper)
    {
        [nameof(ITextInput.Text)] = MapText,
        [nameof(SearchBar.TextTransform)] = MapText,
        [nameof(ITextStyle.TextColor)] = MapTextColor,
        [nameof(ITextStyle.Font)] = MapFont,
        [nameof(ITextStyle.CharacterSpacing)] = MapCharacterSpacing,
        [nameof(IPlaceholder.Placeholder)] = MapPlaceholder,
        [nameof(IPlaceholder.PlaceholderColor)] = MapPlaceholderColor,
        [nameof(ISearchBar.CancelButtonColor)] = MapCancelButtonColor,
        [nameof(ISearchBar.HorizontalTextAlignment)] = MapHorizontalTextAlignment,
        [nameof(ISearchBar.VerticalTextAlignment)] = MapVerticalTextAlignment,
        [nameof(ITextInput.MaxLength)] = MapMaxLength,
        [nameof(ITextInput.Keyboard)] = MapKeyboard,
        [nameof(ITextInput.IsReadOnly)] = MapIsReadOnly,
        [nameof(ITextInput.IsTextPredictionEnabled)] = MapIsTextPredictionEnabled,
        [nameof(ITextInput.IsSpellCheckEnabled)] = MapIsSpellCheckEnabled,
        [nameof(ITextInput.CursorPosition)] = MapCursorPosition,
        [nameof(ITextInput.SelectionLength)] = MapSelectionLength,
        [nameof(ISearchBar.ReturnType)] = MapReturnType,
        [nameof(ISearchBar.SearchIconColor)] = MapSearchIconColor,
        [nameof(IView.Background)] = MapBackground,
    };

    public static CommandMapper<ISearchBar, SearchBarHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
    };

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
        VisualStateBridge.Attach(VirtualView, platformView);
        platformView.TextChanged += OnTextChanged;
        platformView.SearchButtonPressed += OnSearchButtonPressed;
        platformView.SelectionChanged += OnSelectionChanged;
        platformView.FocusGained += OnFocusGained;
        platformView.FocusLost += OnFocusLost;
    }

    protected override void DisconnectHandler(SkiaSearchBar platformView)
    {
        platformView.TextChanged -= OnTextChanged;
        platformView.SearchButtonPressed -= OnSearchButtonPressed;
        platformView.SelectionChanged -= OnSelectionChanged;
        platformView.FocusGained -= OnFocusGained;
        platformView.FocusLost -= OnFocusLost;
        VisualStateBridge.Detach(platformView);
        base.DisconnectHandler(platformView);
    }

    private void OnTextChanged(object? sender, SkiaTextChangedEventArgs e)
    {
        if (VirtualView is null || PlatformView is null) return;

        TextInputText.UpdateVirtualText(VirtualView, e.NewTextValue);
    }

    /// <summary>
    /// The caret or selection moved on the platform (typing, clicks, a replaced
    /// query): report it to the MAUI view, as MAUI's search bars do.
    /// </summary>
    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        // A caret the handler moved while applying the view's own Text, caret
        // or selection is not the user's: the view already holds its values.
        if (VirtualView is null || PlatformView is null || _isMapping) return;

        var cursor = PlatformView.CursorPosition;
        var selection = PlatformView.SelectionLength;
        if (VirtualView.CursorPosition != cursor)
            VirtualView.CursorPosition = cursor;
        if (VirtualView.SelectionLength != selection)
            VirtualView.SelectionLength = selection;
    }

    private void OnFocusGained(object? sender, EventArgs e) => UpdateIsFocused(true);

    private void OnFocusLost(object? sender, EventArgs e) => UpdateIsFocused(false);

    /// <summary>
    /// Reports platform focus to the MAUI view (IView.IsFocused), as MAUI's
    /// handlers do from the native focus events.
    /// </summary>
    private void UpdateIsFocused(bool isFocused)
    {
        if (VirtualView is { } view && view.IsFocused != isFocused)
            view.IsFocused = isFocused;
    }

    private void OnSearchButtonPressed(object? sender, EventArgs e)
    {
        VirtualView?.SearchButtonPressed();
    }

    private bool _isMapping;

    /// <summary>MAUI's <c>MapText</c> signature; maps the property on a Linux <see cref="SearchBarHandler"/>.</summary>
    public static void MapText(IViewHandler handler, ISearchBar searchBar)
    {
        if (handler is SearchBarHandler linuxHandler)
            MapTextCore(linuxHandler, searchBar);
    }

    private static void MapTextCore(SearchBarHandler handler, ISearchBar searchBar)
    {
        if (handler.PlatformView is null) return;

        // The platform holds the query as shown (TextTransform applied, cut to
        // MaxLength); a difference flows back to the view through TextChanged.
        handler._isMapping = true;
        try
        {
            handler.PlatformView.SetDisplayText(searchBar);
        }
        finally
        {
            handler._isMapping = false;
        }

        // The query the platform holds (cut to MaxLength, transformed) is the
        // view's Text, as on MAUI's platforms.
        TextInputText.UpdateVirtualText(searchBar, handler.PlatformView.Text);
    }

    /// <summary>MAUI's <c>MapTextColor</c> signature; maps the property on a Linux <see cref="SearchBarHandler"/>.</summary>
    public static void MapTextColor(IViewHandler handler, ISearchBar searchBar)
    {
        if (handler is SearchBarHandler linuxHandler)
            MapTextColorCore(linuxHandler, searchBar);
    }

    private static void MapTextColorCore(SearchBarHandler handler, ISearchBar searchBar)
    {
        handler.PlatformView?.UpdateTextColor(searchBar);
    }

    /// <summary>MAUI's <c>MapFont</c> signature; maps the property on a Linux <see cref="SearchBarHandler"/>.</summary>
    public static void MapFont(IViewHandler handler, ISearchBar searchBar)
    {
        if (handler is SearchBarHandler linuxHandler)
            MapFontCore(linuxHandler, searchBar);
    }

    private static void MapFontCore(SearchBarHandler handler, ISearchBar searchBar)
    {
        handler.PlatformView?.UpdateFont(searchBar);
    }

    /// <summary>MAUI's <c>MapCharacterSpacing</c> signature; maps the property on a Linux <see cref="SearchBarHandler"/>.</summary>
    public static void MapCharacterSpacing(IViewHandler handler, ISearchBar searchBar)
    {
        if (handler is SearchBarHandler linuxHandler)
            MapCharacterSpacingCore(linuxHandler, searchBar);
    }

    private static void MapCharacterSpacingCore(SearchBarHandler handler, ISearchBar searchBar)
    {
        handler.PlatformView?.UpdateCharacterSpacing(searchBar);
    }

    /// <summary>MAUI's <c>MapHorizontalTextAlignment</c> signature; maps the property on a Linux <see cref="SearchBarHandler"/>.</summary>
    public static void MapHorizontalTextAlignment(IViewHandler handler, ISearchBar searchBar)
    {
        if (handler is SearchBarHandler linuxHandler)
            MapHorizontalTextAlignmentCore(linuxHandler, searchBar);
    }

    private static void MapHorizontalTextAlignmentCore(SearchBarHandler handler, ISearchBar searchBar)
    {
        handler.PlatformView?.UpdateHorizontalTextAlignment(searchBar);
    }

    /// <summary>MAUI's <c>MapVerticalTextAlignment</c> signature; maps the property on a Linux <see cref="SearchBarHandler"/>.</summary>
    public static void MapVerticalTextAlignment(IViewHandler handler, ISearchBar searchBar)
    {
        if (handler is SearchBarHandler linuxHandler)
            MapVerticalTextAlignmentCore(linuxHandler, searchBar);
    }

    private static void MapVerticalTextAlignmentCore(SearchBarHandler handler, ISearchBar searchBar)
    {
        handler.PlatformView?.UpdateVerticalTextAlignment(searchBar);
    }

    /// <summary>MAUI's <c>MapMaxLength</c> signature; maps the property on a Linux <see cref="SearchBarHandler"/>.</summary>
    public static void MapMaxLength(IViewHandler handler, ISearchBar searchBar)
    {
        if (handler is SearchBarHandler linuxHandler)
            MapMaxLengthCore(linuxHandler, searchBar);
    }

    private static void MapMaxLengthCore(SearchBarHandler handler, ISearchBar searchBar)
    {
        handler.PlatformView?.UpdateMaxLength(searchBar);
    }

    /// <summary>MAUI's <c>MapKeyboard</c> signature; maps the property on a Linux <see cref="SearchBarHandler"/>.</summary>
    public static void MapKeyboard(IViewHandler handler, ISearchBar searchBar)
    {
        if (handler is SearchBarHandler linuxHandler)
            MapKeyboardCore(linuxHandler, searchBar);
    }

    private static void MapKeyboardCore(SearchBarHandler handler, ISearchBar searchBar)
    {
        handler.PlatformView?.UpdateKeyboard(searchBar);
    }

    /// <summary>MAUI's <c>MapIsReadOnly</c> signature; maps the property on a Linux <see cref="SearchBarHandler"/>.</summary>
    public static void MapIsReadOnly(IViewHandler handler, ISearchBar searchBar)
    {
        if (handler is SearchBarHandler linuxHandler)
            MapIsReadOnlyCore(linuxHandler, searchBar);
    }

    private static void MapIsReadOnlyCore(SearchBarHandler handler, ISearchBar searchBar)
    {
        handler.PlatformView?.UpdateIsReadOnly(searchBar);
    }

    /// <summary>MAUI's <c>MapIsTextPredictionEnabled</c> signature; maps the property on a Linux <see cref="SearchBarHandler"/>.</summary>
    public static void MapIsTextPredictionEnabled(IViewHandler handler, ISearchBar searchBar)
    {
        if (handler is SearchBarHandler linuxHandler)
            MapIsTextPredictionEnabledCore(linuxHandler, searchBar);
    }

    private static void MapIsTextPredictionEnabledCore(SearchBarHandler handler, ISearchBar searchBar)
    {
        handler.PlatformView?.UpdateIsTextPredictionEnabled(searchBar);
    }

    /// <summary>MAUI's <c>MapIsSpellCheckEnabled</c> signature; maps the property on a Linux <see cref="SearchBarHandler"/>.</summary>
    public static void MapIsSpellCheckEnabled(IViewHandler handler, ISearchBar searchBar)
    {
        if (handler is SearchBarHandler linuxHandler)
            MapIsSpellCheckEnabledCore(linuxHandler, searchBar);
    }

    private static void MapIsSpellCheckEnabledCore(SearchBarHandler handler, ISearchBar searchBar)
    {
        handler.PlatformView?.UpdateIsSpellCheckEnabled(searchBar);
    }

    public static void MapCursorPosition(SearchBarHandler handler, ISearchBar searchBar)
    {
        if (handler.PlatformView is null) return;
        handler._isMapping = true;
        try
        {
            handler.PlatformView.CursorPosition = searchBar.CursorPosition;
        }
        finally
        {
            handler._isMapping = false;
        }
    }

    public static void MapSelectionLength(SearchBarHandler handler, ISearchBar searchBar)
    {
        if (handler.PlatformView is null) return;
        handler._isMapping = true;
        try
        {
            handler.PlatformView.SelectionLength = searchBar.SelectionLength;
        }
        finally
        {
            handler._isMapping = false;
        }
    }

    /// <summary>MAUI's <c>MapReturnType</c> signature; maps the property on a Linux <see cref="SearchBarHandler"/>.</summary>
    public static void MapReturnType(IViewHandler handler, ISearchBar searchBar)
    {
        if (handler is SearchBarHandler linuxHandler)
            MapReturnTypeCore(linuxHandler, searchBar);
    }

    private static void MapReturnTypeCore(SearchBarHandler handler, ISearchBar searchBar)
    {
        handler.PlatformView?.UpdateReturnType(searchBar);
    }

    public static void MapSearchIconColor(SearchBarHandler handler, ISearchBar searchBar)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IconColor = searchBar.SearchIconColor ?? SkiaSearchBar.DefaultIconColor;
        handler.PlatformView.Invalidate();
    }

    /// <summary>MAUI's <c>MapPlaceholder</c> signature; maps the property on a Linux <see cref="SearchBarHandler"/>.</summary>
    public static void MapPlaceholder(IViewHandler handler, ISearchBar searchBar)
    {
        if (handler is SearchBarHandler linuxHandler)
            MapPlaceholderCore(linuxHandler, searchBar);
    }

    private static void MapPlaceholderCore(SearchBarHandler handler, ISearchBar searchBar)
    {
        handler.PlatformView?.UpdatePlaceholder(searchBar);
    }

    /// <summary>MAUI's <c>MapPlaceholderColor</c> signature; maps the property on a Linux <see cref="SearchBarHandler"/>.</summary>
    public static void MapPlaceholderColor(IViewHandler handler, ISearchBar searchBar)
    {
        if (handler is SearchBarHandler linuxHandler)
            MapPlaceholderColorCore(linuxHandler, searchBar);
    }

    private static void MapPlaceholderColorCore(SearchBarHandler handler, ISearchBar searchBar)
    {
        handler.PlatformView?.UpdatePlaceholderColor(searchBar);
    }

    /// <summary>MAUI's <c>MapCancelButtonColor</c> signature; maps the property on a Linux <see cref="SearchBarHandler"/>.</summary>
    public static void MapCancelButtonColor(IViewHandler handler, ISearchBar searchBar)
    {
        if (handler is SearchBarHandler linuxHandler)
            MapCancelButtonColorCore(linuxHandler, searchBar);
    }

    private static void MapCancelButtonColorCore(SearchBarHandler handler, ISearchBar searchBar)
    {
        handler.PlatformView?.UpdateCancelButtonColor(searchBar);
    }


    /// <summary>MAUI's <c>MapBackground</c> signature; maps the property on a Linux <see cref="SearchBarHandler"/>.</summary>
    public static void MapBackground(ISearchBarHandler handler, ISearchBar searchBar)
    {
        if (handler is SearchBarHandler linuxHandler)
            MapBackgroundCore(linuxHandler, searchBar);
    }

    private static void MapBackgroundCore(SearchBarHandler handler, ISearchBar searchBar)
    {
        if (handler.PlatformView is null) return;

        if (searchBar.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.BackgroundColor = solidPaint.Color;
        }
    }

    /// <summary>MAUI's <c>MapIsEnabled</c>: the view mapper's IsEnabled, then a redraw.</summary>
    public static void MapIsEnabled(ISearchBarHandler handler, ISearchBar searchBar)
    {
        ViewHandler.ViewMapper.UpdateProperty(handler, searchBar, nameof(IView.IsEnabled));
        (handler.PlatformView as SkiaView)?.Invalidate();
    }
}
