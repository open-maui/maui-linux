// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for Entry on Linux using Skia rendering.
/// Maps IEntry interface to SkiaEntry platform view.
/// </summary>
public partial class EntryHandler : ViewHandler<IEntry, SkiaEntry>
{
    public static IPropertyMapper<IEntry, EntryHandler> Mapper = new PropertyMapper<IEntry, EntryHandler>(ViewHandler.ViewMapper)
    {
        [nameof(ITextInput.Text)] = MapText,
        [nameof(ITextStyle.TextColor)] = MapTextColor,
        [nameof(ITextStyle.Font)] = MapFont,
        [nameof(ITextStyle.CharacterSpacing)] = MapCharacterSpacing,
        [nameof(IPlaceholder.Placeholder)] = MapPlaceholder,
        [nameof(IPlaceholder.PlaceholderColor)] = MapPlaceholderColor,
        [nameof(ITextInput.IsReadOnly)] = MapIsReadOnly,
        [nameof(ITextInput.MaxLength)] = MapMaxLength,
        [nameof(ITextInput.CursorPosition)] = MapCursorPosition,
        [nameof(ITextInput.SelectionLength)] = MapSelectionLength,
        [nameof(IEntry.IsPassword)] = MapIsPassword,
        [nameof(IEntry.ReturnType)] = MapReturnType,
        [nameof(IEntry.ClearButtonVisibility)] = MapClearButtonVisibility,
        [nameof(IEntry.IsTextPredictionEnabled)] = MapIsTextPredictionEnabled,
        [nameof(IEntry.IsSpellCheckEnabled)] = MapIsSpellCheckEnabled,
        [nameof(ITextAlignment.HorizontalTextAlignment)] = MapHorizontalTextAlignment,
        [nameof(ITextAlignment.VerticalTextAlignment)] = MapVerticalTextAlignment,
        [nameof(IView.Background)] = MapBackground,
        ["BackgroundColor"] = MapBackgroundColor,
        ["SelectAllOnDoubleClick"] = MapSelectAllOnDoubleClick,
    };

    public static CommandMapper<IEntry, EntryHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
    };

    public EntryHandler() : base(Mapper, CommandMapper)
    {
    }

    public EntryHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaEntry CreatePlatformView()
    {
        return new SkiaEntry();
    }

    protected override void ConnectHandler(SkiaEntry platformView)
    {
        base.ConnectHandler(platformView);
        platformView.TextChanged += OnTextChanged;
        platformView.Completed += OnCompleted;
    }

    protected override void DisconnectHandler(SkiaEntry platformView)
    {
        platformView.TextChanged -= OnTextChanged;
        platformView.Completed -= OnCompleted;
        base.DisconnectHandler(platformView);
    }

    private bool _isUpdatingText;

    private void OnTextChanged(object? sender, Platform.TextChangedEventArgs e)
    {
        if (VirtualView is null || PlatformView is null || _isUpdatingText) return;

        if (VirtualView.Text != e.NewTextValue)
        {
            _isUpdatingText = true;
            try
            {
                VirtualView.Text = e.NewTextValue ?? string.Empty;
            }
            finally
            {
                _isUpdatingText = false;
            }
        }
    }

    private void OnCompleted(object? sender, EventArgs e)
    {
        VirtualView?.Completed();
    }

    public static void MapText(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView is null || handler._isUpdatingText) return;

        if (handler.PlatformView.Text != entry.Text)
        {
            handler.PlatformView.Text = entry.Text ?? string.Empty;
            handler.PlatformView.Invalidate();
        }
    }

    public static void MapTextColor(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView is null) return;

        if (entry.TextColor is not null)
            handler.PlatformView.TextColor = entry.TextColor;
    }

    public static void MapFont(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView is null) return;

        var font = entry.Font;
        if (font.Size > 0)
            handler.PlatformView.FontSize = font.Size;

        if (!string.IsNullOrEmpty(font.Family))
            handler.PlatformView.FontFamily = font.Family;

        // Convert Font weight/slant to FontAttributes
        FontAttributes attrs = FontAttributes.None;
        if (font.Weight >= FontWeight.Bold)
            attrs |= FontAttributes.Bold;
        if (font.Slant == FontSlant.Italic || font.Slant == FontSlant.Oblique)
            attrs |= FontAttributes.Italic;
        handler.PlatformView.FontAttributes = attrs;
    }

    public static void MapCharacterSpacing(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.CharacterSpacing = entry.CharacterSpacing;
    }

    public static void MapPlaceholder(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Placeholder = entry.Placeholder ?? string.Empty;
    }

    public static void MapPlaceholderColor(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView is null) return;

        if (entry.PlaceholderColor is not null)
            handler.PlatformView.PlaceholderColor = entry.PlaceholderColor;
    }

    public static void MapIsReadOnly(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsReadOnly = entry.IsReadOnly;
    }

    public static void MapMaxLength(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.MaxLength = entry.MaxLength;
    }

    public static void MapCursorPosition(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.CursorPosition = entry.CursorPosition;
    }

    public static void MapSelectionLength(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.SelectionLength = entry.SelectionLength;
    }

    public static void MapIsPassword(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsPassword = entry.IsPassword;
    }

    public static void MapReturnType(EntryHandler handler, IEntry entry)
    {
        // ReturnType affects keyboard behavior - stored for virtual keyboard integration
        if (handler.PlatformView is null) return;
        // handler.PlatformView.ReturnType = entry.ReturnType; // Would need property on SkiaEntry
    }

    public static void MapClearButtonVisibility(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.ShowClearButton = entry.ClearButtonVisibility == ClearButtonVisibility.WhileEditing;
    }

    public static void MapIsTextPredictionEnabled(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsTextPredictionEnabled = entry.IsTextPredictionEnabled;
    }

    public static void MapIsSpellCheckEnabled(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsSpellCheckEnabled = entry.IsSpellCheckEnabled;
    }

    public static void MapHorizontalTextAlignment(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView is null) return;

        handler.PlatformView.HorizontalTextAlignment = entry.HorizontalTextAlignment switch
        {
            Microsoft.Maui.TextAlignment.Start => TextAlignment.Start,
            Microsoft.Maui.TextAlignment.Center => TextAlignment.Center,
            Microsoft.Maui.TextAlignment.End => TextAlignment.End,
            _ => TextAlignment.Start
        };
    }

    public static void MapVerticalTextAlignment(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView is null) return;

        handler.PlatformView.VerticalTextAlignment = entry.VerticalTextAlignment switch
        {
            Microsoft.Maui.TextAlignment.Start => TextAlignment.Start,
            Microsoft.Maui.TextAlignment.Center => TextAlignment.Center,
            Microsoft.Maui.TextAlignment.End => TextAlignment.End,
            _ => TextAlignment.Center
        };
    }

    public static void MapBackground(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView is null) return;

        if (entry.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.BackgroundColor = solidPaint.Color;
        }
    }

    public static void MapBackgroundColor(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView is null) return;

        if (entry is Entry ve && ve.BackgroundColor != null)
        {
            handler.PlatformView.EntryBackgroundColor = ve.BackgroundColor;
            // Also set base BackgroundColor so SkiaView.DrawBackground() respects transparency
            handler.PlatformView.BackgroundColor = ve.BackgroundColor;
        }
    }

    public static void MapSelectAllOnDoubleClick(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView is null) return;

        if (entry is BindableObject bindable)
        {
            handler.PlatformView.SelectAllOnDoubleClick = EntryExtensions.GetSelectAllOnDoubleClick(bindable);
        }
    }
}
