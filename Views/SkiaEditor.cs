// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered multiline text editor control with full XAML styling support.
/// </summary>
public class SkiaEditor : SkiaView
{
    #region BindableProperties

    /// <summary>
    /// Bindable property for Text.
    /// </summary>
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(
            nameof(Text),
            typeof(string),
            typeof(SkiaEditor),
            "",
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaEditor)b).OnTextPropertyChanged((string)o, (string)n));

    /// <summary>
    /// Bindable property for Placeholder.
    /// </summary>
    public static readonly BindableProperty PlaceholderProperty =
        BindableProperty.Create(
            nameof(Placeholder),
            typeof(string),
            typeof(SkiaEditor),
            "",
            propertyChanged: (b, o, n) => ((SkiaEditor)b).Invalidate());

    /// <summary>
    /// Bindable property for TextColor.
    /// </summary>
    public static readonly BindableProperty TextColorProperty =
        BindableProperty.Create(
            nameof(TextColor),
            typeof(SKColor),
            typeof(SkiaEditor),
            SKColors.Black,
            propertyChanged: (b, o, n) => ((SkiaEditor)b).Invalidate());

    /// <summary>
    /// Bindable property for PlaceholderColor.
    /// </summary>
    public static readonly BindableProperty PlaceholderColorProperty =
        BindableProperty.Create(
            nameof(PlaceholderColor),
            typeof(SKColor),
            typeof(SkiaEditor),
            new SKColor(0x80, 0x80, 0x80),
            propertyChanged: (b, o, n) => ((SkiaEditor)b).Invalidate());

    /// <summary>
    /// Bindable property for BorderColor.
    /// </summary>
    public static readonly BindableProperty BorderColorProperty =
        BindableProperty.Create(
            nameof(BorderColor),
            typeof(SKColor),
            typeof(SkiaEditor),
            new SKColor(0xBD, 0xBD, 0xBD),
            propertyChanged: (b, o, n) => ((SkiaEditor)b).Invalidate());

    /// <summary>
    /// Bindable property for SelectionColor.
    /// </summary>
    public static readonly BindableProperty SelectionColorProperty =
        BindableProperty.Create(
            nameof(SelectionColor),
            typeof(SKColor),
            typeof(SkiaEditor),
            new SKColor(0x21, 0x96, 0xF3, 0x60),
            propertyChanged: (b, o, n) => ((SkiaEditor)b).Invalidate());

    /// <summary>
    /// Bindable property for CursorColor.
    /// </summary>
    public static readonly BindableProperty CursorColorProperty =
        BindableProperty.Create(
            nameof(CursorColor),
            typeof(SKColor),
            typeof(SkiaEditor),
            new SKColor(0x21, 0x96, 0xF3),
            propertyChanged: (b, o, n) => ((SkiaEditor)b).Invalidate());

    /// <summary>
    /// Bindable property for FontFamily.
    /// </summary>
    public static readonly BindableProperty FontFamilyProperty =
        BindableProperty.Create(
            nameof(FontFamily),
            typeof(string),
            typeof(SkiaEditor),
            "Sans",
            propertyChanged: (b, o, n) => ((SkiaEditor)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for FontSize.
    /// </summary>
    public static readonly BindableProperty FontSizeProperty =
        BindableProperty.Create(
            nameof(FontSize),
            typeof(float),
            typeof(SkiaEditor),
            14f,
            propertyChanged: (b, o, n) => ((SkiaEditor)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for LineHeight.
    /// </summary>
    public static readonly BindableProperty LineHeightProperty =
        BindableProperty.Create(
            nameof(LineHeight),
            typeof(float),
            typeof(SkiaEditor),
            1.4f,
            propertyChanged: (b, o, n) => ((SkiaEditor)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for CornerRadius.
    /// </summary>
    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(
            nameof(CornerRadius),
            typeof(float),
            typeof(SkiaEditor),
            4f,
            propertyChanged: (b, o, n) => ((SkiaEditor)b).Invalidate());

    /// <summary>
    /// Bindable property for Padding.
    /// </summary>
    public static readonly BindableProperty PaddingProperty =
        BindableProperty.Create(
            nameof(Padding),
            typeof(float),
            typeof(SkiaEditor),
            12f,
            propertyChanged: (b, o, n) => ((SkiaEditor)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for IsReadOnly.
    /// </summary>
    public static readonly BindableProperty IsReadOnlyProperty =
        BindableProperty.Create(
            nameof(IsReadOnly),
            typeof(bool),
            typeof(SkiaEditor),
            false,
            propertyChanged: (b, o, n) => ((SkiaEditor)b).Invalidate());

    /// <summary>
    /// Bindable property for MaxLength.
    /// </summary>
    public static readonly BindableProperty MaxLengthProperty =
        BindableProperty.Create(
            nameof(MaxLength),
            typeof(int),
            typeof(SkiaEditor),
            -1);

    /// <summary>
    /// Bindable property for AutoSize.
    /// </summary>
    public static readonly BindableProperty AutoSizeProperty =
        BindableProperty.Create(
            nameof(AutoSize),
            typeof(bool),
            typeof(SkiaEditor),
            false,
            propertyChanged: (b, o, n) => ((SkiaEditor)b).InvalidateMeasure());

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the text content.
    /// </summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// Gets or sets the placeholder text.
    /// </summary>
    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>
    /// Gets or sets the text color.
    /// </summary>
    public SKColor TextColor
    {
        get => (SKColor)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the placeholder color.
    /// </summary>
    public SKColor PlaceholderColor
    {
        get => (SKColor)GetValue(PlaceholderColorProperty);
        set => SetValue(PlaceholderColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the border color.
    /// </summary>
    public SKColor BorderColor
    {
        get => (SKColor)GetValue(BorderColorProperty);
        set => SetValue(BorderColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the selection color.
    /// </summary>
    public SKColor SelectionColor
    {
        get => (SKColor)GetValue(SelectionColorProperty);
        set => SetValue(SelectionColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the cursor color.
    /// </summary>
    public SKColor CursorColor
    {
        get => (SKColor)GetValue(CursorColorProperty);
        set => SetValue(CursorColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the font family.
    /// </summary>
    public string FontFamily
    {
        get => (string)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    /// <summary>
    /// Gets or sets the font size.
    /// </summary>
    public float FontSize
    {
        get => (float)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the line height multiplier.
    /// </summary>
    public float LineHeight
    {
        get => (float)GetValue(LineHeightProperty);
        set => SetValue(LineHeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the corner radius.
    /// </summary>
    public float CornerRadius
    {
        get => (float)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>
    /// Gets or sets the padding.
    /// </summary>
    public float Padding
    {
        get => (float)GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the editor is read-only.
    /// </summary>
    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum length. -1 for unlimited.
    /// </summary>
    public int MaxLength
    {
        get => (int)GetValue(MaxLengthProperty);
        set => SetValue(MaxLengthProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the editor auto-sizes to content.
    /// </summary>
    public bool AutoSize
    {
        get => (bool)GetValue(AutoSizeProperty);
        set => SetValue(AutoSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the cursor position.
    /// </summary>
    public int CursorPosition
    {
        get => _cursorPosition;
        set
        {
            _cursorPosition = Math.Clamp(value, 0, Text.Length);
            EnsureCursorVisible();
            Invalidate();
        }
    }

    #endregion

    private int _cursorPosition;
    private int _selectionStart = -1;
    private int _selectionLength;
    private float _scrollOffsetY;
    private bool _cursorVisible = true;
    private DateTime _lastCursorBlink = DateTime.Now;
    private List<string> _lines = new() { "" };
    private float _wrapWidth = 0; // Available width for word wrapping
    private bool _isSelecting; // For mouse-based text selection
    private DateTime _lastClickTime = DateTime.MinValue;
    private float _lastClickX;
    private float _lastClickY;
    private const double DoubleClickThresholdMs = 400;

    /// <summary>
    /// Event raised when text changes.
    /// </summary>
    public event EventHandler? TextChanged;

    /// <summary>
    /// Event raised when editing is completed.
    /// </summary>
    public event EventHandler? Completed;

    public SkiaEditor()
    {
        IsFocusable = true;
    }

    private void OnTextPropertyChanged(string oldText, string newText)
    {
        var text = newText ?? "";

        if (MaxLength > 0 && text.Length > MaxLength)
        {
            text = text.Substring(0, MaxLength);
            SetValue(TextProperty, text);
            return;
        }

        UpdateLines();
        _cursorPosition = Math.Min(_cursorPosition, text.Length);
        _scrollOffsetY = 0; // Reset scroll when text changes externally
        _selectionLength = 0;
        TextChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    private void UpdateLines()
    {
        _lines.Clear();
        var text = Text ?? "";
        if (string.IsNullOrEmpty(text))
        {
            _lines.Add("");
            return;
        }

        using var font = new SKFont(SKTypeface.Default, FontSize);

        // Split by actual newlines first
        var paragraphs = text.Split('\n');

        foreach (var paragraph in paragraphs)
        {
            if (string.IsNullOrEmpty(paragraph))
            {
                _lines.Add("");
                continue;
            }

            // Word wrap this paragraph if we have a known width
            if (_wrapWidth > 0)
            {
                WrapParagraph(paragraph, font, _wrapWidth);
            }
            else
            {
                _lines.Add(paragraph);
            }
        }

        if (_lines.Count == 0)
        {
            _lines.Add("");
        }
    }

    private void WrapParagraph(string paragraph, SKFont font, float maxWidth)
    {
        var words = paragraph.Split(' ');
        var currentLine = "";

        foreach (var word in words)
        {
            var testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
            var lineWidth = MeasureText(testLine, font);

            if (lineWidth > maxWidth && !string.IsNullOrEmpty(currentLine))
            {
                // Line too long, save current and start new
                _lines.Add(currentLine);
                currentLine = word;
            }
            else
            {
                currentLine = testLine;
            }
        }

        // Add remaining text
        if (!string.IsNullOrEmpty(currentLine))
        {
            _lines.Add(currentLine);
        }
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
            pos += lineLength + 1;
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
        return Math.Min(pos, Text.Length);
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        // Update wrap width if bounds changed and re-wrap text
        var newWrapWidth = bounds.Width - Padding * 2;
        if (Math.Abs(newWrapWidth - _wrapWidth) > 1)
        {
            _wrapWidth = newWrapWidth;
            UpdateLines();
        }

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
        // Don't translate - let the text draw at absolute positions
        // canvas.Translate(0, -_scrollOffsetY);

        if (string.IsNullOrEmpty(Text) && !string.IsNullOrEmpty(Placeholder))
        {
            using var placeholderPaint = new SKPaint(font)
            {
                Color = PlaceholderColor,
                IsAntialias = true
            };
            canvas.DrawText(Placeholder, contentRect.Left, contentRect.Top + FontSize, placeholderPaint);
        }
        else
        {
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

                        canvas.DrawRect(new SKRect(startX, y - FontSize, endX, y + lineSpacing - FontSize), selectionPaint);
                    }
                }

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
                charIndex += line.Length + 1;
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

        IsFocused = true;

        // Use screen coordinates for proper hit detection
        var screenBounds = ScreenBounds;
        var contentX = e.X - screenBounds.Left - Padding;
        var contentY = e.Y - screenBounds.Top - Padding + _scrollOffsetY;

        var lineSpacing = FontSize * LineHeight;
        var clickedLine = Math.Clamp((int)(contentY / lineSpacing), 0, _lines.Count - 1);

        using var font = new SKFont(SKTypeface.Default, FontSize);
        var line = _lines[clickedLine];
        var clickedCol = 0;

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

        // Check for double-click (select word)
        var now = DateTime.UtcNow;
        var timeSinceLastClick = (now - _lastClickTime).TotalMilliseconds;
        var distanceFromLastClick = Math.Sqrt(Math.Pow(e.X - _lastClickX, 2) + Math.Pow(e.Y - _lastClickY, 2));

        if (timeSinceLastClick < DoubleClickThresholdMs && distanceFromLastClick < 10)
        {
            // Double-click: select the word at cursor
            SelectWordAtCursor();
            _lastClickTime = DateTime.MinValue; // Reset to prevent triple-click issues
            _isSelecting = false;
        }
        else
        {
            // Single click: start selection
            _selectionStart = _cursorPosition;
            _selectionLength = 0;
            _isSelecting = true;
            _lastClickTime = now;
            _lastClickX = e.X;
            _lastClickY = e.Y;
        }

        _cursorVisible = true;
        _lastCursorBlink = DateTime.Now;

        Invalidate();
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        if (!IsEnabled || !_isSelecting) return;

        // Calculate position from mouse coordinates
        var screenBounds = ScreenBounds;
        var contentX = e.X - screenBounds.Left - Padding;
        var contentY = e.Y - screenBounds.Top - Padding + _scrollOffsetY;

        var lineSpacing = FontSize * LineHeight;
        var clickedLine = Math.Clamp((int)(contentY / lineSpacing), 0, _lines.Count - 1);

        using var font = new SKFont(SKTypeface.Default, FontSize);
        var line = _lines[clickedLine];
        var clickedCol = 0;

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

        var newPosition = GetPosition(clickedLine, clickedCol);
        if (newPosition != _cursorPosition)
        {
            _cursorPosition = newPosition;
            _selectionLength = _cursorPosition - _selectionStart;
            _cursorVisible = true;
            _lastCursorBlink = DateTime.Now;
            Invalidate();
        }
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        _isSelecting = false;
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
                if (_cursorPosition < Text.Length)
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
                if (!IsReadOnly)
                {
                    InsertText("\n");
                }
                e.Handled = true;
                break;

            case Key.Backspace:
                if (!IsReadOnly)
                {
                    if (_selectionLength != 0)
                    {
                        DeleteSelection();
                    }
                    else if (_cursorPosition > 0)
                    {
                        Text = Text.Remove(_cursorPosition - 1, 1);
                        _cursorPosition--;
                    }
                    EnsureCursorVisible();
                }
                e.Handled = true;
                break;

            case Key.Delete:
                if (!IsReadOnly)
                {
                    if (_selectionLength != 0)
                    {
                        DeleteSelection();
                    }
                    else if (_cursorPosition < Text.Length)
                    {
                        Text = Text.Remove(_cursorPosition, 1);
                    }
                }
                e.Handled = true;
                break;

            case Key.Tab:
                if (!IsReadOnly)
                {
                    InsertText("    ");
                }
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
        }

        Invalidate();
    }

    public override void OnTextInput(TextInputEventArgs e)
    {
        if (!IsEnabled || IsReadOnly) return;

        // Ignore control characters (Ctrl+key combinations send ASCII control codes)
        if (!string.IsNullOrEmpty(e.Text) && e.Text.Length == 1 && e.Text[0] < 32)
            return;

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
            var currentText = Text;
            Text = currentText.Remove(_selectionStart, _selectionLength);
            _cursorPosition = _selectionStart;
            _selectionStart = -1;
            _selectionLength = 0;
        }

        if (MaxLength > 0 && Text.Length + text.Length > MaxLength)
        {
            text = text.Substring(0, Math.Max(0, MaxLength - Text.Length));
        }

        if (!string.IsNullOrEmpty(text))
        {
            Text = Text.Insert(_cursorPosition, text);
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

    public override void OnFocusGained()
    {
        base.OnFocusGained();
        SkiaVisualStateManager.GoToState(this, SkiaVisualStateManager.CommonStates.Focused);
    }

    public override void OnFocusLost()
    {
        base.OnFocusLost();
        SkiaVisualStateManager.GoToState(this, SkiaVisualStateManager.CommonStates.Normal);
    }

    #region Selection and Clipboard

    public void SelectAll()
    {
        _selectionStart = 0;
        _cursorPosition = Text.Length;
        _selectionLength = Text.Length;
        Invalidate();
    }

    private void SelectWordAtCursor()
    {
        if (string.IsNullOrEmpty(Text)) return;

        // Find word boundaries
        int start = _cursorPosition;
        int end = _cursorPosition;

        // Move start backwards to beginning of word
        while (start > 0 && IsWordChar(Text[start - 1]))
            start--;

        // Move end forwards to end of word
        while (end < Text.Length && IsWordChar(Text[end]))
            end++;

        _selectionStart = start;
        _cursorPosition = end;
        _selectionLength = end - start;
    }

    private static bool IsWordChar(char c)
    {
        return char.IsLetterOrDigit(c) || c == '_';
    }

    private void CopyToClipboard()
    {
        if (_selectionLength == 0) return;

        var start = Math.Min(_selectionStart, _selectionStart + _selectionLength);
        var length = Math.Abs(_selectionLength);
        var selectedText = Text.Substring(start, length);

        // Use system clipboard via xclip/xsel
        SystemClipboard.SetText(selectedText);
    }

    private void CutToClipboard()
    {
        CopyToClipboard();
        DeleteSelection();
        Invalidate();
    }

    private void PasteFromClipboard()
    {
        // Get from system clipboard
        var text = SystemClipboard.GetText();
        if (string.IsNullOrEmpty(text)) return;

        if (_selectionLength != 0)
        {
            DeleteSelection();
        }

        InsertText(text);
    }

    private void DeleteSelection()
    {
        if (_selectionLength == 0) return;

        var start = Math.Min(_selectionStart, _selectionStart + _selectionLength);
        var length = Math.Abs(_selectionLength);

        Text = Text.Remove(start, length);
        _cursorPosition = start;
        _selectionStart = -1;
        _selectionLength = 0;
    }

    #endregion

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
