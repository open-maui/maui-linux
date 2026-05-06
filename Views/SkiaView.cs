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
    /// Bindable property for IsFocusable.
    /// </summary>
    public static readonly BindableProperty IsFocusableProperty =
        BindableProperty.Create(
            nameof(IsFocusable),
            typeof(bool),
            typeof(SkiaView),
            false);

    /// <summary>
    /// Bindable property for Name (used for template child lookup).
    /// </summary>
    public static readonly BindableProperty NameProperty =
        BindableProperty.Create(
            nameof(Name),
            typeof(string),
            typeof(SkiaView),
            string.Empty);

    #endregion

    private bool _disposed;
    private Rect _bounds;
    private SkiaView? _parent;
    private readonly List<SkiaView> _children = new();
    private IRenderContext? _renderContext;

    /// <summary>
    /// Per-tree rendering context, set when this view is attached to a render
    /// engine (or directly in tests). Replaces the prior global
    /// <c>SkiaRenderingEngine.Current</c> static; assignment propagates
    /// recursively to existing children, and <see cref="AddChild"/> /
    /// <see cref="InsertChild"/> propagate it to incoming children.
    /// </summary>
    public IRenderContext? RenderContext
    {
        get => _renderContext;
        set
        {
            if (ReferenceEquals(_renderContext, value)) return;
            _renderContext = value;
            for (int i = 0; i < _children.Count; i++)
                _children[i].RenderContext = value;
        }
    }

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

    // Backing fields for VisualElement-equivalent properties.
    // These hold defaults when no MauiView is attached. When a MauiView is
    // attached, the public properties below read live from MauiView so there
    // is no duplicate state to keep in sync.
    private bool _isVisible = true;
    private bool _isEnabled = true;
    private float _opacity = 1.0f;
    private Color? _backgroundColor;
    private SKColor _backgroundColorSK = SKColors.Transparent;
    private double _widthRequest = -1.0;
    private double _heightRequest = -1.0;
    private double _minimumWidthRequest = -1.0;
    private double _minimumHeightRequest = -1.0;
    private double _maximumWidthRequest = double.PositiveInfinity;
    private double _maximumHeightRequest = double.PositiveInfinity;
    private Thickness _margin;
    private LayoutOptions _horizontalOptions = LayoutOptions.Fill;
    private LayoutOptions _verticalOptions = LayoutOptions.Fill;
    private double _scale = 1.0;
    private double _scaleX = 1.0;
    private double _scaleY = 1.0;
    private double _rotation;
    private double _rotationX;
    private double _rotationY;
    private double _translationX;
    private double _translationY;
    private double _anchorX = 0.5;
    private double _anchorY = 0.5;
    private bool _inputTransparent;
    private FlowDirection _flowDirection = FlowDirection.LeftToRight;
    private int _zIndex;
    private string _automationId = string.Empty;
    private Thickness _padding;
    private Brush? _background;
    private Geometry? _clip;
    private Shadow? _shadow;
    private IVisual _visual = VisualMarker.Default;

    /// <summary>
    /// Gets or sets whether this view is visible.
    /// </summary>
    public bool IsVisible
    {
        get
        {
            if (_mauiView is VisualElement ve) return ve.IsVisible;
            return _isVisible;
        }
        set
        {
            if (_mauiView is VisualElement ve) { ve.IsVisible = value; return; }
            if (_isVisible == value) return;
            _isVisible = value;
            OnVisibilityChanged();
        }
    }

    /// <summary>
    /// Gets or sets whether this view is enabled for interaction.
    /// </summary>
    public bool IsEnabled
    {
        get
        {
            if (_mauiView is VisualElement ve) return ve.IsEnabled;
            return _isEnabled;
        }
        set
        {
            if (_mauiView is VisualElement ve) { ve.IsEnabled = value; return; }
            if (_isEnabled == value) return;
            _isEnabled = value;
            OnEnabledChanged();
        }
    }

    /// <summary>
    /// Gets or sets the opacity of this view (0.0 to 1.0).
    /// </summary>
    public float Opacity
    {
        get
        {
            if (_mauiView is VisualElement ve) return (float)ve.Opacity;
            return _opacity;
        }
        set
        {
            var coerced = Math.Clamp(value, 0f, 1f);
            if (_mauiView is VisualElement ve) { ve.Opacity = coerced; return; }
            if (_opacity == coerced) return;
            _opacity = coerced;
            Invalidate();
        }
    }

    /// <summary>
    /// Gets or sets the background color.
    /// Uses Microsoft.Maui.Graphics.Color for MAUI compliance.
    /// </summary>
    public Color? BackgroundColor
    {
        get
        {
            if (_mauiView is VisualElement ve) return ve.BackgroundColor;
            return _backgroundColor;
        }
        set
        {
            if (_mauiView is VisualElement ve) { ve.BackgroundColor = value; return; }
            if (EqualityComparer<Color?>.Default.Equals(_backgroundColor, value)) return;
            _backgroundColor = value;
            OnBackgroundColorChanged();
        }
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
    /// Forces a re-read of theme-affected state from <see cref="MauiView"/>. Called
    /// by the application's theme-change handler so views in pushed pages and
    /// other "not currently visible" branches of the tree refresh their cached
    /// SKColors when MAUI has already re-evaluated the underlying AppThemeBinding.
    /// Subclasses with additional theme-aware properties (e.g. SkiaBorder.Stroke)
    /// should override and call <c>base.RefreshThemeFromMauiView()</c>.
    /// </summary>
    public virtual void RefreshThemeFromMauiView()
    {
        if (_mauiView is VisualElement)
            OnBackgroundColorChanged();
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
        get
        {
            if (_mauiView is VisualElement ve) return ve.WidthRequest;
            return _widthRequest;
        }
        set
        {
            if (_mauiView is VisualElement ve) { ve.WidthRequest = value; return; }
            if (_widthRequest == value) return;
            _widthRequest = value;
            InvalidateMeasure();
        }
    }

    /// <summary>
    /// Gets or sets the requested height.
    /// </summary>
    public double HeightRequest
    {
        get
        {
            if (_mauiView is VisualElement ve) return ve.HeightRequest;
            return _heightRequest;
        }
        set
        {
            if (_mauiView is VisualElement ve) { ve.HeightRequest = value; return; }
            if (_heightRequest == value) return;
            _heightRequest = value;
            InvalidateMeasure();
        }
    }

    /// <summary>
    /// Gets or sets the minimum width request.
    /// </summary>
    public double MinimumWidthRequest
    {
        get
        {
            if (_mauiView is VisualElement ve) return ve.MinimumWidthRequest;
            return _minimumWidthRequest;
        }
        set
        {
            if (_mauiView is VisualElement ve) { ve.MinimumWidthRequest = value; return; }
            if (_minimumWidthRequest == value) return;
            _minimumWidthRequest = value;
            InvalidateMeasure();
        }
    }

    /// <summary>
    /// Gets or sets the minimum height request.
    /// </summary>
    public double MinimumHeightRequest
    {
        get
        {
            if (_mauiView is VisualElement ve) return ve.MinimumHeightRequest;
            return _minimumHeightRequest;
        }
        set
        {
            if (_mauiView is VisualElement ve) { ve.MinimumHeightRequest = value; return; }
            if (_minimumHeightRequest == value) return;
            _minimumHeightRequest = value;
            InvalidateMeasure();
        }
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
        get
        {
            if (_mauiView is View v) return v.Margin;
            return _margin;
        }
        set
        {
            if (_mauiView is View v) { v.Margin = value; return; }
            if (_margin == value) return;
            _margin = value;
            InvalidateMeasure();
        }
    }

    /// <summary>
    /// Gets or sets the horizontal layout options.
    /// </summary>
    public LayoutOptions HorizontalOptions
    {
        get
        {
            if (_mauiView is View v) return v.HorizontalOptions;
            return _horizontalOptions;
        }
        set
        {
            if (_mauiView is View v) { v.HorizontalOptions = value; return; }
            if (_horizontalOptions.Equals(value)) return;
            _horizontalOptions = value;
            InvalidateMeasure();
        }
    }

    /// <summary>
    /// Gets or sets the vertical layout options.
    /// </summary>
    public LayoutOptions VerticalOptions
    {
        get
        {
            if (_mauiView is View v) return v.VerticalOptions;
            return _verticalOptions;
        }
        set
        {
            if (_mauiView is View v) { v.VerticalOptions = value; return; }
            if (_verticalOptions.Equals(value)) return;
            _verticalOptions = value;
            InvalidateMeasure();
        }
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
        get
        {
            if (_mauiView is VisualElement ve) return ve.Scale;
            return _scale;
        }
        set
        {
            if (_mauiView is VisualElement ve) { ve.Scale = value; return; }
            if (_scale == value) return;
            _scale = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Gets or sets the X-axis scale factor.
    /// </summary>
    public double ScaleX
    {
        get
        {
            if (_mauiView is VisualElement ve) return ve.ScaleX;
            return _scaleX;
        }
        set
        {
            if (_mauiView is VisualElement ve) { ve.ScaleX = value; return; }
            if (_scaleX == value) return;
            _scaleX = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Gets or sets the Y-axis scale factor.
    /// </summary>
    public double ScaleY
    {
        get
        {
            if (_mauiView is VisualElement ve) return ve.ScaleY;
            return _scaleY;
        }
        set
        {
            if (_mauiView is VisualElement ve) { ve.ScaleY = value; return; }
            if (_scaleY == value) return;
            _scaleY = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Gets or sets the rotation in degrees around the Z-axis.
    /// </summary>
    public double Rotation
    {
        get
        {
            if (_mauiView is VisualElement ve) return ve.Rotation;
            return _rotation;
        }
        set
        {
            if (_mauiView is VisualElement ve) { ve.Rotation = value; return; }
            if (_rotation == value) return;
            _rotation = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Gets or sets the rotation in degrees around the X-axis.
    /// </summary>
    public double RotationX
    {
        get
        {
            if (_mauiView is VisualElement ve) return ve.RotationX;
            return _rotationX;
        }
        set
        {
            if (_mauiView is VisualElement ve) { ve.RotationX = value; return; }
            if (_rotationX == value) return;
            _rotationX = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Gets or sets the rotation in degrees around the Y-axis.
    /// </summary>
    public double RotationY
    {
        get
        {
            if (_mauiView is VisualElement ve) return ve.RotationY;
            return _rotationY;
        }
        set
        {
            if (_mauiView is VisualElement ve) { ve.RotationY = value; return; }
            if (_rotationY == value) return;
            _rotationY = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Gets or sets the X translation offset.
    /// </summary>
    public double TranslationX
    {
        get
        {
            if (_mauiView is VisualElement ve) return ve.TranslationX;
            return _translationX;
        }
        set
        {
            if (_mauiView is VisualElement ve) { ve.TranslationX = value; return; }
            if (_translationX == value) return;
            _translationX = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Gets or sets the Y translation offset.
    /// </summary>
    public double TranslationY
    {
        get
        {
            if (_mauiView is VisualElement ve) return ve.TranslationY;
            return _translationY;
        }
        set
        {
            if (_mauiView is VisualElement ve) { ve.TranslationY = value; return; }
            if (_translationY == value) return;
            _translationY = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Gets or sets the X anchor point for transforms (0.0 to 1.0).
    /// </summary>
    public double AnchorX
    {
        get
        {
            if (_mauiView is VisualElement ve) return ve.AnchorX;
            return _anchorX;
        }
        set
        {
            if (_mauiView is VisualElement ve) { ve.AnchorX = value; return; }
            if (_anchorX == value) return;
            _anchorX = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Gets or sets the Y anchor point for transforms (0.0 to 1.0).
    /// </summary>
    public double AnchorY
    {
        get
        {
            if (_mauiView is VisualElement ve) return ve.AnchorY;
            return _anchorY;
        }
        set
        {
            if (_mauiView is VisualElement ve) { ve.AnchorY = value; return; }
            if (_anchorY == value) return;
            _anchorY = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Gets or sets whether this view is transparent to input.
    /// When true, input events pass through to views below.
    /// </summary>
    public bool InputTransparent
    {
        get
        {
            if (_mauiView is VisualElement ve) return ve.InputTransparent;
            return _inputTransparent;
        }
        set
        {
            if (_mauiView is VisualElement ve) { ve.InputTransparent = value; return; }
            if (_inputTransparent == value) return;
            _inputTransparent = value;
        }
    }

    /// <summary>
    /// Gets or sets the flow direction for RTL support.
    /// </summary>
    public FlowDirection FlowDirection
    {
        get
        {
            if (_mauiView is VisualElement ve) return ve.FlowDirection;
            return _flowDirection;
        }
        set
        {
            if (_mauiView is VisualElement ve) { ve.FlowDirection = value; return; }
            if (_flowDirection == value) return;
            _flowDirection = value;
            InvalidateMeasure();
        }
    }

    /// <summary>
    /// Gets or sets the Z-index for rendering order.
    /// Higher values render on top of lower values.
    /// </summary>
    public int ZIndex
    {
        get
        {
            if (_mauiView is View v) return v.ZIndex;
            return _zIndex;
        }
        set
        {
            if (_mauiView is View v) { v.ZIndex = value; return; }
            if (_zIndex == value) return;
            _zIndex = value;
            Parent?.Invalidate();
        }
    }

    /// <summary>
    /// Gets or sets the maximum width request.
    /// </summary>
    public double MaximumWidthRequest
    {
        get
        {
            if (_mauiView is VisualElement ve) return ve.MaximumWidthRequest;
            return _maximumWidthRequest;
        }
        set
        {
            if (_mauiView is VisualElement ve) { ve.MaximumWidthRequest = value; return; }
            if (_maximumWidthRequest == value) return;
            _maximumWidthRequest = value;
            InvalidateMeasure();
        }
    }

    /// <summary>
    /// Gets or sets the maximum height request.
    /// </summary>
    public double MaximumHeightRequest
    {
        get
        {
            if (_mauiView is VisualElement ve) return ve.MaximumHeightRequest;
            return _maximumHeightRequest;
        }
        set
        {
            if (_mauiView is VisualElement ve) { ve.MaximumHeightRequest = value; return; }
            if (_maximumHeightRequest == value) return;
            _maximumHeightRequest = value;
            InvalidateMeasure();
        }
    }

    /// <summary>
    /// Gets or sets the automation ID for UI testing.
    /// </summary>
    public string AutomationId
    {
        get
        {
            if (_mauiView is Element el) return el.AutomationId ?? string.Empty;
            return _automationId;
        }
        set
        {
            if (_mauiView is Element el) { el.AutomationId = value; return; }
            if (_automationId == value) return;
            _automationId = value;
        }
    }

    /// <summary>
    /// Gets or sets the padding inside the view.
    /// </summary>
    public Thickness Padding
    {
        get
        {
            if (_mauiView is Microsoft.Maui.Controls.Layout layout) return layout.Padding;
            return _padding;
        }
        set
        {
            if (_mauiView is Microsoft.Maui.Controls.Layout layout) { layout.Padding = value; return; }
            if (_padding == value) return;
            _padding = value;
            InvalidateMeasure();
        }
    }

    /// <summary>
    /// Gets or sets the background brush.
    /// </summary>
    public Brush? Background
    {
        get
        {
            if (_mauiView is VisualElement ve) return ve.Background as Brush;
            return _background;
        }
        set
        {
            if (_mauiView is VisualElement ve) { ve.Background = value; return; }
            if (ReferenceEquals(_background, value)) return;
            _background = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Gets or sets the clip geometry.
    /// </summary>
    public Geometry? Clip
    {
        get
        {
            if (_mauiView is VisualElement ve) return ve.Clip;
            return _clip;
        }
        set
        {
            if (_mauiView is VisualElement ve) { ve.Clip = value; return; }
            if (ReferenceEquals(_clip, value)) return;
            _clip = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Gets or sets the shadow.
    /// </summary>
    public Shadow? Shadow
    {
        get
        {
            if (_mauiView is VisualElement ve) return ve.Shadow;
            return _shadow;
        }
        set
        {
            if (_mauiView is VisualElement ve) { ve.Shadow = value; return; }
            if (ReferenceEquals(_shadow, value)) return;
            _shadow = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Gets or sets the visual style.
    /// </summary>
    public IVisual Visual
    {
        get
        {
            if (_mauiView is VisualElement ve) return ve.Visual;
            return _visual;
        }
        set
        {
            if (_mauiView is VisualElement ve) { ve.Visual = value; return; }
            if (ReferenceEquals(_visual, value)) return;
            _visual = value;
        }
    }

    /// <summary>
    /// Gets or sets the cursor type when hovering over this view.
    /// </summary>
    public CursorType CursorType { get; set; }

    /// <summary>
    /// Gets or sets the MAUI View this platform view represents.
    /// Used for gesture processing. When set, all VisualElement-equivalent
    /// properties on this SkiaView read live from the MauiView, eliminating
    /// duplicate state.
    /// </summary>
    private View? _mauiView;
    public View? MauiView
    {
        get => _mauiView;
        set
        {
            if (ReferenceEquals(_mauiView, value)) return;
            if (_mauiView is BindableObject oldBo)
                oldBo.PropertyChanged -= OnMauiViewPropertyChanged;
            _mauiView = value;
            if (_mauiView is BindableObject newBo)
                newBo.PropertyChanged += OnMauiViewPropertyChanged;

            // Repaint and remeasure since the view's effective property values
            // have switched from the backing fields to the new MauiView's values
            // (or vice versa when clearing).
            // Refresh the cached SKColor for BackgroundColor as well.
            OnBackgroundColorChanged();
            InvalidateMeasure();
            Invalidate();
        }
    }

    private void OnMauiViewPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(VisualElement.IsVisible):
                OnVisibilityChanged();
                break;
            case nameof(VisualElement.IsEnabled):
                OnEnabledChanged();
                break;
            case nameof(VisualElement.BackgroundColor):
                OnBackgroundColorChanged();
                break;
            case nameof(VisualElement.Opacity):
            case nameof(VisualElement.Scale):
            case nameof(VisualElement.ScaleX):
            case nameof(VisualElement.ScaleY):
            case nameof(VisualElement.Rotation):
            case nameof(VisualElement.RotationX):
            case nameof(VisualElement.RotationY):
            case nameof(VisualElement.TranslationX):
            case nameof(VisualElement.TranslationY):
            case nameof(VisualElement.AnchorX):
            case nameof(VisualElement.AnchorY):
            case nameof(VisualElement.Background):
            case nameof(VisualElement.Clip):
            case nameof(VisualElement.Shadow):
                Invalidate();
                break;
            case nameof(VisualElement.WidthRequest):
            case nameof(VisualElement.HeightRequest):
            case nameof(VisualElement.MinimumWidthRequest):
            case nameof(VisualElement.MinimumHeightRequest):
            case nameof(VisualElement.MaximumWidthRequest):
            case nameof(VisualElement.MaximumHeightRequest):
            case nameof(View.Margin):
            case nameof(View.HorizontalOptions):
            case nameof(View.VerticalOptions):
            case nameof(VisualElement.FlowDirection):
                InvalidateMeasure();
                break;
        }
    }

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

        // Propagate render context so the child (and its subtree) can resolve
        // typefaces and request invalidation against the right engine.
        if (_renderContext != null)
            child.RenderContext = _renderContext;

        InvalidateMeasure();
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
        InvalidateMeasure();
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

        // Propagate render context (see AddChild for the rationale).
        if (_renderContext != null)
            child.RenderContext = _renderContext;

        InvalidateMeasure();
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
        InvalidateMeasure();
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
            RenderContext?.InvalidateRegion(new SKRect(
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
    private bool _arrangingMauiView;
    private bool _loadedFired;

    public virtual void Arrange(Rect bounds)
    {
        Bounds = ArrangeOverride(bounds);

        // Notify the MAUI virtual view of its final size so that
        // VisualElement.Width/Height update and SizeChanged fires.
        // Controls like LiveCharts depend on SizeChanged to initialize
        // their rendering engine.
        if (!_arrangingMauiView && MauiView != null && Bounds.Width > 0 && Bounds.Height > 0)
        {
            var w = Bounds.Width;
            var h = Bounds.Height;
            if (Math.Abs(MauiView.Width - w) > 0.5 || Math.Abs(MauiView.Height - h) > 0.5)
            {
                _arrangingMauiView = true;
                try
                {
                    MauiView.Frame = new Rect(Bounds.X, Bounds.Y, w, h);
                }
                catch (Exception ex)
                {
                    DiagnosticLog.Error("SkiaView", $"Frame set failed for {MauiView.GetType().Name}: {ex.Message}");
                }
                finally
                {
                    _arrangingMauiView = false;
                }
            }

            // Fire the Loaded event after the first successful arrange.
            // Controls like LiveCharts start their drawing loop in Loaded.
            if (!_loadedFired)
            {
                _loadedFired = true;
                if (!MauiView.IsLoaded)
                    FireLoadedEvent(MauiView);
            }
        }
    }

    private static System.Reflection.FieldInfo? _loadedField;

    private static void FireLoadedEvent(Microsoft.Maui.Controls.VisualElement element)
    {
        try
        {
            _loadedField ??= typeof(Microsoft.Maui.Controls.VisualElement).GetField(
                "_loaded",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var fieldVal = _loadedField?.GetValue(element);

            if (fieldVal is EventHandler handler)
            {
                handler.Invoke(element, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaView", $"FireLoaded failed for {element.GetType().Name}: {ex.Message}");
        }
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
