// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for Button on Linux using Skia rendering.
/// Maps IButton interface to SkiaButton platform view.
/// </summary>
public partial class ButtonHandler : ViewHandler<IButton, SkiaButton>
{
    public static IPropertyMapper<IButton, ButtonHandler> Mapper = new PropertyMapper<IButton, ButtonHandler>(ViewHandler.ViewMapper)
    {
        [nameof(IButtonStroke.StrokeColor)] = MapStrokeColor,
        [nameof(IButtonStroke.StrokeThickness)] = MapStrokeThickness,
        [nameof(IButtonStroke.CornerRadius)] = MapCornerRadius,
        [nameof(IView.Background)] = MapBackground,
        [nameof(IPadding.Padding)] = MapPadding,
    };

    public static CommandMapper<IButton, ButtonHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
    };

    public ButtonHandler() : base(Mapper, CommandMapper)
    {
    }

    public ButtonHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaButton CreatePlatformView()
    {
        var button = new SkiaButton();
        return button;
    }

    protected override void ConnectHandler(SkiaButton platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Clicked += OnClicked;
        platformView.Pressed += OnPressed;
        platformView.Released += OnReleased;
    }

    protected override void DisconnectHandler(SkiaButton platformView)
    {
        platformView.Clicked -= OnClicked;
        platformView.Pressed -= OnPressed;
        platformView.Released -= OnReleased;
        base.DisconnectHandler(platformView);
    }

    private void OnClicked(object? sender, EventArgs e) => VirtualView?.Clicked();
    private void OnPressed(object? sender, EventArgs e) => VirtualView?.Pressed();
    private void OnReleased(object? sender, EventArgs e) => VirtualView?.Released();

    public static void MapStrokeColor(ButtonHandler handler, IButton button)
    {
        if (handler.PlatformView is null) return;

        var strokeColor = button.StrokeColor;
        if (strokeColor is not null)
            handler.PlatformView.BorderColor = strokeColor.ToSKColor();
    }

    public static void MapStrokeThickness(ButtonHandler handler, IButton button)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.BorderWidth = (float)button.StrokeThickness;
    }

    public static void MapCornerRadius(ButtonHandler handler, IButton button)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.CornerRadius = button.CornerRadius;
    }

    public static void MapBackground(ButtonHandler handler, IButton button)
    {
        if (handler.PlatformView is null) return;

        if (button.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.BackgroundColor = solidPaint.Color.ToSKColor();
        }
    }

    public static void MapPadding(ButtonHandler handler, IButton button)
    {
        if (handler.PlatformView is null) return;

        var padding = button.Padding;
        handler.PlatformView.Padding = new SKRect(
            (float)padding.Left,
            (float)padding.Top,
            (float)padding.Right,
            (float)padding.Bottom);
    }
}

/// <summary>
/// Handler for TextButton on Linux - extends ButtonHandler with text support.
/// Maps ITextButton interface (which includes IText properties).
/// </summary>
public partial class TextButtonHandler : ButtonHandler
{
    public static new IPropertyMapper<ITextButton, TextButtonHandler> Mapper =
        new PropertyMapper<ITextButton, TextButtonHandler>(ButtonHandler.Mapper)
    {
        [nameof(IText.Text)] = MapText,
        [nameof(ITextStyle.TextColor)] = MapTextColor,
        [nameof(ITextStyle.Font)] = MapFont,
        [nameof(ITextStyle.CharacterSpacing)] = MapCharacterSpacing,
    };

    public TextButtonHandler() : base(Mapper)
    {
    }

    public static void MapText(TextButtonHandler handler, ITextButton button)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Text = button.Text ?? string.Empty;
    }

    public static void MapTextColor(TextButtonHandler handler, ITextButton button)
    {
        if (handler.PlatformView is null) return;

        if (button.TextColor is not null)
            handler.PlatformView.TextColor = button.TextColor.ToSKColor();
    }

    public static void MapFont(TextButtonHandler handler, ITextButton button)
    {
        if (handler.PlatformView is null) return;

        var font = button.Font;
        if (font.Size > 0)
            handler.PlatformView.FontSize = (float)font.Size;

        if (!string.IsNullOrEmpty(font.Family))
            handler.PlatformView.FontFamily = font.Family;

        handler.PlatformView.IsBold = font.Weight >= FontWeight.Bold;
        handler.PlatformView.IsItalic = font.Slant == FontSlant.Italic || font.Slant == FontSlant.Oblique;
    }

    public static void MapCharacterSpacing(TextButtonHandler handler, ITextButton button)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.CharacterSpacing = (float)button.CharacterSpacing;
    }
}
