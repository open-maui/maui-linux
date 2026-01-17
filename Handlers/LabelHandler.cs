// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Window;
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
        ["LineBreakMode"] = MapLineBreakMode,
        ["MaxLines"] = MapMaxLines,
        [nameof(IPadding.Padding)] = MapPadding,
        [nameof(IView.Background)] = MapBackground,
        [nameof(IView.VerticalLayoutAlignment)] = MapVerticalLayoutAlignment,
        [nameof(IView.HorizontalLayoutAlignment)] = MapHorizontalLayoutAlignment,
        ["FormattedText"] = MapFormattedText,
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

    protected override void ConnectHandler(SkiaLabel platformView)
    {
        base.ConnectHandler(platformView);

        if (VirtualView is View view)
        {
            platformView.MauiView = view;

            // Set hand cursor if the label has tap gesture recognizers
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
            GestureManager.ProcessTap(view, 0, 0);
        }
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
            handler.PlatformView.TextColor = label.TextColor;
    }

    public static void MapFont(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView is null) return;

        var font = label.Font;
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

    public static void MapCharacterSpacing(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.CharacterSpacing = label.CharacterSpacing;
    }

    public static void MapHorizontalTextAlignment(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView is null) return;

        // Map MAUI TextAlignment to our TextAlignment
        handler.PlatformView.HorizontalTextAlignment = label.HorizontalTextAlignment switch
        {
            Microsoft.Maui.TextAlignment.Start => TextAlignment.Start,
            Microsoft.Maui.TextAlignment.Center => TextAlignment.Center,
            Microsoft.Maui.TextAlignment.End => TextAlignment.End,
            _ => TextAlignment.Start
        };
    }

    public static void MapVerticalTextAlignment(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView is null) return;

        handler.PlatformView.VerticalTextAlignment = label.VerticalTextAlignment switch
        {
            Microsoft.Maui.TextAlignment.Start => TextAlignment.Start,
            Microsoft.Maui.TextAlignment.Center => TextAlignment.Center,
            Microsoft.Maui.TextAlignment.End => TextAlignment.End,
            _ => TextAlignment.Center
        };
    }

    public static void MapTextDecorations(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.TextDecorations = label.TextDecorations;
    }

    public static void MapLineHeight(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.LineHeight = label.LineHeight;
    }

    public static void MapLineBreakMode(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView is null) return;

        // LineBreakMode is on Label control, not ILabel interface
        if (label is Microsoft.Maui.Controls.Label mauiLabel)
        {
            handler.PlatformView.LineBreakMode = mauiLabel.LineBreakMode;
        }
    }

    public static void MapMaxLines(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView is null) return;

        // MaxLines is on Label control, not ILabel interface
        if (label is Microsoft.Maui.Controls.Label mauiLabel)
        {
            handler.PlatformView.MaxLines = mauiLabel.MaxLines;
        }
    }

    public static void MapPadding(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView is null) return;

        var padding = label.Padding;
        handler.PlatformView.Padding = new Thickness(
            padding.Left,
            padding.Top,
            padding.Right,
            padding.Bottom);
    }

    public static void MapBackground(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView is null) return;

        if (label.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.BackgroundColor = solidPaint.Color;
        }
    }

    public static void MapVerticalLayoutAlignment(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView is null) return;

        handler.PlatformView.VerticalOptions = label.VerticalLayoutAlignment switch
        {
            Primitives.LayoutAlignment.Start => LayoutOptions.Start,
            Primitives.LayoutAlignment.Center => LayoutOptions.Center,
            Primitives.LayoutAlignment.End => LayoutOptions.End,
            Primitives.LayoutAlignment.Fill => LayoutOptions.Fill,
            _ => LayoutOptions.Start
        };
    }

    public static void MapHorizontalLayoutAlignment(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView is null) return;

        handler.PlatformView.HorizontalOptions = label.HorizontalLayoutAlignment switch
        {
            Primitives.LayoutAlignment.Start => LayoutOptions.Start,
            Primitives.LayoutAlignment.Center => LayoutOptions.Center,
            Primitives.LayoutAlignment.End => LayoutOptions.End,
            Primitives.LayoutAlignment.Fill => LayoutOptions.Fill,
            _ => LayoutOptions.Start
        };
    }

    public static void MapFormattedText(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView is null) return;

        if (label is not Label mauiLabel)
        {
            handler.PlatformView.FormattedText = null;
            return;
        }

        handler.PlatformView.FormattedText = mauiLabel.FormattedText;
    }
}
