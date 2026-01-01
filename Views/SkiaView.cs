// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Rendering;
using Microsoft.Maui.Platform.Linux.Window;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Base class for all Skia-rendered views on Linux.
/// Inherits from BindableObject to enable XAML styling, data binding, and Visual State Manager.
/// </summary>
public abstract class SkiaView : BindableObject, IDisposable
{
    // Popup overlay system for dropdowns, calendars, etc.
    private static readonly List<(SkiaView Owner, Action<SKCanvas> Draw)> _popupOverlays = new();

    public static void RegisterPopupOverlay(SkiaView owner, Action<SKCanvas> drawAction)
    {
        _popupOverlays.RemoveAll(p => p.Owner == owner);
        _popupOverlays.Add((owner, drawAction));
    }

    public static void UnregisterPopupOverlay(SkiaView owner)
    {
        _popupOverlays.RemoveAll(p => p.Owner == owner);
    }

    public static void DrawPopupOverlays(SKCanvas canvas)
    {
        // Restore canvas to clean state for overlay drawing
        // Save count tells us how many unmatched Saves there are
        while (canvas.SaveCount > 1)
        {
            canvas.Restore();
        }

        foreach (var (_, draw) in _popupOverlays)
        {
            canvas.Save();
            draw(canvas);
            canvas.Restore();
        }
    }

    /// <summary>
    /// Gets the popup owner that should receive pointer events at the given coordinates.
    /// This allows popups to receive events even outside their normal bounds.
    /// </summary>
    public static SkiaView? GetPopupOwnerAt(float x, float y)
    {
        // Check in reverse order (topmost popup first)
        for (int i = _popupOverlays.Count - 1; i >= 0; i--)
        {
            var owner = _popupOverlays[i].Owner;
            if (owner.HitTestPopupArea(x, y))
            {
                return owner;
            }
        }
        return null;
    }

    /// <summary>
    /// Checks if there are any active popup overlays.
    /// </summary>
    public static bool HasActivePopup => _popupOverlays.Count > 0;

    /// <summary>
    /// Override this to define the popup area for hit testing.
    /// </summary>
    protected virtual bool HitTestPopupArea(float x, float y)
    {
        // Default: no popup area beyond normal bounds
        return Bounds.Contains(x, y);
    }

    #region BindableProperties

    /// <summary>
    /// Bindable property for IsVisible.
    /// </summary>
    public static readonly BindableProperty IsVisibleProperty =
        BindableProperty.Create(
            nameof(IsVisible),
            typeof(bool),
            typeof(SkiaView),
            true,
            propertyChanged: (b, o, n) => ((SkiaView)b).OnVisibilityChanged());

    /// <summary>
    /// Bindable property for IsEnabled.
    /// </summary>
    public static readonly BindableProperty IsEnabledProperty =
        BindableProperty.Create(
            nameof(IsEnabled),
            typeof(bool),
            typeof(SkiaView),
            true,
            propertyChanged: (b, o, n) => ((SkiaView)b).OnEnabledChanged());

    /// <summary>
    /// Bindable property for Opacity.
    /// </summary>
    public static readonly BindableProperty OpacityProperty =
        BindableProperty.Create(
            nameof(Opacity),
            typeof(float),
            typeof(SkiaView),
            1.0f,
            coerceValue: (b, v) => Math.Clamp((float)v, 0f, 1f),
            propertyChanged: (b, o, n) => ((SkiaView)b).Invalidate());

    /// <summary>
    /// Bindable property for BackgroundColor.
    /// </summary>
    public static readonly BindableProperty BackgroundColorProperty =
        BindableProperty.Create(
            nameof(BackgroundColor),
            typeof(SKColor),
            typeof(SkiaView),
            SKColors.Transparent,
            propertyChanged: (b, o, n) => ((SkiaView)b).Invalidate());

    /// <summary>
    /// Bindable property for WidthRequest.
    /// </summary>
    public static readonly BindableProperty WidthRequestProperty =
        BindableProperty.Create(
            nameof(WidthRequest),
            typeof(double),
            typeof(SkiaView),
            -1.0,
            propertyChanged: (b, o, n) => ((SkiaView)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for HeightRequest.
    /// </summary>
    public static readonly BindableProperty HeightRequestProperty =
        BindableProperty.Create(
            nameof(HeightRequest),
            typeof(double),
            typeof(SkiaView),
            -1.0,
            propertyChanged: (b, o, n) => ((SkiaView)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for MinimumWidthRequest.
    /// </summary>
    public static readonly BindableProperty MinimumWidthRequestProperty =
        BindableProperty.Create(
            nameof(MinimumWidthRequest),
            typeof(double),
            typeof(SkiaView),
            0.0,
            propertyChanged: (b, o, n) => ((SkiaView)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for MinimumHeightRequest.
    /// </summary>
    public static readonly BindableProperty MinimumHeightRequestProperty =
        BindableProperty.Create(
            nameof(MinimumHeightRequest),
            typeof(double),
            typeof(SkiaView),
            0.0,
            propertyChanged: (b, o, n) => ((SkiaView)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for IsFocusable.
    /// </summary>
    public static readonly BindableProperty IsFocusableProperty =
        BindableProperty.Create(
            nameof(IsFocusable),
            typeof(bool),
            typeof(SkiaView),
            false);

    /// <summary>
    /// Bindable property for Margin.
    /// </summary>
    public static readonly BindableProperty MarginProperty =
        BindableProperty.Create(
            nameof(Margin),
            typeof(Thickness),
            typeof(SkiaView),
            default(Thickness),
            propertyChanged: (b, o, n) => ((SkiaView)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for HorizontalOptions.
    /// </summary>
    public static readonly BindableProperty HorizontalOptionsProperty =
        BindableProperty.Create(
            nameof(HorizontalOptions),
            typeof(LayoutOptions),
            typeof(SkiaView),
            LayoutOptions.Fill,
            propertyChanged: (b, o, n) => ((SkiaView)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for VerticalOptions.
    /// </summary>
    public static readonly BindableProperty VerticalOptionsProperty =
        BindableProperty.Create(
            nameof(VerticalOptions),
            typeof(LayoutOptions),
            typeof(SkiaView),
            LayoutOptions.Fill,
            propertyChanged: (b, o, n) => ((SkiaView)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for Name (used for template child lookup).
    /// </summary>
    public static readonly BindableProperty NameProperty =
        BindableProperty.Create(
            nameof(Name),
            typeof(string),
            typeof(SkiaView),
            string.Empty);

    /// <summary>
    /// Bindable property for Scale.
    /// </summary>
    public static readonly BindableProperty ScaleProperty =
        BindableProperty.Create(
            nameof(Scale),
            typeof(double),
            typeof(SkiaView),
            1.0,
            propertyChanged: (b, o, n) => ((SkiaView)b).Invalidate());

    /// <summary>
    /// Bindable property for ScaleX.
    /// </summary>
    public static readonly BindableProperty ScaleXProperty =
        BindableProperty.Create(
            nameof(ScaleX),
            typeof(double),
            typeof(SkiaView),
            1.0,
            propertyChanged: (b, o, n) => ((SkiaView)b).Invalidate());

    /// <summary>
    /// Bindable property for ScaleY.
    /// </summary>
    public static readonly BindableProperty ScaleYProperty =
        BindableProperty.Create(
            nameof(ScaleY),
            typeof(double),
            typeof(SkiaView),
            1.0,
            propertyChanged: (b, o, n) => ((SkiaView)b).Invalidate());

    /// <summary>
    /// Bindable property for Rotation.
    /// </summary>
    public static readonly BindableProperty RotationProperty =
        BindableProperty.Create(
            nameof(Rotation),
            typeof(double),
            typeof(SkiaView),
            0.0,
            propertyChanged: (b, o, n) => ((SkiaView)b).Invalidate());

    /// <summary>
    /// Bindable property for RotationX.
    /// </summary>
    public static readonly BindableProperty RotationXProperty =
        BindableProperty.Create(
            nameof(RotationX),
            typeof(double),
            typeof(SkiaView),
            0.0,
            propertyChanged: (b, o, n) => ((SkiaView)b).Invalidate());

    /// <summary>
    /// Bindable property for RotationY.
    /// </summary>
    public static readonly BindableProperty RotationYProperty =
        BindableProperty.Create(
            nameof(RotationY),
            typeof(double),
            typeof(SkiaView),
            0.0,
            propertyChanged: (b, o, n) => ((SkiaView)b).Invalidate());

    /// <summary>
    /// Bindable property for TranslationX.
    /// </summary>
    public static readonly BindableProperty TranslationXProperty =
        BindableProperty.Create(
            nameof(TranslationX),
            typeof(double),
            typeof(SkiaView),
            0.0,
            propertyChanged: (b, o, n) => ((SkiaView)b).Invalidate());

    /// <summary>
    /// Bindable property for TranslationY.
    /// </summary>
    public static readonly BindableProperty TranslationYProperty =
        BindableProperty.Create(
            nameof(TranslationY),
            typeof(double),
            typeof(SkiaView),
            0.0,
            propertyChanged: (b, o, n) => ((SkiaView)b).Invalidate());

    /// <summary>
    /// Bindable property for AnchorX.
    /// </summary>
    public static readonly BindableProperty AnchorXProperty =
        BindableProperty.Create(
            nameof(AnchorX),
            typeof(double),
            typeof(SkiaView),
            0.5,
            propertyChanged: (b, o, n) => ((SkiaView)b).Invalidate());

    /// <summary>
    /// Bindable property for AnchorY.
    /// </summary>
    public static readonly BindableProperty AnchorYProperty =
        BindableProperty.Create(
            nameof(AnchorY),
            typeof(double),
            typeof(SkiaView),
            0.5,
            propertyChanged: (b, o, n) => ((SkiaView)b).Invalidate());

    #endregion

    private bool _disposed;
    private SKRect _bounds;
    private SkiaView? _parent;
    private readonly List<SkiaView> _children = new();

    /// <summary>
    /// Gets the absolute bounds of this view in screen coordinates.
    /// </summary>
    public SKRect GetAbsoluteBounds()
    {
        var bounds = Bounds;
        var current = Parent;
        while (current != null)
        {
            // Adjust for scroll offset if parent is a ScrollView
            if (current is SkiaScrollView scrollView)
            {
                bounds = new SKRect(
                    bounds.Left - scrollView.ScrollX,
                    bounds.Top - scrollView.ScrollY,
                    bounds.Right - scrollView.ScrollX,
                    bounds.Bottom - scrollView.ScrollY);
            }
            current = current.Parent;
        }
        return bounds;
    }

    /// <summary>
    /// Gets or sets the bounds of this view in parent coordinates.
    /// </summary>
    public SKRect Bounds
    {
        get => _bounds;
        set
        {
            if (_bounds != value)
            {
                _bounds = value;
                OnBoundsChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets whether this view is visible.
    /// </summary>
    public bool IsVisible
    {
        get => (bool)GetValue(IsVisibleProperty);
        set => SetValue(IsVisibleProperty, value);
    }

    /// <summary>
    /// Gets or sets whether this view is enabled for interaction.
    /// </summary>
    public bool IsEnabled
    {
        get => (bool)GetValue(IsEnabledProperty);
        set => SetValue(IsEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets the opacity of this view (0.0 to 1.0).
    /// </summary>
    public float Opacity
    {
        get => (float)GetValue(OpacityProperty);
        set => SetValue(OpacityProperty, value);
    }

    /// <summary>
    /// Gets or sets the background color.
    /// </summary>
    private SKColor _backgroundColor = SKColors.Transparent;
    public SKColor BackgroundColor
    {
        get => _backgroundColor;
        set
        {
            if (_backgroundColor != value)
            {
                _backgroundColor = value;
                SetValue(BackgroundColorProperty, value); // Keep BindableProperty in sync for bindings
                Invalidate();
            }
        }
    }

    /// <summary>
    /// Gets or sets the requested width.
    /// </summary>
    public double WidthRequest
    {
        get => (double)GetValue(WidthRequestProperty);
        set => SetValue(WidthRequestProperty, value);
    }

    /// <summary>
    /// Gets or sets the requested height.
    /// </summary>
    public double HeightRequest
    {
        get => (double)GetValue(HeightRequestProperty);
        set => SetValue(HeightRequestProperty, value);
    }

    /// <summary>
    /// Gets or sets the minimum width request.
    /// </summary>
    public double MinimumWidthRequest
    {
        get => (double)GetValue(MinimumWidthRequestProperty);
        set => SetValue(MinimumWidthRequestProperty, value);
    }

    /// <summary>
    /// Gets or sets the minimum height request.
    /// </summary>
    public double MinimumHeightRequest
    {
        get => (double)GetValue(MinimumHeightRequestProperty);
        set => SetValue(MinimumHeightRequestProperty, value);
    }

    /// <summary>
    /// Gets or sets the requested width (backwards compatibility alias).
    /// </summary>
    public double RequestedWidth
    {
        get => WidthRequest;
        set => WidthRequest = value;
    }

    /// <summary>
    /// Gets or sets the requested height (backwards compatibility alias).
    /// </summary>
    public double RequestedHeight
    {
        get => HeightRequest;
        set => HeightRequest = value;
    }

    /// <summary>
    /// Gets or sets whether this view can receive keyboard focus.
    /// </summary>
    public bool IsFocusable
    {
        get => (bool)GetValue(IsFocusableProperty);
        set => SetValue(IsFocusableProperty, value);
    }

    /// <summary>
    /// Gets or sets the margin around this view.
    /// </summary>
    public Thickness Margin
    {
        get => (Thickness)GetValue(MarginProperty);
        set => SetValue(MarginProperty, value);
    }

    /// <summary>
    /// Gets or sets the horizontal layout options.
    /// </summary>
    public LayoutOptions HorizontalOptions
    {
        get => (LayoutOptions)GetValue(HorizontalOptionsProperty);
        set => SetValue(HorizontalOptionsProperty, value);
    }

    /// <summary>
    /// Gets or sets the vertical layout options.
    /// </summary>
    public LayoutOptions VerticalOptions
    {
        get => (LayoutOptions)GetValue(VerticalOptionsProperty);
        set => SetValue(VerticalOptionsProperty, value);
    }

    /// <summary>
    /// Gets or sets the name of this view (used for template child lookup).
    /// </summary>
    public string Name
    {
        get => (string)GetValue(NameProperty);
        set => SetValue(NameProperty, value);
    }

    /// <summary>
    /// Gets or sets the uniform scale factor.
    /// </summary>
    public double Scale
    {
        get => (double)GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    /// <summary>
    /// Gets or sets the X-axis scale factor.
    /// </summary>
    public double ScaleX
    {
        get => (double)GetValue(ScaleXProperty);
        set => SetValue(ScaleXProperty, value);
    }

    /// <summary>
    /// Gets or sets the Y-axis scale factor.
    /// </summary>
    public double ScaleY
    {
        get => (double)GetValue(ScaleYProperty);
        set => SetValue(ScaleYProperty, value);
    }

    /// <summary>
    /// Gets or sets the rotation in degrees around the Z-axis.
    /// </summary>
    public double Rotation
    {
        get => (double)GetValue(RotationProperty);
        set => SetValue(RotationProperty, value);
    }

    /// <summary>
    /// Gets or sets the rotation in degrees around the X-axis.
    /// </summary>
    public double RotationX
    {
        get => (double)GetValue(RotationXProperty);
        set => SetValue(RotationXProperty, value);
    }

    /// <summary>
    /// Gets or sets the rotation in degrees around the Y-axis.
    /// </summary>
    public double RotationY
    {
        get => (double)GetValue(RotationYProperty);
        set => SetValue(RotationYProperty, value);
    }

    /// <summary>
    /// Gets or sets the X translation offset.
    /// </summary>
    public double TranslationX
    {
        get => (double)GetValue(TranslationXProperty);
        set => SetValue(TranslationXProperty, value);
    }

    /// <summary>
    /// Gets or sets the Y translation offset.
    /// </summary>
    public double TranslationY
    {
        get => (double)GetValue(TranslationYProperty);
        set => SetValue(TranslationYProperty, value);
    }

    /// <summary>
    /// Gets or sets the X anchor point for transforms (0.0 to 1.0).
    /// </summary>
    public double AnchorX
    {
        get => (double)GetValue(AnchorXProperty);
        set => SetValue(AnchorXProperty, value);
    }

    /// <summary>
    /// Gets or sets the Y anchor point for transforms (0.0 to 1.0).
    /// </summary>
    public double AnchorY
    {
        get => (double)GetValue(AnchorYProperty);
        set => SetValue(AnchorYProperty, value);
    }

    /// <summary>
    /// Gets or sets the cursor type when hovering over this view.
    /// </summary>
    public CursorType CursorType { get; set; }

    /// <summary>
    /// Gets or sets the MAUI View this platform view represents.
    /// Used for gesture processing.
    /// </summary>
    public View? MauiView { get; set; }

    /// <summary>
    /// Gets or sets whether this view currently has keyboard focus.
    /// </summary>
    public bool IsFocused { get; internal set; }

    /// <summary>
    /// Gets or sets the parent view.
    /// </summary>
    public SkiaView? Parent
    {
        get => _parent;
        internal set => _parent = value;
    }

    /// <summary>
    /// Gets the bounds of this view in screen coordinates (accounting for scroll offsets).
    /// </summary>
    public SKRect ScreenBounds
    {
        get
        {
            var bounds = Bounds;
            var parent = _parent;

            // Walk up the tree and adjust for scroll offsets
            while (parent != null)
            {
                if (parent is SkiaScrollView scrollView)
                {
                    bounds = new SKRect(
                        bounds.Left - scrollView.ScrollX,
                        bounds.Top - scrollView.ScrollY,
                        bounds.Right - scrollView.ScrollX,
                        bounds.Bottom - scrollView.ScrollY);
                }
                parent = parent.Parent;
            }

            return bounds;
        }
    }

    /// <summary>
    /// Gets the desired size calculated during measure.
    /// </summary>
    public SKSize DesiredSize { get; protected set; }

    /// <summary>
    /// Gets the child views.
    /// </summary>
    public IReadOnlyList<SkiaView> Children => _children;

    /// <summary>
    /// Event raised when this view needs to be redrawn.
    /// </summary>
    public event EventHandler? Invalidated;

    /// <summary>
    /// Called when visibility changes.
    /// </summary>
    protected virtual void OnVisibilityChanged()
    {
        Invalidate();
    }

    /// <summary>
    /// Called when enabled state changes.
    /// </summary>
    protected virtual void OnEnabledChanged()
    {
        Invalidate();
    }

    /// <summary>
    /// Called when binding context changes. Propagates to children.
    /// </summary>
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        // Propagate binding context to children
        foreach (var child in _children)
        {
            SetInheritedBindingContext(child, BindingContext);
        }
    }

    /// <summary>
    /// Adds a child view.
    /// </summary>
    public void AddChild(SkiaView child)
    {
        if (child._parent != null)
            throw new InvalidOperationException("View already has a parent");

        child._parent = this;
        _children.Add(child);

        // Propagate binding context to new child
        if (BindingContext != null)
        {
            SetInheritedBindingContext(child, BindingContext);
        }

        Invalidate();
    }

    /// <summary>
    /// Removes a child view.
    /// </summary>
    public void RemoveChild(SkiaView child)
    {
        if (child._parent != this)
            return;

        child._parent = null;
        _children.Remove(child);
        Invalidate();
    }

    /// <summary>
    /// Inserts a child view at the specified index.
    /// </summary>
    public void InsertChild(int index, SkiaView child)
    {
        if (child._parent != null)
            throw new InvalidOperationException("View already has a parent");

        child._parent = this;
        _children.Insert(index, child);

        // Propagate binding context to new child
        if (BindingContext != null)
        {
            SetInheritedBindingContext(child, BindingContext);
        }

        Invalidate();
    }

    /// <summary>
    /// Removes all child views.
    /// </summary>
    public void ClearChildren()
    {
        foreach (var child in _children)
        {
            child._parent = null;
        }
        _children.Clear();
        Invalidate();
    }

    /// <summary>
    /// Requests that this view be redrawn.
    /// </summary>
    public void Invalidate()
    {
        LinuxApplication.LogInvalidate(GetType().Name);
        Invalidated?.Invoke(this, EventArgs.Empty);

        // Notify rendering engine of dirty region
        if (Bounds.Width > 0 && Bounds.Height > 0)
        {
            SkiaRenderingEngine.Current?.InvalidateRegion(Bounds);
        }

        if (_parent != null)
        {
            _parent.Invalidate();
        }
        else
        {
            LinuxApplication.RequestRedraw();
        }
    }

    /// <summary>
    /// Invalidates the cached measurement.
    /// </summary>
    public void InvalidateMeasure()
    {
        DesiredSize = SKSize.Empty;
        _parent?.InvalidateMeasure();
        Invalidate();
    }

    /// <summary>
    /// Draws this view and its children to the canvas.
    /// </summary>
    public virtual void Draw(SKCanvas canvas)
    {
        if (!IsVisible || Opacity <= 0)
        {
            return;
        }

        canvas.Save();

        // Apply transforms if any are set
        if (Scale != 1.0 || ScaleX != 1.0 || ScaleY != 1.0 ||
            Rotation != 0.0 || RotationX != 0.0 || RotationY != 0.0 ||
            TranslationX != 0.0 || TranslationY != 0.0)
        {
            // Calculate anchor point in absolute coordinates
            float anchorAbsX = Bounds.Left + (float)(Bounds.Width * AnchorX);
            float anchorAbsY = Bounds.Top + (float)(Bounds.Height * AnchorY);

            // Move origin to anchor point
            canvas.Translate(anchorAbsX, anchorAbsY);

            // Apply translation
            if (TranslationX != 0.0 || TranslationY != 0.0)
            {
                canvas.Translate((float)TranslationX, (float)TranslationY);
            }

            // Apply rotation
            if (Rotation != 0.0)
            {
                canvas.RotateDegrees((float)Rotation);
            }

            // Apply scale
            float scaleX = (float)(Scale * ScaleX);
            float scaleY = (float)(Scale * ScaleY);
            if (scaleX != 1f || scaleY != 1f)
            {
                canvas.Scale(scaleX, scaleY);
            }

            // Move origin back
            canvas.Translate(-anchorAbsX, -anchorAbsY);
        }

        // Apply opacity
        if (Opacity < 1.0f)
        {
            canvas.SaveLayer(new SKPaint { Color = SKColors.White.WithAlpha((byte)(Opacity * 255)) });
        }

        // Draw background at absolute bounds
        if (BackgroundColor != SKColors.Transparent)
        {
            using var paint = new SKPaint { Color = BackgroundColor };
            canvas.DrawRect(Bounds, paint);
        }

        // Draw content at absolute bounds
        OnDraw(canvas, Bounds);

        // Draw children - they draw at their own absolute bounds
        foreach (var child in _children)
        {
            child.Draw(canvas);
        }

        if (Opacity < 1.0f)
        {
            canvas.Restore();
        }

        canvas.Restore();
    }

    /// <summary>
    /// Override to draw custom content.
    /// </summary>
    protected virtual void OnDraw(SKCanvas canvas, SKRect bounds)
    {
    }

    /// <summary>
    /// Called when the bounds change.
    /// </summary>
    protected virtual void OnBoundsChanged()
    {
        Invalidate();
    }

    /// <summary>
    /// Measures the desired size of this view.
    /// </summary>
    public SKSize Measure(SKSize availableSize)
    {
        DesiredSize = MeasureOverride(availableSize);
        return DesiredSize;
    }

    /// <summary>
    /// Override to provide custom measurement.
    /// </summary>
    protected virtual SKSize MeasureOverride(SKSize availableSize)
    {
        var width = WidthRequest >= 0 ? (float)WidthRequest : 0;
        var height = HeightRequest >= 0 ? (float)HeightRequest : 0;
        return new SKSize(width, height);
    }

    /// <summary>
    /// Arranges this view within the given bounds.
    /// </summary>
    public void Arrange(SKRect bounds)
    {
        Bounds = ArrangeOverride(bounds);
    }

    /// <summary>
    /// Override to customize arrangement within the given bounds.
    /// </summary>
    protected virtual SKRect ArrangeOverride(SKRect bounds)
    {
        return bounds;
    }

    /// <summary>
    /// Performs hit testing to find the view at the given point.
    /// </summary>
    public virtual SkiaView? HitTest(SKPoint point)
    {
        return HitTest(point.X, point.Y);
    }

    /// <summary>
    /// Performs hit testing to find the view at the given coordinates.
    /// Coordinates are in absolute window space, matching how Bounds are stored.
    /// </summary>
    public virtual SkiaView? HitTest(float x, float y)
    {
        if (!IsVisible || !IsEnabled)
            return null;

        if (!Bounds.Contains(x, y))
            return null;

        // Check children in reverse order (top-most first)
        // Coordinates stay in absolute space since children have absolute Bounds
        for (int i = _children.Count - 1; i >= 0; i--)
        {
            var hit = _children[i].HitTest(x, y);
            if (hit != null)
                return hit;
        }

        return this;
    }

    #region Input Events

    public virtual void OnPointerEntered(PointerEventArgs e)
    {
        if (MauiView != null)
        {
            GestureManager.ProcessPointerEntered(MauiView, e.X, e.Y);
        }
    }

    public virtual void OnPointerExited(PointerEventArgs e)
    {
        if (MauiView != null)
        {
            GestureManager.ProcessPointerExited(MauiView, e.X, e.Y);
        }
    }

    public virtual void OnPointerMoved(PointerEventArgs e)
    {
        if (MauiView != null)
        {
            GestureManager.ProcessPointerMove(MauiView, e.X, e.Y);
        }
    }

    public virtual void OnPointerPressed(PointerEventArgs e)
    {
        if (MauiView != null)
        {
            GestureManager.ProcessPointerDown(MauiView, e.X, e.Y);
        }
    }

    public virtual void OnPointerReleased(PointerEventArgs e)
    {
        Console.WriteLine($"[SkiaView] OnPointerReleased on {GetType().Name}, MauiView={MauiView?.GetType().Name ?? "null"}");
        if (MauiView != null)
        {
            GestureManager.ProcessPointerUp(MauiView, e.X, e.Y);
        }
    }

    public virtual void OnScroll(ScrollEventArgs e) { }
    public virtual void OnKeyDown(KeyEventArgs e) { }
    public virtual void OnKeyUp(KeyEventArgs e) { }
    public virtual void OnTextInput(TextInputEventArgs e) { }

    public virtual void OnFocusGained()
    {
        IsFocused = true;
        Invalidate();
    }

    public virtual void OnFocusLost()
    {
        IsFocused = false;
        Invalidate();
    }

    #endregion

    #region IDisposable

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                foreach (var child in _children)
                {
                    child.Dispose();
                }
                _children.Clear();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}

/// <summary>
/// Event args for pointer events.
/// </summary>
public class PointerEventArgs : EventArgs
{
    public float X { get; }
    public float Y { get; }
    public PointerButton Button { get; }
    public bool Handled { get; set; }

    public PointerEventArgs(float x, float y, PointerButton button = PointerButton.None)
    {
        X = x;
        Y = y;
        Button = button;
    }
}

/// <summary>
/// Mouse button flags.
/// </summary>
[Flags]
public enum PointerButton
{
    None = 0,
    Left = 1,
    Middle = 2,
    Right = 4,
    XButton1 = 8,
    XButton2 = 16
}

/// <summary>
/// Event args for scroll events.
/// </summary>
public class ScrollEventArgs : EventArgs
{
    public float X { get; }
    public float Y { get; }
    public float DeltaX { get; }
    public float DeltaY { get; }
    public bool Handled { get; set; }

    public ScrollEventArgs(float x, float y, float deltaX, float deltaY)
    {
        X = x;
        Y = y;
        DeltaX = deltaX;
        DeltaY = deltaY;
    }
}

/// <summary>
/// Event args for keyboard events.
/// </summary>
public class KeyEventArgs : EventArgs
{
    public Key Key { get; }
    public KeyModifiers Modifiers { get; }
    public bool Handled { get; set; }

    public KeyEventArgs(Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        Key = key;
        Modifiers = modifiers;
    }
}

/// <summary>
/// Event args for text input events.
/// </summary>
public class TextInputEventArgs : EventArgs
{
    public string Text { get; }
    public bool Handled { get; set; }

    public TextInputEventArgs(string text)
    {
        Text = text;
    }
}

/// <summary>
/// Keyboard modifier flags.
/// </summary>
[Flags]
public enum KeyModifiers
{
    None = 0,
    Shift = 1,
    Control = 2,
    Alt = 4,
    Super = 8,
    CapsLock = 16,
    NumLock = 32
}
