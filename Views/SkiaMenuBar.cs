// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// A horizontal menu bar control.
/// </summary>
public class SkiaMenuBar : SkiaView
{
    private readonly List<MenuBarItem> _items = new();
    private int _hoveredIndex = -1;
    private int _openIndex = -1;
    private SkiaMenuFlyout? _openFlyout;

    /// <summary>
    /// Gets the menu bar items.
    /// </summary>
    public IList<MenuBarItem> Items => _items;

    /// <summary>
    /// Gets or sets the background color.
    /// </summary>
    public SKColor BackgroundColor { get; set; } = new SKColor(240, 240, 240);

    /// <summary>
    /// Gets or sets the text color.
    /// </summary>
    public SKColor TextColor { get; set; } = new SKColor(33, 33, 33);

    /// <summary>
    /// Gets or sets the hover background color.
    /// </summary>
    public SKColor HoverBackgroundColor { get; set; } = new SKColor(220, 220, 220);

    /// <summary>
    /// Gets or sets the active background color.
    /// </summary>
    public SKColor ActiveBackgroundColor { get; set; } = new SKColor(200, 200, 200);

    /// <summary>
    /// Gets or sets the bar height.
    /// </summary>
    public float BarHeight { get; set; } = 28f;

    /// <summary>
    /// Gets or sets the font size.
    /// </summary>
    public float FontSize { get; set; } = 13f;

    /// <summary>
    /// Gets or sets the item padding.
    /// </summary>
    public float ItemPadding { get; set; } = 12f;

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        return new SKSize(availableSize.Width, BarHeight);
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        canvas.Save();

        // Draw background
        using var bgPaint = new SKPaint
        {
            Color = BackgroundColor,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(Bounds, bgPaint);

        // Draw bottom border
        using var borderPaint = new SKPaint
        {
            Color = new SKColor(200, 200, 200),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1
        };
        canvas.DrawLine(Bounds.Left, Bounds.Bottom, Bounds.Right, Bounds.Bottom, borderPaint);

        // Draw menu items
        using var textPaint = new SKPaint
        {
            Color = TextColor,
            TextSize = FontSize,
            IsAntialias = true
        };

        float x = Bounds.Left;

        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            var textBounds = new SKRect();
            textPaint.MeasureText(item.Text, ref textBounds);

            float itemWidth = textBounds.Width + ItemPadding * 2;
            var itemBounds = new SKRect(x, Bounds.Top, x + itemWidth, Bounds.Bottom);

            // Draw item background
            if (i == _openIndex)
            {
                using var activePaint = new SKPaint { Color = ActiveBackgroundColor, Style = SKPaintStyle.Fill };
                canvas.DrawRect(itemBounds, activePaint);
            }
            else if (i == _hoveredIndex)
            {
                using var hoverPaint = new SKPaint { Color = HoverBackgroundColor, Style = SKPaintStyle.Fill };
                canvas.DrawRect(itemBounds, hoverPaint);
            }

            // Draw text
            float textX = x + ItemPadding;
            float textY = Bounds.MidY - textBounds.MidY;
            canvas.DrawText(item.Text, textX, textY, textPaint);

            item.Bounds = itemBounds;
            x += itemWidth;
        }

        // Draw open flyout
        _openFlyout?.Draw(canvas);

        canvas.Restore();
    }

    public override SkiaView? HitTest(float x, float y)
    {
        if (!IsVisible) return null;

        // Check flyout first
        if (_openFlyout != null)
        {
            var flyoutHit = _openFlyout.HitTest(x, y);
            if (flyoutHit != null) return flyoutHit;
        }

        if (Bounds.Contains(x, y))
        {
            return this;
        }

        // Close flyout if clicking outside
        if (_openFlyout != null)
        {
            CloseFlyout();
        }

        return null;
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        int newHovered = -1;
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i].Bounds.Contains(e.X, e.Y))
            {
                newHovered = i;
                break;
            }
        }

        if (newHovered != _hoveredIndex)
        {
            _hoveredIndex = newHovered;

            // If a menu is open and we hover another item, open that one
            if (_openIndex >= 0 && newHovered >= 0 && newHovered != _openIndex)
            {
                OpenFlyout(newHovered);
            }

            Invalidate();
        }

        base.OnPointerMoved(e);
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        // Check if clicking on flyout
        if (_openFlyout != null)
        {
            _openFlyout.OnPointerPressed(e);
            if (e.Handled)
            {
                CloseFlyout();
                return;
            }
        }

        // Check menu bar items
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i].Bounds.Contains(e.X, e.Y))
            {
                if (_openIndex == i)
                {
                    CloseFlyout();
                }
                else
                {
                    OpenFlyout(i);
                }
                e.Handled = true;
                return;
            }
        }

        // Click outside - close flyout
        if (_openFlyout != null)
        {
            CloseFlyout();
            e.Handled = true;
        }

        base.OnPointerPressed(e);
    }

    private void OpenFlyout(int index)
    {
        if (index < 0 || index >= _items.Count) return;

        var item = _items[index];
        _openIndex = index;

        _openFlyout = new SkiaMenuFlyout
        {
            Items = item.Items
        };

        // Position below the menu item
        float x = item.Bounds.Left;
        float y = item.Bounds.Bottom;
        _openFlyout.Position = new SKPoint(x, y);

        _openFlyout.ItemClicked += OnFlyoutItemClicked;
        Invalidate();
    }

    private void CloseFlyout()
    {
        if (_openFlyout != null)
        {
            _openFlyout.ItemClicked -= OnFlyoutItemClicked;
            _openFlyout = null;
        }
        _openIndex = -1;
        Invalidate();
    }

    private void OnFlyoutItemClicked(object? sender, MenuItemClickedEventArgs e)
    {
        CloseFlyout();
    }
}

/// <summary>
/// Represents a top-level menu bar item.
/// </summary>
public class MenuBarItem
{
    /// <summary>
    /// Gets or sets the display text.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets the menu items.
    /// </summary>
    public List<MenuItem> Items { get; } = new();

    /// <summary>
    /// Gets or sets the bounds (set during rendering).
    /// </summary>
    internal SKRect Bounds { get; set; }
}

/// <summary>
/// Represents a menu item.
/// </summary>
public class MenuItem
{
    /// <summary>
    /// Gets or sets the display text.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the keyboard shortcut text.
    /// </summary>
    public string? Shortcut { get; set; }

    /// <summary>
    /// Gets or sets whether this is a separator.
    /// </summary>
    public bool IsSeparator { get; set; }

    /// <summary>
    /// Gets or sets whether this item is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether this item is checked.
    /// </summary>
    public bool IsChecked { get; set; }

    /// <summary>
    /// Gets or sets the icon source.
    /// </summary>
    public string? IconSource { get; set; }

    /// <summary>
    /// Gets the sub-menu items.
    /// </summary>
    public List<MenuItem> SubItems { get; } = new();

    /// <summary>
    /// Event raised when the item is clicked.
    /// </summary>
    public event EventHandler? Clicked;

    internal void OnClicked()
    {
        Clicked?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// A dropdown menu flyout.
/// </summary>
public class SkiaMenuFlyout : SkiaView
{
    private int _hoveredIndex = -1;
    private SKRect _bounds;

    /// <summary>
    /// Gets or sets the menu items.
    /// </summary>
    public List<MenuItem> Items { get; set; } = new();

    /// <summary>
    /// Gets or sets the position.
    /// </summary>
    public SKPoint Position { get; set; }

    /// <summary>
    /// Gets or sets the background color.
    /// </summary>
    public SKColor BackgroundColor { get; set; } = SKColors.White;

    /// <summary>
    /// Gets or sets the text color.
    /// </summary>
    public SKColor TextColor { get; set; } = new SKColor(33, 33, 33);

    /// <summary>
    /// Gets or sets the disabled text color.
    /// </summary>
    public SKColor DisabledTextColor { get; set; } = new SKColor(160, 160, 160);

    /// <summary>
    /// Gets or sets the hover background color.
    /// </summary>
    public SKColor HoverBackgroundColor { get; set; } = new SKColor(230, 230, 230);

    /// <summary>
    /// Gets or sets the separator color.
    /// </summary>
    public SKColor SeparatorColor { get; set; } = new SKColor(220, 220, 220);

    /// <summary>
    /// Gets or sets the font size.
    /// </summary>
    public float FontSize { get; set; } = 13f;

    /// <summary>
    /// Gets or sets the item height.
    /// </summary>
    public float ItemHeight { get; set; } = 28f;

    /// <summary>
    /// Gets or sets the separator height.
    /// </summary>
    public float SeparatorHeight { get; set; } = 9f;

    /// <summary>
    /// Gets or sets the minimum width.
    /// </summary>
    public float MinWidth { get; set; } = 180f;

    /// <summary>
    /// Event raised when an item is clicked.
    /// </summary>
    public event EventHandler<MenuItemClickedEventArgs>? ItemClicked;

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        if (Items.Count == 0) return;

        // Calculate bounds
        float width = MinWidth;
        float height = 0;

        using var textPaint = new SKPaint
        {
            TextSize = FontSize,
            IsAntialias = true
        };

        foreach (var item in Items)
        {
            if (item.IsSeparator)
            {
                height += SeparatorHeight;
            }
            else
            {
                height += ItemHeight;

                var textBounds = new SKRect();
                textPaint.MeasureText(item.Text, ref textBounds);
                float itemWidth = textBounds.Width + 50; // Padding + icon space
                if (!string.IsNullOrEmpty(item.Shortcut))
                {
                    textPaint.MeasureText(item.Shortcut, ref textBounds);
                    itemWidth += textBounds.Width + 20;
                }
                width = Math.Max(width, itemWidth);
            }
        }

        _bounds = new SKRect(Position.X, Position.Y, Position.X + width, Position.Y + height);

        // Draw shadow
        using var shadowPaint = new SKPaint
        {
            ImageFilter = SKImageFilter.CreateDropShadow(0, 2, 8, 8, new SKColor(0, 0, 0, 40))
        };
        canvas.DrawRect(_bounds, shadowPaint);

        // Draw background
        using var bgPaint = new SKPaint
        {
            Color = BackgroundColor,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(_bounds, bgPaint);

        // Draw border
        using var borderPaint = new SKPaint
        {
            Color = new SKColor(200, 200, 200),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1
        };
        canvas.DrawRect(_bounds, borderPaint);

        // Draw items
        float y = _bounds.Top;
        textPaint.Color = TextColor;

        for (int i = 0; i < Items.Count; i++)
        {
            var item = Items[i];

            if (item.IsSeparator)
            {
                float separatorY = y + SeparatorHeight / 2;
                using var sepPaint = new SKPaint { Color = SeparatorColor, StrokeWidth = 1 };
                canvas.DrawLine(_bounds.Left + 8, separatorY, _bounds.Right - 8, separatorY, sepPaint);
                y += SeparatorHeight;
            }
            else
            {
                var itemBounds = new SKRect(_bounds.Left, y, _bounds.Right, y + ItemHeight);

                // Draw hover background
                if (i == _hoveredIndex && item.IsEnabled)
                {
                    using var hoverPaint = new SKPaint { Color = HoverBackgroundColor, Style = SKPaintStyle.Fill };
                    canvas.DrawRect(itemBounds, hoverPaint);
                }

                // Draw check mark
                if (item.IsChecked)
                {
                    using var checkPaint = new SKPaint
                    {
                        Color = item.IsEnabled ? TextColor : DisabledTextColor,
                        TextSize = FontSize,
                        IsAntialias = true
                    };
                    canvas.DrawText("✓", _bounds.Left + 8, y + ItemHeight / 2 + 5, checkPaint);
                }

                // Draw text
                textPaint.Color = item.IsEnabled ? TextColor : DisabledTextColor;
                canvas.DrawText(item.Text, _bounds.Left + 28, y + ItemHeight / 2 + 5, textPaint);

                // Draw shortcut
                if (!string.IsNullOrEmpty(item.Shortcut))
                {
                    textPaint.Color = DisabledTextColor;
                    var shortcutBounds = new SKRect();
                    textPaint.MeasureText(item.Shortcut, ref shortcutBounds);
                    canvas.DrawText(item.Shortcut, _bounds.Right - shortcutBounds.Width - 12, y + ItemHeight / 2 + 5, textPaint);
                }

                // Draw submenu arrow
                if (item.SubItems.Count > 0)
                {
                    canvas.DrawText("▸", _bounds.Right - 16, y + ItemHeight / 2 + 5, textPaint);
                }

                y += ItemHeight;
            }
        }
    }

    public override SkiaView? HitTest(float x, float y)
    {
        if (_bounds.Contains(x, y))
        {
            return this;
        }
        return null;
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        if (!_bounds.Contains(e.X, e.Y))
        {
            _hoveredIndex = -1;
            Invalidate();
            return;
        }

        float y = _bounds.Top;
        int newHovered = -1;

        for (int i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            float itemHeight = item.IsSeparator ? SeparatorHeight : ItemHeight;

            if (e.Y >= y && e.Y < y + itemHeight && !item.IsSeparator)
            {
                newHovered = i;
                break;
            }

            y += itemHeight;
        }

        if (newHovered != _hoveredIndex)
        {
            _hoveredIndex = newHovered;
            Invalidate();
        }
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (_hoveredIndex >= 0 && _hoveredIndex < Items.Count)
        {
            var item = Items[_hoveredIndex];
            if (item.IsEnabled && !item.IsSeparator)
            {
                item.OnClicked();
                ItemClicked?.Invoke(this, new MenuItemClickedEventArgs(item));
                e.Handled = true;
            }
        }
    }
}

/// <summary>
/// Event args for menu item clicked.
/// </summary>
public class MenuItemClickedEventArgs : EventArgs
{
    public MenuItem Item { get; }

    public MenuItemClickedEventArgs(MenuItem item)
    {
        Item = item;
    }
}
