// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
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
        [nameof(ITextAlignment.HorizontalTextAlignment)] = MapHorizontalTextAlignment,
        [nameof(ITextAlignment.VerticalTextAlignment)] = MapVerticalTextAlignment,
        [nameof(IView.Background)] = MapBackground,
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

    private void OnTextChanged(object? sender, Platform.TextChangedEventArgs e)
    {
        if (VirtualView is null || PlatformView is null) return;

        if (VirtualView.Text != e.NewTextValue)
        {
            VirtualView.Text = e.NewTextValue ?? string.Empty;
        }
    }

    private void OnCompleted(object? sender, EventArgs e)
    {
        VirtualView?.Completed();
    }

    public static void MapText(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView is null) return;

        if (handler.PlatformView.Text != entry.Text)
            handler.PlatformView.Text = entry.Text ?? string.Empty;
    }

    public static void MapTextColor(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView is null) return;

        if (entry.TextColor is not null)
            handler.PlatformView.TextColor = entry.TextColor.ToSKColor();
    }

    public static void MapFont(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView is null) return;

        var font = entry.Font;
        if (font.Size > 0)
            handler.PlatformView.FontSize = (float)font.Size;

        if (!string.IsNullOrEmpty(font.Family))
            handler.PlatformView.FontFamily = font.Family;

        handler.PlatformView.IsBold = font.Weight >= FontWeight.Bold;
        handler.PlatformView.IsItalic = font.Slant == FontSlant.Italic || font.Slant == FontSlant.Oblique;
    }

    public static void MapCharacterSpacing(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.CharacterSpacing = (float)entry.CharacterSpacing;
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
            handler.PlatformView.PlaceholderColor = entry.PlaceholderColor.ToSKColor();
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

    public static void MapHorizontalTextAlignment(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView is null) return;

        handler.PlatformView.HorizontalTextAlignment = entry.HorizontalTextAlignment switch
        {
            Microsoft.Maui.TextAlignment.Start => Platform.TextAlignment.Start,
            Microsoft.Maui.TextAlignment.Center => Platform.TextAlignment.Center,
            Microsoft.Maui.TextAlignment.End => Platform.TextAlignment.End,
            _ => Platform.TextAlignment.Start
        };
    }

    public static void MapVerticalTextAlignment(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView is null) return;

        handler.PlatformView.VerticalTextAlignment = entry.VerticalTextAlignment switch
        {
            Microsoft.Maui.TextAlignment.Start => Platform.TextAlignment.Start,
            Microsoft.Maui.TextAlignment.Center => Platform.TextAlignment.Center,
            Microsoft.Maui.TextAlignment.End => Platform.TextAlignment.End,
            _ => Platform.TextAlignment.Center
        };
    }

    public static void MapBackground(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView is null) return;

        if (entry.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.BackgroundColor = solidPaint.Color.ToSKColor();
        }
    }
}
