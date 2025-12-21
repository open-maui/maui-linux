// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Linux handler for Label control.
/// </summary>
public partial class LabelHandler : ViewHandler<ILabel, SkiaLabel>
{
    /// <summary>
    /// Maps the property mapper for the handler.
    /// </summary>
    public static IPropertyMapper<ILabel, LabelHandler> Mapper = new PropertyMapper<ILabel, LabelHandler>(ViewHandler.ViewMapper)
    {
        [nameof(ILabel.Text)] = MapText,
        [nameof(ILabel.TextColor)] = MapTextColor,
        [nameof(ILabel.Font)] = MapFont,
        [nameof(ILabel.HorizontalTextAlignment)] = MapHorizontalTextAlignment,
        [nameof(ILabel.VerticalTextAlignment)] = MapVerticalTextAlignment,
        [nameof(ILabel.LineBreakMode)] = MapLineBreakMode,
        [nameof(ILabel.MaxLines)] = MapMaxLines,
        [nameof(ILabel.Padding)] = MapPadding,
        [nameof(ILabel.TextDecorations)] = MapTextDecorations,
        [nameof(ILabel.LineHeight)] = MapLineHeight,
        [nameof(ILabel.Background)] = MapBackground,
        ["BackgroundColor"] = MapBackgroundColor,
    };

    /// <summary>
    /// Maps the command mapper for the handler.
    /// </summary>
    public static CommandMapper<ILabel, LabelHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
    };

    public LabelHandler() : base(Mapper, CommandMapper)
    {
    }

    public LabelHandler(IPropertyMapper? mapper)
        : base(mapper ?? Mapper, CommandMapper)
    {
    }

    public LabelHandler(IPropertyMapper? mapper, CommandMapper? commandMapper)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaLabel CreatePlatformView()
    {
        return new SkiaLabel();
    }

    public static void MapText(LabelHandler handler, ILabel label)
    {
        handler.PlatformView.Text = label.Text ?? "";
        handler.PlatformView.Invalidate();
    }

    public static void MapTextColor(LabelHandler handler, ILabel label)
    {
        if (label.TextColor != null)
        {
            handler.PlatformView.TextColor = label.TextColor.ToSKColor();
        }
        handler.PlatformView.Invalidate();
    }

    public static void MapFont(LabelHandler handler, ILabel label)
    {
        var font = label.Font;
        if (font.Family != null)
        {
            handler.PlatformView.FontFamily = font.Family;
        }
        handler.PlatformView.FontSize = (float)font.Size;
        handler.PlatformView.IsBold = font.Weight == FontWeight.Bold;
        handler.PlatformView.IsItalic = font.Slant == FontSlant.Italic;
        handler.PlatformView.Invalidate();
    }

    public static void MapHorizontalTextAlignment(LabelHandler handler, ILabel label)
    {
        handler.PlatformView.HorizontalTextAlignment = label.HorizontalTextAlignment switch
        {
            Microsoft.Maui.TextAlignment.Start => TextAlignment.Start,
            Microsoft.Maui.TextAlignment.Center => TextAlignment.Center,
            Microsoft.Maui.TextAlignment.End => TextAlignment.End,
            _ => TextAlignment.Start
        };
        handler.PlatformView.Invalidate();
    }

    public static void MapVerticalTextAlignment(LabelHandler handler, ILabel label)
    {
        handler.PlatformView.VerticalTextAlignment = label.VerticalTextAlignment switch
        {
            Microsoft.Maui.TextAlignment.Start => TextAlignment.Start,
            Microsoft.Maui.TextAlignment.Center => TextAlignment.Center,
            Microsoft.Maui.TextAlignment.End => TextAlignment.End,
            _ => TextAlignment.Center
        };
        handler.PlatformView.Invalidate();
    }

    public static void MapLineBreakMode(LabelHandler handler, ILabel label)
    {
        handler.PlatformView.LineBreakMode = label.LineBreakMode switch
        {
            Microsoft.Maui.LineBreakMode.NoWrap => LineBreakMode.NoWrap,
            Microsoft.Maui.LineBreakMode.WordWrap => LineBreakMode.WordWrap,
            Microsoft.Maui.LineBreakMode.CharacterWrap => LineBreakMode.CharacterWrap,
            Microsoft.Maui.LineBreakMode.HeadTruncation => LineBreakMode.HeadTruncation,
            Microsoft.Maui.LineBreakMode.TailTruncation => LineBreakMode.TailTruncation,
            Microsoft.Maui.LineBreakMode.MiddleTruncation => LineBreakMode.MiddleTruncation,
            _ => LineBreakMode.TailTruncation
        };
        handler.PlatformView.Invalidate();
    }

    public static void MapMaxLines(LabelHandler handler, ILabel label)
    {
        handler.PlatformView.MaxLines = label.MaxLines;
        handler.PlatformView.Invalidate();
    }

    public static void MapPadding(LabelHandler handler, ILabel label)
    {
        var padding = label.Padding;
        handler.PlatformView.Padding = new SKRect(
            (float)padding.Left,
            (float)padding.Top,
            (float)padding.Right,
            (float)padding.Bottom);
        handler.PlatformView.Invalidate();
    }

    public static void MapTextDecorations(LabelHandler handler, ILabel label)
    {
        var decorations = label.TextDecorations;
        handler.PlatformView.IsUnderline = decorations.HasFlag(TextDecorations.Underline);
        handler.PlatformView.IsStrikethrough = decorations.HasFlag(TextDecorations.Strikethrough);
        handler.PlatformView.Invalidate();
    }

    public static void MapLineHeight(LabelHandler handler, ILabel label)
    {
        handler.PlatformView.LineHeight = (float)label.LineHeight;
        handler.PlatformView.Invalidate();
    }

    public static void MapBackground(LabelHandler handler, ILabel label)
    {
        if (label.Background is SolidColorBrush solidBrush && solidBrush.Color != null)
        {
            handler.PlatformView.BackgroundColor = solidBrush.Color.ToSKColor();
            handler.PlatformView.Invalidate();
        }
    }

    public static void MapBackgroundColor(LabelHandler handler, ILabel label)
    {
        if (label is Microsoft.Maui.Controls.VisualElement ve && ve.BackgroundColor != null)
        {
            handler.PlatformView.BackgroundColor = ve.BackgroundColor.ToSKColor();
            handler.PlatformView.Invalidate();
        }
    }
}
