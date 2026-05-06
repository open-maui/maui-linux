// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Rendering;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public partial class SkiaEntry
{
    protected override void DrawBackground(SKCanvas canvas, SKRect bounds)
    {
        // Skip base background drawing if Entry is transparent
        // (transparent Entry is likely inside a Border that handles appearance)
        var bgColor = ToSKColor(EntryBackgroundColor);
        var baseBgColor = GetEffectiveBackgroundColor();
        if (bgColor.Alpha < 10 && baseBgColor.Alpha < 10)
            return;

        // Otherwise let base class draw
        base.DrawBackground(canvas, bounds);
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        var bgColor = ToSKColor(EntryBackgroundColor);
        var isTransparent = bgColor.Alpha < 10; // Consider nearly transparent as transparent

        if (!isTransparent)
        {
            // Draw background
            using var bgPaint = new SKPaint
            {
                Color = bgColor,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };

            var rect = new SKRoundRect(bounds, (float)CornerRadius);
            canvas.DrawRoundRect(rect, bgPaint);

            // Draw border
            var borderColor = IsFocused ? ToSKColor(FocusedBorderColor) : ToSKColor(BorderColor);
            var borderWidth = IsFocused ? (float)BorderWidth + 1 : (float)BorderWidth;

            using var borderPaint = new SKPaint
            {
                Color = borderColor,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = borderWidth
            };
            canvas.DrawRoundRect(rect, borderPaint);
        }

        // Calculate content bounds
        var contentBounds = new SKRect(
            bounds.Left + (float)Padding.Left,
            bounds.Top + (float)Padding.Top,
            bounds.Right - (float)Padding.Right,
            bounds.Bottom - (float)Padding.Bottom);

        // Reserve space for clear button if shown
        var clearButtonSize = 20f;
        var clearButtonMargin = 8f;
        var showClear = ShouldShowClearButton();
        if (showClear)
        {
            contentBounds.Right -= clearButtonSize + clearButtonMargin;
        }

        // Set up clipping for text area
        canvas.Save();
        canvas.ClipRect(contentBounds);

        var fontStyle = GetFontStyle();
        var typeface = SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(GetEffectiveFontFamily(), fontStyle)
                      ?? SKTypeface.Default;

        using var font = new SKFont(typeface, (float)FontSize);
        using var paint = new SKPaint(font) { IsAntialias = true };

        var displayText = GetDisplayText();
        // Append pre-edit text at cursor position for IME composition display
        var preEditInsertPos = Math.Min(_cursorPosition, displayText.Length);
        var displayTextWithPreEdit = string.IsNullOrEmpty(_preEditText)
            ? displayText
            : displayText.Insert(preEditInsertPos, _preEditText);
        var hasText = !string.IsNullOrEmpty(displayTextWithPreEdit);

        if (hasText)
        {
            paint.Color = GetEffectiveTextColor();

            // Measure text to cursor position for scrolling
            var textToCursor = displayText.Substring(0, Math.Min(_cursorPosition, displayText.Length));
            var cursorX = paint.MeasureText(textToCursor);

            // Auto-scroll to keep cursor visible
            if (cursorX - _scrollOffset > contentBounds.Width - 10)
            {
                _scrollOffset = cursorX - contentBounds.Width + 10;
            }
            else if (cursorX - _scrollOffset < 0)
            {
                _scrollOffset = cursorX;
            }

            // Draw selection (check != 0 to handle both forward and backward selection)
            if (IsFocused && _selectionLength != 0)
            {
                DrawSelection(canvas, paint, displayText, contentBounds);
            }

            // Calculate text position based on vertical alignment
            var textBounds = new SKRect();
            paint.MeasureText(displayText, ref textBounds);

            float x = contentBounds.Left - _scrollOffset;
            float y = VerticalTextAlignment switch
            {
                TextAlignment.Start => contentBounds.Top - textBounds.Top,
                TextAlignment.End => contentBounds.Bottom - textBounds.Bottom,
                _ => contentBounds.MidY - textBounds.MidY // Center
            };

            // Draw the text with font fallback for emoji/CJK support
            DrawTextWithFallback(canvas, displayTextWithPreEdit, x, y, paint, typeface);

            // Draw underline for pre-edit (composition) text
            if (!string.IsNullOrEmpty(_preEditText))
            {
                DrawPreEditUnderline(canvas, paint, displayText, x, y, contentBounds);
            }

            // Draw cursor
            if (IsFocused && !IsReadOnly && _cursorVisible)
            {
                DrawCursor(canvas, paint, displayText, contentBounds);
            }
        }
        else if (!string.IsNullOrEmpty(Placeholder))
        {
            // Draw placeholder
            paint.Color = GetEffectivePlaceholderColor();

            var textBounds = new SKRect();
            paint.MeasureText(Placeholder, ref textBounds);

            float x = contentBounds.Left;
            float y = contentBounds.MidY - textBounds.MidY;

            canvas.DrawText(Placeholder, x, y, paint);
        }
        else if (IsFocused && !IsReadOnly && _cursorVisible)
        {
            // Draw cursor even with no text
            DrawCursor(canvas, paint, "", contentBounds);
        }

        canvas.Restore();

        // Draw clear button if applicable
        if (showClear)
        {
            DrawClearButton(canvas, bounds, clearButtonSize, clearButtonMargin);
        }
    }

    private bool ShouldShowClearButton()
    {
        if (string.IsNullOrEmpty(Text)) return false;

        // Check both legacy ShowClearButton and MAUI ClearButtonVisibility
        if (ShowClearButton && IsFocused) return true;

        return ClearButtonVisibility switch
        {
            ClearButtonVisibility.WhileEditing => IsFocused,
            ClearButtonVisibility.Never => false,
            _ => false
        };
    }

    private void DrawClearButton(SKCanvas canvas, SKRect bounds, float size, float margin)
    {
        var centerX = bounds.Right - margin - size / 2;
        var centerY = bounds.MidY;

        // Draw circle background
        using var circlePaint = new SKPaint
        {
            Color = SkiaTheme.Gray400SK,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawCircle(centerX, centerY, size / 2 - 2, circlePaint);

        // Draw X
        using var xPaint = new SKPaint
        {
            Color = SkiaTheme.BackgroundWhiteSK,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            StrokeCap = SKStrokeCap.Round
        };

        var offset = size / 4 - 1;
        canvas.DrawLine(centerX - offset, centerY - offset, centerX + offset, centerY + offset, xPaint);
        canvas.DrawLine(centerX - offset, centerY + offset, centerX + offset, centerY - offset, xPaint);
    }

    private void DrawSelection(SKCanvas canvas, SKPaint paint, string displayText, SKRect bounds)
    {
        var selStart = Math.Min(_selectionStart, _selectionStart + _selectionLength);
        var selEnd = Math.Max(_selectionStart, _selectionStart + _selectionLength);

        var textToStart = displayText.Substring(0, selStart);
        var textToEnd = displayText.Substring(0, selEnd);

        var startX = bounds.Left - _scrollOffset + paint.MeasureText(textToStart);
        var endX = bounds.Left - _scrollOffset + paint.MeasureText(textToEnd);

        using var selPaint = new SKPaint
        {
            Color = ToSKColor(SelectionColor),
            Style = SKPaintStyle.Fill
        };

        canvas.DrawRect(startX, bounds.Top, endX - startX, bounds.Height, selPaint);
    }

    private void DrawCursor(SKCanvas canvas, SKPaint paint, string displayText, SKRect bounds)
    {
        var textToCursor = displayText.Substring(0, Math.Min(_cursorPosition, displayText.Length));
        var cursorX = bounds.Left - _scrollOffset + paint.MeasureText(textToCursor);

        using var cursorPaint = new SKPaint
        {
            Color = ToSKColor(CursorColor),
            StrokeWidth = 2,
            IsAntialias = true
        };

        canvas.DrawLine(cursorX, bounds.Top + 2, cursorX, bounds.Bottom - 2, cursorPaint);
    }

    /// <summary>
    /// Draws text with font fallback for emoji, CJK, and other scripts.
    /// </summary>
    private void DrawTextWithFallback(SKCanvas canvas, string text, float x, float y, SKPaint paint, SKTypeface preferredTypeface)
        => TextRenderingHelper.DrawTextWithFallback(canvas, text, x, y, paint, preferredTypeface, (float)FontSize);

    /// <summary>
    /// Draws underline for IME pre-edit (composition) text.
    /// </summary>
    private void DrawPreEditUnderline(SKCanvas canvas, SKPaint paint, string displayText, float x, float y, SKRect bounds)
        => TextRenderingHelper.DrawPreEditUnderline(canvas, paint, displayText, _cursorPosition, _preEditText, x, y);

    private void ResetCursorBlink()
    {
        _cursorBlinkTime = DateTime.UtcNow;
        _cursorVisible = true;
    }

    /// <summary>
    /// Updates cursor blink animation.
    /// </summary>
    public void UpdateCursorBlink()
    {
        if (!IsFocused) return;

        var elapsed = (DateTime.UtcNow - _cursorBlinkTime).TotalMilliseconds;
        var newVisible = ((int)(elapsed / 500) % 2) == 0;

        if (newVisible != _cursorVisible)
        {
            _cursorVisible = newVisible;
            Invalidate();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var fontStyle = GetFontStyle();
        var typeface = SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(GetEffectiveFontFamily(), fontStyle)
                      ?? SKTypeface.Default;

        using var font = new SKFont(typeface, (float)FontSize);

        // Use font metrics for consistent height regardless of text content
        // This prevents size changes when placeholder disappears or text changes
        var metrics = font.Metrics;
        var textHeight = metrics.Descent - metrics.Ascent + metrics.Leading;

        return new Size(
            200, // Default width, will be overridden by layout
            textHeight + Padding.Top + Padding.Bottom + BorderWidth * 2);
    }
}
