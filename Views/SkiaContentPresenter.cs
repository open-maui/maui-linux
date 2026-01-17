// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Presents content within a ControlTemplate.
/// This control acts as a placeholder that gets replaced with the actual content
/// when the template is applied to a control.
/// </summary>
public class SkiaContentPresenter : SkiaView
{
    #region BindableProperties

    public static readonly BindableProperty ContentProperty =
        BindableProperty.Create(nameof(Content), typeof(SkiaView), typeof(SkiaContentPresenter), null,
            propertyChanged: (b, o, n) => ((SkiaContentPresenter)b).OnContentChanged((SkiaView?)o, (SkiaView?)n));

    public static readonly BindableProperty HorizontalContentAlignmentProperty =
        BindableProperty.Create(nameof(HorizontalContentAlignment), typeof(LayoutAlignment), typeof(SkiaContentPresenter), LayoutAlignment.Fill,
            propertyChanged: (b, o, n) => ((SkiaContentPresenter)b).InvalidateMeasure());

    public static readonly BindableProperty VerticalContentAlignmentProperty =
        BindableProperty.Create(nameof(VerticalContentAlignment), typeof(LayoutAlignment), typeof(SkiaContentPresenter), LayoutAlignment.Fill,
            propertyChanged: (b, o, n) => ((SkiaContentPresenter)b).InvalidateMeasure());

    public static readonly BindableProperty PaddingProperty =
        BindableProperty.Create(nameof(Padding), typeof(Thickness), typeof(SkiaContentPresenter), default(Thickness),
            propertyChanged: (b, o, n) => ((SkiaContentPresenter)b).InvalidateMeasure());

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the content to present.
    /// </summary>
    public SkiaView? Content
    {
        get => (SkiaView?)GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <summary>
    /// Gets or sets the horizontal alignment of the content.
    /// </summary>
    public LayoutAlignment HorizontalContentAlignment
    {
        get => (LayoutAlignment)GetValue(HorizontalContentAlignmentProperty);
        set => SetValue(HorizontalContentAlignmentProperty, value);
    }

    /// <summary>
    /// Gets or sets the vertical alignment of the content.
    /// </summary>
    public LayoutAlignment VerticalContentAlignment
    {
        get => (LayoutAlignment)GetValue(VerticalContentAlignmentProperty);
        set => SetValue(VerticalContentAlignmentProperty, value);
    }

    /// <summary>
    /// Gets or sets the padding around the content.
    /// </summary>
    public Thickness Padding
    {
        get => (Thickness)GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    #endregion

    private void OnContentChanged(SkiaView? oldContent, SkiaView? newContent)
    {
        if (oldContent != null)
        {
            oldContent.Parent = null;
        }

        if (newContent != null)
        {
            newContent.Parent = this;

            // Propagate binding context to new content
            if (BindingContext != null)
            {
                SetInheritedBindingContext(newContent, BindingContext);
            }
        }

        InvalidateMeasure();
    }

    /// <summary>
    /// Called when binding context changes. Propagates to content.
    /// </summary>
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        // Propagate binding context to content
        if (Content != null)
        {
            SetInheritedBindingContext(Content, BindingContext);
        }
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        // Draw background if set
        if (BackgroundColor != null && BackgroundColor != Colors.Transparent)
        {
            using var bgPaint = new SKPaint
            {
                Color = GetEffectiveBackgroundColor(),
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(bounds, bgPaint);
        }

        // Draw content
        Content?.Draw(canvas);
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        var padding = Padding;
        var paddingLeft = (float)padding.Left;
        var paddingTop = (float)padding.Top;
        var paddingRight = (float)padding.Right;
        var paddingBottom = (float)padding.Bottom;

        if (Content == null)
            return new SKSize(paddingLeft + paddingRight, paddingTop + paddingBottom);

        // When alignment is not Fill, give content unlimited size in that dimension
        // so it can measure its natural size without truncation
        var measureWidth = HorizontalContentAlignment == LayoutAlignment.Fill
            ? Math.Max(0, availableSize.Width - paddingLeft - paddingRight)
            : float.PositiveInfinity;
        var measureHeight = VerticalContentAlignment == LayoutAlignment.Fill
            ? Math.Max(0, availableSize.Height - paddingTop - paddingBottom)
            : float.PositiveInfinity;

        var contentSize = Content.Measure(new SKSize(measureWidth, measureHeight));
        return new SKSize(
            contentSize.Width + paddingLeft + paddingRight,
            contentSize.Height + paddingTop + paddingBottom);
    }

    protected override SKRect ArrangeOverride(SKRect bounds)
    {
        if (Content != null)
        {
            var padding = Padding;
            var contentBounds = new SKRect(
                bounds.Left + (float)padding.Left,
                bounds.Top + (float)padding.Top,
                bounds.Right - (float)padding.Right,
                bounds.Bottom - (float)padding.Bottom);

            // Apply alignment
            var contentSize = Content.DesiredSize;
            var arrangedBounds = ApplyAlignment(contentBounds, contentSize, HorizontalContentAlignment, VerticalContentAlignment);
            Content.Arrange(arrangedBounds);
        }

        return bounds;
    }

    private static SKRect ApplyAlignment(SKRect availableBounds, SKSize contentSize, LayoutAlignment horizontal, LayoutAlignment vertical)
    {
        float x = availableBounds.Left;
        float y = availableBounds.Top;
        float width = horizontal == LayoutAlignment.Fill ? availableBounds.Width : contentSize.Width;
        float height = vertical == LayoutAlignment.Fill ? availableBounds.Height : contentSize.Height;

        // Horizontal alignment
        switch (horizontal)
        {
            case LayoutAlignment.Center:
                x = availableBounds.Left + (availableBounds.Width - width) / 2;
                break;
            case LayoutAlignment.End:
                x = availableBounds.Right - width;
                break;
        }

        // Vertical alignment
        switch (vertical)
        {
            case LayoutAlignment.Center:
                y = availableBounds.Top + (availableBounds.Height - height) / 2;
                break;
            case LayoutAlignment.End:
                y = availableBounds.Bottom - height;
                break;
        }

        return new SKRect(x, y, x + width, y + height);
    }

    public override SkiaView? HitTest(float x, float y)
    {
        if (!IsVisible || !Bounds.Contains(x, y))
            return null;

        // Check content first
        if (Content != null)
        {
            var hit = Content.HitTest(x, y);
            if (hit != null)
                return hit;
        }

        return this;
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        Content?.OnPointerPressed(e);
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        Content?.OnPointerMoved(e);
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        Content?.OnPointerReleased(e);
    }
}
