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
public partial class LabelHandler : LinuxViewHandler<ILabel, SkiaLabel>, ILabelHandler
{
    ILabel ILabelHandler.VirtualView => VirtualView;
    object ILabelHandler.PlatformView => PlatformView;

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
        // Controls' Label remaps these on MAUI's LabelHandler.Mapper (Label.Mapper.cs),
        // which never reaches this handler's own mapper.
        [nameof(Microsoft.Maui.Controls.Label.TextTransform)] = MapTextTransform,
        [nameof(Microsoft.Maui.Controls.Label.TextType)] = MapTextType,
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
        VisualStateBridge.Attach(VirtualView, platformView);

        if (VirtualView is View view)
        {
            platformView.MauiView = view;

            // Set hand cursor if the label has tap gesture recognizers
            if (view.GestureRecognizers.OfType<TapGestureRecognizer>().Any())
            {
                platformView.CursorType = CursorType.Hand;
            }
        }

        // Explicitly map LineBreakMode on connect - MAUI may not trigger property change for defaults
        if (VirtualView is Microsoft.Maui.Controls.Label mauiLabel)
        {
            platformView.LineBreakMode = mauiLabel.LineBreakMode;
            platformView.MaxLines = EffectiveMaxLines(mauiLabel.LineBreakMode, mauiLabel.MaxLines);
        }
    }

    protected override void DisconnectHandler(SkiaLabel platformView)
    {
        platformView.MauiView = null;
        VisualStateBridge.Detach(platformView);
        base.DisconnectHandler(platformView);
    }

    // The SkiaView base class already routes pointer-up through GestureManager
    // (SkiaView.Input.cs → ProcessPointerUp → ProcessTap), which fires the
    // user's TapGestureRecognizer. Subscribing to SkiaLabel.Tapped here would
    // call ProcessTap a second time, double-firing handlers (e.g. opening the
    // browser twice on link clicks). The subscription is intentionally absent.

    /// <summary>MAUI's <c>MapText</c> signature; maps the property on a Linux <see cref="LabelHandler"/>.</summary>
    public static void MapText(ILabelHandler handler, ILabel label)
    {
        if (handler is LabelHandler linuxHandler)
            MapTextCore(linuxHandler, label);
    }

    private static void MapTextCore(LabelHandler handler, ILabel label)
    {
        handler.PlatformView?.UpdateText(label);
    }

    /// <summary>MAUI's <c>MapTextColor</c> signature; maps the property on a Linux <see cref="LabelHandler"/>.</summary>
    public static void MapTextColor(ILabelHandler handler, ILabel label)
    {
        if (handler is LabelHandler linuxHandler)
            MapTextColorCore(linuxHandler, label);
    }

    private static void MapTextColorCore(LabelHandler handler, ILabel label)
    {
        handler.PlatformView?.UpdateTextColor(label);
    }

    /// <summary>MAUI's <c>MapFont</c> signature; maps the property on a Linux <see cref="LabelHandler"/>.</summary>
    public static void MapFont(ILabelHandler handler, ILabel label)
    {
        if (handler is LabelHandler linuxHandler)
            MapFontCore(linuxHandler, label);
    }

    private static void MapFontCore(LabelHandler handler, ILabel label)
    {
        handler.PlatformView?.UpdateFont(label);
    }

    /// <summary>MAUI's <c>MapCharacterSpacing</c> signature; maps the property on a Linux <see cref="LabelHandler"/>.</summary>
    public static void MapCharacterSpacing(ILabelHandler handler, ILabel label)
    {
        if (handler is LabelHandler linuxHandler)
            MapCharacterSpacingCore(linuxHandler, label);
    }

    private static void MapCharacterSpacingCore(LabelHandler handler, ILabel label)
    {
        handler.PlatformView?.UpdateCharacterSpacing(label);
    }

    /// <summary>MAUI's <c>MapHorizontalTextAlignment</c> signature; maps the property on a Linux <see cref="LabelHandler"/>.</summary>
    public static void MapHorizontalTextAlignment(ILabelHandler handler, ILabel label)
    {
        if (handler is LabelHandler linuxHandler)
            MapHorizontalTextAlignmentCore(linuxHandler, label);
    }

    private static void MapHorizontalTextAlignmentCore(LabelHandler handler, ILabel label)
    {
        handler.PlatformView?.UpdateHorizontalTextAlignment(label);
    }

    /// <summary>MAUI's <c>MapVerticalTextAlignment</c> signature; maps the property on a Linux <see cref="LabelHandler"/>.</summary>
    public static void MapVerticalTextAlignment(ILabelHandler handler, ILabel label)
    {
        if (handler is LabelHandler linuxHandler)
            MapVerticalTextAlignmentCore(linuxHandler, label);
    }

    private static void MapVerticalTextAlignmentCore(LabelHandler handler, ILabel label)
    {
        handler.PlatformView?.UpdateVerticalTextAlignment(label);
    }

    /// <summary>MAUI's <c>MapTextDecorations</c> signature; maps the property on a Linux <see cref="LabelHandler"/>.</summary>
    public static void MapTextDecorations(ILabelHandler handler, ILabel label)
    {
        if (handler is LabelHandler linuxHandler)
            MapTextDecorationsCore(linuxHandler, label);
    }

    private static void MapTextDecorationsCore(LabelHandler handler, ILabel label)
    {
        handler.PlatformView?.UpdateTextDecorations(label);
    }

    /// <summary>MAUI's <c>MapLineHeight</c> signature; maps the property on a Linux <see cref="LabelHandler"/>.</summary>
    public static void MapLineHeight(ILabelHandler handler, ILabel label)
    {
        if (handler is LabelHandler linuxHandler)
            MapLineHeightCore(linuxHandler, label);
    }

    private static void MapLineHeightCore(LabelHandler handler, ILabel label)
    {
        handler.PlatformView?.UpdateLineHeight(label);
    }

    public static void MapLineBreakMode(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView is null) return;

        // LineBreakMode is on Label control, not ILabel interface; the line limit
        // depends on the mode (truncating modes are single-line).
        if (label is Microsoft.Maui.Controls.Label mauiLabel)
            handler.PlatformView.UpdateLineBreakMode(mauiLabel);
    }

    /// <summary>
    /// The number of lines a label shows, by MAUI's rules on its platforms
    /// (Controls' TextViewExtensions.SetLineBreakMode on Android, the UILabel
    /// equivalent on iOS): NoWrap, HeadTruncation and MiddleTruncation are one line;
    /// an unset (or non-positive) MaxLines is one line for TailTruncation and
    /// unlimited (int.MaxValue) otherwise.
    /// </summary>
    internal static int EffectiveMaxLines(LineBreakMode lineBreakMode, int maxLines)
    {
        if (maxLines <= 0)
            maxLines = lineBreakMode == LineBreakMode.TailTruncation ? 1 : int.MaxValue;
        return lineBreakMode switch
        {
            LineBreakMode.NoWrap or LineBreakMode.HeadTruncation or LineBreakMode.MiddleTruncation => 1,
            _ => maxLines,
        };
    }

    /// <summary>The label shows its text (and spans without their own transform) transformed, as MAUI's platforms do.</summary>
    public static void MapTextTransform(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView is null) return;
        if (label is Microsoft.Maui.Controls.Label mauiLabel)
            handler.PlatformView.TextTransform = mauiLabel.TextTransform;
    }

    public static void MapTextType(LabelHandler handler, ILabel label)
    {
        if (handler.PlatformView is null) return;
        if (label is Microsoft.Maui.Controls.Label mauiLabel)
            handler.PlatformView.TextType = mauiLabel.TextType;
    }

    /// <summary>MAUI's <c>MapMaxLines</c> signature; maps the property on a Linux <see cref="LabelHandler"/>.</summary>
    public static void MapMaxLines(ILabelHandler handler, ILabel label)
    {
        if (handler is LabelHandler linuxHandler)
            MapMaxLinesCore(linuxHandler, label);
    }

    private static void MapMaxLinesCore(LabelHandler handler, ILabel label)
    {
        // MaxLines is on Label control, not ILabel interface
        if (label is Microsoft.Maui.Controls.Label mauiLabel)
            handler.PlatformView?.UpdateMaxLines(mauiLabel);
    }

    /// <summary>MAUI's <c>MapPadding</c> signature; maps the property on a Linux <see cref="LabelHandler"/>.</summary>
    public static void MapPadding(ILabelHandler handler, ILabel label)
    {
        if (handler is LabelHandler linuxHandler)
            MapPaddingCore(linuxHandler, label);
    }

    private static void MapPaddingCore(LabelHandler handler, ILabel label)
    {
        handler.PlatformView?.UpdatePadding(label);
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
