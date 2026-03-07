// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Native;
using Microsoft.Maui.Platform.Linux.Rendering;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Window;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Base class for all Skia-rendered views on Linux.
/// Inherits from BindableObject to enable XAML styling, data binding, and Visual State Manager.
/// Implements IAccessible for screen reader support.
/// </summary>
public abstract partial class SkiaView : BindableObject, IDisposable, IAccessible
{
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
    /// Uses Microsoft.Maui.Graphics.Color for MAUI compliance.
    /// </summary>
    public static readonly BindableProperty BackgroundColorProperty =
        BindableProperty.Create(
            nameof(BackgroundColor),
            typeof(Color),
            typeof(SkiaView),
            null,
            propertyChanged: (b, o, n) => ((SkiaView)b).OnBackgroundColorChanged());

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
    /// Default is -1 (unset) to match MAUI View.MinimumWidthRequest.
    /// </summary>
    public static readonly BindableProperty MinimumWidthRequestProperty =
        BindableProperty.Create(
            nameof(MinimumWidthRequest),
            typeof(double),
            typeof(SkiaView),
            -1.0,
            propertyChanged: (b, o, n) => ((SkiaView)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for MinimumHeightRequest.
    /// Default is -1 (unset) to match MAUI View.MinimumHeightRequest.
    /// </summary>
    public static readonly BindableProperty MinimumHeightRequestProperty =
        BindableProperty.Create(
            nameof(MinimumHeightRequest),
            typeof(double),
            typeof(SkiaView),
            -1.0,
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

    /// <summary>
    /// Bindable property for InputTransparent.
    /// When true, the view does not receive input events and they pass through to views below.
    /// </summary>
    public static readonly BindableProperty InputTransparentProperty =
        BindableProperty.Create(
            nameof(InputTransparent),
            typeof(bool),
            typeof(SkiaView),
            false);

    /// <summary>
    /// Bindable property for FlowDirection.
    /// Controls the layout direction for RTL language support.
    /// </summary>
    public static readonly BindableProperty FlowDirectionProperty =
        BindableProperty.Create(
            nameof(FlowDirection),
            typeof(FlowDirection),
            typeof(SkiaView),
            FlowDirection.MatchParent,
            propertyChanged: (b, o, n) => ((SkiaView)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for ZIndex.
    /// Controls the rendering order within a layout.
    /// </summary>
    public static readonly BindableProperty ZIndexProperty =
        BindableProperty.Create(
            nameof(ZIndex),
            typeof(int),
            typeof(SkiaView),
            0,
            propertyChanged: (b, o, n) => ((SkiaView)b).Parent?.Invalidate());

    /// <summary>
    /// Bindable property for MaximumWidthRequest.
    /// </summary>
    public static readonly BindableProperty MaximumWidthRequestProperty =
        BindableProperty.Create(
            nameof(MaximumWidthRequest),
            typeof(double),
            typeof(SkiaView),
            double.PositiveInfinity,
            propertyChanged: (b, o, n) => ((SkiaView)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for MaximumHeightRequest.
    /// </summary>
    public static readonly BindableProperty MaximumHeightRequestProperty =
        BindableProperty.Create(
            nameof(MaximumHeightRequest),
            typeof(double),
            typeof(SkiaView),
            double.PositiveInfinity,
            propertyChanged: (b, o, n) => ((SkiaView)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for AutomationId.
    /// Used for UI testing and accessibility.
    /// </summary>
    public static readonly BindableProperty AutomationIdProperty =
        BindableProperty.Create(
            nameof(AutomationId),
            typeof(string),
            typeof(SkiaView),
            string.Empty);

    /// <summary>
    /// Bindable property for Padding.
    /// </summary>
    public static readonly BindableProperty PaddingProperty =
        BindableProperty.Create(
            nameof(Padding),
            typeof(Thickness),
            typeof(SkiaView),
            default(Thickness),
            propertyChanged: (b, o, n) => ((SkiaView)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for Background (Brush).
    /// </summary>
    public static readonly BindableProperty BackgroundProperty =
        BindableProperty.Create(
            nameof(Background),
            typeof(Brush),
            typeof(SkiaView),
            null,
            propertyChanged: (b, o, n) => ((SkiaView)b).Invalidate());

    /// <summary>
    /// Bindable property for Clip geometry.
    /// </summary>
    public static readonly BindableProperty ClipProperty =
        BindableProperty.Create(
            nameof(Clip),
            typeof(Geometry),
            typeof(SkiaView),
            null,
            propertyChanged: (b, o, n) => ((SkiaView)b).Invalidate());

    /// <summary>
    /// Bindable property for Shadow.
    /// </summary>
    public static readonly BindableProperty ShadowProperty =
        BindableProperty.Create(
            nameof(Shadow),
            typeof(Shadow),
            typeof(SkiaView),
            null,
            propertyChanged: (b, o, n) => ((SkiaView)b).Invalidate());

    /// <summary>
    /// Bindable property for Visual.
    /// </summary>
    public static readonly BindableProperty VisualProperty =
        BindableProperty.Create(
            nameof(Visual),
            typeof(IVisual),
            typeof(SkiaView),
            VisualMarker.Default);

    #endregion

    private bool _disposed;
    private Rect _bounds;
    private SkiaView? _parent;
    private readonly List<SkiaView> _children = new();

    /// <summary>
    /// Gets the absolute bounds of this view in screen coordinates.
    /// </summary>
    public Rect GetAbsoluteBounds()
    {
        var bounds = Bounds;
        var current = Parent;
        while (current != null)
        {
            // Adjust for scroll offset if parent is a ScrollView
            if (current is SkiaScrollView scrollView)
            {
                bounds = new Rect(
                    bounds.Left - scrollView.ScrollX,
                    bounds.Top - scrollView.ScrollY,
                    bounds.Width,
                    bounds.Height);
            }
            current = current.Parent;
        }
        return bounds;
    }

    /// <summary>
    /// Gets or sets the bounds of this view in parent coordinates.
    /// Uses MAUI Rect for public API compliance.
    /// </summary>
    public Rect Bounds
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
    /// Gets the bounds as SKRect for internal SkiaSharp rendering.
    /// </summary>
    internal SKRect BoundsSK => new SKRect(
        (float)_bounds.Left,
        (float)_bounds.Top,
        (float)_bounds.Right,
        (float)_bounds.Bottom);

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
    /// Uses Microsoft.Maui.Graphics.Color for MAUI compliance.
    /// </summary>
    private SKColor _backgroundColorSK = SKColors.Transparent;
    public Color? BackgroundColor
    {
        get => (Color?)GetValue(BackgroundColorProperty);
        set => SetValue(BackgroundColorProperty, value);
    }

    /// <summary>
    /// Called when BackgroundColor changes.
    /// </summary>
    private void OnBackgroundColorChanged()
    {
        var color = BackgroundColor;
        _backgroundColorSK = color != null ? color.ToSKColor() : SKColors.Transparent;
        Invalidate();
    }

    /// <summary>
    /// Gets the effective background color as SKColor for rendering.
    /// </summary>
    protected SKColor GetEffectiveBackgroundColor() => _backgroundColorSK;

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
    /// Gets or sets whether this view is transparent to input.
    /// When true, input events pass through to views below.
    /// </summary>
    public bool InputTransparent
    {
        get => (bool)GetValue(InputTransparentProperty);
        set => SetValue(InputTransparentProperty, value);
    }

    /// <summary>
    /// Gets or sets the flow direction for RTL support.
    /// </summary>
    public FlowDirection FlowDirection
    {
        get => (FlowDirection)GetValue(FlowDirectionProperty);
        set => SetValue(FlowDirectionProperty, value);
    }

    /// <summary>
    /// Gets or sets the Z-index for rendering order.
    /// Higher values render on top of lower values.
    /// </summary>
    public int ZIndex
    {
        get => (int)GetValue(ZIndexProperty);
        set => SetValue(ZIndexProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum width request.
    /// </summary>
    public double MaximumWidthRequest
    {
        get => (double)GetValue(MaximumWidthRequestProperty);
        set => SetValue(MaximumWidthRequestProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum height request.
    /// </summary>
    public double MaximumHeightRequest
    {
        get => (double)GetValue(MaximumHeightRequestProperty);
        set => SetValue(MaximumHeightRequestProperty, value);
    }

    /// <summary>
    /// Gets or sets the automation ID for UI testing.
    /// </summary>
    public string AutomationId
    {
        get => (string)GetValue(AutomationIdProperty);
        set => SetValue(AutomationIdProperty, value);
    }

    /// <summary>
    /// Gets or sets the padding inside the view.
    /// </summary>
    public Thickness Padding
    {
        get => (Thickness)GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    /// <summary>
    /// Gets or sets the background brush.
    /// </summary>
    public Brush? Background
    {
        get => (Brush?)GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the clip geometry.
    /// </summary>
    public Geometry? Clip
    {
        get => (Geometry?)GetValue(ClipProperty);
        set => SetValue(ClipProperty, value);
    }

    /// <summary>
    /// Gets or sets the shadow.
    /// </summary>
    public Shadow? Shadow
    {
        get => (Shadow?)GetValue(ShadowProperty);
        set => SetValue(ShadowProperty, value);
    }

    /// <summary>
    /// Gets or sets the visual style.
    /// </summary>
    public IVisual Visual
    {
        get => (IVisual)GetValue(VisualProperty);
        set => SetValue(VisualProperty, value);
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
    public Rect ScreenBounds
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
                    bounds = new Rect(
                        bounds.Left - scrollView.ScrollX,
                        bounds.Top - scrollView.ScrollY,
                        bounds.Width,
                        bounds.Height);
                }
                parent = parent.Parent;
            }

            return bounds;
        }
    }

    /// <summary>
    /// Gets the desired size calculated during measure.
    /// Uses MAUI Size for public API compliance.
    /// </summary>
    public Size DesiredSize { get; protected set; }

    /// <summary>
    /// Gets the desired size as SKSize for internal SkiaSharp rendering.
    /// </summary>
    internal SKSize DesiredSizeSK => new SKSize((float)DesiredSize.Width, (float)DesiredSize.Height);

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
    /// Thread-safe - will marshal to GTK thread if needed.
    /// </summary>
    public void Invalidate()
    {
        // Check if we're on the GTK thread - if not, marshal the entire call
        int currentThread = Environment.CurrentManagedThreadId;
        int gtkThread = LinuxApplication.GtkThreadId;
        if (gtkThread != 0 && currentThread != gtkThread)
        {
            GLibNative.IdleAdd(() =>
            {
                InvalidateInternal();
                return false;
            });
            return;
        }

        InvalidateInternal();
    }

    private void InvalidateInternal()
    {
        LinuxApplication.LogInvalidate(GetType().Name);
        Invalidated?.Invoke(this, EventArgs.Empty);

        // Notify rendering engine of dirty region
        if (Bounds.Width > 0 && Bounds.Height > 0)
        {
            SkiaRenderingEngine.Current?.InvalidateRegion(new SKRect(
                (float)Bounds.Left, (float)Bounds.Top,
                (float)Bounds.Right, (float)Bounds.Bottom));
        }

        if (_parent != null)
        {
            _parent.InvalidateInternal();
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
        DesiredSize = Size.Zero;
        _parent?.InvalidateMeasure();
        Invalidate();
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
    /// Uses MAUI Size for public API compliance.
    /// </summary>
    public Size Measure(Size availableSize)
    {
        DesiredSize = MeasureOverride(availableSize);
        return DesiredSize;
    }

    /// <summary>
    /// Override to provide custom measurement.
    /// Uses MAUI Size for public API compliance.
    /// </summary>
    protected virtual Size MeasureOverride(Size availableSize)
    {
        var width = WidthRequest >= 0 ? WidthRequest : 0;
        var height = HeightRequest >= 0 ? HeightRequest : 0;
        return new Size(width, height);
    }

    /// <summary>
    /// Arranges this view within the given bounds.
    /// Uses MAUI Rect for public API compliance.
    /// </summary>
    public virtual void Arrange(Rect bounds)
    {
        Bounds = ArrangeOverride(bounds);
    }

    /// <summary>
    /// Override to customize arrangement within the given bounds.
    /// Uses MAUI Rect for public API compliance.
    /// </summary>
    protected virtual Rect ArrangeOverride(Rect bounds)
    {
        return bounds;
    }

    /// <summary>
    /// Performs hit testing to find the view at the given point.
    /// Uses MAUI Point for public API compliance.
    /// </summary>
    public virtual SkiaView? HitTest(Point point)
    {
        return HitTest((float)point.X, (float)point.Y);
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

        // If InputTransparent, don't capture input - let it pass through
        if (InputTransparent)
            return null;

        return this;
    }
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
    public KeyModifiers Modifiers { get; }
    public bool Handled { get; set; }

    /// <summary>
    /// Gets whether the Control key is pressed during this scroll event.
    /// </summary>
    public bool IsControlPressed => (Modifiers & KeyModifiers.Control) != 0;

    public ScrollEventArgs(float x, float y, float deltaX, float deltaY, KeyModifiers modifiers = KeyModifiers.None)
    {
        X = x;
        Y = y;
        DeltaX = deltaX;
        DeltaY = deltaY;
        Modifiers = modifiers;
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
