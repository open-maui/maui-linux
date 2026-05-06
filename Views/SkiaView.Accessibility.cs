// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Rendering;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public abstract partial class SkiaView
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

    /// <summary>
    /// DPI scale factor for popup overlay rendering. Set by the rendering engine.
    /// </summary>
    internal static float PopupDpiScale { get; set; } = 1.0f;

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
            if (PopupDpiScale > 1.0f)
                canvas.Scale(PopupDpiScale);
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
}
