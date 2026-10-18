// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Handlers;

namespace Microsoft.Maui.Platform;

/// <summary>
/// MAUI's platform extensions for a button (<c>UpdateText(platformButton, button)</c>,
/// <c>UpdateStrokeColor</c>, <c>UpdatePadding</c>, ...) on OpenMaui's <see cref="SkiaButton"/>.
/// Each does what OpenMaui's <c>ButtonHandler</c> mapping of that property does; the handler
/// calls them.
/// </summary>
public static class SkiaButtonExtensions
{
    /// <summary>Shows the button's text.</summary>
    public static void UpdateText(this SkiaButton platformButton, IText text)
    {
        platformButton.Text = text.Text ?? string.Empty;
    }

    /// <summary>Applies the text color (a null color keeps the current one).</summary>
    public static void UpdateTextColor(this SkiaButton platformButton, ITextStyle textStyle)
    {
        if (textStyle.TextColor is not null)
            platformButton.TextColor = textStyle.TextColor;
    }

    /// <summary>Applies the font: size, family and FontAttributes.</summary>
    public static void UpdateFont(this SkiaButton platformButton, ITextStyle textStyle)
    {
        var font = textStyle.Font;
        if (font.Size > 0)
            platformButton.FontSize = font.Size;
        if (!string.IsNullOrEmpty(font.Family))
            platformButton.FontFamily = font.Family;
        platformButton.FontAttributes = TextStyleMapping.ToFontAttributes(font);
    }

    /// <summary>MAUI's signature with a font manager; the Skia view resolves the family itself.</summary>
    public static void UpdateFont(this SkiaButton platformButton, ITextStyle textStyle, IFontManager fontManager) =>
        platformButton.UpdateFont(textStyle);

    /// <summary>Applies the character spacing.</summary>
    public static void UpdateCharacterSpacing(this SkiaButton platformButton, ITextStyle textStyle)
    {
        platformButton.CharacterSpacing = textStyle.CharacterSpacing;
    }

    /// <summary>Applies the padding around the content.</summary>
    public static void UpdatePadding(this SkiaButton platformButton, IPadding padding)
    {
        var value = padding.Padding;
        platformButton.Padding = new Thickness(value.Left, value.Top, value.Right, value.Bottom);
    }

    /// <summary>Applies the border color (a null color keeps the current one).</summary>
    public static void UpdateStrokeColor(this SkiaButton platformButton, IButtonStroke buttonStroke)
    {
        if (buttonStroke.StrokeColor is not null)
            platformButton.BorderColor = buttonStroke.StrokeColor;
    }

    /// <summary>Applies the border width.</summary>
    public static void UpdateStrokeThickness(this SkiaButton platformButton, IButtonStroke buttonStroke)
    {
        platformButton.BorderWidth = buttonStroke.StrokeThickness;
    }

    /// <summary>Applies the corner radius.</summary>
    public static void UpdateCornerRadius(this SkiaButton platformButton, IButtonStroke buttonStroke)
    {
        platformButton.CornerRadius = buttonStroke.CornerRadius;
    }
}

/// <summary>
/// MAUI's platform extensions for an image (<c>UpdateAspect(platformImage, image)</c>,
/// <c>UpdateIsAnimationPlaying</c>) on OpenMaui's <see cref="SkiaImage"/>. Each does what
/// OpenMaui's <c>ImageHandler</c> mapping of that property does; the handler calls them.
/// </summary>
public static class SkiaImageExtensions
{
    /// <summary>Applies the aspect (how the picture fills the view).</summary>
    public static void UpdateAspect(this SkiaImage platformImage, IImage image)
    {
        platformImage.Aspect = image.Aspect;
    }

    /// <summary>Starts or stops an animated picture.</summary>
    public static void UpdateIsAnimationPlaying(this SkiaImage platformImage, IImageSourcePart image)
    {
        platformImage.IsAnimationPlaying = image.IsAnimationPlaying;
    }
}
