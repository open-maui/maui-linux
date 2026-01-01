// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;
using Microsoft.Maui.Platform.Linux.Rendering;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered text entry control with full XAML styling and data binding support.
/// </summary>
public class SkiaEntry : SkiaView
{
    #region BindableProperties

    /// <summary>
    /// Bindable property for Text.
    /// </summary>
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(
            nameof(Text),
            typeof(string),
            typeof(SkiaEntry),
            "",
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaEntry)b).OnTextPropertyChanged((string)o, (string)n));

    /// <summary>
    /// Bindable property for Placeholder.
    /// </summary>
    public static readonly BindableProperty PlaceholderProperty =
        BindableProperty.Create(
            nameof(Placeholder),
            typeof(string),
            typeof(SkiaEntry),
            "",
            propertyChanged: (b, o, n) => ((SkiaEntry)b).Invalidate());

    /// <summary>
    /// Bindable property for PlaceholderColor.
    /// </summary>
    public static readonly BindableProperty PlaceholderColorProperty =
        BindableProperty.Create(
            nameof(PlaceholderColor),
            typeof(SKColor),
            typeof(SkiaEntry),
            new SKColor(0x9E, 0x9E, 0x9E),
            propertyChanged: (b, o, n) => ((SkiaEntry)b).Invalidate());

    /// <summary>
    /// Bindable property for TextColor.
    /// </summary>
    public static readonly BindableProperty TextColorProperty =
        BindableProperty.Create(
            nameof(TextColor),
            typeof(SKColor),
            typeof(SkiaEntry),
            SKColors.Black,
            propertyChanged: (b, o, n) => ((SkiaEntry)b).Invalidate());

    /// <summary>
    /// Bindable property for EntryBackgroundColor.
    /// </summary>
    public static readonly BindableProperty EntryBackgroundColorProperty =
        BindableProperty.Create(
            nameof(EntryBackgroundColor),
            typeof(SKColor),
            typeof(SkiaEntry),
            SKColors.White,
            propertyChanged: (b, o, n) => ((SkiaEntry)b).Invalidate());

    /// <summary>
    /// Bindable property for BorderColor.
    /// </summary>
    public static readonly BindableProperty BorderColorProperty =
        BindableProperty.Create(
            nameof(BorderColor),
            typeof(SKColor),
            typeof(SkiaEntry),
            new SKColor(0xBD, 0xBD, 0xBD),
            propertyChanged: (b, o, n) => ((SkiaEntry)b).Invalidate());

    /// <summary>
    /// Bindable property for FocusedBorderColor.
    /// </summary>
    public static readonly BindableProperty FocusedBorderColorProperty =
        BindableProperty.Create(
            nameof(FocusedBorderColor),
            typeof(SKColor),
            typeof(SkiaEntry),
            new SKColor(0x21, 0x96, 0xF3),
            propertyChanged: (b, o, n) => ((SkiaEntry)b).Invalidate());

    /// <summary>
    /// Bindable property for SelectionColor.
    /// </summary>
    public static readonly BindableProperty SelectionColorProperty =
        BindableProperty.Create(
            nameof(SelectionColor),
            typeof(SKColor),
            typeof(SkiaEntry),
            new SKColor(0x21, 0x96, 0xF3, 0x80),
            propertyChanged: (b, o, n) => ((SkiaEntry)b).Invalidate());

    /// <summary>
    /// Bindable property for CursorColor.
    /// </summary>
    public static readonly BindableProperty CursorColorProperty =
        BindableProperty.Create(
            nameof(CursorColor),
            typeof(SKColor),
            typeof(SkiaEntry),
            new SKColor(0x21, 0x96, 0xF3),
            propertyChanged: (b, o, n) => ((SkiaEntry)b).Invalidate());

    /// <summary>
    /// Bindable property for FontFamily.
    /// </summary>
    public static readonly BindableProperty FontFamilyProperty =
        BindableProperty.Create(
            nameof(FontFamily),
            typeof(string),
            typeof(SkiaEntry),
            "Sans",
            propertyChanged: (b, o, n) => ((SkiaEntry)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for FontSize.
    /// </summary>
    public static readonly BindableProperty FontSizeProperty =
        BindableProperty.Create(
            nameof(FontSize),
            typeof(float),
            typeof(SkiaEntry),
            14f,
            propertyChanged: (b, o, n) => ((SkiaEntry)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for IsBold.
    /// </summary>
    public static readonly BindableProperty IsBoldProperty =
        BindableProperty.Create(
            nameof(IsBold),
            typeof(bool),
            typeof(SkiaEntry),
            false,
            propertyChanged: (b, o, n) => ((SkiaEntry)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for IsItalic.
    /// </summary>
    public static readonly BindableProperty IsItalicProperty =
        BindableProperty.Create(
            nameof(IsItalic),
            typeof(bool),
            typeof(SkiaEntry),
            false,
            propertyChanged: (b, o, n) => ((SkiaEntry)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for CornerRadius.
    /// </summary>
    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(
            nameof(CornerRadius),
            typeof(float),
            typeof(SkiaEntry),
            4f,
            propertyChanged: (b, o, n) => ((SkiaEntry)b).Invalidate());

    /// <summary>
    /// Bindable property for BorderWidth.
    /// </summary>
    public static readonly BindableProperty BorderWidthProperty =
        BindableProperty.Create(
            nameof(BorderWidth),
            typeof(float),
            typeof(SkiaEntry),
            1f,
            propertyChanged: (b, o, n) => ((SkiaEntry)b).Invalidate());

    /// <summary>
    /// Bindable property for Padding.
    /// </summary>
    public static readonly BindableProperty PaddingProperty =
        BindableProperty.Create(
            nameof(Padding),
            typeof(SKRect),
            typeof(SkiaEntry),
            new SKRect(12, 8, 12, 8),
            propertyChanged: (b, o, n) => ((SkiaEntry)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for IsPassword.
    /// </summary>
    public static readonly BindableProperty IsPasswordProperty =
        BindableProperty.Create(
            nameof(IsPassword),
            typeof(bool),
            typeof(SkiaEntry),
            false,
            propertyChanged: (b, o, n) => ((SkiaEntry)b).Invalidate());

    /// <summary>
    /// Bindable property for PasswordChar.
    /// </summary>
    public static readonly BindableProperty PasswordCharProperty =
        BindableProperty.Create(
            nameof(PasswordChar),
            typeof(char),
            typeof(SkiaEntry),
            '*', // Use asterisk for universal font compatibility
            propertyChanged: (b, o, n) => ((SkiaEntry)b).Invalidate());

    /// <summary>
    /// Bindable property for MaxLength.
    /// </summary>
    public static readonly BindableProperty MaxLengthProperty =
        BindableProperty.Create(
            nameof(MaxLength),
            typeof(int),
            typeof(SkiaEntry),
            0);

    /// <summary>
    /// Bindable property for IsReadOnly.
    /// </summary>
    public static readonly BindableProperty IsReadOnlyProperty =
        BindableProperty.Create(
            nameof(IsReadOnly),
            typeof(bool),
            typeof(SkiaEntry),
            false,
            propertyChanged: (b, o, n) => ((SkiaEntry)b).Invalidate());

    /// <summary>
    /// Bindable property for HorizontalTextAlignment.
    /// </summary>
    public static readonly BindableProperty HorizontalTextAlignmentProperty =
        BindableProperty.Create(
            nameof(HorizontalTextAlignment),
            typeof(TextAlignment),
            typeof(SkiaEntry),
            TextAlignment.Start,
            propertyChanged: (b, o, n) => ((SkiaEntry)b).Invalidate());

    /// <summary>
    /// Bindable property for VerticalTextAlignment.
    /// </summary>
    public static readonly BindableProperty VerticalTextAlignmentProperty =
        BindableProperty.Create(
            nameof(VerticalTextAlignment),
            typeof(TextAlignment),
            typeof(SkiaEntry),
            TextAlignment.Center,
            propertyChanged: (b, o, n) => ((SkiaEntry)b).Invalidate());

    /// <summary>
    /// Bindable property for ShowClearButton.
    /// </summary>
    public static readonly BindableProperty ShowClearButtonProperty =
        BindableProperty.Create(
            nameof(ShowClearButton),
            typeof(bool),
            typeof(SkiaEntry),
            false,
            propertyChanged: (b, o, n) => ((SkiaEntry)b).Invalidate());

    /// <summary>
    /// Bindable property for CharacterSpacing.
    /// </summary>
    public static readonly BindableProperty CharacterSpacingProperty =
        BindableProperty.Create(
            nameof(CharacterSpacing),
            typeof(float),
            typeof(SkiaEntry),
            0f,
            propertyChanged: (b, o, n) => ((SkiaEntry)b).Invalidate());

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
    /// Gets or sets the placeholder color.
    /// </summary>
    public SKColor PlaceholderColor
    {
        get => (SKColor)GetValue(PlaceholderColorProperty);
        set => SetValue(PlaceholderColorProperty, value);
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
    /// Gets or sets the entry background color.
    /// </summary>
    public SKColor EntryBackgroundColor
    {
        get => (SKColor)GetValue(EntryBackgroundColorProperty);
        set => SetValue(EntryBackgroundColorProperty, value);
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
    /// Gets or sets the focused border color.
    /// </summary>
    public SKColor FocusedBorderColor
    {
        get => (SKColor)GetValue(FocusedBorderColorProperty);
        set => SetValue(FocusedBorderColorProperty, value);
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
    /// Gets or sets whether the text is bold.
    /// </summary>
    public bool IsBold
    {
        get => (bool)GetValue(IsBoldProperty);
        set => SetValue(IsBoldProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the text is italic.
    /// </summary>
    public bool IsItalic
    {
        get => (bool)GetValue(IsItalicProperty);
        set => SetValue(IsItalicProperty, value);
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
    /// Gets or sets the border width.
    /// </summary>
    public float BorderWidth
    {
        get => (float)GetValue(BorderWidthProperty);
        set => SetValue(BorderWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the padding.
    /// </summary>
    public SKRect Padding
    {
        get => (SKRect)GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    /// <summary>
    /// Gets or sets whether this is a password field.
    /// </summary>
    public bool IsPassword
    {
        get => (bool)GetValue(IsPasswordProperty);
        set => SetValue(IsPasswordProperty, value);
    }

    /// <summary>
    /// Gets or sets the password masking character.
    /// </summary>
    public char PasswordChar
    {
        get => (char)GetValue(PasswordCharProperty);
        set => SetValue(PasswordCharProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum text length. 0 = unlimited.
    /// </summary>
    public int MaxLength
    {
        get => (int)GetValue(MaxLengthProperty);
        set => SetValue(MaxLengthProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the entry is read-only.
    /// </summary>
    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>
    /// Gets or sets the horizontal text alignment.
    /// </summary>
    public TextAlignment HorizontalTextAlignment
    {
        get => (TextAlignment)GetValue(HorizontalTextAlignmentProperty);
        set => SetValue(HorizontalTextAlignmentProperty, value);
    }

    /// <summary>
    /// Gets or sets the vertical text alignment.
    /// </summary>
    public TextAlignment VerticalTextAlignment
    {
        get => (TextAlignment)GetValue(VerticalTextAlignmentProperty);
        set => SetValue(VerticalTextAlignmentProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to show the clear button.
    /// </summary>
    public bool ShowClearButton
    {
        get => (bool)GetValue(ShowClearButtonProperty);
        set => SetValue(ShowClearButtonProperty, value);
    }

    /// <summary>
    /// Gets or sets the character spacing.
    /// </summary>
    public float CharacterSpacing
    {
        get => (float)GetValue(CharacterSpacingProperty);
        set => SetValue(CharacterSpacingProperty, value);
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
            ResetCursorBlink();
            Invalidate();
        }
    }

    /// <summary>
    /// Gets or sets the selection length.
    /// </summary>
    public int SelectionLength
    {
        get => _selectionLength;
        set
        {
            _selectionLength = value;
            Invalidate();
        }
    }

    #endregion

    private int _cursorPosition;
    private int _selectionStart;
    private int _selectionLength;
    private float _scrollOffset;
    private DateTime _cursorBlinkTime = DateTime.UtcNow;
    private bool _cursorVisible = true;
    private bool _isSelecting; // For mouse-based text selection
    private DateTime _lastClickTime = DateTime.MinValue;
    private float _lastClickX;
    private const double DoubleClickThresholdMs = 400;

    /// <summary>
    /// Event raised when text changes.
    /// </summary>
    public event EventHandler<TextChangedEventArgs>? TextChanged;

    /// <summary>
    /// Event raised when Enter is pressed.
    /// </summary>
    public event EventHandler? Completed;

    public SkiaEntry()
    {
        IsFocusable = true;
    }

    private void OnTextPropertyChanged(string oldText, string newText)
    {
        _cursorPosition = Math.Min(_cursorPosition, (newText ?? "").Length);
        _scrollOffset = 0; // Reset scroll when text changes externally
        _selectionLength = 0;
        TextChanged?.Invoke(this, new TextChangedEventArgs(oldText, newText ?? ""));
        Invalidate();
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        // Draw background
        using var bgPaint = new SKPaint
        {
            Color = EntryBackgroundColor,
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
        if (ShowClearButton && !string.IsNullOrEmpty(Text) && IsFocused)
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
        if (ShowClearButton && !string.IsNullOrEmpty(Text) && IsFocused)
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
        if (IsPassword && !string.IsNullOrEmpty(Text))
        {
            return new string(PasswordChar, Text.Length);
        }
        return Text;
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

    public override void OnTextInput(TextInputEventArgs e)
    {
        if (!IsEnabled || IsReadOnly) return;

        // Ignore control characters (Ctrl+key combinations send ASCII control codes)
        if (!string.IsNullOrEmpty(e.Text) && e.Text.Length == 1 && e.Text[0] < 32)
            return;

        // Delete selection if any
        if (_selectionLength != 0)
        {
            DeleteSelection();
        }

        // Check max length
        if (MaxLength > 0 && Text.Length >= MaxLength)
            return;

        // Insert text at cursor
        var insertText = e.Text;
        if (MaxLength > 0)
        {
            var remaining = MaxLength - Text.Length;
            insertText = insertText.Substring(0, Math.Min(insertText.Length, remaining));
        }

        var newText = Text.Insert(_cursorPosition, insertText);
        var oldPos = _cursorPosition;
        Text = newText;
        _cursorPosition = oldPos + insertText.Length;

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
                        var newText = Text.Remove(_cursorPosition - 1, 1);
                        var newPos = _cursorPosition - 1;
                        Text = newText;
                        _cursorPosition = newPos;
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
                    else if (_cursorPosition < Text.Length)
                    {
                        Text = Text.Remove(_cursorPosition, 1);
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
                if (_cursorPosition < Text.Length)
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
                    ExtendSelectionTo(Text.Length);
                }
                else
                {
                    ClearSelection();
                    _cursorPosition = Text.Length;
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
        Console.WriteLine($"[SkiaEntry] OnPointerPressed - Text='{Text}', Placeholder='{Placeholder}', IsEnabled={IsEnabled}, IsFocused={IsFocused}");
        Console.WriteLine($"[SkiaEntry] Bounds={Bounds}, ScreenBounds={ScreenBounds}, e.X={e.X}, e.Y={e.Y}");

        if (!IsEnabled) return;

        // Check if clicked on clear button
        if (ShowClearButton && !string.IsNullOrEmpty(Text) && IsFocused)
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
                Text = "";
                _cursorPosition = 0;
                _selectionLength = 0;
                Invalidate();
                return;
            }
        }

        // Calculate cursor position from click using screen coordinates
        var screenBounds = ScreenBounds;
        var clickX = e.X - screenBounds.Left - Padding.Left + _scrollOffset;
        _cursorPosition = GetCharacterIndexAtX(clickX);

        // Check for double-click (select word)
        var now = DateTime.UtcNow;
        var timeSinceLastClick = (now - _lastClickTime).TotalMilliseconds;
        var distanceFromLastClick = Math.Abs(e.X - _lastClickX);

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
        }

        ResetCursorBlink();
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

    public override void OnPointerMoved(PointerEventArgs e)
    {
        if (!IsEnabled || !_isSelecting) return;

        // Extend selection to current mouse position
        var screenBounds = ScreenBounds;
        var clickX = e.X - screenBounds.Left - Padding.Left + _scrollOffset;
        var newPosition = GetCharacterIndexAtX(clickX);

        if (newPosition != _cursorPosition)
        {
            _cursorPosition = newPosition;
            _selectionLength = _cursorPosition - _selectionStart;
            ResetCursorBlink();
            Invalidate();
        }
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        _isSelecting = false;
    }

    private int GetCharacterIndexAtX(float x)
    {
        if (string.IsNullOrEmpty(Text)) return 0;

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

        Text = Text.Remove(start, length);
        _cursorPosition = start;
        _selectionLength = 0;
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

    /// <summary>
    /// Selects all text.
    /// </summary>
    public void SelectAll()
    {
        _selectionStart = 0;
        _cursorPosition = Text.Length;
        _selectionLength = Text.Length;
        Invalidate();
    }

    private void CopyToClipboard()
    {
        // Password fields should not allow copying
        if (IsPassword) return;
        if (_selectionLength == 0) return;

        var start = Math.Min(_selectionStart, _selectionStart + _selectionLength);
        var length = Math.Abs(_selectionLength);
        var selectedText = Text.Substring(start, length);

        // Use system clipboard via xclip/xsel
        SystemClipboard.SetText(selectedText);
    }

    private void CutToClipboard()
    {
        // Password fields should not allow cutting
        if (IsPassword) return;

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

        // Check max length
        if (MaxLength > 0)
        {
            var remaining = MaxLength - Text.Length;
            text = text.Substring(0, Math.Min(text.Length, remaining));
        }

        var newText = Text.Insert(_cursorPosition, text);
        var newPos = _cursorPosition + text.Length;
        Text = newText;
        _cursorPosition = newPos;
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

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        var fontStyle = GetFontStyle();
        var typeface = SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(FontFamily, fontStyle)
                      ?? SKTypeface.Default;

        using var font = new SKFont(typeface, FontSize);

        // Use font metrics for consistent height regardless of text content
        // This prevents size changes when placeholder disappears or text changes
        var metrics = font.Metrics;
        var textHeight = metrics.Descent - metrics.Ascent + metrics.Leading;

        return new SKSize(
            200, // Default width, will be overridden by layout
            textHeight + Padding.Top + Padding.Bottom + BorderWidth * 2);
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
