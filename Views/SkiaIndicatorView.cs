// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// A view that displays indicators for a collection of items.
/// Used to show page indicators for CarouselView or similar controls.
/// </summary>
public class SkiaIndicatorView : SkiaView
{
    private int _count = 0;
    private int _position = 0;

    /// <summary>
    /// Gets or sets the number of indicators to display.
    /// </summary>
    public int Count
    {
        get => _count;
        set
        {
            if (_count != value)
            {
                _count = Math.Max(0, value);
                if (_position >= _count)
                {
                    _position = Math.Max(0, _count - 1);
                }
                InvalidateMeasure();
                Invalidate();
            }
        }
    }

    /// <summary>
    /// Gets or sets the selected position.
    /// </summary>
    public int Position
    {
        get => _position;
        set
        {
            int newValue = Math.Clamp(value, 0, Math.Max(0, _count - 1));
            if (_position != newValue)
            {
                _position = newValue;
                Invalidate();
            }
        }
    }

    /// <summary>
    /// Gets or sets the indicator color.
    /// </summary>
    public SKColor IndicatorColor { get; set; } = new SKColor(180, 180, 180);

    /// <summary>
    /// Gets or sets the selected indicator color.
    /// </summary>
    public SKColor SelectedIndicatorColor { get; set; } = new SKColor(33, 150, 243);

    /// <summary>
    /// Gets or sets the indicator size.
    /// </summary>
    public float IndicatorSize { get; set; } = 10f;

    /// <summary>
    /// Gets or sets the selected indicator size.
    /// </summary>
    public float SelectedIndicatorSize { get; set; } = 10f;

    /// <summary>
    /// Gets or sets the spacing between indicators.
    /// </summary>
    public float IndicatorSpacing { get; set; } = 8f;

    /// <summary>
    /// Gets or sets the indicator shape.
    /// </summary>
    public IndicatorShape IndicatorShape { get; set; } = IndicatorShape.Circle;

    /// <summary>
    /// Gets or sets whether indicators should have a border.
    /// </summary>
    public bool ShowBorder { get; set; } = false;

    /// <summary>
    /// Gets or sets the border color.
    /// </summary>
    public SKColor BorderColor { get; set; } = new SKColor(100, 100, 100);

    /// <summary>
    /// Gets or sets the border width.
    /// </summary>
    public float BorderWidth { get; set; } = 1f;

    /// <summary>
    /// Gets or sets the maximum visible indicators.
    /// </summary>
    public int MaximumVisible { get; set; } = 10;

    /// <summary>
    /// Gets or sets whether to hide indicators when count is 1 or less.
    /// </summary>
    public bool HideSingle { get; set; } = true;

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        if (_count <= 0 || (HideSingle && _count <= 1))
        {
            return SKSize.Empty;
        }

        int visibleCount = Math.Min(_count, MaximumVisible);
        float totalWidth = visibleCount * IndicatorSize + (visibleCount - 1) * IndicatorSpacing;
        float height = Math.Max(IndicatorSize, SelectedIndicatorSize);

        return new SKSize(totalWidth, height);
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        if (_count <= 0 || (HideSingle && _count <= 1)) return;

        canvas.Save();
        canvas.ClipRect(Bounds);

        int visibleCount = Math.Min(_count, MaximumVisible);
        float totalWidth = visibleCount * IndicatorSize + (visibleCount - 1) * IndicatorSpacing;
        float startX = Bounds.MidX - totalWidth / 2 + IndicatorSize / 2;
        float centerY = Bounds.MidY;

        // Determine visible range if count > MaximumVisible
        int startIndex = 0;
        int endIndex = visibleCount;

        if (_count > MaximumVisible)
        {
            int halfVisible = MaximumVisible / 2;
            startIndex = Math.Max(0, _position - halfVisible);
            endIndex = Math.Min(_count, startIndex + MaximumVisible);
            if (endIndex == _count)
            {
                startIndex = _count - MaximumVisible;
            }
        }

        using var normalPaint = new SKPaint
        {
            Color = IndicatorColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        using var selectedPaint = new SKPaint
        {
            Color = SelectedIndicatorColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        using var borderPaint = new SKPaint
        {
            Color = BorderColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = BorderWidth,
            IsAntialias = true
        };

        for (int i = startIndex; i < endIndex; i++)
        {
            int visualIndex = i - startIndex;
            float x = startX + visualIndex * (IndicatorSize + IndicatorSpacing);
            bool isSelected = i == _position;

            var paint = isSelected ? selectedPaint : normalPaint;
            float size = isSelected ? SelectedIndicatorSize : IndicatorSize;

            DrawIndicator(canvas, x, centerY, size, paint, borderPaint);
        }

        canvas.Restore();
    }

    private void DrawIndicator(SKCanvas canvas, float x, float y, float size, SKPaint fillPaint, SKPaint borderPaint)
    {
        float radius = size / 2;

        switch (IndicatorShape)
        {
            case IndicatorShape.Circle:
                canvas.DrawCircle(x, y, radius, fillPaint);
                if (ShowBorder)
                {
                    canvas.DrawCircle(x, y, radius, borderPaint);
                }
                break;

            case IndicatorShape.Square:
                var rect = new SKRect(x - radius, y - radius, x + radius, y + radius);
                canvas.DrawRect(rect, fillPaint);
                if (ShowBorder)
                {
                    canvas.DrawRect(rect, borderPaint);
                }
                break;

            case IndicatorShape.RoundedSquare:
                var roundRect = new SKRect(x - radius, y - radius, x + radius, y + radius);
                float cornerRadius = radius * 0.3f;
                canvas.DrawRoundRect(roundRect, cornerRadius, cornerRadius, fillPaint);
                if (ShowBorder)
                {
                    canvas.DrawRoundRect(roundRect, cornerRadius, cornerRadius, borderPaint);
                }
                break;

            case IndicatorShape.Diamond:
                using (var path = new SKPath())
                {
                    path.MoveTo(x, y - radius);
                    path.LineTo(x + radius, y);
                    path.LineTo(x, y + radius);
                    path.LineTo(x - radius, y);
                    path.Close();
                    canvas.DrawPath(path, fillPaint);
                    if (ShowBorder)
                    {
                        canvas.DrawPath(path, borderPaint);
                    }
                }
                break;
        }
    }

    public override SkiaView? HitTest(float x, float y)
    {
        if (!IsVisible || !Bounds.Contains(x, y)) return null;

        // Check if click is on an indicator
        if (_count > 0)
        {
            int visibleCount = Math.Min(_count, MaximumVisible);
            float totalWidth = visibleCount * IndicatorSize + (visibleCount - 1) * IndicatorSpacing;
            float startX = Bounds.MidX - totalWidth / 2;

            int startIndex = 0;
            if (_count > MaximumVisible)
            {
                int halfVisible = MaximumVisible / 2;
                startIndex = Math.Max(0, _position - halfVisible);
                if (startIndex + MaximumVisible > _count)
                {
                    startIndex = _count - MaximumVisible;
                }
            }

            for (int i = 0; i < visibleCount; i++)
            {
                float indicatorX = startX + i * (IndicatorSize + IndicatorSpacing);
                if (x >= indicatorX && x <= indicatorX + IndicatorSize)
                {
                    return this;
                }
            }
        }

        return null;
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled || _count <= 0) return;

        // Calculate which indicator was clicked
        int visibleCount = Math.Min(_count, MaximumVisible);
        float totalWidth = visibleCount * IndicatorSize + (visibleCount - 1) * IndicatorSpacing;
        float startX = Bounds.MidX - totalWidth / 2;

        int startIndex = 0;
        if (_count > MaximumVisible)
        {
            int halfVisible = MaximumVisible / 2;
            startIndex = Math.Max(0, _position - halfVisible);
            if (startIndex + MaximumVisible > _count)
            {
                startIndex = _count - MaximumVisible;
            }
        }

        float relativeX = e.X - startX;
        int visualIndex = (int)(relativeX / (IndicatorSize + IndicatorSpacing));

        if (visualIndex >= 0 && visualIndex < visibleCount)
        {
            Position = startIndex + visualIndex;
            e.Handled = true;
        }

        base.OnPointerPressed(e);
    }
}

/// <summary>
/// Shape of indicator dots.
/// </summary>
public enum IndicatorShape
{
    Circle,
    Square,
    RoundedSquare,
    Diamond
}
