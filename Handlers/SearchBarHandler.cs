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
public partial class SearchBarHandler : LinuxViewHandler<ISearchBar, SkiaSearchBar>
{
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

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
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

    public static void MapText(SearchBarHandler handler, ISearchBar searchBar)
    {
        if (handler.PlatformView is null) return;

        // The platform holds the query as shown (TextTransform applied, cut to
        // MaxLength); a difference flows back to the view through TextChanged.
        var text = TextInputText.GetDisplayText(searchBar);
        if (handler.PlatformView.Text != text)
        {
            handler._isMapping = true;
            try
            {
                handler.PlatformView.Text = text;
            }
            finally
            {
                handler._isMapping = false;
            }
        }

        // The query the platform holds (cut to MaxLength, transformed) is the
        // view's Text, as on MAUI's platforms.
        TextInputText.UpdateVirtualText(searchBar, handler.PlatformView.Text);
    }

    public static void MapTextColor(SearchBarHandler handler, ISearchBar searchBar)
    {
        if (handler.PlatformView is null) return;

        if (searchBar.TextColor is not null)
            handler.PlatformView.TextColor = searchBar.TextColor;
    }

    public static void MapFont(SearchBarHandler handler, ISearchBar searchBar)
    {
        if (handler.PlatformView is null) return;

        var font = searchBar.Font;
        if (font.Size > 0)
            handler.PlatformView.FontSize = font.Size;

        if (!string.IsNullOrEmpty(font.Family))
            handler.PlatformView.FontFamily = font.Family;

        // Convert Font weight/slant to FontAttributes
        var attrs = FontAttributes.None;
        if (font.Weight >= FontWeight.Bold)
            attrs |= FontAttributes.Bold;
        if (font.Slant == FontSlant.Italic || font.Slant == FontSlant.Oblique)
            attrs |= FontAttributes.Italic;
        handler.PlatformView.FontAttributes = attrs;
    }

    public static void MapCharacterSpacing(SearchBarHandler handler, ISearchBar searchBar)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.CharacterSpacing = searchBar.CharacterSpacing;
    }

    public static void MapHorizontalTextAlignment(SearchBarHandler handler, ISearchBar searchBar)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.HorizontalTextAlignment = searchBar.HorizontalTextAlignment;
    }

    public static void MapVerticalTextAlignment(SearchBarHandler handler, ISearchBar searchBar)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.VerticalTextAlignment = searchBar.VerticalTextAlignment;
    }

    public static void MapMaxLength(SearchBarHandler handler, ISearchBar searchBar)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.MaxLength = searchBar.MaxLength;
    }

    public static void MapKeyboard(SearchBarHandler handler, ISearchBar searchBar)
    {
        // On desktop the keyboard is the input method's content type.
        if (handler.PlatformView is null) return;
        handler.PlatformView.Keyboard = searchBar.Keyboard;
    }

    public static void MapIsReadOnly(SearchBarHandler handler, ISearchBar searchBar)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsReadOnly = searchBar.IsReadOnly;
    }

    public static void MapIsTextPredictionEnabled(SearchBarHandler handler, ISearchBar searchBar)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsTextPredictionEnabled = searchBar.IsTextPredictionEnabled;
    }

    public static void MapIsSpellCheckEnabled(SearchBarHandler handler, ISearchBar searchBar)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsSpellCheckEnabled = searchBar.IsSpellCheckEnabled;
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

    public static void MapReturnType(SearchBarHandler handler, ISearchBar searchBar)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.ReturnType = searchBar.ReturnType;
    }

    public static void MapSearchIconColor(SearchBarHandler handler, ISearchBar searchBar)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IconColor = searchBar.SearchIconColor ?? SkiaSearchBar.DefaultIconColor;
        handler.PlatformView.Invalidate();
    }

    public static void MapPlaceholder(SearchBarHandler handler, ISearchBar searchBar)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Placeholder = searchBar.Placeholder ?? string.Empty;
    }

    public static void MapPlaceholderColor(SearchBarHandler handler, ISearchBar searchBar)
    {
        if (handler.PlatformView is null) return;

        if (searchBar.PlaceholderColor is not null)
            handler.PlatformView.PlaceholderColor = searchBar.PlaceholderColor;
    }

    public static void MapCancelButtonColor(SearchBarHandler handler, ISearchBar searchBar)
    {
        if (handler.PlatformView is null) return;

        // CancelButtonColor maps to ClearButtonColor
        if (searchBar.CancelButtonColor is not null)
            handler.PlatformView.ClearButtonColor = searchBar.CancelButtonColor;
    }


    public static void MapBackground(SearchBarHandler handler, ISearchBar searchBar)
    {
        if (handler.PlatformView is null) return;

        if (searchBar.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.BackgroundColor = solidPaint.Color;
        }
    }
}
