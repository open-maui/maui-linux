// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Linux handler for Entry control.
/// </summary>
public partial class EntryHandler : ViewHandler<IEntry, SkiaEntry>
{
    /// <summary>
    /// Maps the property mapper for the handler.
    /// </summary>
    public static IPropertyMapper<IEntry, EntryHandler> Mapper = new PropertyMapper<IEntry, EntryHandler>(ViewHandler.ViewMapper)
    {
        [nameof(IEntry.Text)] = MapText,
        [nameof(IEntry.TextColor)] = MapTextColor,
        [nameof(IEntry.Placeholder)] = MapPlaceholder,
        [nameof(IEntry.PlaceholderColor)] = MapPlaceholderColor,
        [nameof(IEntry.Font)] = MapFont,
        [nameof(IEntry.IsPassword)] = MapIsPassword,
        [nameof(IEntry.MaxLength)] = MapMaxLength,
        [nameof(IEntry.IsReadOnly)] = MapIsReadOnly,
        [nameof(IEntry.HorizontalTextAlignment)] = MapHorizontalTextAlignment,
        [nameof(IEntry.CursorPosition)] = MapCursorPosition,
        [nameof(IEntry.SelectionLength)] = MapSelectionLength,
        [nameof(IEntry.ReturnType)] = MapReturnType,
        [nameof(IView.IsEnabled)] = MapIsEnabled,
        [nameof(IEntry.Background)] = MapBackground,
        ["BackgroundColor"] = MapBackgroundColor,
    };

    /// <summary>
    /// Maps the command mapper for the handler.
    /// </summary>
    public static CommandMapper<IEntry, EntryHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
    };

    public EntryHandler() : base(Mapper, CommandMapper)
    {
    }

    public EntryHandler(IPropertyMapper? mapper)
        : base(mapper ?? Mapper, CommandMapper)
    {
    }

    public EntryHandler(IPropertyMapper? mapper, CommandMapper? commandMapper)
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
        if (VirtualView != null && VirtualView.Text != e.NewText)
        {
            VirtualView.Text = e.NewText;
        }
    }

    private void OnCompleted(object? sender, EventArgs e)
    {
        VirtualView?.Completed();
    }

    public static void MapText(EntryHandler handler, IEntry entry)
    {
        if (handler.PlatformView.Text != entry.Text)
        {
            handler.PlatformView.Text = entry.Text ?? "";
        }
    }

    public static void MapTextColor(EntryHandler handler, IEntry entry)
    {
        if (entry.TextColor != null)
        {
            handler.PlatformView.TextColor = entry.TextColor.ToSKColor();
        }
        handler.PlatformView.Invalidate();
    }

    public static void MapPlaceholder(EntryHandler handler, IEntry entry)
    {
        handler.PlatformView.Placeholder = entry.Placeholder ?? "";
        handler.PlatformView.Invalidate();
    }

    public static void MapPlaceholderColor(EntryHandler handler, IEntry entry)
    {
        if (entry.PlaceholderColor != null)
        {
            handler.PlatformView.PlaceholderColor = entry.PlaceholderColor.ToSKColor();
        }
        handler.PlatformView.Invalidate();
    }

    public static void MapFont(EntryHandler handler, IEntry entry)
    {
        var font = entry.Font;
        if (font.Family != null)
        {
            handler.PlatformView.FontFamily = font.Family;
        }
        handler.PlatformView.FontSize = (float)font.Size;
        handler.PlatformView.Invalidate();
    }

    public static void MapIsPassword(EntryHandler handler, IEntry entry)
    {
        handler.PlatformView.IsPassword = entry.IsPassword;
        handler.PlatformView.Invalidate();
    }

    public static void MapMaxLength(EntryHandler handler, IEntry entry)
    {
        handler.PlatformView.MaxLength = entry.MaxLength;
    }

    public static void MapIsReadOnly(EntryHandler handler, IEntry entry)
    {
        handler.PlatformView.IsReadOnly = entry.IsReadOnly;
    }

    public static void MapHorizontalTextAlignment(EntryHandler handler, IEntry entry)
    {
        handler.PlatformView.HorizontalTextAlignment = entry.HorizontalTextAlignment switch
        {
            Microsoft.Maui.TextAlignment.Start => TextAlignment.Start,
            Microsoft.Maui.TextAlignment.Center => TextAlignment.Center,
            Microsoft.Maui.TextAlignment.End => TextAlignment.End,
            _ => TextAlignment.Start
        };
        handler.PlatformView.Invalidate();
    }

    public static void MapCursorPosition(EntryHandler handler, IEntry entry)
    {
        handler.PlatformView.CursorPosition = entry.CursorPosition;
    }

    public static void MapSelectionLength(EntryHandler handler, IEntry entry)
    {
        // Selection length is handled internally by SkiaEntry
    }

    public static void MapReturnType(EntryHandler handler, IEntry entry)
    {
        // Return type affects keyboard on mobile; on desktop, Enter always completes
    }

    public static void MapIsEnabled(EntryHandler handler, IEntry entry)
    {
        handler.PlatformView.IsEnabled = entry.IsEnabled;
        handler.PlatformView.Invalidate();
    }

    public static void MapBackground(EntryHandler handler, IEntry entry)
    {
        var background = entry.Background;
        if (background is SolidColorBrush solidBrush && solidBrush.Color != null)
        {
            handler.PlatformView.BackgroundColor = solidBrush.Color.ToSKColor();
        }
        handler.PlatformView.Invalidate();
    }

    public static void MapBackgroundColor(EntryHandler handler, IEntry entry)
    {
        if (entry is Microsoft.Maui.Controls.VisualElement ve && ve.BackgroundColor != null)
        {
            handler.PlatformView.BackgroundColor = ve.BackgroundColor.ToSKColor();
            handler.PlatformView.Invalidate();
        }
    }
}
