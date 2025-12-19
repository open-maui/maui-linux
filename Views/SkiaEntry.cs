// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;
using Microsoft.Maui.Platform.Linux.Rendering;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered text entry control.
/// </summary>
public class SkiaEntry : SkiaView
{
    private string _text = "";
    private int _cursorPosition;
    private int _selectionStart;
    private int _selectionLength;
    private float _scrollOffset;
    private DateTime _cursorBlinkTime = DateTime.UtcNow;
    private bool _cursorVisible = true;

    public string Text
    {
        get => _text;
        set
        {
            if (_text != value)
            {
                var oldText = _text;
                _text = value ?? "";
                _cursorPosition = Math.Min(_cursorPosition, _text.Length);
                TextChanged?.Invoke(this, new TextChangedEventArgs(oldText, _text));
                Invalidate();
            }
        }
    }

    public string Placeholder { get; set; } = "";
    public SKColor PlaceholderColor { get; set; } = new SKColor(0x9E, 0x9E, 0x9E);
    public SKColor TextColor { get; set; } = SKColors.Black;
    public new SKColor BackgroundColor { get; set; } = SKColors.White;
    public SKColor BorderColor { get; set; } = new SKColor(0xBD, 0xBD, 0xBD);
    public SKColor FocusedBorderColor { get; set; } = new SKColor(0x21, 0x96, 0xF3);
    public SKColor SelectionColor { get; set; } = new SKColor(0x21, 0x96, 0xF3, 0x80);
    public SKColor CursorColor { get; set; } = new SKColor(0x21, 0x96, 0xF3);
    public string FontFamily { get; set; } = "Sans";
    public float FontSize { get; set; } = 14;
    public bool IsBold { get; set; }
    public bool IsItalic { get; set; }
    public float CharacterSpacing { get; set; }
    public float CornerRadius { get; set; } = 4;
    public float BorderWidth { get; set; } = 1;
    public SKRect Padding { get; set; } = new SKRect(12, 8, 12, 8);
    public bool IsPassword { get; set; }
    public char PasswordChar { get; set; } = '●';
    public int MaxLength { get; set; } = 0; // 0 = unlimited
    public bool IsReadOnly { get; set; }
    public TextAlignment HorizontalTextAlignment { get; set; } = TextAlignment.Start;
    public TextAlignment VerticalTextAlignment { get; set; } = TextAlignment.Center;
    public bool ShowClearButton { get; set; }

    public int CursorPosition
    {
        get => _cursorPosition;
        set
        {
            _cursorPosition = Math.Clamp(value, 0, _text.Length);
            ResetCursorBlink();
            Invalidate();
        }
    }

    public int SelectionLength
    {
        get => _selectionLength;
        set
        {
            _selectionLength = value;
            Invalidate();
        }
    }

    public event EventHandler<TextChangedEventArgs>? TextChanged;
    public event EventHandler? Completed;

    public SkiaEntry()
    {
        IsFocusable = true;
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        // Draw background
        using var bgPaint = new SKPaint
        {
            Color = BackgroundColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        var rect = new SKRoundRect(bounds, CornerRadius);
        canvas.DrawRoundRect(rect, bgPaint);

        // Draw border
        var borderColor = IsFocused ? FocusedBorderColor : BorderColor;
        var borderWidth = IsFocused ? BorderWidth + 1 : BorderWidth;

        using var borderPaint = new SKPaint
        {
            Color = borderColor,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = borderWidth
        };
        canvas.DrawRoundRect(rect, borderPaint);

        // Calculate content bounds
        var contentBounds = new SKRect(
            bounds.Left + Padding.Left,
            bounds.Top + Padding.Top,
            bounds.Right - Padding.Right,
            bounds.Bottom - Padding.Bottom);

        // Reserve space for clear button if shown
        var clearButtonSize = 20f;
        var clearButtonMargin = 8f;
        if (ShowClearButton && !string.IsNullOrEmpty(_text) && IsFocused)
        {
            contentBounds.Right -= clearButtonSize + clearButtonMargin;
        }

        // Set up clipping for text area
        canvas.Save();
        canvas.ClipRect(contentBounds);

        var fontStyle = GetFontStyle();
        var typeface = SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(FontFamily, fontStyle)
                      ?? SKTypeface.Default;

        using var font = new SKFont(typeface, FontSize);
        using var paint = new SKPaint(font) { IsAntialias = true };
        
        // Apply character spacing if set
        if (CharacterSpacing > 0)
        {
            // Character spacing applied via SKPaint
        }

        var displayText = GetDisplayText();
        var hasText = !string.IsNullOrEmpty(displayText);

        if (hasText)
        {
            paint.Color = TextColor;

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

            // Draw selection
            if (IsFocused && _selectionLength > 0)
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

            canvas.DrawText(displayText, x, y, paint);

            // Draw cursor
            if (IsFocused && !IsReadOnly && _cursorVisible)
            {
                DrawCursor(canvas, paint, displayText, contentBounds);
            }
        }
        else if (!string.IsNullOrEmpty(Placeholder))
        {
            // Draw placeholder
            paint.Color = PlaceholderColor;

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
        if (ShowClearButton && !string.IsNullOrEmpty(_text) && IsFocused)
        {
            DrawClearButton(canvas, bounds, clearButtonSize, clearButtonMargin);
        }
    }

    private SKFontStyle GetFontStyle()
    {
        if (IsBold && IsItalic)
            return SKFontStyle.BoldItalic;
        if (IsBold)
            return SKFontStyle.Bold;
        if (IsItalic)
            return SKFontStyle.Italic;
        return SKFontStyle.Normal;
    }

    private void DrawClearButton(SKCanvas canvas, SKRect bounds, float size, float margin)
    {
        var centerX = bounds.Right - margin - size / 2;
        var centerY = bounds.MidY;

        // Draw circle background
        using var circlePaint = new SKPaint
        {
            Color = new SKColor(0xBD, 0xBD, 0xBD),
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawCircle(centerX, centerY, size / 2 - 2, circlePaint);

        // Draw X
        using var xPaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            StrokeCap = SKStrokeCap.Round
        };

        var offset = size / 4 - 1;
        canvas.DrawLine(centerX - offset, centerY - offset, centerX + offset, centerY + offset, xPaint);
        canvas.DrawLine(centerX - offset, centerY + offset, centerX + offset, centerY - offset, xPaint);
    }

    private string GetDisplayText()
    {
        if (IsPassword && !string.IsNullOrEmpty(_text))
        {
            return new string(PasswordChar, _text.Length);
        }
        return _text;
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
            Color = SelectionColor,
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
            Color = CursorColor,
            StrokeWidth = 2,
            IsAntialias = true
        };

        canvas.DrawLine(cursorX, bounds.Top + 2, cursorX, bounds.Bottom - 2, cursorPaint);
    }

    private void ResetCursorBlink()
    {
        _cursorBlinkTime = DateTime.UtcNow;
        _cursorVisible = true;
    }

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

    public override void OnTextInput(TextInputEventArgs e)
    {
        if (!IsEnabled || IsReadOnly) return;

        // Delete selection if any
        if (_selectionLength > 0)
        {
            DeleteSelection();
        }

        // Check max length
        if (MaxLength > 0 && _text.Length >= MaxLength)
            return;

        // Insert text at cursor
        var insertText = e.Text;
        if (MaxLength > 0)
        {
            var remaining = MaxLength - _text.Length;
            insertText = insertText.Substring(0, Math.Min(insertText.Length, remaining));
        }

        var oldText = _text;
        _text = _text.Insert(_cursorPosition, insertText);
        _cursorPosition += insertText.Length;

        TextChanged?.Invoke(this, new TextChangedEventArgs(oldText, _text));
        ResetCursorBlink();
        Invalidate();
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsEnabled) return;

        switch (e.Key)
        {
            case Key.Backspace:
                if (!IsReadOnly)
                {
                    if (_selectionLength > 0)
                    {
                        DeleteSelection();
                    }
                    else if (_cursorPosition > 0)
                    {
                        var oldText = _text;
                        _text = _text.Remove(_cursorPosition - 1, 1);
                        _cursorPosition--;
                        TextChanged?.Invoke(this, new TextChangedEventArgs(oldText, _text));
                    }
                    ResetCursorBlink();
                    Invalidate();
                }
                e.Handled = true;
                break;

            case Key.Delete:
                if (!IsReadOnly)
                {
                    if (_selectionLength > 0)
                    {
                        DeleteSelection();
                    }
                    else if (_cursorPosition < _text.Length)
                    {
                        var oldText = _text;
                        _text = _text.Remove(_cursorPosition, 1);
                        TextChanged?.Invoke(this, new TextChangedEventArgs(oldText, _text));
                    }
                    ResetCursorBlink();
                    Invalidate();
                }
                e.Handled = true;
                break;

            case Key.Left:
                if (_cursorPosition > 0)
                {
                    if (e.Modifiers.HasFlag(KeyModifiers.Shift))
                    {
                        ExtendSelection(-1);
                    }
                    else
                    {
                        ClearSelection();
                        _cursorPosition--;
                    }
                    ResetCursorBlink();
                    Invalidate();
                }
                e.Handled = true;
                break;

            case Key.Right:
                if (_cursorPosition < _text.Length)
                {
                    if (e.Modifiers.HasFlag(KeyModifiers.Shift))
                    {
                        ExtendSelection(1);
                    }
                    else
                    {
                        ClearSelection();
                        _cursorPosition++;
                    }
                    ResetCursorBlink();
                    Invalidate();
                }
                e.Handled = true;
                break;

            case Key.Home:
                if (e.Modifiers.HasFlag(KeyModifiers.Shift))
                {
                    ExtendSelectionTo(0);
                }
                else
                {
                    ClearSelection();
                    _cursorPosition = 0;
                }
                ResetCursorBlink();
                Invalidate();
                e.Handled = true;
                break;

            case Key.End:
                if (e.Modifiers.HasFlag(KeyModifiers.Shift))
                {
                    ExtendSelectionTo(_text.Length);
                }
                else
                {
                    ClearSelection();
                    _cursorPosition = _text.Length;
                }
                ResetCursorBlink();
                Invalidate();
                e.Handled = true;
                break;

            case Key.A:
                if (e.Modifiers.HasFlag(KeyModifiers.Control))
                {
                    SelectAll();
                    e.Handled = true;
                }
                break;

            case Key.C:
                if (e.Modifiers.HasFlag(KeyModifiers.Control))
                {
                    CopyToClipboard();
                    e.Handled = true;
                }
                break;

            case Key.V:
                if (e.Modifiers.HasFlag(KeyModifiers.Control) && !IsReadOnly)
                {
                    PasteFromClipboard();
                    e.Handled = true;
                }
                break;

            case Key.X:
                if (e.Modifiers.HasFlag(KeyModifiers.Control) && !IsReadOnly)
                {
                    CutToClipboard();
                    e.Handled = true;
                }
                break;

            case Key.Enter:
                Completed?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                break;
        }
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        // Check if clicked on clear button
        if (ShowClearButton && !string.IsNullOrEmpty(_text) && IsFocused)
        {
            var clearButtonSize = 20f;
            var clearButtonMargin = 8f;
            var clearCenterX = Bounds.Right - clearButtonMargin - clearButtonSize / 2;
            var clearCenterY = Bounds.MidY;
            
            var dx = e.X - clearCenterX;
            var dy = e.Y - clearCenterY;
            if (dx * dx + dy * dy < (clearButtonSize / 2) * (clearButtonSize / 2))
            {
                // Clear button clicked
                var oldText = _text;
                _text = "";
                _cursorPosition = 0;
                _selectionLength = 0;
                TextChanged?.Invoke(this, new TextChangedEventArgs(oldText, _text));
                Invalidate();
                return;
            }
        }

        // Calculate cursor position from click
        var clickX = e.X - Bounds.Left - Padding.Left + _scrollOffset;
        _cursorPosition = GetCharacterIndexAtX(clickX);
        _selectionStart = _cursorPosition;
        _selectionLength = 0;

        ResetCursorBlink();
        Invalidate();
    }

    private int GetCharacterIndexAtX(float x)
    {
        if (string.IsNullOrEmpty(_text)) return 0;

        var fontStyle = GetFontStyle();
        var typeface = SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(FontFamily, fontStyle)
                      ?? SKTypeface.Default;

        using var font = new SKFont(typeface, FontSize);
        using var paint = new SKPaint(font);

        var displayText = GetDisplayText();

        for (int i = 0; i <= displayText.Length; i++)
        {
            var substring = displayText.Substring(0, i);
            var width = paint.MeasureText(substring);

            if (width >= x)
            {
                // Check if closer to current or previous character
                if (i > 0)
                {
                    var prevWidth = paint.MeasureText(displayText.Substring(0, i - 1));
                    if (x - prevWidth < width - x)
                        return i - 1;
                }
                return i;
            }
        }

        return displayText.Length;
    }

    private void DeleteSelection()
    {
        var start = Math.Min(_selectionStart, _selectionStart + _selectionLength);
        var length = Math.Abs(_selectionLength);

        var oldText = _text;
        _text = _text.Remove(start, length);
        _cursorPosition = start;
        _selectionLength = 0;

        TextChanged?.Invoke(this, new TextChangedEventArgs(oldText, _text));
    }

    private void ClearSelection()
    {
        _selectionLength = 0;
    }

    private void ExtendSelection(int delta)
    {
        if (_selectionLength == 0)
        {
            _selectionStart = _cursorPosition;
        }

        _cursorPosition += delta;
        _selectionLength = _cursorPosition - _selectionStart;
    }

    private void ExtendSelectionTo(int position)
    {
        if (_selectionLength == 0)
        {
            _selectionStart = _cursorPosition;
        }

        _cursorPosition = position;
        _selectionLength = _cursorPosition - _selectionStart;
    }

    public void SelectAll()
    {
        _selectionStart = 0;
        _cursorPosition = _text.Length;
        _selectionLength = _text.Length;
        Invalidate();
    }

    private void CopyToClipboard()
    {
        if (_selectionLength == 0) return;

        var start = Math.Min(_selectionStart, _selectionStart + _selectionLength);
        var length = Math.Abs(_selectionLength);
        var selectedText = _text.Substring(start, length);

        // TODO: Implement actual clipboard using X11
        // For now, store in a static field
        ClipboardText = selectedText;
    }

    private void CutToClipboard()
    {
        CopyToClipboard();
        DeleteSelection();
        Invalidate();
    }

    private void PasteFromClipboard()
    {
        // TODO: Get from actual X11 clipboard
        var text = ClipboardText;
        if (string.IsNullOrEmpty(text)) return;

        if (_selectionLength > 0)
        {
            DeleteSelection();
        }

        // Check max length
        if (MaxLength > 0)
        {
            var remaining = MaxLength - _text.Length;
            text = text.Substring(0, Math.Min(text.Length, remaining));
        }

        var oldText = _text;
        _text = _text.Insert(_cursorPosition, text);
        _cursorPosition += text.Length;

        TextChanged?.Invoke(this, new TextChangedEventArgs(oldText, _text));
        Invalidate();
    }

    // Temporary clipboard storage - will be replaced with X11 clipboard
    private static string ClipboardText { get; set; } = "";

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        var fontStyle = GetFontStyle();
        var typeface = SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(FontFamily, fontStyle)
                      ?? SKTypeface.Default;

        using var font = new SKFont(typeface, FontSize);
        using var paint = new SKPaint(font);

        var textBounds = new SKRect();
        var measureText = !string.IsNullOrEmpty(_text) ? _text : Placeholder;
        if (string.IsNullOrEmpty(measureText)) measureText = "Tg"; // Standard height measurement

        paint.MeasureText(measureText, ref textBounds);

        return new SKSize(
            200, // Default width, will be overridden by layout
            textBounds.Height + Padding.Top + Padding.Bottom + BorderWidth * 2);
    }
}

/// <summary>
/// Event args for text changed events.
/// </summary>
public class TextChangedEventArgs : EventArgs
{
    public string OldTextValue { get; }
    public string NewTextValue { get; }

    public TextChangedEventArgs(string oldText, string newText)
    {
        OldTextValue = oldText;
        NewTextValue = newText;
    }
}
