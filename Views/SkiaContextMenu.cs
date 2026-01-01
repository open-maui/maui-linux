using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaContextMenu : SkiaView
{
    private readonly List<ContextMenuItem> _items;
    private readonly float _x;
    private readonly float _y;
    private int _hoveredIndex = -1;
    private SKRect[] _itemBounds = Array.Empty<SKRect>();

    private static readonly SKColor MenuBackground = new SKColor(255, 255, 255);
    private static readonly SKColor MenuBackgroundDark = new SKColor(48, 48, 48);
    private static readonly SKColor ItemHoverBackground = new SKColor(227, 242, 253);
    private static readonly SKColor ItemHoverBackgroundDark = new SKColor(80, 80, 80);
    private static readonly SKColor ItemTextColor = new SKColor(33, 33, 33);
    private static readonly SKColor ItemTextColorDark = new SKColor(224, 224, 224);
    private static readonly SKColor DisabledTextColor = new SKColor(158, 158, 158);
    private static readonly SKColor SeparatorColor = new SKColor(224, 224, 224);
    private static readonly SKColor ShadowColor = new SKColor(0, 0, 0, 40);

    private const float MenuPadding = 4f;
    private const float ItemHeight = 32f;
    private const float ItemPaddingH = 16f;
    private const float SeparatorHeight = 9f;
    private const float CornerRadius = 4f;
    private const float MinWidth = 120f;

    private bool _isDarkTheme;

    public SkiaContextMenu(float x, float y, List<ContextMenuItem> items, bool isDarkTheme = false)
    {
        _x = x;
        _y = y;
        _items = items;
        _isDarkTheme = isDarkTheme;
        IsFocusable = true;
    }

    public override void Draw(SKCanvas canvas)
    {
        float menuWidth = CalculateMenuWidth();
        float menuHeight = CalculateMenuHeight();
        float posX = _x;
        float posY = _y;

        // Adjust position to stay within bounds
        canvas.GetDeviceClipBounds(out var clipBounds);
        if (posX + menuWidth > clipBounds.Right)
        {
            posX = clipBounds.Right - menuWidth - 4f;
        }
        if (posY + menuHeight > clipBounds.Bottom)
        {
            posY = clipBounds.Bottom - menuHeight - 4f;
        }

        var menuRect = new SKRect(posX, posY, posX + menuWidth, posY + menuHeight);

        // Draw shadow
        using (var shadowPaint = new SKPaint
        {
            Color = ShadowColor,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f)
        })
        {
            canvas.DrawRoundRect(menuRect.Left + 2f, menuRect.Top + 2f, menuWidth, menuHeight, CornerRadius, CornerRadius, shadowPaint);
        }

        // Draw background
        using (var bgPaint = new SKPaint
        {
            Color = _isDarkTheme ? MenuBackgroundDark : MenuBackground,
            IsAntialias = true
        })
        {
            canvas.DrawRoundRect(menuRect, CornerRadius, CornerRadius, bgPaint);
        }

        // Draw border
        using (var borderPaint = new SKPaint
        {
            Color = SeparatorColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            IsAntialias = true
        })
        {
            canvas.DrawRoundRect(menuRect, CornerRadius, CornerRadius, borderPaint);
        }

        // Draw items
        _itemBounds = new SKRect[_items.Count];
        float itemY = posY + MenuPadding;

        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];

            if (item.IsSeparator)
            {
                float separatorY = itemY + SeparatorHeight / 2f;
                using (var sepPaint = new SKPaint { Color = SeparatorColor, StrokeWidth = 1f })
                {
                    canvas.DrawLine(posX + 8f, separatorY, posX + menuWidth - 8f, separatorY, sepPaint);
                }
                _itemBounds[i] = new SKRect(posX, itemY, posX + menuWidth, itemY + SeparatorHeight);
                itemY += SeparatorHeight;
                continue;
            }

            var itemRect = new SKRect(posX + MenuPadding, itemY, posX + menuWidth - MenuPadding, itemY + ItemHeight);
            _itemBounds[i] = itemRect;

            // Draw hover background
            if (i == _hoveredIndex && item.IsEnabled)
            {
                using (var hoverPaint = new SKPaint
                {
                    Color = _isDarkTheme ? ItemHoverBackgroundDark : ItemHoverBackground,
                    IsAntialias = true
                })
                {
                    canvas.DrawRoundRect(itemRect, CornerRadius, CornerRadius, hoverPaint);
                }
            }

            // Draw text
            using (var textPaint = new SKPaint
            {
                Color = !item.IsEnabled ? DisabledTextColor : (_isDarkTheme ? ItemTextColorDark : ItemTextColor),
                TextSize = 14f,
                IsAntialias = true,
                Typeface = SKTypeface.Default
            })
            {
                float textY = itemRect.MidY + textPaint.TextSize / 3f;
                canvas.DrawText(item.Text, itemRect.Left + ItemPaddingH, textY, textPaint);
            }

            itemY += ItemHeight;
        }
    }

    private float CalculateMenuWidth()
    {
        float maxWidth = MinWidth;
        using (var paint = new SKPaint { TextSize = 14f, Typeface = SKTypeface.Default })
        {
            foreach (var item in _items)
            {
                if (!item.IsSeparator)
                {
                    float textWidth = paint.MeasureText(item.Text) + ItemPaddingH * 2f;
                    maxWidth = Math.Max(maxWidth, textWidth);
                }
            }
        }
        return maxWidth + MenuPadding * 2f;
    }

    private float CalculateMenuHeight()
    {
        float height = MenuPadding * 2f;
        foreach (var item in _items)
        {
            height += item.IsSeparator ? SeparatorHeight : ItemHeight;
        }
        return height;
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        int oldHovered = _hoveredIndex;
        _hoveredIndex = -1;

        for (int i = 0; i < _itemBounds.Length; i++)
        {
            if (_itemBounds[i].Contains(e.X, e.Y) && !_items[i].IsSeparator)
            {
                _hoveredIndex = i;
                break;
            }
        }

        if (oldHovered != _hoveredIndex)
        {
            Invalidate();
        }
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        for (int i = 0; i < _itemBounds.Length; i++)
        {
            if (_itemBounds[i].Contains(e.X, e.Y))
            {
                var item = _items[i];
                if (item.IsEnabled && !item.IsSeparator && item.Action != null)
                {
                    LinuxDialogService.HideContextMenu();
                    item.Action();
                    return;
                }
            }
        }
        LinuxDialogService.HideContextMenu();
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            LinuxDialogService.HideContextMenu();
            e.Handled = true;
        }
    }
}
