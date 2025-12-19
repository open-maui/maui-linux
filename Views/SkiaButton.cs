// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;
using Microsoft.Maui.Platform.Linux.Rendering;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered button control.
/// </summary>
public class SkiaButton : SkiaView
{
    public string Text { get; set; } = "";
    public SKColor TextColor { get; set; } = SKColors.White;
    public new SKColor BackgroundColor { get; set; } = new SKColor(0x21, 0x96, 0xF3); // Material Blue
    public SKColor PressedBackgroundColor { get; set; } = new SKColor(0x19, 0x76, 0xD2);
    public SKColor DisabledBackgroundColor { get; set; } = new SKColor(0xBD, 0xBD, 0xBD);
    public SKColor HoveredBackgroundColor { get; set; } = new SKColor(0x42, 0xA5, 0xF5);
    public SKColor BorderColor { get; set; } = SKColors.Transparent;
    public string FontFamily { get; set; } = "Sans";
    public float FontSize { get; set; } = 14;
    public bool IsBold { get; set; }
    public bool IsItalic { get; set; }
    public float CharacterSpacing { get; set; }
    public float CornerRadius { get; set; } = 4;
    public float BorderWidth { get; set; } = 0;
    public SKRect Padding { get; set; } = new SKRect(16, 8, 16, 8);

    public bool IsPressed { get; private set; }
    public bool IsHovered { get; private set; }
    private bool _focusFromKeyboard;

    public event EventHandler? Clicked;
    public event EventHandler? Pressed;
    public event EventHandler? Released;

    public SkiaButton()
    {
        IsFocusable = true;
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        // Determine background color based on state
        var bgColor = !IsEnabled ? DisabledBackgroundColor
                    : IsPressed ? PressedBackgroundColor
                    : IsHovered ? HoveredBackgroundColor
                    : BackgroundColor;

        // Draw shadow (for elevation effect)
        if (IsEnabled && !IsPressed)
        {
            DrawShadow(canvas, bounds);
        }

        // Draw background with rounded corners
        using var bgPaint = new SKPaint
        {
            Color = bgColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        var rect = new SKRoundRect(bounds, CornerRadius);
        canvas.DrawRoundRect(rect, bgPaint);

        // Draw border
        if (BorderWidth > 0 && BorderColor != SKColors.Transparent)
        {
            using var borderPaint = new SKPaint
            {
                Color = BorderColor,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = BorderWidth
            };
            canvas.DrawRoundRect(rect, borderPaint);
        }

        // Draw focus ring only for keyboard focus
        if (IsFocused && _focusFromKeyboard)
        {
            using var focusPaint = new SKPaint
            {
                Color = new SKColor(0x21, 0x96, 0xF3, 0x80),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2
            };
            var focusRect = new SKRoundRect(bounds, CornerRadius + 2);
            focusRect.Inflate(2, 2);
            canvas.DrawRoundRect(focusRect, focusPaint);
        }

        // Draw text
        if (!string.IsNullOrEmpty(Text))
        {
            var fontStyle = new SKFontStyle(
                IsBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                IsItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
            var typeface = SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(FontFamily, fontStyle)
                          ?? SKTypeface.Default;

            using var font = new SKFont(typeface, FontSize);
            using var paint = new SKPaint(font)
            {
                Color = IsEnabled ? TextColor : TextColor.WithAlpha(128),
                IsAntialias = true
            };

            // Measure text
            var textBounds = new SKRect();
            paint.MeasureText(Text, ref textBounds);

            // Center text
            var x = bounds.MidX - textBounds.MidX;
            var y = bounds.MidY - textBounds.MidY;

            canvas.DrawText(Text, x, y, paint);
        }
    }

    private void DrawShadow(SKCanvas canvas, SKRect bounds)
    {
        using var shadowPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 50),
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4)
        };

        var shadowRect = new SKRect(
            bounds.Left + 2,
            bounds.Top + 4,
            bounds.Right + 2,
            bounds.Bottom + 4);

        var roundRect = new SKRoundRect(shadowRect, CornerRadius);
        canvas.DrawRoundRect(roundRect, shadowPaint);
    }

    public override void OnPointerEntered(PointerEventArgs e)
    {
        if (!IsEnabled) return;
        IsHovered = true;
        Invalidate();
    }

    public override void OnPointerExited(PointerEventArgs e)
    {
        IsHovered = false;
        if (IsPressed)
        {
            IsPressed = false;
        }
        Invalidate();
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        IsPressed = true;
        _focusFromKeyboard = false;
        Invalidate();
        Pressed?.Invoke(this, EventArgs.Empty);
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        var wasPressed = IsPressed;
        IsPressed = false;
        Invalidate();

        Released?.Invoke(this, EventArgs.Empty);

        // Fire click if released within bounds
        if (wasPressed && Bounds.Contains(new SKPoint(e.X, e.Y)))
        {
            Clicked?.Invoke(this, EventArgs.Empty);
        }
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsEnabled) return;

        // Activate on Enter or Space
        if (e.Key == Key.Enter || e.Key == Key.Space)
        {
            IsPressed = true;
        _focusFromKeyboard = true;
            Invalidate();
            Pressed?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    public override void OnKeyUp(KeyEventArgs e)
    {
        if (!IsEnabled) return;

        if (e.Key == Key.Enter || e.Key == Key.Space)
        {
            if (IsPressed)
            {
                IsPressed = false;
                Invalidate();
                Released?.Invoke(this, EventArgs.Empty);
                Clicked?.Invoke(this, EventArgs.Empty);
            }
            e.Handled = true;
        }
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        if (string.IsNullOrEmpty(Text))
        {
            return new SKSize(
                Padding.Left + Padding.Right + 40, // Minimum width
                Padding.Top + Padding.Bottom + FontSize);
        }

        var fontStyle = new SKFontStyle(
                IsBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                IsItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
        var typeface = SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(FontFamily, fontStyle)
                      ?? SKTypeface.Default;

        using var font = new SKFont(typeface, FontSize);
        using var paint = new SKPaint(font);

        var textBounds = new SKRect();
        paint.MeasureText(Text, ref textBounds);

        return new SKSize(
            textBounds.Width + Padding.Left + Padding.Right,
            textBounds.Height + Padding.Top + Padding.Bottom);
    }
}
