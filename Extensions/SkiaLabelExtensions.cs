// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Handlers;

namespace Microsoft.Maui.Platform;

/// <summary>
/// MAUI's platform extensions for a label (<c>UpdateText(platformLabel, label)</c>,
/// <c>UpdateTextColor</c>, <c>UpdateFont</c>, ...) on OpenMaui's <see cref="SkiaLabel"/>.
/// Each does what OpenMaui's <c>LabelHandler</c> mapping of that property does; the handler
/// calls them.
/// </summary>
public static class SkiaLabelExtensions
{
    /// <summary>Shows the label's text.</summary>
    public static void UpdateText(this SkiaLabel platformLabel, ILabel label)
    {
        platformLabel.Text = label.Text ?? string.Empty;
    }

    /// <summary>Applies the text color (a null color keeps the current one).</summary>
    public static void UpdateTextColor(this SkiaLabel platformLabel, ITextStyle textStyle)
    {
        if (textStyle.TextColor is not null)
            platformLabel.TextColor = textStyle.TextColor;
    }

    /// <summary>Applies the font: size, family and FontAttributes (bold from weight Bold up, italic for italic or oblique).</summary>
    public static void UpdateFont(this SkiaLabel platformLabel, ITextStyle textStyle)
    {
        var font = textStyle.Font;
        if (font.Size > 0)
            platformLabel.FontSize = font.Size;
        if (!string.IsNullOrEmpty(font.Family))
            platformLabel.FontFamily = font.Family;
        platformLabel.FontAttributes = TextStyleMapping.ToFontAttributes(font);
    }

    /// <summary>
    /// MAUI's signature with a font manager. The Skia view resolves the font family itself,
    /// so the manager is not used.
    /// </summary>
    public static void UpdateFont(this SkiaLabel platformLabel, ITextStyle textStyle, IFontManager fontManager) =>
        platformLabel.UpdateFont(textStyle);

    /// <summary>Applies the character spacing.</summary>
    public static void UpdateCharacterSpacing(this SkiaLabel platformLabel, ITextStyle textStyle)
    {
        platformLabel.CharacterSpacing = textStyle.CharacterSpacing;
    }

    /// <summary>Applies the horizontal text alignment.</summary>
    public static void UpdateHorizontalTextAlignment(this SkiaLabel platformLabel, ITextAlignment textAlignment)
    {
        platformLabel.HorizontalTextAlignment = textAlignment.HorizontalTextAlignment switch
        {
            TextAlignment.Center => TextAlignment.Center,
            TextAlignment.End => TextAlignment.End,
            _ => TextAlignment.Start,
        };
    }

    /// <summary>Applies the vertical text alignment.</summary>
    public static void UpdateVerticalTextAlignment(this SkiaLabel platformLabel, ITextAlignment textAlignment)
    {
        platformLabel.VerticalTextAlignment = textAlignment.VerticalTextAlignment switch
        {
            TextAlignment.Start => TextAlignment.Start,
            TextAlignment.End => TextAlignment.End,
            _ => TextAlignment.Center,
        };
    }

    /// <summary>Applies the text decorations (underline, strikethrough).</summary>
    public static void UpdateTextDecorations(this SkiaLabel platformLabel, ILabel label)
    {
        platformLabel.TextDecorations = label.TextDecorations;
    }

    /// <summary>Applies the line height multiplier.</summary>
    public static void UpdateLineHeight(this SkiaLabel platformLabel, ILabel label)
    {
        platformLabel.LineHeight = label.LineHeight;
    }

    /// <summary>Applies the padding around the text.</summary>
    public static void UpdatePadding(this SkiaLabel platformLabel, ILabel label)
    {
        var padding = label.Padding;
        platformLabel.Padding = new Thickness(padding.Left, padding.Top, padding.Right, padding.Bottom);
    }

    /// <summary>
    /// Applies the Controls label's LineBreakMode, and the line limit that follows from it
    /// (the truncating and no-wrap modes are single-line; TailTruncation without MaxLines is one line).
    /// </summary>
    public static void UpdateLineBreakMode(this SkiaLabel platformLabel, Label label)
    {
        platformLabel.LineBreakMode = label.LineBreakMode;
        platformLabel.MaxLines = LabelHandler.EffectiveMaxLines(label.LineBreakMode, label.MaxLines);
    }

    /// <summary>Applies the Controls label's MaxLines, as limited by its LineBreakMode.</summary>
    public static void UpdateMaxLines(this SkiaLabel platformLabel, Label label)
    {
        platformLabel.MaxLines = LabelHandler.EffectiveMaxLines(label.LineBreakMode, label.MaxLines);
    }
}
