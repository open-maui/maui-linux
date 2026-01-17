// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
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

    // Internal SKColor fields for rendering
    private SKColor _menuBackgroundColorSK = SkiaTheme.MenuBackgroundSK;
    private SKColor _textColorSK = SkiaTheme.TextPrimarySK;
    private SKColor _hoverBackgroundColorSK = SkiaTheme.MenuHoverSK;
    private SKColor _activeBackgroundColorSK = SkiaTheme.MenuActiveSK;

    // MAUI Color backing fields
    private Color _menuBackgroundColor = Color.FromRgb(240, 240, 240);
    private Color _textColor = Color.FromRgb(33, 33, 33);
    private Color _hoverBackgroundColor = Color.FromRgb(220, 220, 220);
    private Color _activeBackgroundColor = Color.FromRgb(200, 200, 200);

    /// <summary>
    /// Gets the menu bar items.
    /// </summary>
    public IList<MenuBarItem> Items => _items;

    /// <summary>
    /// Gets or sets the menu bar background color.
    /// </summary>
    public new Color MenuBackgroundColor
    {
        get => _menuBackgroundColor;
        set
        {
            _menuBackgroundColor = value;
            _menuBackgroundColorSK = value.ToSKColor();
            Invalidate();
        }
    }

    /// <summary>
    /// Gets or sets the text color.
    /// </summary>
    public Color TextColor
    {
        get => _textColor;
        set
        {
            _textColor = value;
            _textColorSK = value.ToSKColor();
            Invalidate();
        }
    }

    /// <summary>
    /// Gets or sets the hover background color.
    /// </summary>
    public Color HoverBackgroundColor
    {
        get => _hoverBackgroundColor;
        set
        {
            _hoverBackgroundColor = value;
            _hoverBackgroundColorSK = value.ToSKColor();
            Invalidate();
        }
    }

    /// <summary>
    /// Gets or sets the active background color.
    /// </summary>
    public Color ActiveBackgroundColor
    {
        get => _activeBackgroundColor;
        set
        {
            _activeBackgroundColor = value;
            _activeBackgroundColorSK = value.ToSKColor();
            Invalidate();
        }
    }

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
            Color = _menuBackgroundColorSK,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(Bounds, bgPaint);

        // Draw bottom border
        using var borderPaint = new SKPaint
        {
            Color = SkiaTheme.BorderMediumSK,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1
        };
        canvas.DrawLine(Bounds.Left, Bounds.Bottom, Bounds.Right, Bounds.Bottom, borderPaint);

        // Draw menu items
        using var textPaint = new SKPaint
        {
            Color = _textColorSK,
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
                using var activePaint = new SKPaint { Color = _activeBackgroundColorSK, Style = SKPaintStyle.Fill };
                canvas.DrawRect(itemBounds, activePaint);
            }
            else if (i == _hoveredIndex)
            {
                using var hoverPaint = new SKPaint { Color = _hoverBackgroundColorSK, Style = SKPaintStyle.Fill };
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
