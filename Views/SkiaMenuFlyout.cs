// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaMenuFlyout : SkiaView
{
    private int _hoveredIndex = -1;
    private SKRect _bounds;

    public List<MenuItem> Items { get; set; } = new List<MenuItem>();

    public SKPoint Position { get; set; }

    public new SKColor BackgroundColor { get; set; } = SKColors.White;

    public SKColor TextColor { get; set; } = new SKColor(33, 33, 33);

    public SKColor DisabledTextColor { get; set; } = new SKColor(160, 160, 160);

    public SKColor HoverBackgroundColor { get; set; } = new SKColor(230, 230, 230);

    public SKColor SeparatorColor { get; set; } = new SKColor(220, 220, 220);

    public float FontSize { get; set; } = 13f;

    public float ItemHeight { get; set; } = 28f;

    public float SeparatorHeight { get; set; } = 9f;

    public float MinWidth { get; set; } = 180f;

    public event EventHandler<MenuItemClickedEventArgs>? ItemClicked;

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        if (Items.Count == 0)
            return;

        float width = MinWidth;
        float height = 0f;

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
                continue;
            }

            height += ItemHeight;
            var textBounds = new SKRect();
            textPaint.MeasureText(item.Text, ref textBounds);
            float itemWidth = textBounds.Width + 50f;

            if (!string.IsNullOrEmpty(item.Shortcut))
            {
                textPaint.MeasureText(item.Shortcut, ref textBounds);
                itemWidth += textBounds.Width + 20f;
            }

            width = Math.Max(width, itemWidth);
        }

        _bounds = new SKRect(Position.X, Position.Y, Position.X + width, Position.Y + height);

        // Draw shadow
        using var shadowPaint = new SKPaint
        {
            ImageFilter = SKImageFilter.CreateDropShadow(0f, 2f, 8f, 8f, new SKColor(0, 0, 0, 40))
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
            StrokeWidth = 1f
        };
        canvas.DrawRect(_bounds, borderPaint);

        // Draw items
        float y = _bounds.Top;
        textPaint.Color = TextColor;

        for (int i = 0; i < Items.Count; i++)
        {
            var menuItem = Items[i];

            if (menuItem.IsSeparator)
            {
                float separatorY = y + SeparatorHeight / 2f;
                using var sepPaint = new SKPaint
                {
                    Color = SeparatorColor,
                    StrokeWidth = 1f
                };
                canvas.DrawLine(_bounds.Left + 8f, separatorY, _bounds.Right - 8f, separatorY, sepPaint);
                y += SeparatorHeight;
                continue;
            }

            var itemBounds = new SKRect(_bounds.Left, y, _bounds.Right, y + ItemHeight);

            // Draw hover background
            if (i == _hoveredIndex && menuItem.IsEnabled)
            {
                using var hoverPaint = new SKPaint
                {
                    Color = HoverBackgroundColor,
                    Style = SKPaintStyle.Fill
                };
                canvas.DrawRect(itemBounds, hoverPaint);
            }

            // Draw check mark
            if (menuItem.IsChecked)
            {
                using var checkPaint = new SKPaint
                {
                    Color = menuItem.IsEnabled ? TextColor : DisabledTextColor,
                    TextSize = FontSize,
                    IsAntialias = true
                };
                canvas.DrawText("\u2713", _bounds.Left + 8f, y + ItemHeight / 2f + 5f, checkPaint);
            }

            // Draw text
            textPaint.Color = menuItem.IsEnabled ? TextColor : DisabledTextColor;
            canvas.DrawText(menuItem.Text, _bounds.Left + 28f, y + ItemHeight / 2f + 5f, textPaint);

            // Draw shortcut
            if (!string.IsNullOrEmpty(menuItem.Shortcut))
            {
                textPaint.Color = DisabledTextColor;
                var shortcutBounds = new SKRect();
                textPaint.MeasureText(menuItem.Shortcut, ref shortcutBounds);
                canvas.DrawText(menuItem.Shortcut, _bounds.Right - shortcutBounds.Width - 12f, y + ItemHeight / 2f + 5f, textPaint);
            }

            // Draw submenu arrow
            if (menuItem.SubItems.Count > 0)
            {
                canvas.DrawText("\u25B8", _bounds.Right - 16f, y + ItemHeight / 2f + 5f, textPaint);
            }

            y += ItemHeight;
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
            var menuItem = Items[i];
            float itemHeight = menuItem.IsSeparator ? SeparatorHeight : ItemHeight;

            if (e.Y >= y && e.Y < y + itemHeight && !menuItem.IsSeparator)
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
            var menuItem = Items[_hoveredIndex];
            if (menuItem.IsEnabled && !menuItem.IsSeparator)
            {
                menuItem.OnClicked();
                ItemClicked?.Invoke(this, new MenuItemClickedEventArgs(menuItem));
                e.Handled = true;
            }
        }
    }
}
