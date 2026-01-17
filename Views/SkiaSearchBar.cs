// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Rendering;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered search bar control.
/// Implements MAUI ISearchBar interface patterns.
/// </summary>
public class SkiaSearchBar : SkiaView
{
    #region Fields

    private readonly SkiaEntry _entry;
    private bool _showClearButton;

    #endregion

    #region Properties

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

    public Color TextColor
    {
        get => _entry.TextColor;
        set => _entry.TextColor = value;
    }

    public Color PlaceholderColor
    {
        get => _entry.PlaceholderColor;
        set => _entry.PlaceholderColor = value;
    }

    public Color SearchBarBackgroundColor { get; set; } = Color.FromRgb(245, 245, 245);
    public Color IconColor { get; set; } = Color.FromRgb(117, 117, 117);
    public Color ClearButtonColor { get; set; } = Color.FromRgb(158, 158, 158);
    public Color FocusedBorderColor { get; set; } = Color.FromRgb(33, 150, 243);

    public string FontFamily
    {
        get => _entry.FontFamily;
        set => _entry.FontFamily = value;
    }

    public double FontSize
    {
        get => _entry.FontSize;
        set => _entry.FontSize = value;
    }

    public FontAttributes FontAttributes
    {
        get => _entry.FontAttributes;
        set => _entry.FontAttributes = value;
    }

    public double CharacterSpacing
    {
        get => _entry.CharacterSpacing;
        set => _entry.CharacterSpacing = value;
    }

    public TextAlignment HorizontalTextAlignment
    {
        get => _entry.HorizontalTextAlignment;
        set => _entry.HorizontalTextAlignment = value;
    }

    public double CornerRadius { get; set; } = 8.0;
    public double IconSize { get; set; } = 20.0;

    public ICommand? SearchCommand { get; set; }
    public object? SearchCommandParameter { get; set; }

    #endregion

    #region Events

    public event EventHandler<TextChangedEventArgs>? TextChanged;
    public event EventHandler? SearchButtonPressed;

    #endregion

    #region Constructor

    public SkiaSearchBar()
    {
        _entry = new SkiaEntry
        {
            Placeholder = "Search...",
            EntryBackgroundColor = Colors.Transparent,
            BackgroundColor = Colors.Transparent,
            BorderColor = Colors.Transparent,
            FocusedBorderColor = Colors.Transparent,
            BorderWidth = 0
        };

        _entry.TextChanged += (s, e) =>
        {
            _showClearButton = !string.IsNullOrEmpty(e.NewTextValue);
            TextChanged?.Invoke(this, e);
            Invalidate();
        };

        _entry.Completed += (s, e) =>
        {
            SearchButtonPressed?.Invoke(this, EventArgs.Empty);
            if (SearchCommand?.CanExecute(SearchCommandParameter) == true)
            {
                SearchCommand.Execute(SearchCommandParameter);
            }
        };

        IsFocusable = true;
    }

    #endregion

    #region Drawing

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        float iconPadding = 12f;
        float clearButtonSize = 20f;
        float cornerRadius = (float)CornerRadius;
        float iconSize = (float)IconSize;

        // Draw background
        using var bgPaint = new SKPaint
        {
            Color = SearchBarBackgroundColor.ToSKColor(),
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        var bgRect = new SKRoundRect(bounds, cornerRadius);
        canvas.DrawRoundRect(bgRect, bgPaint);

        // Draw focus border
        if (IsFocused || _entry.IsFocused)
        {
            using var borderPaint = new SKPaint
            {
                Color = FocusedBorderColor.ToSKColor(),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2
            };
            canvas.DrawRoundRect(bgRect, borderPaint);
        }

        // Draw search icon
        var iconX = bounds.Left + iconPadding;
        var iconY = bounds.MidY;
        DrawSearchIcon(canvas, iconX, iconY, iconSize);

        // Calculate entry bounds - leave space for clear button
        var entryLeft = iconX + iconSize + iconPadding;
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
            Color = IconColor.ToSKColor(),
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
            Color = ClearButtonColor.ToSKColor().WithAlpha(80),
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawCircle(x, y, radius + 2, bgPaint);

        // Draw X
        using var paint = new SKPaint
        {
            Color = ClearButtonColor.ToSKColor(),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            StrokeCap = SKStrokeCap.Round
        };

        var offset = radius * 0.5f;
        canvas.DrawLine(x - offset, y - offset, x + offset, y + offset, paint);
        canvas.DrawLine(x + offset, y - offset, x - offset, y + offset, paint);
    }

    #endregion

    #region Input Handling

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

    #endregion

    #region Measurement

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        return new SKSize(250, 40);
    }

    #endregion
}
