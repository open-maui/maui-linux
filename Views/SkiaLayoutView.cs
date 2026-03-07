// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using Microsoft.Maui;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Base class for layout containers that can arrange child views.
/// </summary>
public abstract class SkiaLayoutView : SkiaView
{
    #region BindableProperties

    /// <summary>
    /// Bindable property for Spacing.
    /// </summary>
    public static readonly BindableProperty SpacingProperty =
        BindableProperty.Create(
            nameof(Spacing),
            typeof(double),
            typeof(SkiaLayoutView),
            0.0,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaLayoutView)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for Padding.
    /// </summary>
    public static readonly BindableProperty PaddingProperty =
        BindableProperty.Create(
            nameof(Padding),
            typeof(Thickness),
            typeof(SkiaLayoutView),
            default(Thickness),
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaLayoutView)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for ClipToBounds.
    /// </summary>
    public static readonly BindableProperty ClipToBoundsProperty =
        BindableProperty.Create(
            nameof(ClipToBounds),
            typeof(bool),
            typeof(SkiaLayoutView),
            false,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaLayoutView)b).Invalidate());

    #endregion

    private readonly List<SkiaView> _children = new();

    /// <summary>
    /// Gets the children of this layout.
    /// </summary>
    public new IReadOnlyList<SkiaView> Children => _children;

    /// <summary>
    /// Spacing between children.
    /// </summary>
    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>
    /// Padding around the content.
    /// </summary>
    public Thickness Padding
    {
        get => (Thickness)GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    /// <summary>
    /// Gets or sets whether child views are clipped to the bounds.
    /// </summary>
    public bool ClipToBounds
    {
        get => (bool)GetValue(ClipToBoundsProperty);
        set => SetValue(ClipToBoundsProperty, value);
    }

    /// <summary>
    /// Called when binding context changes. Propagates to layout children.
    /// </summary>
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        // Propagate binding context to layout children
        foreach (var child in _children)
        {
            SetInheritedBindingContext(child, BindingContext);
        }
    }

    /// <summary>
    /// Adds a child view.
    /// </summary>
    public virtual void AddChild(SkiaView child)
    {
        if (child.Parent != null)
        {
            throw new InvalidOperationException("View already has a parent");
        }

        _children.Add(child);
        child.Parent = this;

        // Propagate binding context to new child
        if (BindingContext != null)
        {
            SetInheritedBindingContext(child, BindingContext);
        }

        InvalidateMeasure();
        Invalidate();
    }

    /// <summary>
    /// Removes a child view.
    /// </summary>
    public virtual void RemoveChild(SkiaView child)
    {
        if (_children.Remove(child))
        {
            child.Parent = null;
            InvalidateMeasure();
            Invalidate();
        }
    }

    /// <summary>
    /// Removes a child at the specified index.
    /// </summary>
    public virtual void RemoveChildAt(int index)
    {
        if (index >= 0 && index < _children.Count)
        {
            var child = _children[index];
            _children.RemoveAt(index);
            child.Parent = null;
            InvalidateMeasure();
            Invalidate();
        }
    }

    /// <summary>
    /// Inserts a child at the specified index.
    /// </summary>
    public virtual void InsertChild(int index, SkiaView child)
    {
        if (child.Parent != null)
        {
            throw new InvalidOperationException("View already has a parent");
        }

        _children.Insert(index, child);
        child.Parent = this;

        // Propagate binding context to new child
        if (BindingContext != null)
        {
            SetInheritedBindingContext(child, BindingContext);
        }

        InvalidateMeasure();
        Invalidate();
    }

    /// <summary>
    /// Clears all children.
    /// </summary>
    public virtual void ClearChildren()
    {
        foreach (var child in _children)
        {
            child.Parent = null;
        }
        _children.Clear();
        InvalidateMeasure();
        Invalidate();
    }

    /// <summary>
    /// Gets the content bounds (bounds minus padding).
    /// </summary>
    protected virtual SKRect GetContentBounds()
    {
        return GetContentBounds(new SKRect((float)Bounds.Left, (float)Bounds.Top, (float)(Bounds.Left + Bounds.Width), (float)(Bounds.Top + Bounds.Height)));
    }

    /// <summary>
    /// Gets the content bounds for a given bounds rectangle.
    /// </summary>
    protected SKRect GetContentBounds(SKRect bounds)
    {
        return new SKRect(
            bounds.Left + (float)Padding.Left,
            bounds.Top + (float)Padding.Top,
            bounds.Right - (float)Padding.Right,
            bounds.Bottom - (float)Padding.Bottom);
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        // Draw background if set (for layouts inside CollectionView items)
        if (BackgroundColor != null && BackgroundColor != Colors.Transparent)
        {
            using var bgPaint = new SKPaint { Color = GetEffectiveBackgroundColor(), Style = SKPaintStyle.Fill };
            canvas.DrawRect(bounds, bgPaint);
        }

        // Log for StackLayout
        if (this is SkiaStackLayout)
        {
            bool hasCV = false;
            foreach (var c in _children)
            {
                if (c is SkiaCollectionView) hasCV = true;
            }
            if (hasCV)
            {
                DiagnosticLog.Debug("SkiaLayoutView", $"[SkiaStackLayout+CV] OnDraw - bounds={bounds}, children={_children.Count}");
                foreach (var c in _children)
                {
                    DiagnosticLog.Debug("SkiaLayoutView", $"[SkiaStackLayout+CV] Child: {c.GetType().Name}, IsVisible={c.IsVisible}, Bounds={c.Bounds}");
                }
            }
        }

        // Draw children in order
        foreach (var child in _children)
        {
            if (child.IsVisible)
            {
                child.Draw(canvas);
            }
        }
    }

    public override SkiaView? HitTest(float x, float y)
    {
        if (!IsVisible || !IsEnabled || !Bounds.Contains(x, y))
            return null;

        // Hit test children in reverse order (top-most first)
        for (int i = _children.Count - 1; i >= 0; i--)
        {
            var child = _children[i];
            var hit = child.HitTest(x, y);
            if (hit != null)
                return hit;
        }

        return this;
    }

    /// <summary>
    /// Forward pointer pressed events to the appropriate child.
    /// </summary>
    public override void OnPointerPressed(PointerEventArgs e)
    {
        // Find which child was hit and forward the event
        var hit = HitTest(e.X, e.Y);
        if (hit != null && hit != this)
        {
            hit.OnPointerPressed(e);
        }
    }

    /// <summary>
    /// Forward pointer released events to the appropriate child.
    /// </summary>
    public override void OnPointerReleased(PointerEventArgs e)
    {
        // Find which child was hit and forward the event
        var hit = HitTest(e.X, e.Y);
        if (hit != null && hit != this)
        {
            hit.OnPointerReleased(e);
        }
    }

    /// <summary>
    /// Forward pointer moved events to the appropriate child.
    /// </summary>
    public override void OnPointerMoved(PointerEventArgs e)
    {
        // Find which child was hit and forward the event
        var hit = HitTest(e.X, e.Y);
        if (hit != null && hit != this)
        {
            hit.OnPointerMoved(e);
        }
    }

    /// <summary>
    /// Forward scroll events to the appropriate child.
    /// </summary>
    public override void OnScroll(ScrollEventArgs e)
    {
        // Find which child was hit and forward the event
        var hit = HitTest(e.X, e.Y);
        if (hit != null && hit != this)
        {
            hit.OnScroll(e);
        }
    }
}
