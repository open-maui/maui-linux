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
public abstract class SkiaView : BindableObject, IDisposable, IAccessible
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

    #region High Contrast Support

    private static HighContrastService? _highContrastService;
    private static bool _highContrastInitialized;

    /// <summary>
    /// Gets whether high contrast mode is enabled.
    /// </summary>
    public static bool IsHighContrastEnabled => _highContrastService?.IsHighContrastEnabled ?? false;

    /// <summary>
    /// Gets the current high contrast colors, or default colors if not in high contrast mode.
    /// </summary>
    public static HighContrastColors GetHighContrastColors()
    {
        InitializeHighContrastService();
        return _highContrastService?.GetColors() ?? new HighContrastColors
        {
            Background = SkiaTheme.BackgroundWhiteSK,
            Foreground = SkiaTheme.TextPrimarySK,
            Accent = SkiaTheme.PrimarySK,
            Border = SkiaTheme.BorderMediumSK,
            Error = SkiaTheme.ErrorSK,
            Success = SkiaTheme.SuccessSK,
            Warning = SkiaTheme.WarningSK,
            Link = SkiaTheme.TextLinkSK,
            LinkVisited = SkiaTheme.TextLinkVisitedSK,
            Selection = SkiaTheme.PrimarySK,
            SelectionText = SkiaTheme.BackgroundWhiteSK,
            DisabledText = SkiaTheme.TextDisabledSK,
            DisabledBackground = SkiaTheme.BackgroundDisabledSK
        };
    }

    private static void InitializeHighContrastService()
    {
        if (_highContrastInitialized) return;
        _highContrastInitialized = true;

        try
        {
            _highContrastService = new HighContrastService();
            _highContrastService.HighContrastChanged += OnHighContrastChanged;
            _highContrastService.Initialize();
        }
        catch
        {
            // Ignore errors - high contrast is optional
        }
    }

    private static void OnHighContrastChanged(object? sender, HighContrastChangedEventArgs e)
    {
        // Request a full repaint of the UI
        SkiaRenderingEngine.Current?.InvalidateAll();
    }

    #endregion

    #region Accessibility Support (IAccessible)

    private static IAccessibilityService? _accessibilityService;
    private static bool _accessibilityInitialized;
    private string _accessibleId = Guid.NewGuid().ToString();
    private List<IAccessible>? _accessibleChildren;

    /// <summary>
    /// Gets or sets the accessibility name for screen readers.
    /// </summary>
    public string? SemanticName { get; set; }

    /// <summary>
    /// Gets or sets the accessibility description for screen readers.
    /// </summary>
    public string? SemanticDescription { get; set; }

    /// <summary>
    /// Gets or sets the accessibility hint for screen readers.
    /// </summary>
    public string? SemanticHint { get; set; }

    /// <summary>
    /// Gets the accessibility service instance.
    /// </summary>
    protected static IAccessibilityService? AccessibilityService
    {
        get
        {
            InitializeAccessibilityService();
            return _accessibilityService;
        }
    }

    private static void InitializeAccessibilityService()
    {
        if (_accessibilityInitialized) return;
        _accessibilityInitialized = true;

        try
        {
            _accessibilityService = AccessibilityServiceFactory.Instance;
            _accessibilityService?.Initialize();
        }
        catch
        {
            // Ignore errors - accessibility is optional
        }
    }

    /// <summary>
    /// Registers this view with the accessibility service.
    /// </summary>
    protected void RegisterAccessibility()
    {
        AccessibilityService?.Register(this);
    }

    /// <summary>
    /// Unregisters this view from the accessibility service.
    /// </summary>
    protected void UnregisterAccessibility()
    {
        AccessibilityService?.Unregister(this);
    }

    /// <summary>
    /// Announces text to screen readers.
    /// </summary>
    protected void AnnounceToScreenReader(string text, AnnouncementPriority priority = AnnouncementPriority.Polite)
    {
        AccessibilityService?.Announce(text, priority);
    }

    // IAccessible implementation
    string IAccessible.AccessibleId => _accessibleId;

    string IAccessible.AccessibleName => SemanticName ?? GetDefaultAccessibleName();

    string IAccessible.AccessibleDescription => SemanticDescription ?? SemanticHint ?? string.Empty;

    AccessibleRole IAccessible.Role => GetAccessibleRole();

    AccessibleStates IAccessible.States => GetAccessibleStates();

    IAccessible? IAccessible.Parent => Parent as IAccessible;

    IReadOnlyList<IAccessible> IAccessible.Children => _accessibleChildren ??= GetAccessibleChildren();

    AccessibleRect IAccessible.Bounds => new AccessibleRect(
        (int)ScreenBounds.Left,
        (int)ScreenBounds.Top,
        (int)ScreenBounds.Width,
        (int)ScreenBounds.Height);

    IReadOnlyList<AccessibleAction> IAccessible.Actions => GetAccessibleActions();

    double? IAccessible.Value => GetAccessibleValue();
    double? IAccessible.MinValue => GetAccessibleMinValue();
    double? IAccessible.MaxValue => GetAccessibleMaxValue();

    bool IAccessible.DoAction(string actionName) => DoAccessibleAction(actionName);
    bool IAccessible.SetValue(double value) => SetAccessibleValue(value);

    /// <summary>
    /// Gets the default accessible name based on view content.
    /// </summary>
    protected virtual string GetDefaultAccessibleName() => string.Empty;

    /// <summary>
    /// Gets the accessible role for this view.
    /// </summary>
    protected virtual AccessibleRole GetAccessibleRole() => AccessibleRole.Unknown;

    /// <summary>
    /// Gets the current accessible states.
    /// </summary>
    protected virtual AccessibleStates GetAccessibleStates()
    {
        var states = AccessibleStates.None;
        if (IsVisible) states |= AccessibleStates.Visible;
        if (IsEnabled) states |= AccessibleStates.Enabled;
        if (IsFocused) states |= AccessibleStates.Focused;
        if (IsFocusable) states |= AccessibleStates.Focusable;
        return states;
    }

    /// <summary>
    /// Gets the accessible children of this view.
    /// </summary>
    protected virtual List<IAccessible> GetAccessibleChildren()
    {
        var children = new List<IAccessible>();
        foreach (var child in Children)
        {
            if (child is IAccessible accessible)
            {
                children.Add(accessible);
            }
        }
        return children;
    }

    /// <summary>
    /// Gets the available accessible actions.
    /// </summary>
    protected virtual IReadOnlyList<AccessibleAction> GetAccessibleActions()
    {
        return Array.Empty<AccessibleAction>();
    }

    /// <summary>
    /// Performs an accessible action.
    /// </summary>
    protected virtual bool DoAccessibleAction(string actionName) => false;

    /// <summary>
    /// Gets the accessible value (for sliders, progress bars, etc.).
    /// </summary>
    protected virtual double? GetAccessibleValue() => null;

    /// <summary>
    /// Gets the minimum accessible value.
    /// </summary>
    protected virtual double? GetAccessibleMinValue() => null;

    /// <summary>
    /// Gets the maximum accessible value.
    /// </summary>
    protected virtual double? GetAccessibleMaxValue() => null;

    /// <summary>
    /// Sets the accessible value.
    /// </summary>
    protected virtual bool SetAccessibleValue(double value) => false;

    #endregion

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
    /// Draws this view and its children to the canvas.
    /// </summary>
    public virtual void Draw(SKCanvas canvas)
    {
        if (!IsVisible || Opacity <= 0)
        {
            return;
        }

        canvas.Save();

        // Get SKRect for internal rendering
        var skBounds = BoundsSK;

        // Apply transforms if any are set
        if (Scale != 1.0 || ScaleX != 1.0 || ScaleY != 1.0 ||
            Rotation != 0.0 || RotationX != 0.0 || RotationY != 0.0 ||
            TranslationX != 0.0 || TranslationY != 0.0)
        {
            // Calculate anchor point in absolute coordinates
            float anchorAbsX = skBounds.Left + (float)(Bounds.Width * AnchorX);
            float anchorAbsY = skBounds.Top + (float)(Bounds.Height * AnchorY);

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

        // Draw shadow if set
        if (Shadow != null)
        {
            DrawShadow(canvas, skBounds);
        }

        // Apply clip geometry if set
        if (Clip != null)
        {
            ApplyClip(canvas, skBounds);
        }

        // Draw background at absolute bounds
        DrawBackground(canvas, skBounds);

        // Draw content at absolute bounds
        OnDraw(canvas, skBounds);

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
    /// Draws the shadow for this view.
    /// </summary>
    protected virtual void DrawShadow(SKCanvas canvas, SKRect bounds)
    {
        if (Shadow == null) return;

        var shadowColor = Shadow.Brush is SolidColorBrush scb
            ? scb.Color.ToSKColor().WithAlpha((byte)(scb.Color.Alpha * 255 * Shadow.Opacity))
            : SKColors.Black.WithAlpha((byte)(255 * Shadow.Opacity));

        using var shadowPaint = new SKPaint
        {
            Color = shadowColor,
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, (float)Shadow.Radius / 2)
        };

        var shadowBounds = new SKRect(
            bounds.Left + (float)Shadow.Offset.X,
            bounds.Top + (float)Shadow.Offset.Y,
            bounds.Right + (float)Shadow.Offset.X,
            bounds.Bottom + (float)Shadow.Offset.Y);

        canvas.DrawRect(shadowBounds, shadowPaint);
    }

    /// <summary>
    /// Applies the clip geometry to the canvas.
    /// </summary>
    protected virtual void ApplyClip(SKCanvas canvas, SKRect bounds)
    {
        if (Clip == null) return;

        // Convert MAUI Geometry to SkiaSharp path
        var path = ConvertGeometryToPath(Clip, bounds);
        if (path != null)
        {
            canvas.ClipPath(path);
        }
    }

    /// <summary>
    /// Converts a MAUI Geometry to a SkiaSharp path.
    /// </summary>
    private SKPath? ConvertGeometryToPath(Geometry geometry, SKRect bounds)
    {
        var path = new SKPath();

        if (geometry is RectangleGeometry rect)
        {
            var r = rect.Rect;
            path.AddRect(new SKRect(
                bounds.Left + (float)r.Left,
                bounds.Top + (float)r.Top,
                bounds.Left + (float)r.Right,
                bounds.Top + (float)r.Bottom));
        }
        else if (geometry is EllipseGeometry ellipse)
        {
            path.AddOval(new SKRect(
                bounds.Left + (float)(ellipse.Center.X - ellipse.RadiusX),
                bounds.Top + (float)(ellipse.Center.Y - ellipse.RadiusY),
                bounds.Left + (float)(ellipse.Center.X + ellipse.RadiusX),
                bounds.Top + (float)(ellipse.Center.Y + ellipse.RadiusY)));
        }
        else if (geometry is RoundRectangleGeometry roundRect)
        {
            var r = roundRect.Rect;
            var cr = roundRect.CornerRadius;
            var skRect = new SKRect(
                bounds.Left + (float)r.Left,
                bounds.Top + (float)r.Top,
                bounds.Left + (float)r.Right,
                bounds.Top + (float)r.Bottom);
            var skRoundRect = new SKRoundRect();
            skRoundRect.SetRectRadii(skRect, new[]
            {
                new SKPoint((float)cr.TopLeft, (float)cr.TopLeft),
                new SKPoint((float)cr.TopRight, (float)cr.TopRight),
                new SKPoint((float)cr.BottomRight, (float)cr.BottomRight),
                new SKPoint((float)cr.BottomLeft, (float)cr.BottomLeft)
            });
            path.AddRoundRect(skRoundRect);
        }
        // Add more geometry types as needed

        return path;
    }

    /// <summary>
    /// Draws the background (color or brush) for this view.
    /// </summary>
    protected virtual void DrawBackground(SKCanvas canvas, SKRect bounds)
    {
        // First try to use Background brush
        if (Background != null)
        {
            using var paint = new SKPaint { IsAntialias = true };

            if (Background is SolidColorBrush scb)
            {
                paint.Color = scb.Color.ToSKColor();
                canvas.DrawRect(bounds, paint);
            }
            else if (Background is LinearGradientBrush lgb)
            {
                var start = new SKPoint(
                    bounds.Left + (float)(lgb.StartPoint.X * bounds.Width),
                    bounds.Top + (float)(lgb.StartPoint.Y * bounds.Height));
                var end = new SKPoint(
                    bounds.Left + (float)(lgb.EndPoint.X * bounds.Width),
                    bounds.Top + (float)(lgb.EndPoint.Y * bounds.Height));

                var colors = lgb.GradientStops.Select(s => s.Color.ToSKColor()).ToArray();
                var positions = lgb.GradientStops.Select(s => s.Offset).ToArray();

                paint.Shader = SKShader.CreateLinearGradient(start, end, colors, positions, SKShaderTileMode.Clamp);
                canvas.DrawRect(bounds, paint);
            }
            else if (Background is RadialGradientBrush rgb)
            {
                var center = new SKPoint(
                    bounds.Left + (float)(rgb.Center.X * bounds.Width),
                    bounds.Top + (float)(rgb.Center.Y * bounds.Height));
                var radius = (float)(rgb.Radius * Math.Max(bounds.Width, bounds.Height));

                var colors = rgb.GradientStops.Select(s => s.Color.ToSKColor()).ToArray();
                var positions = rgb.GradientStops.Select(s => s.Offset).ToArray();

                paint.Shader = SKShader.CreateRadialGradient(center, radius, colors, positions, SKShaderTileMode.Clamp);
                canvas.DrawRect(bounds, paint);
            }
        }
        // Fall back to BackgroundColor (skip if transparent)
        else if (_backgroundColorSK.Alpha > 0)
        {
            using var paint = new SKPaint { Color = _backgroundColorSK };
            canvas.DrawRect(bounds, paint);
        }
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
        DiagnosticLog.Debug("SkiaView", $"OnPointerReleased on {GetType().Name}, MauiView={MauiView?.GetType().Name ?? "null"}");
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
