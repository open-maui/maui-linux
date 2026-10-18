// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Rendering;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// A toolbar item's badge (ToolbarItem.BadgeText, BadgeColor, BadgeTextColor, new in MAUI 11),
/// drawn as MAUI's Windows toolbar shows it, a WinUI InfoBadge over the item's icon or text,
/// aligned to its top-right corner and 4 pixels out (Margin 0,-4,-4,0). A non-negative whole
/// number shows the number in a pill 16 pixels high (11-pixel text, 4 pixels each side, at least
/// round); empty or non-numeric text shows a 4-pixel dot. Null text shows no badge. Its colours
/// default to the accent and the text on it (InfoBadge's AccentFillColorDefault and
/// TextOnAccentFillColorPrimary).
/// </summary>
internal static class ToolbarBadge
{
    private const float DotSize = 4f;
    private const float ValueHeight = 16f;
    private const float ValueFontSize = 11f;
    private const float ValueSideMargin = 4f;
    private const float ValueBottomMargin = 2f;
    private const float Offset = 4f;

    /// <summary>
    /// The InfoBadge value Windows shows for <paramref name="text"/>: the number, or -1 (a dot)
    /// for empty or non-numeric text (MAUI's Toolbar.UpdateInfoBadge).
    /// </summary>
    internal static int ValueOf(string text)
    {
        if (text.Length == 0)
            return -1;
        return int.TryParse(text, out var value) && value >= 0 ? value : -1;
    }

    /// <summary>
    /// Where the badge of <paramref name="text"/> goes over <paramref name="content"/> (the
    /// item's icon or text); empty when <paramref name="text"/> is null.
    /// </summary>
    internal static SKRect BoundsFor(SKRect content, string? text)
    {
        if (text == null)
            return SKRect.Empty;
        float right = content.Right + Offset, top = content.Top - Offset;
        int value = ValueOf(text);
        if (value < 0)
            return new SKRect(right - DotSize, top, right, top + DotSize);
        using var font = SkiaFontFactory.Create(ValueFontSize);
        float width = Math.Max(ValueHeight, font.MeasureText(value.ToString(System.Globalization.CultureInfo.CurrentCulture)) + ValueSideMargin * 2);
        return new SKRect(right - width, top, right, top + ValueHeight);
    }

    /// <summary>Draws the badge of <paramref name="text"/> over <paramref name="content"/>; returns where.</summary>
    internal static SKRect Draw(SKCanvas canvas, SKRect content, string? text, Color? background, Color? foreground)
    {
        var bounds = BoundsFor(content, text);
        if (bounds.IsEmpty || text == null)
            return SKRect.Empty;

        bool dark = SkiaTheme.IsDarkMode;
        using var fill = new SKPaint
        {
            Color = background?.ToSKColor() ?? SkiaTheme.PrimarySK,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };
        float radius = bounds.Height / 2;
        canvas.DrawRoundRect(bounds, radius, radius, fill);

        int value = ValueOf(text);
        if (value < 0)
            return bounds;
        using var font = SkiaFontFactory.Create(ValueFontSize);
        using var textPaint = new SKPaint
        {
            Color = foreground?.ToSKColor() ?? (dark ? SKColors.Black : SKColors.White),
            IsAntialias = true,
        };
        // Centred in the text block's area, which keeps 2 pixels free at the bottom.
        float midY = bounds.Top + (bounds.Height - ValueBottomMargin) / 2;
        canvas.DrawText(value.ToString(System.Globalization.CultureInfo.CurrentCulture), bounds.MidX,
            TextRenderingHelper.BaselineForVerticalCenter(font, midY), SKTextAlign.Center, font, textPaint);
        return bounds;
    }
}
