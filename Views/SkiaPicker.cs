// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered picker/dropdown control.
/// </summary>
public class SkiaPicker : SkiaView
{
    private List<string> _items = new();
    private int _selectedIndex = -1;
    private bool _isOpen;
    private string _title = "";
    private float _dropdownMaxHeight = 200;
    private int _hoveredItemIndex = -1;

    // Styling
    public SKColor TextColor { get; set; } = SKColors.Black;
    public SKColor TitleColor { get; set; } = new SKColor(0x80, 0x80, 0x80);
    public SKColor BorderColor { get; set; } = new SKColor(0xBD, 0xBD, 0xBD);
    public SKColor DropdownBackgroundColor { get; set; } = SKColors.White;
    public SKColor SelectedItemBackgroundColor { get; set; } = new SKColor(0x21, 0x96, 0xF3, 0x30);
    public SKColor HoverItemBackgroundColor { get; set; } = new SKColor(0xE0, 0xE0, 0xE0);
    public string FontFamily { get; set; } = "Sans";
    public float FontSize { get; set; } = 14;
    public float ItemHeight { get; set; } = 40;
    public float CornerRadius { get; set; } = 4;

    public IList<string> Items => _items;

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (_selectedIndex != value)
            {
                _selectedIndex = value;
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
        }
    }

    public string? SelectedItem => _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : null;

    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            Invalidate();
        }
    }

    public bool IsOpen
    {
        get => _isOpen;
        set
        {
            _isOpen = value;
            Invalidate();
        }
    }

    public event EventHandler? SelectedIndexChanged;

    public SkiaPicker()
    {
        IsFocusable = true;
    }

    public void SetItems(IEnumerable<string> items)
    {
        _items.Clear();
        _items.AddRange(items);
        if (_selectedIndex >= _items.Count)
        {
            _selectedIndex = _items.Count > 0 ? 0 : -1;
        }
        Invalidate();
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        DrawPickerButton(canvas, bounds);

        if (_isOpen)
        {
            DrawDropdown(canvas, bounds);
        }
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
        if (_selectedIndex >= 0 && _selectedIndex < _items.Count)
        {
            displayText = _items[_selectedIndex];
            textPaint.Color = IsEnabled ? TextColor : TextColor.WithAlpha(128);
        }
        else
        {
            displayText = _title;
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
            // Up arrow
            path.MoveTo(centerX - arrowSize, centerY + arrowSize / 2);
            path.LineTo(centerX, centerY - arrowSize / 2);
            path.LineTo(centerX + arrowSize, centerY + arrowSize / 2);
        }
        else
        {
            // Down arrow
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
            if (i == _selectedIndex)
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

        if (_isOpen)
        {
            // Check if clicked on dropdown item
            var dropdownTop = Bounds.Bottom + 4;
            if (e.Y >= dropdownTop)
            {
                var itemIndex = (int)((e.Y - dropdownTop) / ItemHeight);
                if (itemIndex >= 0 && itemIndex < _items.Count)
                {
                    SelectedIndex = itemIndex;
                }
            }
            _isOpen = false;
        }
        else
        {
            // Check if clicked on picker button
            if (e.Y < Bounds.Bottom)
            {
                _isOpen = true;
            }
        }

        Invalidate();
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        if (!_isOpen) return;

        var dropdownTop = Bounds.Bottom + 4;
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
                _isOpen = !_isOpen;
                e.Handled = true;
                Invalidate();
                break;

            case Key.Escape:
                if (_isOpen)
                {
                    _isOpen = false;
                    e.Handled = true;
                    Invalidate();
                }
                break;

            case Key.Up:
                if (_isOpen && _selectedIndex > 0)
                {
                    SelectedIndex--;
                    e.Handled = true;
                }
                else if (!_isOpen && _selectedIndex > 0)
                {
                    SelectedIndex--;
                    e.Handled = true;
                }
                break;

            case Key.Down:
                if (_isOpen && _selectedIndex < _items.Count - 1)
                {
                    SelectedIndex++;
                    e.Handled = true;
                }
                else if (!_isOpen && _selectedIndex < _items.Count - 1)
                {
                    SelectedIndex++;
                    e.Handled = true;
                }
                break;
        }
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        return new SKSize(
            availableSize.Width < float.MaxValue ? Math.Min(availableSize.Width, 200) : 200,
            40);
    }
}
