// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered border/frame container control.
/// </summary>
public class SkiaBorder : SkiaLayoutView
{
    private float _strokeThickness = 1;
    private float _cornerRadius = 0;
    private SKColor _stroke = SKColors.Black;
    private float _paddingLeft = 0;
    private float _paddingTop = 0;
    private float _paddingRight = 0;
    private float _paddingBottom = 0;
    private bool _hasShadow;

    public float StrokeThickness
    {
        get => _strokeThickness;
        set { _strokeThickness = value; Invalidate(); }
    }

    public float CornerRadius
    {
        get => _cornerRadius;
        set { _cornerRadius = value; Invalidate(); }
    }

    public SKColor Stroke
    {
        get => _stroke;
        set { _stroke = value; Invalidate(); }
    }

    public float PaddingLeft
    {
        get => _paddingLeft;
        set { _paddingLeft = value; InvalidateMeasure(); }
    }

    public float PaddingTop
    {
        get => _paddingTop;
        set { _paddingTop = value; InvalidateMeasure(); }
    }

    public float PaddingRight
    {
        get => _paddingRight;
        set { _paddingRight = value; InvalidateMeasure(); }
    }

    public float PaddingBottom
    {
        get => _paddingBottom;
        set { _paddingBottom = value; InvalidateMeasure(); }
    }

    public bool HasShadow
    {
        get => _hasShadow;
        set { _hasShadow = value; Invalidate(); }
    }

    public void SetPadding(float all)
    {
        _paddingLeft = _paddingTop = _paddingRight = _paddingBottom = all;
        InvalidateMeasure();
    }

    public void SetPadding(float horizontal, float vertical)
    {
        _paddingLeft = _paddingRight = horizontal;
        _paddingTop = _paddingBottom = vertical;
        InvalidateMeasure();
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        var borderRect = new SKRect(
            bounds.Left + _strokeThickness / 2,
            bounds.Top + _strokeThickness / 2,
            bounds.Right - _strokeThickness / 2,
            bounds.Bottom - _strokeThickness / 2);

        // Draw shadow if enabled
        if (_hasShadow)
        {
            using var shadowPaint = new SKPaint
            {
                Color = new SKColor(0, 0, 0, 40),
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4),
                Style = SKPaintStyle.Fill
            };
            var shadowRect = new SKRect(borderRect.Left + 2, borderRect.Top + 2, borderRect.Right + 2, borderRect.Bottom + 2);
            canvas.DrawRoundRect(new SKRoundRect(shadowRect, _cornerRadius), shadowPaint);
        }

        // Draw background
        using var bgPaint = new SKPaint
        {
            Color = BackgroundColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(borderRect, _cornerRadius), bgPaint);

        // Draw border
        if (_strokeThickness > 0)
        {
            using var borderPaint = new SKPaint
            {
                Color = _stroke,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = _strokeThickness,
                IsAntialias = true
            };
            canvas.DrawRoundRect(new SKRoundRect(borderRect, _cornerRadius), borderPaint);
        }

        // Draw children (call base which draws children)
        foreach (var child in Children)
        {
            if (child.IsVisible)
            {
                child.Draw(canvas);
            }
        }
    }

    protected override SKRect GetContentBounds()
    {
        return GetContentBounds(Bounds);
    }

    protected new SKRect GetContentBounds(SKRect bounds)
    {
        return new SKRect(
            bounds.Left + _paddingLeft + _strokeThickness,
            bounds.Top + _paddingTop + _strokeThickness,
            bounds.Right - _paddingRight - _strokeThickness,
            bounds.Bottom - _paddingBottom - _strokeThickness);
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        var paddingWidth = _paddingLeft + _paddingRight + _strokeThickness * 2;
        var paddingHeight = _paddingTop + _paddingBottom + _strokeThickness * 2;

        var childAvailable = new SKSize(
            availableSize.Width - paddingWidth,
            availableSize.Height - paddingHeight);

        var maxChildSize = SKSize.Empty;

        foreach (var child in Children)
        {
            var childSize = child.Measure(childAvailable);
            maxChildSize = new SKSize(
                Math.Max(maxChildSize.Width, childSize.Width),
                Math.Max(maxChildSize.Height, childSize.Height));
        }

        return new SKSize(
            maxChildSize.Width + paddingWidth,
            maxChildSize.Height + paddingHeight);
    }

    protected override SKRect ArrangeOverride(SKRect bounds)
    {
        
        var contentBounds = GetContentBounds(bounds);

        foreach (var child in Children)
        {
            child.Arrange(contentBounds);
        }

        return bounds;
    }
}

/// <summary>
/// Frame control (alias for Border with shadow enabled).
/// </summary>
public class SkiaFrame : SkiaBorder
{
    public SkiaFrame()
    {
        HasShadow = true;
        CornerRadius = 4;
        SetPadding(10);
        BackgroundColor = SKColors.White;
    }
}
