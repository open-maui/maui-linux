// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;
using Microsoft.Maui.Platform.Linux.Rendering;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered search bar control.
/// </summary>
public class SkiaSearchBar : SkiaView
{
    private readonly SkiaEntry _entry;
    private bool _showClearButton;

    public string Text
    {
        get => _entry.Text;
        set => _entry.Text = value;
    }

    public string Placeholder
    {
        get => _entry.Placeholder;
        set => _entry.Placeholder = value;
    }

    public SKColor TextColor
    {
        get => _entry.TextColor;
        set => _entry.TextColor = value;
    }

    public SKColor PlaceholderColor
    {
        get => _entry.PlaceholderColor;
        set => _entry.PlaceholderColor = value;
    }

    public new SKColor BackgroundColor { get; set; } = new SKColor(0xF5, 0xF5, 0xF5);
    public SKColor IconColor { get; set; } = new SKColor(0x75, 0x75, 0x75);
    public SKColor ClearButtonColor { get; set; } = new SKColor(0x9E, 0x9E, 0x9E);
    public SKColor FocusedBorderColor { get; set; } = new SKColor(0x21, 0x96, 0xF3);
    public string FontFamily { get; set; } = "Sans";
    public float FontSize { get; set; } = 14;
    public float CornerRadius { get; set; } = 8;
    public float IconSize { get; set; } = 20;

    public event EventHandler<TextChangedEventArgs>? TextChanged;
    public event EventHandler? SearchButtonPressed;

    public SkiaSearchBar()
    {
        _entry = new SkiaEntry
        {
            Placeholder = "Search...",
            EntryBackgroundColor = SKColors.Transparent,
            BackgroundColor = SKColors.Transparent,
            BorderColor = SKColors.Transparent,
            FocusedBorderColor = SKColors.Transparent,
            BorderWidth = 0
        };

        _entry.TextChanged += (s, e) =>
        {
            _showClearButton = !string.IsNullOrEmpty(e.NewTextValue);
            TextChanged?.Invoke(this, e);
            Invalidate();
        };

        _entry.Completed += (s, e) => SearchButtonPressed?.Invoke(this, EventArgs.Empty);

        IsFocusable = true;
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        var iconPadding = 12f;
        var clearButtonSize = 20f;

        // Draw background
        using var bgPaint = new SKPaint
        {
            Color = BackgroundColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        var bgRect = new SKRoundRect(bounds, CornerRadius);
        canvas.DrawRoundRect(bgRect, bgPaint);

        // Draw focus border
        if (IsFocused || _entry.IsFocused)
        {
            using var borderPaint = new SKPaint
            {
                Color = FocusedBorderColor,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2
            };
            canvas.DrawRoundRect(bgRect, borderPaint);
        }

        // Draw search icon
        var iconX = bounds.Left + iconPadding;
        var iconY = bounds.MidY;
        DrawSearchIcon(canvas, iconX, iconY, IconSize);

        // Calculate entry bounds - leave space for clear button
        var entryLeft = iconX + IconSize + iconPadding;
        var entryRight = _showClearButton
            ? bounds.Right - clearButtonSize - iconPadding * 2
            : bounds.Right - iconPadding;

        var entryBounds = new SKRect(entryLeft, bounds.Top, entryRight, bounds.Bottom);
        _entry.Arrange(entryBounds);
        _entry.Draw(canvas);

        // Draw clear button
        if (_showClearButton)
        {
            var clearX = bounds.Right - iconPadding - clearButtonSize / 2;
            var clearY = bounds.MidY;
            DrawClearButton(canvas, clearX, clearY, clearButtonSize / 2);
        }
    }

    private void DrawSearchIcon(SKCanvas canvas, float x, float y, float size)
    {
        using var paint = new SKPaint
        {
            Color = IconColor,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            StrokeCap = SKStrokeCap.Round
        };

        var circleRadius = size * 0.35f;
        var circleCenter = new SKPoint(x + circleRadius, y - circleRadius * 0.3f);

        // Draw magnifying glass circle
        canvas.DrawCircle(circleCenter, circleRadius, paint);

        // Draw handle
        var handleStart = new SKPoint(
            circleCenter.X + circleRadius * 0.7f,
            circleCenter.Y + circleRadius * 0.7f);
        var handleEnd = new SKPoint(
            x + size * 0.8f,
            y + size * 0.3f);
        canvas.DrawLine(handleStart, handleEnd, paint);
    }

    private void DrawClearButton(SKCanvas canvas, float x, float y, float radius)
    {
        // Draw circle background
        using var bgPaint = new SKPaint
        {
            Color = ClearButtonColor.WithAlpha(80),
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawCircle(x, y, radius + 2, bgPaint);

        // Draw X
        using var paint = new SKPaint
        {
            Color = ClearButtonColor,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            StrokeCap = SKStrokeCap.Round
        };

        var offset = radius * 0.5f;
        canvas.DrawLine(x - offset, y - offset, x + offset, y + offset, paint);
        canvas.DrawLine(x + offset, y - offset, x - offset, y + offset, paint);
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        // Convert to local coordinates (relative to this view's bounds)
        var localX = e.X - Bounds.Left;

        // Check if clear button was clicked (in the rightmost 40 pixels)
        if (_showClearButton && localX >= Bounds.Width - 40)
        {
            Text = "";
            Invalidate();
            return;
        }

        // Forward to entry for text input focus and selection
        _entry.IsFocused = true;
        IsFocused = true;
        _entry.OnPointerPressed(e);
        Invalidate();
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        if (!IsEnabled) return;
        _entry.OnPointerMoved(e);
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        _entry.OnPointerReleased(e);
    }

    public override void OnTextInput(TextInputEventArgs e)
    {
        _entry.OnTextInput(e);
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _showClearButton)
        {
            Text = "";
            e.Handled = true;
            return;
        }

        _entry.OnKeyDown(e);
    }

    public override void OnKeyUp(KeyEventArgs e)
    {
        _entry.OnKeyUp(e);
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        return new SKSize(250, 40);
    }
}
