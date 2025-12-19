// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered multiline text editor control.
/// </summary>
public class SkiaEditor : SkiaView
{
    private string _text = "";
    private string _placeholder = "";
    private int _cursorPosition;
    private int _selectionStart = -1;
    private int _selectionLength;
    private float _scrollOffsetY;
    private bool _isReadOnly;
    private int _maxLength = -1;
    private bool _cursorVisible = true;
    private DateTime _lastCursorBlink = DateTime.Now;

    // Cached line information
    private List<string> _lines = new() { "" };
    private List<float> _lineHeights = new();

    // Styling
    public SKColor TextColor { get; set; } = SKColors.Black;
    public SKColor PlaceholderColor { get; set; } = new SKColor(0x80, 0x80, 0x80);
    public SKColor BorderColor { get; set; } = new SKColor(0xBD, 0xBD, 0xBD);
    public SKColor SelectionColor { get; set; } = new SKColor(0x21, 0x96, 0xF3, 0x60);
    public SKColor CursorColor { get; set; } = new SKColor(0x21, 0x96, 0xF3);
    public string FontFamily { get; set; } = "Sans";
    public float FontSize { get; set; } = 14;
    public float LineHeight { get; set; } = 1.4f;
    public float CornerRadius { get; set; } = 4;
    public float Padding { get; set; } = 12;
    public bool AutoSize { get; set; }

    public string Text
    {
        get => _text;
        set
        {
            var newText = value ?? "";
            if (_maxLength > 0 && newText.Length > _maxLength)
            {
                newText = newText.Substring(0, _maxLength);
            }

            if (_text != newText)
            {
                _text = newText;
                UpdateLines();
                _cursorPosition = Math.Min(_cursorPosition, _text.Length);
                TextChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
        }
    }

    public string Placeholder
    {
        get => _placeholder;
        set { _placeholder = value ?? ""; Invalidate(); }
    }

    public bool IsReadOnly
    {
        get => _isReadOnly;
        set { _isReadOnly = value; Invalidate(); }
    }

    public int MaxLength
    {
        get => _maxLength;
        set { _maxLength = value; }
    }

    public int CursorPosition
    {
        get => _cursorPosition;
        set
        {
            _cursorPosition = Math.Clamp(value, 0, _text.Length);
            EnsureCursorVisible();
            Invalidate();
        }
    }

    public event EventHandler? TextChanged;
    public event EventHandler? Completed;

    public SkiaEditor()
    {
        IsFocusable = true;
    }

    private void UpdateLines()
    {
        _lines.Clear();
        if (string.IsNullOrEmpty(_text))
        {
            _lines.Add("");
            return;
        }

        var currentLine = "";
        foreach (var ch in _text)
        {
            if (ch == '\n')
            {
                _lines.Add(currentLine);
                currentLine = "";
            }
            else
            {
                currentLine += ch;
            }
        }
        _lines.Add(currentLine);
    }

    private (int line, int column) GetLineColumn(int position)
    {
        var pos = 0;
        for (int i = 0; i < _lines.Count; i++)
        {
            var lineLength = _lines[i].Length;
            if (pos + lineLength >= position || i == _lines.Count - 1)
            {
                return (i, position - pos);
            }
            pos += lineLength + 1; // +1 for newline
        }
        return (_lines.Count - 1, _lines[^1].Length);
    }

    private int GetPosition(int line, int column)
    {
        var pos = 0;
        for (int i = 0; i < line && i < _lines.Count; i++)
        {
            pos += _lines[i].Length + 1;
        }
        if (line < _lines.Count)
        {
            pos += Math.Min(column, _lines[line].Length);
        }
        return Math.Min(pos, _text.Length);
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        // Handle cursor blinking
        if (IsFocused && (DateTime.Now - _lastCursorBlink).TotalMilliseconds > 500)
        {
            _cursorVisible = !_cursorVisible;
            _lastCursorBlink = DateTime.Now;
        }

        // Draw background
        using var bgPaint = new SKPaint
        {
            Color = IsEnabled ? BackgroundColor : new SKColor(0xF5, 0xF5, 0xF5),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(bounds, CornerRadius), bgPaint);

        // Draw border
        using var borderPaint = new SKPaint
        {
            Color = IsFocused ? CursorColor : BorderColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = IsFocused ? 2 : 1,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(bounds, CornerRadius), borderPaint);

        // Setup text rendering
        using var font = new SKFont(SKTypeface.Default, FontSize);
        var lineSpacing = FontSize * LineHeight;

        // Clip to content area
        var contentRect = new SKRect(
            bounds.Left + Padding,
            bounds.Top + Padding,
            bounds.Right - Padding,
            bounds.Bottom - Padding);

        canvas.Save();
        canvas.ClipRect(contentRect);
        canvas.Translate(0, -_scrollOffsetY);

        if (string.IsNullOrEmpty(_text) && !string.IsNullOrEmpty(_placeholder))
        {
            // Draw placeholder
            using var placeholderPaint = new SKPaint(font)
            {
                Color = PlaceholderColor,
                IsAntialias = true
            };
            canvas.DrawText(_placeholder, contentRect.Left, contentRect.Top + FontSize, placeholderPaint);
        }
        else
        {
            // Draw text with selection
            using var textPaint = new SKPaint(font)
            {
                Color = IsEnabled ? TextColor : TextColor.WithAlpha(128),
                IsAntialias = true
            };
            using var selectionPaint = new SKPaint
            {
                Color = SelectionColor,
                Style = SKPaintStyle.Fill
            };

            var y = contentRect.Top + FontSize;
            var charIndex = 0;

            for (int lineIndex = 0; lineIndex < _lines.Count; lineIndex++)
            {
                var line = _lines[lineIndex];
                var x = contentRect.Left;

                // Draw selection for this line if applicable
                if (_selectionStart >= 0 && _selectionLength > 0)
                {
                    var selEnd = _selectionStart + _selectionLength;
                    var lineStart = charIndex;
                    var lineEnd = charIndex + line.Length;

                    if (selEnd > lineStart && _selectionStart < lineEnd)
                    {
                        var selStartInLine = Math.Max(0, _selectionStart - lineStart);
                        var selEndInLine = Math.Min(line.Length, selEnd - lineStart);

                        var startX = x + MeasureText(line.Substring(0, selStartInLine), font);
                        var endX = x + MeasureText(line.Substring(0, selEndInLine), font);

                        canvas.DrawRect(new SKRect(startX, y - FontSize, endX, y + lineSpacing - FontSize), selectionPaint);
                    }
                }

                // Draw line text
                canvas.DrawText(line, x, y, textPaint);

                // Draw cursor if on this line
                if (IsFocused && _cursorVisible)
                {
                    var (cursorLine, cursorCol) = GetLineColumn(_cursorPosition);
                    if (cursorLine == lineIndex)
                    {
                        var cursorX = x + MeasureText(line.Substring(0, Math.Min(cursorCol, line.Length)), font);
                        using var cursorPaint = new SKPaint
                        {
                            Color = CursorColor,
                            Style = SKPaintStyle.Stroke,
                            StrokeWidth = 2,
                            IsAntialias = true
                        };
                        canvas.DrawLine(cursorX, y - FontSize + 2, cursorX, y + 2, cursorPaint);
                    }
                }

                y += lineSpacing;
                charIndex += line.Length + 1; // +1 for newline
            }
        }

        canvas.Restore();

        // Draw scrollbar if needed
        var totalHeight = _lines.Count * FontSize * LineHeight;
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
        var scrollbarHeight = Math.Max(20, viewHeight * (viewHeight / contentHeight));
        var scrollbarY = bounds.Top + Padding + (_scrollOffsetY / contentHeight) * (viewHeight - scrollbarHeight);

        using var paint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 60),
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

    private void EnsureCursorVisible()
    {
        var (line, col) = GetLineColumn(_cursorPosition);
        var lineSpacing = FontSize * LineHeight;
        var cursorY = line * lineSpacing;
        var viewHeight = Bounds.Height - Padding * 2;

        if (cursorY < _scrollOffsetY)
        {
            _scrollOffsetY = cursorY;
        }
        else if (cursorY + lineSpacing > _scrollOffsetY + viewHeight)
        {
            _scrollOffsetY = cursorY + lineSpacing - viewHeight;
        }
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        // Request focus by notifying parent
        IsFocused = true;

        // Calculate cursor position from click
        var contentX = e.X - Bounds.Left - Padding;
        var contentY = e.Y - Bounds.Top - Padding + _scrollOffsetY;

        var lineSpacing = FontSize * LineHeight;
        var clickedLine = Math.Clamp((int)(contentY / lineSpacing), 0, _lines.Count - 1);

        using var font = new SKFont(SKTypeface.Default, FontSize);
        var line = _lines[clickedLine];
        var clickedCol = 0;

        // Find closest character position
        for (int i = 0; i <= line.Length; i++)
        {
            var charX = MeasureText(line.Substring(0, i), font);
            if (charX > contentX)
            {
                clickedCol = i > 0 ? i - 1 : 0;
                break;
            }
            clickedCol = i;
        }

        _cursorPosition = GetPosition(clickedLine, clickedCol);
        _selectionStart = -1;
        _selectionLength = 0;
        _cursorVisible = true;
        _lastCursorBlink = DateTime.Now;

        Invalidate();
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsEnabled) return;

        var (line, col) = GetLineColumn(_cursorPosition);
        _cursorVisible = true;
        _lastCursorBlink = DateTime.Now;

        switch (e.Key)
        {
            case Key.Left:
                if (_cursorPosition > 0)
                {
                    _cursorPosition--;
                    EnsureCursorVisible();
                }
                e.Handled = true;
                break;

            case Key.Right:
                if (_cursorPosition < _text.Length)
                {
                    _cursorPosition++;
                    EnsureCursorVisible();
                }
                e.Handled = true;
                break;

            case Key.Up:
                if (line > 0)
                {
                    _cursorPosition = GetPosition(line - 1, col);
                    EnsureCursorVisible();
                }
                e.Handled = true;
                break;

            case Key.Down:
                if (line < _lines.Count - 1)
                {
                    _cursorPosition = GetPosition(line + 1, col);
                    EnsureCursorVisible();
                }
                e.Handled = true;
                break;

            case Key.Home:
                _cursorPosition = GetPosition(line, 0);
                EnsureCursorVisible();
                e.Handled = true;
                break;

            case Key.End:
                _cursorPosition = GetPosition(line, _lines[line].Length);
                EnsureCursorVisible();
                e.Handled = true;
                break;

            case Key.Enter:
                if (!_isReadOnly)
                {
                    InsertText("\n");
                }
                e.Handled = true;
                break;

            case Key.Backspace:
                if (!_isReadOnly && _cursorPosition > 0)
                {
                    Text = _text.Remove(_cursorPosition - 1, 1);
                    _cursorPosition--;
                    EnsureCursorVisible();
                }
                e.Handled = true;
                break;

            case Key.Delete:
                if (!_isReadOnly && _cursorPosition < _text.Length)
                {
                    Text = _text.Remove(_cursorPosition, 1);
                }
                e.Handled = true;
                break;

            case Key.Tab:
                if (!_isReadOnly)
                {
                    InsertText("    "); // 4 spaces for tab
                }
                e.Handled = true;
                break;
        }

        Invalidate();
    }

    public override void OnTextInput(TextInputEventArgs e)
    {
        if (!IsEnabled || _isReadOnly) return;

        if (!string.IsNullOrEmpty(e.Text))
        {
            InsertText(e.Text);
            e.Handled = true;
        }
    }

    private void InsertText(string text)
    {
        if (_selectionLength > 0)
        {
            // Replace selection
            _text = _text.Remove(_selectionStart, _selectionLength);
            _cursorPosition = _selectionStart;
            _selectionStart = -1;
            _selectionLength = 0;
        }

        if (_maxLength > 0 && _text.Length + text.Length > _maxLength)
        {
            text = text.Substring(0, Math.Max(0, _maxLength - _text.Length));
        }

        if (!string.IsNullOrEmpty(text))
        {
            Text = _text.Insert(_cursorPosition, text);
            _cursorPosition += text.Length;
            EnsureCursorVisible();
        }
    }

    public override void OnScroll(ScrollEventArgs e)
    {
        var lineSpacing = FontSize * LineHeight;
        var totalHeight = _lines.Count * lineSpacing;
        var viewHeight = Bounds.Height - Padding * 2;
        var maxScroll = Math.Max(0, totalHeight - viewHeight);

        _scrollOffsetY = Math.Clamp(_scrollOffsetY - e.DeltaY * 3, 0, maxScroll);
        Invalidate();
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        if (AutoSize)
        {
            var lineSpacing = FontSize * LineHeight;
            var height = Math.Max(lineSpacing + Padding * 2, _lines.Count * lineSpacing + Padding * 2);
            return new SKSize(
                availableSize.Width < float.MaxValue ? availableSize.Width : 200,
                (float)Math.Min(height, availableSize.Height < float.MaxValue ? availableSize.Height : 200));
        }

        return new SKSize(
            availableSize.Width < float.MaxValue ? Math.Min(availableSize.Width, 200) : 200,
            availableSize.Height < float.MaxValue ? Math.Min(availableSize.Height, 150) : 150);
    }
}
