// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Handlers;
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
    /// The layout's children in the accessibility tree (its own child list, which hides the base
    /// view's: assistive technology saw no children inside any layout).
    /// </summary>
    protected override List<IAccessible> GetAccessibleChildren()
    {
        var children = new List<IAccessible>();
        foreach (var child in _children.ToArray())
        {
            if (child is IAccessible accessible && !child.IsExcludedWithChildren)
                children.Add(accessible);
        }
        return children;
    }

    /// <summary>
    /// The children in drawing order: by ZIndex, lowest first, and in child
    /// order within the same ZIndex, as MAUI stacks them on every platform
    /// (a list's sticky group header, on a higher ZIndex, stays over the rows
    /// that scroll beneath it). Hit-testing walks it from the top.
    /// </summary>
    internal SkiaView[] ChildrenInZOrder()
    {
        var children = _children.ToArray();
        bool ordered = true;
        for (int i = 1; i < children.Length && ordered; i++)
            ordered = children[i - 1].ZIndex <= children[i].ZIndex;
        if (ordered)
            return children;
        return children
            .Select((child, index) => (child, index))
            .OrderBy(x => x.child.ZIndex)
            .ThenBy(x => x.index)
            .Select(x => x.child)
            .ToArray();
    }

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
        // Snapshot: a child's measure/arrange/draw may add or remove siblings
        // (a layout that builds its panes while arranging, as MALayout does).
        foreach (var child in _children.ToArray())
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
        foreach (var child in _children.ToArray())
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
            foreach (var c in _children.ToArray())
            {
                if (c is SkiaCollectionView) hasCV = true;
            }
            if (hasCV)
            {
                DiagnosticLog.Debug("SkiaLayoutView", $"[SkiaStackLayout+CV] OnDraw - bounds={bounds}, children={_children.Count}");
                foreach (var c in _children.ToArray())
                {
                    DiagnosticLog.Debug("SkiaLayoutView", $"[SkiaStackLayout+CV] Child: {c.GetType().Name}, IsVisible={c.IsVisible}, Bounds={c.Bounds}");
                }
            }
        }

        // Draw children in order; IsClippedToBounds keeps them inside the
        // layout (a translated strip of pages, as SfTabView slides its tabs).
        bool clip = ClipToBounds;
        if (clip)
        {
            canvas.Save();
            canvas.ClipRect(bounds);
        }
        foreach (var child in ChildrenInZOrder())
        {
            if (child.IsVisible)
            {
                child.Draw(canvas);
            }
        }
        if (clip)
            canvas.Restore();
    }

    public override SkiaView? HitTest(float x, float y)
    {
        if (!IsVisible || !IsEnabled || !Bounds.Contains(x, y))
            return null;

        var childHit = HitTestChildren(x, y);

        // A tappable layout takes the pointer unless the child under it handles
        // input itself (a Button over a tappable backdrop keeps its clicks, as
        // in MAUI); taps on plain content still reach the layout's recognizer.
        if (HasTapRecognizer(MauiView) && (childHit == null || !ClaimsInput(childHit, this)))
            return this;

        return childHit ?? this;
    }

    /// <summary>The top-most child hit at (x, y), or null.</summary>
    protected SkiaView? HitTestChildren(float x, float y)
    {
        var ordered = ChildrenInZOrder();
        for (int i = ordered.Length - 1; i >= 0; i--)
        {
            var hit = ordered[i].HitTestAt(x, y);
            if (hit != null)
                return hit;
        }
        return null;
    }

    /// <summary>
    /// True when the view has a tap recognizer the current press's button fires:
    /// a right-click-only recognizer (a context menu) does not make a layout take
    /// left clicks away from the controls in it, and does take right clicks.
    /// </summary>
    internal static bool HasTapRecognizer(Microsoft.Maui.Controls.View? view) => GestureManager.HasTapRecognizerForCurrentButton(view);

    /// <summary>
    /// True when <paramref name="hit"/>, or a view between it and
    /// <paramref name="container"/>, handles input itself: a focusable control,
    /// a nested tappable view, or an open swipe view (a tap on it invokes an
    /// item or closes it).
    /// </summary>
    internal static bool ClaimsInput(SkiaView hit, SkiaView container)
    {
        for (var view = hit; view != null && !ReferenceEquals(view, container); view = view.Parent)
        {
            if (view.IsFocusable || HasTapRecognizer(view.MauiView) || view is SkiaSwipeView { IsOpen: true })
                return true;
        }
        return false;
    }

    /// <summary>
    /// Forward pointer pressed events to the appropriate child,
    /// or handle locally if this layout has gesture recognizers.
    /// </summary>
    private bool _layoutPressed;

    public override void OnPointerPressed(PointerEventArgs e)
    {
        var hit = HitTest(e.X, e.Y);
        if (hit != null && hit != this)
        {
            hit.OnPointerPressed(e);
        }
        else if (hit == this && MauiView != null)
        {
            // This layout has gesture recognizers — handle locally.
            _layoutPressed = true;
            e.Handled = true;
            GestureManager.ProcessPointerDown(MauiView, e.X, e.Y);
            RaisePointerRoutedChain(RoutedPointerKind.Pressed, e);
        }
    }

    /// <summary>
    /// Forward pointer released events to the appropriate child,
    /// or handle locally if this layout has gesture recognizers.
    /// </summary>
    public override void OnPointerReleased(PointerEventArgs e)
    {
        if (_layoutPressed)
        {
            _layoutPressed = false;
            e.Handled = true;
            if (MauiView != null)
            {
                GestureManager.ProcessPointerUp(MauiView, e.X, e.Y);
            }
            RaisePointerRoutedChain(RoutedPointerKind.Released, e);
        }
        else
        {
            var hit = HitTest(e.X, e.Y);
            if (hit != null && hit != this)
            {
                hit.OnPointerReleased(e);
            }
            else if (hit == this)
            {
                RaisePointerRoutedChain(RoutedPointerKind.Released, e);
            }
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
        else if (hit == this)
        {
            RaisePointerRoutedChain(RoutedPointerKind.Moved, e);
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
