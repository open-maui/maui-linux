// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Rendering;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public partial class SkiaEditor
{
    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        var paddingLeft = (float)Padding.Left;
        var paddingTop = (float)Padding.Top;
        var paddingRight = (float)Padding.Right;
        var paddingBottom = (float)Padding.Bottom;
        var fontSize = (float)FontSize;
        var lineHeight = (float)LineHeight;
        var cornerRadius = (float)CornerRadius;

        // Update wrap width if bounds changed and re-wrap text
        var newWrapWidth = bounds.Width - paddingLeft - paddingRight;
        if (Math.Abs(newWrapWidth - _wrapWidth) > 1)
        {
            _wrapWidth = newWrapWidth;
            UpdateLines();
        }

        // Draw background
        var bgColor = EditorBackgroundColor != null ? ToSKColor(EditorBackgroundColor) :
            (IsEnabled ? SkiaTheme.BackgroundWhiteSK : SkiaTheme.Gray100SK);
        using var bgPaint = new SKPaint
        {
            Color = bgColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(bounds, cornerRadius), bgPaint);

        // Draw border only if BorderColor is not transparent
        if (BorderColor != null && BorderColor != Colors.Transparent && BorderColor.Alpha > 0)
        {
            using var borderPaint = new SKPaint
            {
                Color = IsFocused ? ToSKColor(CursorColor) : ToSKColor(BorderColor),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = IsFocused ? 2 : 1,
                IsAntialias = true
            };
            canvas.DrawRoundRect(new SKRoundRect(bounds, cornerRadius), borderPaint);
        }

        // Setup text rendering
        using var font = new SKFont(SKTypeface.Default, fontSize);
        var lineSpacing = fontSize * lineHeight;

        // Clip to content area
        var contentRect = new SKRect(
            bounds.Left + paddingLeft,
            bounds.Top + paddingTop,
            bounds.Right - paddingRight,
            bounds.Bottom - paddingBottom);

        canvas.Save();
        canvas.ClipRect(contentRect);
        // Don't translate - let the text draw at absolute positions
        // canvas.Translate(0, -_scrollOffsetY);

        if (string.IsNullOrEmpty(Text) && !string.IsNullOrEmpty(Placeholder))
        {
            using var placeholderPaint = new SKPaint(font)
            {
                Color = GetEffectivePlaceholderColor(),
                IsAntialias = true
            };
            // Handle multiline placeholder text by splitting on newlines
            var placeholderLines = Placeholder.Split('\n');
            var y = contentRect.Top + fontSize;
            foreach (var line in placeholderLines)
            {
                canvas.DrawText(line, contentRect.Left, y, placeholderPaint);
                y += lineSpacing;
            }
        }
        else
        {
            var textColor = GetEffectiveTextColor();
            using var textPaint = new SKPaint(font)
            {
                Color = IsEnabled ? textColor : textColor.WithAlpha(128),
                IsAntialias = true
            };
            using var selectionPaint = new SKPaint
            {
                Color = ToSKColor(SelectionColor),
                Style = SKPaintStyle.Fill
            };

            var y = contentRect.Top + fontSize;
            var charIndex = 0;

            for (int lineIndex = 0; lineIndex < _lines.Count; lineIndex++)
            {
                var line = _lines[lineIndex];
                var x = contentRect.Left;

                // Draw selection for this line if applicable
                if (_selectionStart >= 0 && _selectionLength != 0)
                {
                    // Handle both positive and negative selection lengths
                    var selStart = _selectionLength > 0 ? _selectionStart : _selectionStart + _selectionLength;
                    var selEnd = _selectionLength > 0 ? _selectionStart + _selectionLength : _selectionStart;
                    var lineStart = charIndex;
                    var lineEnd = charIndex + line.Length;

                    if (selEnd > lineStart && selStart < lineEnd)
                    {
                        var selStartInLine = Math.Max(0, selStart - lineStart);
                        var selEndInLine = Math.Min(line.Length, selEnd - lineStart);

                        var startX = x + MeasureText(line.Substring(0, selStartInLine), font);
                        var endX = x + MeasureText(line.Substring(0, selEndInLine), font);

                        canvas.DrawRect(new SKRect(startX, y - fontSize, endX, y + lineSpacing - fontSize), selectionPaint);
                    }
                }

                // Determine if pre-edit text should be displayed on this line
                var (cursorLine, cursorCol) = GetLineColumn(_cursorPosition);
                var displayLine = line;
                var hasPreEditOnThisLine = !string.IsNullOrEmpty(_preEditText) && cursorLine == lineIndex;

                if (hasPreEditOnThisLine)
                {
                    // Insert pre-edit text at cursor position within this line
                    var insertPos = Math.Min(cursorCol, line.Length);
                    displayLine = line.Insert(insertPos, _preEditText);
                }

                // Draw the text with font fallback for emoji/CJK support
                DrawTextWithFallback(canvas, displayLine, x, y, textPaint, SKTypeface.Default);

                // Draw underline for pre-edit (composition) text
                if (hasPreEditOnThisLine)
                {
                    DrawPreEditUnderline(canvas, textPaint, line, x, y, contentRect);
                }

                // Draw cursor if on this line
                if (IsFocused && _cursorVisible)
                {
                    if (cursorLine == lineIndex)
                    {
                        // Account for pre-edit text when calculating cursor position
                        var textToCursor = line.Substring(0, Math.Min(cursorCol, line.Length));
                        var cursorX = x + MeasureText(textToCursor, font);

                        // If there's pre-edit text, cursor goes after it
                        if (hasPreEditOnThisLine && _preEditText.Length > 0)
                        {
                            cursorX += MeasureText(_preEditText, font);
                        }

                        using var cursorPaint = new SKPaint
                        {
                            Color = ToSKColor(CursorColor),
                            Style = SKPaintStyle.Stroke,
                            StrokeWidth = 2,
                            IsAntialias = true
                        };
                        canvas.DrawLine(cursorX, y - fontSize + 2, cursorX, y + 2, cursorPaint);
                    }
                }

                y += lineSpacing;
                charIndex += line.Length + 1;
            }
        }

        canvas.Restore();

        // Draw scrollbar if needed
        var totalHeight = _lines.Count * fontSize * lineHeight;
        if (totalHeight > contentRect.Height)
        {
            DrawScrollbar(canvas, bounds, contentRect.Height, totalHeight);
        }
    }

    private float MeasureText(string text, SKFont font)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        using var paint = new SKPaint(font);
        return paint.MeasureText(text);
    }

    private void DrawScrollbar(SKCanvas canvas, SKRect bounds, float viewHeight, float contentHeight)
    {
        var scrollbarWidth = 6f;
        var scrollbarMargin = 2f;
        var paddingTop = (float)Padding.Top;
        var scrollbarHeight = Math.Max(20, viewHeight * (viewHeight / contentHeight));
        var scrollbarY = bounds.Top + paddingTop + (_scrollOffsetY / contentHeight) * (viewHeight - scrollbarHeight);

        using var paint = new SKPaint
        {
            Color = SkiaTheme.Shadow25SK,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        canvas.DrawRoundRect(new SKRoundRect(
            new SKRect(
                bounds.Right - scrollbarWidth - scrollbarMargin,
                scrollbarY,
                bounds.Right - scrollbarMargin,
                scrollbarY + scrollbarHeight),
            scrollbarWidth / 2), paint);
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

    protected override Size MeasureOverride(Size availableSize)
    {
        if (AutoSize)
        {
            var fontSize = (float)FontSize;
            var lineHeight = (float)LineHeight;
            var lineSpacing = fontSize * lineHeight;
            var verticalPadding = Padding.Top + Padding.Bottom;
            var height = Math.Max(lineSpacing + verticalPadding, _lines.Count * lineSpacing + verticalPadding);
            return new Size(
                availableSize.Width < double.MaxValue ? availableSize.Width : 200,
                Math.Min(height, availableSize.Height < double.MaxValue ? availableSize.Height : 200));
        }

        return new Size(
            availableSize.Width < double.MaxValue ? Math.Min(availableSize.Width, 200) : 200,
            availableSize.Height < double.MaxValue ? Math.Min(availableSize.Height, 150) : 150);
    }
}
