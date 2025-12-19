// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for Label on Linux using Skia rendering.
/// Maps ILabel interface to SkiaLabel platform view.
/// </summary>
public partial class LabelHandler : ViewHandler<ILabel, SkiaLabel>
{
    public static IPropertyMapper<ILabel, LabelHandler> Mapper = new PropertyMapper<ILabel, LabelHandler>(ViewHandler.ViewMapper)
    {
        [nameof(IText.Text)] = MapText,
        [nameof(ITextStyle.TextColor)] = MapTextColor,
        [nameof(ITextStyle.Font)] = MapFont,
        [nameof(ITextStyle.CharacterSpacing)] = MapCharacterSpacing,
        [nameof(ITextAlignment.HorizontalTextAlignment)] = MapHorizontalTextAlignment,
        [nameof(ITextAlignment.VerticalTextAlignment)] = MapVerticalTextAlignment,
        [nameof(ILabel.TextDecorations)] = MapTextDecorations,
        [nameof(ILabel.LineHeight)] = MapLineHeight,
        [nameof(IPadding.Padding)] = MapPadding,
        [nameof(IView.Background)] = MapBackground,
    };

    public static CommandMapper<ILabel, LabelHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
    };

    public LabelHandler() : base(Mapper, CommandMapper)
    {
    }

    public LabelHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaLabel CreatePlatformView()
    {
        return new SkiaLabel();
    }

    public static void MapText(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Text = label.Text ?? string.Empty;
    }

    public static void MapTextColor(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView is null) return;

        if (label.TextColor is not null)
            handler.PlatformView.TextColor = label.TextColor.ToSKColor();
    }

    public static void MapFont(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView is null) return;

        var font = label.Font;
        if (font.Size > 0)
            handler.PlatformView.FontSize = (float)font.Size;

        if (!string.IsNullOrEmpty(font.Family))
            handler.PlatformView.FontFamily = font.Family;

        handler.PlatformView.IsBold = font.Weight >= FontWeight.Bold;
        handler.PlatformView.IsItalic = font.Slant == FontSlant.Italic || font.Slant == FontSlant.Oblique;
    }

    public static void MapCharacterSpacing(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.CharacterSpacing = (float)label.CharacterSpacing;
    }

    public static void MapHorizontalTextAlignment(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView is null) return;

        // Map MAUI TextAlignment to our internal TextAlignment
        handler.PlatformView.HorizontalTextAlignment = label.HorizontalTextAlignment switch
        {
            Microsoft.Maui.TextAlignment.Start => Platform.TextAlignment.Start,
            Microsoft.Maui.TextAlignment.Center => Platform.TextAlignment.Center,
            Microsoft.Maui.TextAlignment.End => Platform.TextAlignment.End,
            _ => Platform.TextAlignment.Start
        };
    }

    public static void MapVerticalTextAlignment(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView is null) return;

        handler.PlatformView.VerticalTextAlignment = label.VerticalTextAlignment switch
        {
            Microsoft.Maui.TextAlignment.Start => Platform.TextAlignment.Start,
            Microsoft.Maui.TextAlignment.Center => Platform.TextAlignment.Center,
            Microsoft.Maui.TextAlignment.End => Platform.TextAlignment.End,
            _ => Platform.TextAlignment.Center
        };
    }

    public static void MapTextDecorations(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView is null) return;

        handler.PlatformView.IsUnderline = (label.TextDecorations & TextDecorations.Underline) != 0;
        handler.PlatformView.IsStrikethrough = (label.TextDecorations & TextDecorations.Strikethrough) != 0;
    }

    public static void MapLineHeight(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.LineHeight = (float)label.LineHeight;
    }

    public static void MapPadding(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView is null) return;

        var padding = label.Padding;
        handler.PlatformView.Padding = new SKRect(
            (float)padding.Left,
            (float)padding.Top,
            (float)padding.Right,
            (float)padding.Bottom);
    }

    public static void MapBackground(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView is null) return;

        if (label.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.BackgroundColor = solidPaint.Color.ToSKColor();
        }
    }
}
