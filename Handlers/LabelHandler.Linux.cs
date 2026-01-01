// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Window;
using Microsoft.Maui.Primitives;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Linux handler for Label control.
/// </summary>
public class LabelHandler : ViewHandler<ILabel, SkiaLabel>
{
    public static IPropertyMapper<ILabel, LabelHandler> Mapper = new PropertyMapper<ILabel, LabelHandler>(ViewHandler.ViewMapper)
    {
        ["Text"] = MapText,
        ["TextColor"] = MapTextColor,
        ["Font"] = MapFont,
        ["CharacterSpacing"] = MapCharacterSpacing,
        ["HorizontalTextAlignment"] = MapHorizontalTextAlignment,
        ["VerticalTextAlignment"] = MapVerticalTextAlignment,
        ["TextDecorations"] = MapTextDecorations,
        ["LineHeight"] = MapLineHeight,
        ["LineBreakMode"] = MapLineBreakMode,
        ["MaxLines"] = MapMaxLines,
        ["Padding"] = MapPadding,
        ["Background"] = MapBackground,
        ["VerticalLayoutAlignment"] = MapVerticalLayoutAlignment,
        ["HorizontalLayoutAlignment"] = MapHorizontalLayoutAlignment,
        ["FormattedText"] = MapFormattedText
    };

    public static CommandMapper<ILabel, LabelHandler> CommandMapper = new(ViewHandler.ViewCommandMapper);

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

    protected override void ConnectHandler(SkiaLabel platformView)
    {
        base.ConnectHandler(platformView);

        if (VirtualView is View view)
        {
            platformView.MauiView = view;
            if (view.GestureRecognizers.OfType<TapGestureRecognizer>().Any())
            {
                platformView.CursorType = CursorType.Hand;
            }
        }

        platformView.Tapped += OnPlatformViewTapped;
    }

    protected override void DisconnectHandler(SkiaLabel platformView)
    {
        platformView.Tapped -= OnPlatformViewTapped;
        platformView.MauiView = null;
        base.DisconnectHandler(platformView);
    }

    private void OnPlatformViewTapped(object? sender, EventArgs e)
    {
        if (VirtualView is View view)
        {
            GestureManager.ProcessTap(view, 0.0, 0.0);
        }
    }

    public static void MapText(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.Text = label.Text ?? string.Empty;
        }
    }

    public static void MapTextColor(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView != null && label.TextColor != null)
        {
            handler.PlatformView.TextColor = label.TextColor.ToSKColor();
        }
    }

    public static void MapFont(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView != null)
        {
            var font = label.Font;
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

    public static void MapCharacterSpacing(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.CharacterSpacing = (float)label.CharacterSpacing;
        }
    }

    public static void MapHorizontalTextAlignment(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.HorizontalTextAlignment = (int)label.HorizontalTextAlignment switch
            {
                0 => TextAlignment.Start,
                1 => TextAlignment.Center,
                2 => TextAlignment.End,
                _ => TextAlignment.Start
            };
        }
    }

    public static void MapVerticalTextAlignment(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.VerticalTextAlignment = (int)label.VerticalTextAlignment switch
            {
                0 => TextAlignment.Start,
                1 => TextAlignment.Center,
                2 => TextAlignment.End,
                _ => TextAlignment.Center
            };
        }
    }

    public static void MapTextDecorations(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.IsUnderline = (label.TextDecorations & TextDecorations.Underline) != 0;
            handler.PlatformView.IsStrikethrough = (label.TextDecorations & TextDecorations.Strikethrough) != 0;
        }
    }

    public static void MapLineHeight(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.LineHeight = (float)label.LineHeight;
        }
    }

    public static void MapLineBreakMode(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView != null)
        {
            if (label is Label mauiLabel)
            {
                handler.PlatformView.LineBreakMode = (int)mauiLabel.LineBreakMode switch
                {
                    0 => LineBreakMode.NoWrap,
                    1 => LineBreakMode.WordWrap,
                    2 => LineBreakMode.CharacterWrap,
                    3 => LineBreakMode.HeadTruncation,
                    4 => LineBreakMode.TailTruncation,
                    5 => LineBreakMode.MiddleTruncation,
                    _ => LineBreakMode.TailTruncation
                };
            }
        }
    }

    public static void MapMaxLines(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView != null)
        {
            if (label is Label mauiLabel)
            {
                handler.PlatformView.MaxLines = mauiLabel.MaxLines;
            }
        }
    }

    public static void MapPadding(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView != null)
        {
            var padding = label.Padding;
            handler.PlatformView.Padding = new SKRect(
                (float)padding.Left,
                (float)padding.Top,
                (float)padding.Right,
                (float)padding.Bottom);
        }
    }

    public static void MapBackground(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView != null)
        {
            if (label.Background is SolidPaint solidPaint && solidPaint.Color != null)
            {
                handler.PlatformView.BackgroundColor = solidPaint.Color.ToSKColor();
            }
        }
    }

    public static void MapVerticalLayoutAlignment(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.VerticalOptions = (int)label.VerticalLayoutAlignment switch
            {
                1 => LayoutOptions.Start,
                2 => LayoutOptions.Center,
                3 => LayoutOptions.End,
                0 => LayoutOptions.Fill,
                _ => LayoutOptions.Start
            };
        }
    }

    public static void MapHorizontalLayoutAlignment(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.HorizontalOptions = (int)label.HorizontalLayoutAlignment switch
            {
                1 => LayoutOptions.Start,
                2 => LayoutOptions.Center,
                3 => LayoutOptions.End,
                0 => LayoutOptions.Fill,
                _ => LayoutOptions.Start
            };
        }
    }

    public static void MapFormattedText(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView == null)
        {
            return;
        }

        if (label is not Label mauiLabel)
        {
            handler.PlatformView.FormattedSpans = null;
            return;
        }

        var formattedText = mauiLabel.FormattedText;
        if (formattedText == null || formattedText.Spans.Count == 0)
        {
            handler.PlatformView.FormattedSpans = null;
            return;
        }

        var spans = new List<SkiaTextSpan>();
        foreach (var span in formattedText.Spans)
        {
            var skiaSpan = new SkiaTextSpan
            {
                Text = span.Text ?? "",
                IsBold = span.FontAttributes.HasFlag(FontAttributes.Bold),
                IsItalic = span.FontAttributes.HasFlag(FontAttributes.Italic),
                IsUnderline = (span.TextDecorations & TextDecorations.Underline) != 0,
                IsStrikethrough = (span.TextDecorations & TextDecorations.Strikethrough) != 0,
                CharacterSpacing = (float)span.CharacterSpacing,
                LineHeight = (float)span.LineHeight
            };

            if (span.TextColor != null)
            {
                skiaSpan.TextColor = span.TextColor.ToSKColor();
            }
            if (span.BackgroundColor != null)
            {
                skiaSpan.BackgroundColor = span.BackgroundColor.ToSKColor();
            }
            if (!string.IsNullOrEmpty(span.FontFamily))
            {
                skiaSpan.FontFamily = span.FontFamily;
            }
            if (span.FontSize > 0)
            {
                skiaSpan.FontSize = (float)span.FontSize;
            }

            spans.Add(skiaSpan);
        }

        handler.PlatformView.FormattedSpans = spans;
    }
}
