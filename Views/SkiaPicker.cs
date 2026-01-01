// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered picker/dropdown control with full XAML styling support.
/// </summary>
public class SkiaPicker : SkiaView
{
    #region BindableProperties

    /// <summary>
    /// Bindable property for SelectedIndex.
    /// </summary>
    public static readonly BindableProperty SelectedIndexProperty =
        BindableProperty.Create(
            nameof(SelectedIndex),
            typeof(int),
            typeof(SkiaPicker),
            -1,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaPicker)b).OnSelectedIndexChanged());

    /// <summary>
    /// Bindable property for Title.
    /// </summary>
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(
            nameof(Title),
            typeof(string),
            typeof(SkiaPicker),
            "",
            propertyChanged: (b, o, n) => ((SkiaPicker)b).Invalidate());

    /// <summary>
    /// Bindable property for TextColor.
    /// </summary>
    public static readonly BindableProperty TextColorProperty =
        BindableProperty.Create(
            nameof(TextColor),
            typeof(SKColor),
            typeof(SkiaPicker),
            SKColors.Black,
            propertyChanged: (b, o, n) => ((SkiaPicker)b).Invalidate());

    /// <summary>
    /// Bindable property for TitleColor.
    /// </summary>
    public static readonly BindableProperty TitleColorProperty =
        BindableProperty.Create(
            nameof(TitleColor),
            typeof(SKColor),
            typeof(SkiaPicker),
            new SKColor(0x80, 0x80, 0x80),
            propertyChanged: (b, o, n) => ((SkiaPicker)b).Invalidate());

    /// <summary>
    /// Bindable property for BorderColor.
    /// </summary>
    public static readonly BindableProperty BorderColorProperty =
        BindableProperty.Create(
            nameof(BorderColor),
            typeof(SKColor),
            typeof(SkiaPicker),
            new SKColor(0xBD, 0xBD, 0xBD),
            propertyChanged: (b, o, n) => ((SkiaPicker)b).Invalidate());

    /// <summary>
    /// Bindable property for DropdownBackgroundColor.
    /// </summary>
    public static readonly BindableProperty DropdownBackgroundColorProperty =
        BindableProperty.Create(
            nameof(DropdownBackgroundColor),
            typeof(SKColor),
            typeof(SkiaPicker),
            SKColors.White,
            propertyChanged: (b, o, n) => ((SkiaPicker)b).Invalidate());

    /// <summary>
    /// Bindable property for SelectedItemBackgroundColor.
    /// </summary>
    public static readonly BindableProperty SelectedItemBackgroundColorProperty =
        BindableProperty.Create(
            nameof(SelectedItemBackgroundColor),
            typeof(SKColor),
            typeof(SkiaPicker),
            new SKColor(0x21, 0x96, 0xF3, 0x30),
            propertyChanged: (b, o, n) => ((SkiaPicker)b).Invalidate());

    /// <summary>
    /// Bindable property for HoverItemBackgroundColor.
    /// </summary>
    public static readonly BindableProperty HoverItemBackgroundColorProperty =
        BindableProperty.Create(
            nameof(HoverItemBackgroundColor),
            typeof(SKColor),
            typeof(SkiaPicker),
            new SKColor(0xE0, 0xE0, 0xE0),
            propertyChanged: (b, o, n) => ((SkiaPicker)b).Invalidate());

    /// <summary>
    /// Bindable property for FontFamily.
    /// </summary>
    public static readonly BindableProperty FontFamilyProperty =
        BindableProperty.Create(
            nameof(FontFamily),
            typeof(string),
            typeof(SkiaPicker),
            "Sans",
            propertyChanged: (b, o, n) => ((SkiaPicker)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for FontSize.
    /// </summary>
    public static readonly BindableProperty FontSizeProperty =
        BindableProperty.Create(
            nameof(FontSize),
            typeof(float),
            typeof(SkiaPicker),
            14f,
            propertyChanged: (b, o, n) => ((SkiaPicker)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for ItemHeight.
    /// </summary>
    public static readonly BindableProperty ItemHeightProperty =
        BindableProperty.Create(
            nameof(ItemHeight),
            typeof(float),
            typeof(SkiaPicker),
            40f,
            propertyChanged: (b, o, n) => ((SkiaPicker)b).Invalidate());

    /// <summary>
    /// Bindable property for CornerRadius.
    /// </summary>
    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(
            nameof(CornerRadius),
            typeof(float),
            typeof(SkiaPicker),
            4f,
            propertyChanged: (b, o, n) => ((SkiaPicker)b).Invalidate());

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the selected index.
    /// </summary>
    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <summary>
    /// Gets or sets the title/placeholder.
    /// </summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
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
    /// Gets or sets the title color.
    /// </summary>
    public SKColor TitleColor
    {
        get => (SKColor)GetValue(TitleColorProperty);
        set => SetValue(TitleColorProperty, value);
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
    /// Gets or sets the dropdown background color.
    /// </summary>
    public SKColor DropdownBackgroundColor
    {
        get => (SKColor)GetValue(DropdownBackgroundColorProperty);
        set => SetValue(DropdownBackgroundColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the selected item background color.
    /// </summary>
    public SKColor SelectedItemBackgroundColor
    {
        get => (SKColor)GetValue(SelectedItemBackgroundColorProperty);
        set => SetValue(SelectedItemBackgroundColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the hover item background color.
    /// </summary>
    public SKColor HoverItemBackgroundColor
    {
        get => (SKColor)GetValue(HoverItemBackgroundColorProperty);
        set => SetValue(HoverItemBackgroundColorProperty, value);
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
    /// Gets or sets the item height.
    /// </summary>
    public float ItemHeight
    {
        get => (float)GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
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
    /// Gets the items list.
    /// </summary>
    public IList<string> Items => _items;

    /// <summary>
    /// Gets the selected item.
    /// </summary>
    public string? SelectedItem => SelectedIndex >= 0 && SelectedIndex < _items.Count ? _items[SelectedIndex] : null;

    /// <summary>
    /// Gets or sets whether the dropdown is open.
    /// </summary>
    public bool IsOpen
    {
        get => _isOpen;
        set
        {
            if (_isOpen != value)
            {
                _isOpen = value;
                if (_isOpen)
                {
                    RegisterPopupOverlay(this, DrawDropdownOverlay);
                }
                else
                {
                    UnregisterPopupOverlay(this);
                }
                Invalidate();
            }
        }
    }

    #endregion

    private readonly List<string> _items = new();
    private bool _isOpen;
    private float _dropdownMaxHeight = 200;
    private int _hoveredItemIndex = -1;

    /// <summary>
    /// Event raised when selected index changes.
    /// </summary>
    public event EventHandler? SelectedIndexChanged;

    public SkiaPicker()
    {
        IsFocusable = true;
    }

    private void OnSelectedIndexChanged()
    {
        SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    /// <summary>
    /// Sets the items in the picker.
    /// </summary>
    public void SetItems(IEnumerable<string> items)
    {
        _items.Clear();
        _items.AddRange(items);
        if (SelectedIndex >= _items.Count)
        {
            SelectedIndex = _items.Count > 0 ? 0 : -1;
        }
        Invalidate();
    }

    private void DrawDropdownOverlay(SKCanvas canvas)
    {
        if (_items.Count == 0 || !_isOpen) return;
        // Use ScreenBounds for overlay drawing to account for scroll offset
        DrawDropdown(canvas, ScreenBounds);
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        DrawPickerButton(canvas, bounds);
    }

    private void DrawPickerButton(SKCanvas canvas, SKRect bounds)
    {
        // Draw background
        using var bgPaint = new SKPaint
        {
            Color = IsEnabled ? BackgroundColor : new SKColor(0xF5, 0xF5, 0xF5),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        var buttonRect = new SKRoundRect(bounds, CornerRadius);
        canvas.DrawRoundRect(buttonRect, bgPaint);

        // Draw border
        using var borderPaint = new SKPaint
        {
            Color = IsFocused ? new SKColor(0x21, 0x96, 0xF3) : BorderColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = IsFocused ? 2 : 1,
            IsAntialias = true
        };
        canvas.DrawRoundRect(buttonRect, borderPaint);

        // Draw text or title
        using var font = new SKFont(SKTypeface.Default, FontSize);
        using var textPaint = new SKPaint(font)
        {
            IsAntialias = true
        };

        string displayText;
        if (SelectedIndex >= 0 && SelectedIndex < _items.Count)
        {
            displayText = _items[SelectedIndex];
            textPaint.Color = IsEnabled ? TextColor : TextColor.WithAlpha(128);
        }
        else
        {
            displayText = Title;
            textPaint.Color = TitleColor;
        }

        var textBounds = new SKRect();
        textPaint.MeasureText(displayText, ref textBounds);

        var textX = bounds.Left + 12;
        var textY = bounds.MidY - textBounds.MidY;
        canvas.DrawText(displayText, textX, textY, textPaint);

        // Draw dropdown arrow
        DrawDropdownArrow(canvas, bounds);
    }

    private void DrawDropdownArrow(SKCanvas canvas, SKRect bounds)
    {
        using var paint = new SKPaint
        {
            Color = IsEnabled ? TextColor : TextColor.WithAlpha(128),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        };

        var arrowSize = 6f;
        var centerX = bounds.Right - 20;
        var centerY = bounds.MidY;

        using var path = new SKPath();
        if (_isOpen)
        {
            path.MoveTo(centerX - arrowSize, centerY + arrowSize / 2);
            path.LineTo(centerX, centerY - arrowSize / 2);
            path.LineTo(centerX + arrowSize, centerY + arrowSize / 2);
        }
        else
        {
            path.MoveTo(centerX - arrowSize, centerY - arrowSize / 2);
            path.LineTo(centerX, centerY + arrowSize / 2);
            path.LineTo(centerX + arrowSize, centerY - arrowSize / 2);
        }

        canvas.DrawPath(path, paint);
    }

    private void DrawDropdown(SKCanvas canvas, SKRect bounds)
    {
        if (_items.Count == 0) return;

        var dropdownHeight = Math.Min(_items.Count * ItemHeight, _dropdownMaxHeight);
        var dropdownRect = new SKRect(
            bounds.Left,
            bounds.Bottom + 4,
            bounds.Right,
            bounds.Bottom + 4 + dropdownHeight);

        // Draw shadow
        using var shadowPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 40),
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4),
            Style = SKPaintStyle.Fill
        };
        var shadowRect = new SKRect(dropdownRect.Left + 2, dropdownRect.Top + 2, dropdownRect.Right + 2, dropdownRect.Bottom + 2);
        canvas.DrawRoundRect(new SKRoundRect(shadowRect, CornerRadius), shadowPaint);

        // Draw dropdown background
        using var bgPaint = new SKPaint
        {
            Color = DropdownBackgroundColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(dropdownRect, CornerRadius), bgPaint);

        // Draw border
        using var borderPaint = new SKPaint
        {
            Color = BorderColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(dropdownRect, CornerRadius), borderPaint);

        // Clip to dropdown bounds
        canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(dropdownRect, CornerRadius));

        // Draw items
        using var font = new SKFont(SKTypeface.Default, FontSize);
        using var textPaint = new SKPaint(font)
        {
            Color = TextColor,
            IsAntialias = true
        };

        for (int i = 0; i < _items.Count; i++)
        {
            var itemTop = dropdownRect.Top + i * ItemHeight;
            if (itemTop > dropdownRect.Bottom) break;

            var itemRect = new SKRect(dropdownRect.Left, itemTop, dropdownRect.Right, itemTop + ItemHeight);

            // Draw item background
            if (i == SelectedIndex)
            {
                using var selectedPaint = new SKPaint
                {
                    Color = SelectedItemBackgroundColor,
                    Style = SKPaintStyle.Fill
                };
                canvas.DrawRect(itemRect, selectedPaint);
            }
            else if (i == _hoveredItemIndex)
            {
                using var hoverPaint = new SKPaint
                {
                    Color = HoverItemBackgroundColor,
                    Style = SKPaintStyle.Fill
                };
                canvas.DrawRect(itemRect, hoverPaint);
            }

            // Draw item text
            var textBounds = new SKRect();
            textPaint.MeasureText(_items[i], ref textBounds);

            var textX = itemRect.Left + 12;
            var textY = itemRect.MidY - textBounds.MidY;
            canvas.DrawText(_items[i], textX, textY, textPaint);
        }

        canvas.Restore();
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        if (IsOpen)
        {
            // Use ScreenBounds for popup coordinate calculations (accounts for scroll offset)
            var screenBounds = ScreenBounds;
            var dropdownTop = screenBounds.Bottom + 4;
            if (e.Y >= dropdownTop)
            {
                var itemIndex = (int)((e.Y - dropdownTop) / ItemHeight);
                if (itemIndex >= 0 && itemIndex < _items.Count)
                {
                    SelectedIndex = itemIndex;
                }
            }
            IsOpen = false;
        }
        else
        {
            IsOpen = true;
        }

        Invalidate();
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        if (!_isOpen) return;

        // Use ScreenBounds for popup coordinate calculations (accounts for scroll offset)
        var screenBounds = ScreenBounds;
        var dropdownTop = screenBounds.Bottom + 4;
        if (e.Y >= dropdownTop)
        {
            var newHovered = (int)((e.Y - dropdownTop) / ItemHeight);
            if (newHovered != _hoveredItemIndex && newHovered >= 0 && newHovered < _items.Count)
            {
                _hoveredItemIndex = newHovered;
                Invalidate();
            }
        }
        else
        {
            if (_hoveredItemIndex != -1)
            {
                _hoveredItemIndex = -1;
                Invalidate();
            }
        }
    }

    public override void OnPointerExited(PointerEventArgs e)
    {
        _hoveredItemIndex = -1;
        Invalidate();
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsEnabled) return;

        switch (e.Key)
        {
            case Key.Enter:
            case Key.Space:
                IsOpen = !IsOpen;
                e.Handled = true;
                Invalidate();
                break;

            case Key.Escape:
                if (IsOpen)
                {
                    IsOpen = false;
                    e.Handled = true;
                    Invalidate();
                }
                break;

            case Key.Up:
                if (SelectedIndex > 0)
                {
                    SelectedIndex--;
                    e.Handled = true;
                }
                break;

            case Key.Down:
                if (SelectedIndex < _items.Count - 1)
                {
                    SelectedIndex++;
                    e.Handled = true;
                }
                break;
        }
    }

    public override void OnFocusLost()
    {
        base.OnFocusLost();
        if (IsOpen)
        {
            IsOpen = false;
        }
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        return new SKSize(
            availableSize.Width < float.MaxValue ? Math.Min(availableSize.Width, 200) : 200,
            40);
    }

    /// <summary>
    /// Override to include dropdown area in hit testing.
    /// </summary>
    protected override bool HitTestPopupArea(float x, float y)
    {
        // Use ScreenBounds for hit testing (accounts for scroll offset)
        var screenBounds = ScreenBounds;

        // Always include the picker button itself
        if (screenBounds.Contains(x, y))
            return true;

        // When open, also include the dropdown area
        if (_isOpen && _items.Count > 0)
        {
            var dropdownHeight = Math.Min(_items.Count * ItemHeight, _dropdownMaxHeight);
            var dropdownRect = new SKRect(
                screenBounds.Left,
                screenBounds.Bottom + 4,
                screenBounds.Right,
                screenBounds.Bottom + 4 + dropdownHeight);

            return dropdownRect.Contains(x, y);
        }

        return false;
    }
}
