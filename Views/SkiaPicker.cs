// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered picker/dropdown control with full MAUI compliance.
/// Implements IPicker interface requirements:
/// - Title, TitleColor for placeholder
/// - SelectedIndex, SelectedItem for selection
/// - TextColor, Font properties for styling
/// - Items collection
/// </summary>
public class SkiaPicker : SkiaView
{
    #region SKColor Helper

    private static SKColor ToSKColor(Color? color)
    {
        if (color == null) return SKColors.Transparent;
        return new SKColor(
            (byte)(color.Red * 255),
            (byte)(color.Green * 255),
            (byte)(color.Blue * 255),
            (byte)(color.Alpha * 255));
    }

    #endregion

    #region BindableProperties

    public static readonly BindableProperty SelectedIndexProperty =
        BindableProperty.Create(
            nameof(SelectedIndex),
            typeof(int),
            typeof(SkiaPicker),
            -1,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaPicker)b).OnSelectedIndexChanged((int)o, (int)n));

    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(
            nameof(Title),
            typeof(string),
            typeof(SkiaPicker),
            "",
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaPicker)b).Invalidate());

    public static readonly BindableProperty TextColorProperty =
        BindableProperty.Create(
            nameof(TextColor),
            typeof(Color),
            typeof(SkiaPicker),
            Colors.Black,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaPicker)b).Invalidate());

    public static readonly BindableProperty TitleColorProperty =
        BindableProperty.Create(
            nameof(TitleColor),
            typeof(Color),
            typeof(SkiaPicker),
            Color.FromRgb(0x80, 0x80, 0x80),
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaPicker)b).Invalidate());

    public static readonly BindableProperty BorderColorProperty =
        BindableProperty.Create(
            nameof(BorderColor),
            typeof(Color),
            typeof(SkiaPicker),
            Color.FromRgb(0xBD, 0xBD, 0xBD),
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaPicker)b).Invalidate());

    public static readonly BindableProperty DropdownBackgroundColorProperty =
        BindableProperty.Create(
            nameof(DropdownBackgroundColor),
            typeof(Color),
            typeof(SkiaPicker),
            Colors.White,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaPicker)b).Invalidate());

    public static readonly BindableProperty SelectedItemBackgroundColorProperty =
        BindableProperty.Create(
            nameof(SelectedItemBackgroundColor),
            typeof(Color),
            typeof(SkiaPicker),
            Color.FromRgba(0x21, 0x96, 0xF3, 0x30),
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaPicker)b).Invalidate());

    public static readonly BindableProperty HoverItemBackgroundColorProperty =
        BindableProperty.Create(
            nameof(HoverItemBackgroundColor),
            typeof(Color),
            typeof(SkiaPicker),
            Color.FromRgb(0xE0, 0xE0, 0xE0),
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaPicker)b).Invalidate());

    public static readonly BindableProperty FontFamilyProperty =
        BindableProperty.Create(
            nameof(FontFamily),
            typeof(string),
            typeof(SkiaPicker),
            "Sans",
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaPicker)b).InvalidateMeasure());

    public static readonly BindableProperty FontSizeProperty =
        BindableProperty.Create(
            nameof(FontSize),
            typeof(double),
            typeof(SkiaPicker),
            14.0,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaPicker)b).InvalidateMeasure());

    public static readonly BindableProperty ItemHeightProperty =
        BindableProperty.Create(
            nameof(ItemHeight),
            typeof(double),
            typeof(SkiaPicker),
            40.0,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaPicker)b).Invalidate());

    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(
            nameof(CornerRadius),
            typeof(double),
            typeof(SkiaPicker),
            4.0,
            BindingMode.TwoWay,
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
    public Color TextColor
    {
        get => (Color)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the title color.
    /// </summary>
    public Color TitleColor
    {
        get => (Color)GetValue(TitleColorProperty);
        set => SetValue(TitleColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the border color.
    /// </summary>
    public Color BorderColor
    {
        get => (Color)GetValue(BorderColorProperty);
        set => SetValue(BorderColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the dropdown background color.
    /// </summary>
    public Color DropdownBackgroundColor
    {
        get => (Color)GetValue(DropdownBackgroundColorProperty);
        set => SetValue(DropdownBackgroundColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the selected item background color.
    /// </summary>
    public Color SelectedItemBackgroundColor
    {
        get => (Color)GetValue(SelectedItemBackgroundColorProperty);
        set => SetValue(SelectedItemBackgroundColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the hover item background color.
    /// </summary>
    public Color HoverItemBackgroundColor
    {
        get => (Color)GetValue(HoverItemBackgroundColorProperty);
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
    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the item height.
    /// </summary>
    public double ItemHeight
    {
        get => (double)GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the corner radius.
    /// </summary>
    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
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

    #region Private Fields

    private readonly List<string> _items = new();
    private bool _isOpen;
    private double _dropdownMaxHeight = 200;
    private int _hoveredItemIndex = -1;

    #endregion

    #region Events

    /// <summary>
    /// Event raised when selected index changes.
    /// </summary>
    public event EventHandler<SelectedIndexChangedEventArgs>? SelectedIndexChanged;

    #endregion

    #region Constructor

    public SkiaPicker()
    {
        IsFocusable = true;
    }

    #endregion

    #region Event Handlers

    private void OnSelectedIndexChanged(int oldValue, int newValue)
    {
        SelectedIndexChanged?.Invoke(this, new SelectedIndexChangedEventArgs(oldValue, newValue));
        Invalidate();
    }

    #endregion

    #region Public Methods

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

    #endregion

    #region Rendering

    private void DrawDropdownOverlay(SKCanvas canvas)
    {
        if (_items.Count == 0 || !_isOpen) return;
        DrawDropdown(canvas, ScreenBounds);
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        DrawPickerButton(canvas, bounds);
    }

    private void DrawPickerButton(SKCanvas canvas, SKRect bounds)
    {
        var cornerRadius = (float)CornerRadius;
        var fontSize = (float)FontSize;

        // Get colors
        var textColorSK = ToSKColor(TextColor);
        var titleColorSK = ToSKColor(TitleColor);
        var borderColorSK = ToSKColor(BorderColor);
        var focusColorSK = ToSKColor(Color.FromRgb(0x21, 0x96, 0xF3));

        // Draw background
        using var bgPaint = new SKPaint
        {
            Color = IsEnabled ? BackgroundColor : ToSKColor(Color.FromRgb(0xF5, 0xF5, 0xF5)),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        var buttonRect = new SKRoundRect(bounds, cornerRadius);
        canvas.DrawRoundRect(buttonRect, bgPaint);

        // Draw border
        using var borderPaint = new SKPaint
        {
            Color = IsFocused ? focusColorSK : borderColorSK,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = IsFocused ? 2 : 1,
            IsAntialias = true
        };
        canvas.DrawRoundRect(buttonRect, borderPaint);

        // Draw text or title
        using var font = new SKFont(SKTypeface.Default, fontSize);
        using var textPaint = new SKPaint(font)
        {
            IsAntialias = true
        };

        string displayText;
        if (SelectedIndex >= 0 && SelectedIndex < _items.Count)
        {
            displayText = _items[SelectedIndex];
            textPaint.Color = IsEnabled ? textColorSK : textColorSK.WithAlpha(128);
        }
        else
        {
            displayText = Title;
            textPaint.Color = titleColorSK;
        }

        var textBounds = new SKRect();
        textPaint.MeasureText(displayText, ref textBounds);

        var textX = bounds.Left + 12;
        var textY = bounds.MidY - textBounds.MidY;
        canvas.DrawText(displayText, textX, textY, textPaint);

        // Draw dropdown arrow
        DrawDropdownArrow(canvas, bounds, textColorSK);
    }

    private void DrawDropdownArrow(SKCanvas canvas, SKRect bounds, SKColor color)
    {
        using var paint = new SKPaint
        {
            Color = IsEnabled ? color : color.WithAlpha(128),
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

        var itemHeight = (float)ItemHeight;
        var cornerRadius = (float)CornerRadius;
        var fontSize = (float)FontSize;
        var dropdownMaxHeight = (float)_dropdownMaxHeight;

        var dropdownHeight = Math.Min(_items.Count * itemHeight, dropdownMaxHeight);
        var dropdownRect = new SKRect(
            bounds.Left,
            bounds.Bottom + 4,
            bounds.Right,
            bounds.Bottom + 4 + dropdownHeight);

        // Get colors
        var dropdownBgColorSK = ToSKColor(DropdownBackgroundColor);
        var borderColorSK = ToSKColor(BorderColor);
        var textColorSK = ToSKColor(TextColor);
        var selectedBgColorSK = ToSKColor(SelectedItemBackgroundColor);
        var hoverBgColorSK = ToSKColor(HoverItemBackgroundColor);

        // Draw shadow
        using var shadowPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 40),
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4),
            Style = SKPaintStyle.Fill
        };
        var shadowRect = new SKRect(dropdownRect.Left + 2, dropdownRect.Top + 2, dropdownRect.Right + 2, dropdownRect.Bottom + 2);
        canvas.DrawRoundRect(new SKRoundRect(shadowRect, cornerRadius), shadowPaint);

        // Draw dropdown background
        using var bgPaint = new SKPaint
        {
            Color = dropdownBgColorSK,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(dropdownRect, cornerRadius), bgPaint);

        // Draw border
        using var borderPaint = new SKPaint
        {
            Color = borderColorSK,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(dropdownRect, cornerRadius), borderPaint);

        // Clip to dropdown bounds
        canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(dropdownRect, cornerRadius));

        // Draw items
        using var font = new SKFont(SKTypeface.Default, fontSize);
        using var textPaint = new SKPaint(font)
        {
            Color = textColorSK,
            IsAntialias = true
        };

        for (int i = 0; i < _items.Count; i++)
        {
            var itemTop = dropdownRect.Top + i * itemHeight;
            if (itemTop > dropdownRect.Bottom) break;

            var itemRect = new SKRect(dropdownRect.Left, itemTop, dropdownRect.Right, itemTop + itemHeight);

            // Draw item background
            if (i == SelectedIndex)
            {
                using var selectedPaint = new SKPaint
                {
                    Color = selectedBgColorSK,
                    Style = SKPaintStyle.Fill
                };
                canvas.DrawRect(itemRect, selectedPaint);
            }
            else if (i == _hoveredItemIndex)
            {
                using var hoverPaint = new SKPaint
                {
                    Color = hoverBgColorSK,
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

    #endregion

    #region Pointer Events

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        var itemHeight = (float)ItemHeight;

        if (IsOpen)
        {
            var screenBounds = ScreenBounds;
            var dropdownTop = screenBounds.Bottom + 4;
            if (e.Y >= dropdownTop)
            {
                var itemIndex = (int)((e.Y - dropdownTop) / itemHeight);
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

        e.Handled = true;
        Invalidate();
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        if (!_isOpen) return;

        var itemHeight = (float)ItemHeight;
        var screenBounds = ScreenBounds;
        var dropdownTop = screenBounds.Bottom + 4;

        if (e.Y >= dropdownTop)
        {
            var newHovered = (int)((e.Y - dropdownTop) / itemHeight);
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

    #endregion

    #region Keyboard Events

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

            case Key.Home:
                if (_items.Count > 0)
                {
                    SelectedIndex = 0;
                    e.Handled = true;
                }
                break;

            case Key.End:
                if (_items.Count > 0)
                {
                    SelectedIndex = _items.Count - 1;
                    e.Handled = true;
                }
                break;
        }
    }

    #endregion

    #region Lifecycle

    public override void OnFocusLost()
    {
        base.OnFocusLost();
        if (IsOpen)
        {
            IsOpen = false;
        }
    }

    protected override void OnEnabledChanged()
    {
        base.OnEnabledChanged();
        SkiaVisualStateManager.GoToState(this, IsEnabled
            ? SkiaVisualStateManager.CommonStates.Normal
            : SkiaVisualStateManager.CommonStates.Disabled);
    }

    #endregion

    #region Layout

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        return new SKSize(
            availableSize.Width < float.MaxValue ? Math.Min(availableSize.Width, 200) : 200,
            40);
    }

    #endregion

    #region Hit Testing

    /// <summary>
    /// Override to include dropdown area in hit testing.
    /// </summary>
    protected override bool HitTestPopupArea(float x, float y)
    {
        var screenBounds = ScreenBounds;

        // Always include the picker button itself
        if (screenBounds.Contains(x, y))
            return true;

        // When open, also include the dropdown area
        if (_isOpen && _items.Count > 0)
        {
            var itemHeight = (float)ItemHeight;
            var dropdownMaxHeight = (float)_dropdownMaxHeight;
            var dropdownHeight = Math.Min(_items.Count * itemHeight, dropdownMaxHeight);
            var dropdownRect = new SKRect(
                screenBounds.Left,
                screenBounds.Bottom + 4,
                screenBounds.Right,
                screenBounds.Bottom + 4 + dropdownHeight);

            return dropdownRect.Contains(x, y);
        }

        return false;
    }

    #endregion
}

/// <summary>
/// Event args for selected index changed events.
/// </summary>
public class SelectedIndexChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the old selected index.
    /// </summary>
    public int OldIndex { get; }

    /// <summary>
    /// Gets the new selected index.
    /// </summary>
    public int NewIndex { get; }

    public SelectedIndexChangedEventArgs(int oldIndex, int newIndex)
    {
        OldIndex = oldIndex;
        NewIndex = newIndex;
    }
}
