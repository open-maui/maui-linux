// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Linux handler for Entry control.
/// </summary>
public class EntryHandler : ViewHandler<IEntry, SkiaEntry>
{
    public static IPropertyMapper<IEntry, EntryHandler> Mapper = new PropertyMapper<IEntry, EntryHandler>(ViewHandler.ViewMapper)
    {
        ["Text"] = MapText,
        ["TextColor"] = MapTextColor,
        ["Font"] = MapFont,
        ["CharacterSpacing"] = MapCharacterSpacing,
        ["Placeholder"] = MapPlaceholder,
        ["PlaceholderColor"] = MapPlaceholderColor,
        ["IsReadOnly"] = MapIsReadOnly,
        ["MaxLength"] = MapMaxLength,
        ["CursorPosition"] = MapCursorPosition,
        ["SelectionLength"] = MapSelectionLength,
        ["IsPassword"] = MapIsPassword,
        ["ReturnType"] = MapReturnType,
        ["ClearButtonVisibility"] = MapClearButtonVisibility,
        ["HorizontalTextAlignment"] = MapHorizontalTextAlignment,
        ["VerticalTextAlignment"] = MapVerticalTextAlignment,
        ["Background"] = MapBackground,
        ["BackgroundColor"] = MapBackgroundColor
    };

    public static CommandMapper<IEntry, EntryHandler> CommandMapper = new(ViewHandler.ViewCommandMapper);

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

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (VirtualView != null && PlatformView != null && VirtualView.Text != e.NewTextValue)
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
        if (handler.PlatformView != null && handler.PlatformView.Text != entry.Text)
        {
            handler.PlatformView.Text = entry.Text ?? string.Empty;
            handler.PlatformView.Invalidate();
        }
    }

    public static void MapTextColor(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView != null && entry.TextColor != null)
        {
            handler.PlatformView.TextColor = entry.TextColor.ToSKColor();
        }
    }

    public static void MapFont(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView != null)
        {
            var font = entry.Font;
            if (font.Size > 0)
            {
                handler.PlatformView.FontSize = (float)font.Size;
            }
            if (!string.IsNullOrEmpty(font.Family))
            {
                handler.PlatformView.FontFamily = font.Family;
            }
            handler.PlatformView.IsBold = (int)font.Weight >= 700;
            handler.PlatformView.IsItalic = font.Slant == FontSlant.Italic || font.Slant == FontSlant.Oblique;
        }
    }

    public static void MapCharacterSpacing(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.CharacterSpacing = (float)entry.CharacterSpacing;
        }
    }

    public static void MapPlaceholder(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.Placeholder = entry.Placeholder ?? string.Empty;
        }
    }

    public static void MapPlaceholderColor(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView != null && entry.PlaceholderColor != null)
        {
            handler.PlatformView.PlaceholderColor = entry.PlaceholderColor.ToSKColor();
        }
    }

    public static void MapIsReadOnly(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.IsReadOnly = entry.IsReadOnly;
        }
    }

    public static void MapMaxLength(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.MaxLength = entry.MaxLength;
        }
    }

    public static void MapCursorPosition(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.CursorPosition = entry.CursorPosition;
        }
    }

    public static void MapSelectionLength(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.SelectionLength = entry.SelectionLength;
        }
    }

    public static void MapIsPassword(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.IsPassword = entry.IsPassword;
        }
    }

    public static void MapReturnType(EntryHandler handler, IEntry entry)
    {
        // ReturnType affects keyboard on mobile; access PlatformView to ensure it exists
        _ = handler.PlatformView;
    }

    public static void MapClearButtonVisibility(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView != null)
        {
            // ClearButtonVisibility.WhileEditing = 1
            handler.PlatformView.ShowClearButton = (int)entry.ClearButtonVisibility == 1;
        }
    }

    public static void MapHorizontalTextAlignment(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.HorizontalTextAlignment = (int)entry.HorizontalTextAlignment switch
            {
                0 => TextAlignment.Start,
                1 => TextAlignment.Center,
                2 => TextAlignment.End,
                _ => TextAlignment.Start
            };
        }
    }

    public static void MapVerticalTextAlignment(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.VerticalTextAlignment = (int)entry.VerticalTextAlignment switch
            {
                0 => TextAlignment.Start,
                1 => TextAlignment.Center,
                2 => TextAlignment.End,
                _ => TextAlignment.Center
            };
        }
    }

    public static void MapBackground(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView != null)
        {
            if (entry.Background is SolidPaint solidPaint && solidPaint.Color != null)
            {
                handler.PlatformView.BackgroundColor = solidPaint.Color;
            }
        }
    }

    public static void MapBackgroundColor(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView == null)
        {
            return;
        }

        if (entry is Entry mauiEntry)
        {
            Console.WriteLine($"[EntryHandler] MapBackgroundColor: {mauiEntry.BackgroundColor}");
            if (mauiEntry.BackgroundColor != null)
            {
                var color = mauiEntry.BackgroundColor.ToSKColor();
                Console.WriteLine($"[EntryHandler] Setting EntryBackgroundColor to: {color}");
                handler.PlatformView.EntryBackgroundColor = color;
            }
        }
    }
}
