// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Rendering;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Base class for the app-modal Skia dialogs managed by
/// <see cref="LinuxDialogService"/> (alert, prompt, action sheet). Provides
/// the shared look (dimmed overlay, rounded card with shadow, filled buttons),
/// the text-wrapping helper, and the modal input contract: the dialog takes
/// the whole window for its overlay and captures every pointer event.
/// </summary>
public abstract class SkiaModalDialog : SkiaView
{
    // Theme-aware colors (evaluated at draw time so a theme flip while the
    // dialog is open repaints correctly).
    protected static SKColor OverlayColor => SkiaTheme.Overlay50SK;
    protected static SKColor DialogBackground => SkiaTheme.CurrentSurfaceSK;
    protected static SKColor TitleColor => SkiaTheme.CurrentTextSK;
    protected static SKColor MessageColor => SkiaTheme.IsDarkMode ? SkiaTheme.Gray400SK : SkiaTheme.TextSecondarySK;
    protected static SKColor ButtonColor => SkiaTheme.PrimarySK;
    protected static SKColor ButtonHoverColor => SkiaTheme.PrimaryDarkSK;
    protected static SKColor ButtonTextColor => SKColors.White;
    protected static SKColor CancelButtonColor => SkiaTheme.IsDarkMode ? SkiaTheme.Gray600SK : SkiaTheme.ButtonCancelSK;
    protected static SKColor CancelButtonHoverColor => SkiaTheme.IsDarkMode ? SkiaTheme.Gray700SK : SkiaTheme.ButtonCancelHoverSK;
    protected static SKColor DestructiveButtonColor => SkiaTheme.ErrorSK;
    protected static SKColor DestructiveButtonHoverColor => new SKColor(0xB7, 0x1C, 0x1C);

    protected const float DialogWidth = 400;
    protected const float DialogPadding = 24;
    protected const float ButtonHeight = 44;
    protected const float ButtonSpacing = 12;
    protected const float CornerRadius = 12;

    protected SkiaModalDialog()
    {
        IsFocusable = true;
    }

    /// <summary>
    /// Draws the semi-transparent overlay covering the whole window.
    /// </summary>
    protected static void DrawOverlay(SKCanvas canvas, SKRect bounds)
    {
        using var overlayPaint = new SKPaint
        {
            Color = OverlayColor,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(bounds, overlayPaint);
    }

    /// <summary>
    /// Draws the dialog card (drop shadow plus rounded surface) at the given
    /// bounds.
    /// </summary>
    protected static void DrawCard(SKCanvas canvas, SKRect dialogBounds)
    {
        using var shadowPaint = new SKPaint
        {
            Color = SkiaTheme.Shadow25SK,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 8),
            Style = SKPaintStyle.Fill
        };
        var shadowRect = new SKRect(dialogBounds.Left + 4, dialogBounds.Top + 4,
                                     dialogBounds.Right + 4, dialogBounds.Bottom + 4);
        canvas.DrawRoundRect(shadowRect, CornerRadius, CornerRadius, shadowPaint);

        using var bgPaint = new SKPaint
        {
            Color = DialogBackground,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRoundRect(dialogBounds, CornerRadius, CornerRadius, bgPaint);
    }

    /// <summary>
    /// Draws a filled, rounded button with centered bold label.
    /// </summary>
    protected static void DrawButton(SKCanvas canvas, SKRect bounds, string text, SKColor bgColor)
    {
        using var bgPaint = new SKPaint
        {
            Color = bgColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRoundRect(bounds, 8, 8, bgPaint);

        using var font = SkiaFontFactory.Create(16);
        font.Embolden = true;
        using var textPaint = new SKPaint
        {
            Color = ButtonTextColor,
            IsAntialias = true
        };

        font.MeasureText(text, out var textBounds);

        var x = bounds.MidX - textBounds.MidX;
        var y = TextRenderingHelper.BaselineForVerticalCenter(font, bounds.MidY);
        canvas.DrawText(text, x, y, font, textPaint);
    }

    /// <summary>
    /// Greedy word-wrap of <paramref name="text"/> to <paramref name="maxWidth"/>
    /// at the given font size.
    /// </summary>
    protected static List<string> WrapText(string text, float maxWidth, float fontSize)
    {
        var lines = new List<string>();
        if (string.IsNullOrEmpty(text))
            return lines;

        using var font = SkiaFontFactory.Create(fontSize);

        var words = text.Split(' ');
        var currentLine = "";

        foreach (var word in words)
        {
            var testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
            var width = font.MeasureText(testLine);

            if (width > maxWidth && !string.IsNullOrEmpty(currentLine))
            {
                lines.Add(currentLine);
                currentLine = word;
            }
            else
            {
                currentLine = testLine;
            }
        }

        if (!string.IsNullOrEmpty(currentLine))
            lines.Add(currentLine);

        return lines;
    }

    /// <summary>
    /// Removes this dialog from <see cref="LinuxDialogService"/>. Subclasses
    /// call this before completing their result.
    /// </summary>
    protected void Hide()
    {
        LinuxDialogService.HideDialog(this);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Dialog takes full screen for the overlay
        return availableSize;
    }

    public override SkiaView? HitTest(float x, float y)
    {
        // Modal dialogs capture all input
        return this;
    }
}
