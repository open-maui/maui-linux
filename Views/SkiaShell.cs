// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using Svg.Skia;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Shell provides a common navigation experience for MAUI applications.
/// Supports flyout menu, tabs, and URI-based navigation.
/// </summary>
public class SkiaShell : SkiaLayoutView
{
    #region BindableProperties

    /// <summary>
    /// Bindable property for FlyoutIsPresented.
    /// </summary>
    public static readonly BindableProperty FlyoutIsPresentedProperty =
        BindableProperty.Create(
            nameof(FlyoutIsPresented),
            typeof(bool),
            typeof(SkiaShell),
            false,
            BindingMode.OneWay,
            propertyChanged: (b, o, n) => ((SkiaShell)b).OnFlyoutIsPresentedChanged((bool)n));

    /// <summary>
    /// Bindable property for FlyoutBehavior.
    /// </summary>
    public static readonly BindableProperty FlyoutBehaviorProperty =
        BindableProperty.Create(
            nameof(FlyoutBehavior),
            typeof(ShellFlyoutBehavior),
            typeof(SkiaShell),
            ShellFlyoutBehavior.Flyout,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaShell)b).OnFlyoutBehaviorChanged((ShellFlyoutBehavior)n));

    /// <summary>
    /// Bindable property for FlyoutWidth.
    /// </summary>
    public static readonly BindableProperty FlyoutWidthProperty =
        BindableProperty.Create(
            nameof(FlyoutWidth),
            typeof(float),
            typeof(SkiaShell),
            280f,
            BindingMode.TwoWay,
            coerceValue: (b, v) => Math.Max(100f, (float)v),
            propertyChanged: (b, o, n) => ((SkiaShell)b).Invalidate());

    /// <summary>
    /// Bindable property for FlyoutBackgroundColor.
    /// </summary>
    public static readonly BindableProperty FlyoutBackgroundColorProperty =
        BindableProperty.Create(
            nameof(FlyoutBackgroundColor),
            typeof(Color),
            typeof(SkiaShell),
            Colors.White,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaShell)b).OnFlyoutBackgroundColorChanged());

    /// <summary>
    /// Bindable property for FlyoutTextColor.
    /// </summary>
    public static readonly BindableProperty FlyoutTextColorProperty =
        BindableProperty.Create(
            nameof(FlyoutTextColor),
            typeof(Color),
            typeof(SkiaShell),
            Color.FromRgb(33, 33, 33),
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaShell)b).OnFlyoutTextColorChanged());

    /// <summary>
    /// Bindable property for NavBarBackgroundColor.
    /// </summary>
    public static readonly BindableProperty NavBarBackgroundColorProperty =
        BindableProperty.Create(
            nameof(NavBarBackgroundColor),
            typeof(Color),
            typeof(SkiaShell),
            Color.FromRgb(33, 150, 243),
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaShell)b).OnNavBarBackgroundColorChanged());

    /// <summary>
    /// Bindable property for NavBarTextColor.
    /// </summary>
    public static readonly BindableProperty NavBarTextColorProperty =
        BindableProperty.Create(
            nameof(NavBarTextColor),
            typeof(Color),
            typeof(SkiaShell),
            Colors.White,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaShell)b).OnNavBarTextColorChanged());

    /// <summary>
    /// Bindable property for NavBarHeight.
    /// </summary>
    public static readonly BindableProperty NavBarHeightProperty =
        BindableProperty.Create(
            nameof(NavBarHeight),
            typeof(float),
            typeof(SkiaShell),
            56f,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaShell)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for TabBarHeight.
    /// </summary>
    public static readonly BindableProperty TabBarHeightProperty =
        BindableProperty.Create(
            nameof(TabBarHeight),
            typeof(float),
            typeof(SkiaShell),
            56f,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaShell)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for NavBarIsVisible.
    /// </summary>
    public static readonly BindableProperty NavBarIsVisibleProperty =
        BindableProperty.Create(
            nameof(NavBarIsVisible),
            typeof(bool),
            typeof(SkiaShell),
            true,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaShell)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for TabBarIsVisible.
    /// </summary>
    public static readonly BindableProperty TabBarIsVisibleProperty =
        BindableProperty.Create(
            nameof(TabBarIsVisible),
            typeof(bool),
            typeof(SkiaShell),
            false,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaShell)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for ContentPadding.
    /// </summary>
    public static readonly BindableProperty ContentPaddingProperty =
        BindableProperty.Create(
            nameof(ContentPadding),
            typeof(float),
            typeof(SkiaShell),
            0f,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaShell)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for ContentBackgroundColor.
    /// </summary>
    public static readonly BindableProperty ContentBackgroundColorProperty =
        BindableProperty.Create(
            nameof(ContentBackgroundColor),
            typeof(Color),
            typeof(SkiaShell),
            Color.FromRgb(250, 250, 250),
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaShell)b).OnContentBackgroundColorChanged());

    /// <summary>
    /// Bindable property for Title.
    /// </summary>
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(
            nameof(Title),
            typeof(string),
            typeof(SkiaShell),
            string.Empty,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaShell)b).Invalidate());

    #endregion

    private readonly List<ShellSection> _sections = new();
    private SkiaView? _currentContent;
    private float _flyoutAnimationProgress = 0f;
    private int _selectedSectionIndex = 0;
    private int _selectedItemIndex = 0;

    // Navigation stack for push/pop navigation
    private readonly Stack<(SkiaView Content, string Title)> _navigationStack = new();

    private float _flyoutScrollOffset;
    private readonly Dictionary<string, Func<SkiaView?>> _registeredRoutes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _routeTitles = new(StringComparer.OrdinalIgnoreCase);

    // Icon cache for flyout items (keyed by icon path)
    private readonly Dictionary<string, SKBitmap?> _iconCache = new();

    // Internal SKColor fields for rendering
    private SKColor _flyoutBackgroundColorSK = SkiaTheme.BackgroundWhiteSK;
    private SKColor _flyoutTextColorSK = SkiaTheme.TextPrimarySK;
    private SKColor _navBarBackgroundColorSK = SkiaTheme.PrimarySK;
    private SKColor _navBarTextColorSK = SkiaTheme.BackgroundWhiteSK;
    private SKColor _contentBackgroundColorSK = SkiaTheme.Gray50SK;

    private void OnFlyoutBackgroundColorChanged()
    {
        _flyoutBackgroundColorSK = FlyoutBackgroundColor?.ToSKColor() ?? SkiaTheme.BackgroundWhiteSK;
        Invalidate();
    }

    private void OnFlyoutTextColorChanged()
    {
        _flyoutTextColorSK = FlyoutTextColor?.ToSKColor() ?? SkiaTheme.TextPrimarySK;
        Invalidate();
    }

    private void OnNavBarBackgroundColorChanged()
    {
        _navBarBackgroundColorSK = NavBarBackgroundColor?.ToSKColor() ?? SkiaTheme.PrimarySK;
        Invalidate();
    }

    private void OnNavBarTextColorChanged()
    {
        _navBarTextColorSK = NavBarTextColor?.ToSKColor() ?? SkiaTheme.BackgroundWhiteSK;
        Invalidate();
    }

    private void OnContentBackgroundColorChanged()
    {
        _contentBackgroundColorSK = ContentBackgroundColor?.ToSKColor() ?? SkiaTheme.Gray50SK;
        Invalidate();
    }

    private void OnFlyoutBehaviorChanged(ShellFlyoutBehavior newBehavior)
    {
        if (newBehavior == ShellFlyoutBehavior.Locked)
        {
            _flyoutAnimationProgress = 1f;
        }
        else if (newBehavior == ShellFlyoutBehavior.Disabled)
        {
            _flyoutAnimationProgress = 0f;
        }
        Invalidate();
    }

    private void OnFlyoutIsPresentedChanged(bool newValue)
    {
        // In Locked mode, flyout is always visible regardless of FlyoutIsPresented
        if (FlyoutBehavior == ShellFlyoutBehavior.Locked)
        {
            _flyoutAnimationProgress = 1f;
        }
        else
        {
            _flyoutAnimationProgress = newValue ? 1f : 0f;
        }
        FlyoutIsPresentedChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    /// <summary>
    /// Gets or sets whether the flyout is presented.
    /// </summary>
    public bool FlyoutIsPresented
    {
        get => (bool)GetValue(FlyoutIsPresentedProperty);
        set => SetValue(FlyoutIsPresentedProperty, value);
    }

    /// <summary>
    /// Gets or sets the flyout behavior.
    /// </summary>
    public ShellFlyoutBehavior FlyoutBehavior
    {
        get => (ShellFlyoutBehavior)GetValue(FlyoutBehaviorProperty);
        set => SetValue(FlyoutBehaviorProperty, value);
    }

    /// <summary>
    /// Gets or sets the flyout width.
    /// </summary>
    public float FlyoutWidth
    {
        get => (float)GetValue(FlyoutWidthProperty);
        set => SetValue(FlyoutWidthProperty, value);
    }

    /// <summary>
    /// Background color of the flyout.
    /// </summary>
    public Color? FlyoutBackgroundColor
    {
        get => (Color?)GetValue(FlyoutBackgroundColorProperty);
        set => SetValue(FlyoutBackgroundColorProperty, value);
    }

    /// <summary>
    /// Text color in the flyout.
    /// </summary>
    public Color? FlyoutTextColor
    {
        get => (Color?)GetValue(FlyoutTextColorProperty);
        set => SetValue(FlyoutTextColorProperty, value);
    }

    /// <summary>
    /// Optional header view in the flyout.
    /// </summary>
    public SkiaView? FlyoutHeaderView { get; set; }

    /// <summary>
    /// Height of the flyout header.
    /// </summary>
    public float FlyoutHeaderHeight { get; set; } = 140f;

    /// <summary>
    /// Optional footer text in the flyout (fallback if no FlyoutFooterView).
    /// </summary>
    public string? FlyoutFooterText { get; set; }

    /// <summary>
    /// Optional footer view in the flyout.
    /// </summary>
    public SkiaView? FlyoutFooterView { get; set; }

    /// <summary>
    /// Height of the flyout footer.
    /// </summary>
    public float FlyoutFooterHeight { get; set; } = 40f;

    /// <summary>
    /// Background color of the navigation bar.
    /// </summary>
    public Color? NavBarBackgroundColor
    {
        get => (Color?)GetValue(NavBarBackgroundColorProperty);
        set => SetValue(NavBarBackgroundColorProperty, value);
    }

    /// <summary>
    /// Text color of the navigation bar title.
    /// </summary>
    public Color? NavBarTextColor
    {
        get => (Color?)GetValue(NavBarTextColorProperty);
        set => SetValue(NavBarTextColorProperty, value);
    }

    /// <summary>
    /// Height of the navigation bar.
    /// </summary>
    public float NavBarHeight
    {
        get => (float)GetValue(NavBarHeightProperty);
        set => SetValue(NavBarHeightProperty, value);
    }

    /// <summary>
    /// Height of the tab bar (when using bottom tabs).
    /// </summary>
    public float TabBarHeight
    {
        get => (float)GetValue(TabBarHeightProperty);
        set => SetValue(TabBarHeightProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the navigation bar is visible.
    /// </summary>
    public bool NavBarIsVisible
    {
        get => (bool)GetValue(NavBarIsVisibleProperty);
        set => SetValue(NavBarIsVisibleProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the tab bar is visible.
    /// </summary>
    public bool TabBarIsVisible
    {
        get => (bool)GetValue(TabBarIsVisibleProperty);
        set => SetValue(TabBarIsVisibleProperty, value);
    }

    /// <summary>
    /// Gets or sets the padding applied to page content.
    /// </summary>
    public float ContentPadding
    {
        get => (float)GetValue(ContentPaddingProperty);
        set => SetValue(ContentPaddingProperty, value);
    }

    /// <summary>
    /// Background color of the content area.
    /// </summary>
    public Color? ContentBackgroundColor
    {
        get => (Color?)GetValue(ContentBackgroundColorProperty);
        set => SetValue(ContentBackgroundColorProperty, value);
    }

    /// <summary>
    /// Current title displayed in the navigation bar.
    /// </summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>
    /// The sections in this shell.
    /// </summary>
    public IReadOnlyList<ShellSection> Sections => _sections;

    /// <summary>
    /// Gets the currently selected section index.
    /// </summary>
    public int CurrentSectionIndex => _selectedSectionIndex;

    /// <summary>
    /// Reference to the MAUI Shell this view represents.
    /// </summary>
    public Shell? MauiShell { get; set; }

    /// <summary>
    /// Callback to render content from a ShellContent.
    /// </summary>
    public Func<Microsoft.Maui.Controls.ShellContent, SkiaView?>? ContentRenderer { get; set; }

    /// <summary>
    /// Callback to refresh shell colors.
    /// </summary>
    public Action<SkiaShell, Shell>? ColorRefresher { get; set; }

    /// <summary>
    /// Event raised when FlyoutIsPresented changes.
    /// </summary>
    public event EventHandler? FlyoutIsPresentedChanged;

    /// <summary>
    /// Event raised when navigation occurs.
    /// </summary>
    public event EventHandler<ShellNavigationEventArgs>? Navigated;

    /// <summary>
    /// Adds a section to the shell.
    /// </summary>
    public void AddSection(ShellSection section)
    {
        _sections.Add(section);

        if (_sections.Count == 1)
        {
            NavigateToSection(0, 0);
        }

        Invalidate();
    }

    /// <summary>
    /// Removes a section from the shell.
    /// </summary>
    public void RemoveSection(ShellSection section)
    {
        _sections.Remove(section);
        Invalidate();
    }

    /// <summary>
    /// Navigates to a specific section and item.
    /// </summary>
    public void NavigateToSection(int sectionIndex, int itemIndex = 0)
    {
        if (sectionIndex < 0 || sectionIndex >= _sections.Count) return;

        var section = _sections[sectionIndex];
        if (itemIndex < 0 || itemIndex >= section.Items.Count) return;

        // Clear navigation stack when navigating to a new section
        _navigationStack.Clear();

        _selectedSectionIndex = sectionIndex;
        _selectedItemIndex = itemIndex;

        var item = section.Items[itemIndex];
        SetCurrentContent(item.Content);
        Title = item.Title;

        Navigated?.Invoke(this, new ShellNavigationEventArgs(section, item));
        Invalidate();
    }

    /// <summary>
    /// Refreshes the shell theme and re-renders all pages.
    /// </summary>
    public void RefreshTheme()
    {
        DiagnosticLog.Debug("SkiaShell", "RefreshTheme called - refreshing all pages");
        if (MauiShell != null && ColorRefresher != null)
        {
            DiagnosticLog.Debug("SkiaShell", "Refreshing shell colors");
            ColorRefresher(this, MauiShell);
        }

        // If no explicit colors were set, use theme-aware defaults
        if (FlyoutBackgroundColor == null)
        {
            _flyoutBackgroundColorSK = SkiaTheme.CurrentSurfaceSK;
        }
        if (FlyoutTextColor == null)
        {
            _flyoutTextColorSK = SkiaTheme.CurrentTextSK;
        }
        if (ContentRenderer != null)
        {
            foreach (var section in _sections)
            {
                foreach (var item in section.Items)
                {
                    if (item.MauiShellContent != null)
                    {
                        DiagnosticLog.Debug("SkiaShell", "Re-rendering: " + item.Title);
                        var skiaView = ContentRenderer(item.MauiShellContent);
                        if (skiaView != null)
                        {
                            item.Content = skiaView;
                        }
                    }
                }
            }
        }
        // Only update current content if there are no pushed pages on the navigation stack
        // Pushed pages are handled separately by LinuxApplication.RefreshViewTheme
        if (_navigationStack.Count == 0 && _selectedSectionIndex >= 0 && _selectedSectionIndex < _sections.Count)
        {
            var section = _sections[_selectedSectionIndex];
            if (_selectedItemIndex >= 0 && _selectedItemIndex < section.Items.Count)
            {
                var item = section.Items[_selectedItemIndex];
                SetCurrentContent(item.Content);
            }
        }
        // Clear icon cache so icons reload with new theme paths
        ClearIconCache();

        // Re-sync flyout item icon paths from MAUI Shell
        IconSyncer?.Invoke(this);

        InvalidateMeasure();
        Invalidate();
    }

    /// <summary>
    /// Delegate to re-sync flyout item icons from the MAUI Shell (called on theme change).
    /// </summary>
    public Action<SkiaShell>? IconSyncer { get; set; }

    /// <summary>
    /// Clears the cached flyout icons so they reload on next draw.
    /// </summary>
    public void ClearIconCache()
    {
        foreach (var bitmap in _iconCache.Values)
        {
            bitmap?.Dispose();
        }
        _iconCache.Clear();
    }

    /// <summary>
    /// Loads a flyout icon from file (SVG or PNG), with caching.
    /// </summary>
    private SKBitmap? GetFlyoutIcon(string? iconPath)
    {
        if (string.IsNullOrEmpty(iconPath)) return null;

        if (_iconCache.TryGetValue(iconPath, out var cached))
            return cached;

        SKBitmap? bitmap = null;
        try
        {
            string baseDir = AppContext.BaseDirectory;
            string fullPath = System.IO.Path.IsPathRooted(iconPath)
                ? iconPath
                : System.IO.Path.Combine(baseDir, iconPath);

            if (System.IO.File.Exists(fullPath))
            {
                if (fullPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                {
                    using var svg = new SKSvg();
                    svg.Load(fullPath);
                    if (svg.Picture != null)
                    {
                        var cullRect = svg.Picture.CullRect;
                        float iconSize = 24f;
                        float scale = iconSize / Math.Max(cullRect.Width, cullRect.Height);
                        bitmap = new SKBitmap((int)iconSize, (int)iconSize, false);
                        using var canvas = new SKCanvas(bitmap);
                        canvas.Clear(SKColors.Transparent);
                        canvas.Scale(scale);
                        canvas.DrawPicture(svg.Picture, null);
                    }
                }
                else
                {
                    using var stream = System.IO.File.OpenRead(fullPath);
                    bitmap = SKBitmap.Decode(stream);
                }
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("SkiaShell", $"Failed to load flyout icon: {iconPath}", ex);
        }

        _iconCache[iconPath] = bitmap;
        return bitmap;
    }

    /// <summary>
    /// Navigates using a URI route.
    /// </summary>
    public void GoToAsync(string route)
    {
        GoToAsync(route, null);
    }

    /// <summary>
    /// Navigates using a URI route with parameters.
    /// </summary>
    public void GoToAsync(string route, IDictionary<string, object>? parameters)
    {
        if (string.IsNullOrEmpty(route)) return;

        string routePath = route;
        Dictionary<string, string> queryParams = new Dictionary<string, string>();
        int queryIndex = route.IndexOf('?');
        if (queryIndex >= 0)
        {
            routePath = route.Substring(0, queryIndex);
            queryParams = ParseQueryString(route.Substring(queryIndex + 1));
        }

        Dictionary<string, object> allParams = new Dictionary<string, object>();
        foreach (var kvp in queryParams)
        {
            allParams[kvp.Key] = kvp.Value;
        }
        if (parameters != null)
        {
            foreach (var kvp in parameters)
            {
                allParams[kvp.Key] = kvp.Value;
            }
        }

        var parts = routePath.TrimStart('/').Split('/');
        if (parts.Length == 0) return;

        // Check registered routes first
        if (_registeredRoutes.TryGetValue(routePath.TrimStart('/'), out Func<SkiaView?>? factory))
        {
            var view = factory();
            if (view != null)
            {
                ApplyQueryParameters(view, allParams);
                PushAsync(view, GetRouteTitle(routePath.TrimStart('/')));
                return;
            }
        }

        // Find matching section
        for (int i = 0; i < _sections.Count; i++)
        {
            var section = _sections[i];
            if (!section.Route.Equals(parts[0], StringComparison.OrdinalIgnoreCase))
                continue;

            if (parts.Length > 1)
            {
                // Find matching item
                for (int j = 0; j < section.Items.Count; j++)
                {
                    if (section.Items[j].Route.Equals(parts[1], StringComparison.OrdinalIgnoreCase))
                    {
                        NavigateToSection(i, j);
                        if (section.Items[j].Content != null && allParams.Count > 0)
                        {
                            ApplyQueryParameters(section.Items[j].Content!, allParams);
                        }
                        return;
                    }
                }
            }
            NavigateToSection(i);
            if (section.Items.Count > 0 && section.Items[0].Content != null && allParams.Count > 0)
            {
                ApplyQueryParameters(section.Items[0].Content!, allParams);
            }
            break;
        }
    }

    private static Dictionary<string, string> ParseQueryString(string queryString)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(queryString)) return result;

        var pairs = queryString.Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in pairs)
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2)
            {
                result[Uri.UnescapeDataString(parts[0])] = Uri.UnescapeDataString(parts[1]);
            }
            else if (parts.Length == 1)
            {
                result[Uri.UnescapeDataString(parts[0])] = string.Empty;
            }
        }
        return result;
    }

    private static void ApplyQueryParameters(SkiaView content, IDictionary<string, object> parameters)
    {
        if (parameters.Count == 0) return;

        if (content is ISkiaQueryAttributable attributable)
        {
            attributable.ApplyQueryAttributes(parameters);
        }

        var type = content.GetType();
        foreach (var param in parameters)
        {
            var prop = type.GetProperty(param.Key, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (prop != null && prop.CanWrite)
            {
                try
                {
                    var value = Convert.ChangeType(param.Value, prop.PropertyType);
                    prop.SetValue(content, value);
                }
                catch (Exception ex) { DiagnosticLog.Debug("SkiaShell", "Parameter type conversion failed", ex); }
            }
        }
    }

    /// <summary>
    /// Registers a route with a content factory.
    /// </summary>
    public void RegisterRoute(string route, Func<SkiaView?> contentFactory, string? title = null)
    {
        var key = route.TrimStart('/');
        _registeredRoutes[key] = contentFactory;
        if (!string.IsNullOrEmpty(title))
        {
            _routeTitles[key] = title;
        }
    }

    /// <summary>
    /// Unregisters a route.
    /// </summary>
    public void UnregisterRoute(string route)
    {
        var key = route.TrimStart('/');
        _registeredRoutes.Remove(key);
        _routeTitles.Remove(key);
    }

    private string GetRouteTitle(string route)
    {
        if (_routeTitles.TryGetValue(route, out string? title))
        {
            return title;
        }
        return route.Split('/').LastOrDefault() ?? route;
    }

    /// <summary>
    /// Gets whether there are pages on the navigation stack.
    /// </summary>
    public bool CanGoBack => _navigationStack.Count > 0;

    /// <summary>
    /// Gets the current navigation stack depth.
    /// </summary>
    public int NavigationStackDepth => _navigationStack.Count;

    /// <summary>
    /// Pushes a new page onto the navigation stack.
    /// </summary>
    public void PushAsync(SkiaView page, string title)
    {
        // Save current content to stack
        if (_currentContent != null)
        {
            _navigationStack.Push((_currentContent, Title));
        }

        // Set new content
        SetCurrentContent(page);
        Title = title;
        Invalidate();
    }

    /// <summary>
    /// Pops the current page from the navigation stack.
    /// </summary>
    public bool PopAsync()
    {
        if (_navigationStack.Count == 0) return false;

        var (previousContent, previousTitle) = _navigationStack.Pop();
        SetCurrentContent(previousContent);
        Title = previousTitle;
        Invalidate();
        return true;
    }

    /// <summary>
    /// Pops all pages from the navigation stack, returning to the root.
    /// </summary>
    public void PopToRootAsync()
    {
        if (_navigationStack.Count == 0) return;

        // Get the root content
        (SkiaView Content, string Title) root = default;
        while (_navigationStack.Count > 0)
        {
            root = _navigationStack.Pop();
        }

        SetCurrentContent(root.Content);
        Title = root.Title ?? string.Empty;
        Invalidate();
    }

    private void SetCurrentContent(SkiaView? content)
    {
        if (_currentContent != null)
        {
            RemoveChild(_currentContent);
        }

        _currentContent = content;

        if (_currentContent != null)
        {
            AddChild(_currentContent);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Measure current content with padding accounted for (consistent with ArrangeOverride)
        if (_currentContent != null)
        {
            float contentTop = NavBarIsVisible ? NavBarHeight : 0;
            float contentBottom = TabBarIsVisible ? TabBarHeight : 0;
            float flyoutOffset = FlyoutBehavior == ShellFlyoutBehavior.Locked ? FlyoutWidth : 0;
            var contentSize = new Size(
                availableSize.Width - Padding.Left - Padding.Right - flyoutOffset,
                availableSize.Height - contentTop - contentBottom - Padding.Top - Padding.Bottom);
            _currentContent.Measure(contentSize);
        }

        return availableSize;
    }

    protected override Rect ArrangeOverride(Rect bounds)
    {
        DiagnosticLog.Debug("SkiaShell", $"ArrangeOverride - bounds={bounds}");

        // Arrange current content with padding, offset for locked flyout
        if (_currentContent != null)
        {
            float flyoutOffset = FlyoutBehavior == ShellFlyoutBehavior.Locked ? FlyoutWidth : 0;
            float contentTop = (float)bounds.Top + (NavBarIsVisible ? NavBarHeight : 0) + ContentPadding;
            float contentBottom = (float)bounds.Bottom - (TabBarIsVisible ? TabBarHeight : 0) - ContentPadding;
            var contentBounds = new Rect(
                bounds.Left + flyoutOffset + ContentPadding,
                contentTop,
                bounds.Width - flyoutOffset - ContentPadding * 2,
                contentBottom - contentTop);
            DiagnosticLog.Debug("SkiaShell", $"Arranging content with bounds={contentBounds}, padding={ContentPadding}");
            _currentContent.Arrange(contentBounds);
        }

        return bounds;
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        canvas.Save();
        canvas.ClipRect(bounds);

        bool isLocked = FlyoutBehavior == ShellFlyoutBehavior.Locked;

        // In Locked mode, draw flyout first (it's a permanent panel, not an overlay)
        if (isLocked)
        {
            DrawFlyout(canvas, bounds);
        }

        // Draw content
        _currentContent?.Draw(canvas);

        // Draw navigation bar (offset for locked flyout)
        if (NavBarIsVisible)
        {
            if (isLocked)
            {
                var navBounds = new SKRect(bounds.Left + FlyoutWidth, bounds.Top, bounds.Right, bounds.Bottom);
                DrawNavBar(canvas, navBounds);
            }
            else
            {
                DrawNavBar(canvas, bounds);
            }
        }

        // Draw tab bar
        if (TabBarIsVisible)
        {
            DrawTabBar(canvas, bounds);
        }

        // Draw flyout overlay and panel (non-locked mode)
        if (!isLocked && _flyoutAnimationProgress > 0)
        {
            DrawFlyout(canvas, bounds);
        }

        canvas.Restore();
    }

    private void DrawNavBar(SKCanvas canvas, SKRect bounds)
    {
        var navBarBounds = new SKRect(
            bounds.Left,
            bounds.Top,
            bounds.Right,
            bounds.Top + NavBarHeight);

        // Draw background
        using var bgPaint = new SKPaint
        {
            Color = _navBarBackgroundColorSK,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRect(navBarBounds, bgPaint);

        // Draw nav icon (back arrow if can go back, else hamburger menu if flyout enabled)
        using var iconPaint = new SKPaint
        {
            Color = _navBarTextColorSK,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = true
        };

        float iconLeft = navBarBounds.Left + 16;
        float iconCenter = navBarBounds.MidY;

        if (CanGoBack)
        {
            // Draw iOS-style back chevron "<"
            using var chevronPaint = new SKPaint
            {
                Color = _navBarTextColorSK,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2.5f,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round,
                IsAntialias = true
            };

            // Clean chevron pointing left
            float chevronX = iconLeft + 6;
            float chevronSize = 10;
            canvas.DrawLine(chevronX + chevronSize, iconCenter - chevronSize, chevronX, iconCenter, chevronPaint);
            canvas.DrawLine(chevronX, iconCenter, chevronX + chevronSize, iconCenter + chevronSize, chevronPaint);
        }
        else if (FlyoutBehavior == ShellFlyoutBehavior.Flyout)
        {
            // Draw hamburger menu icon
            canvas.DrawLine(iconLeft, iconCenter - 8, iconLeft + 18, iconCenter - 8, iconPaint);
            canvas.DrawLine(iconLeft, iconCenter, iconLeft + 18, iconCenter, iconPaint);
            canvas.DrawLine(iconLeft, iconCenter + 8, iconLeft + 18, iconCenter + 8, iconPaint);
        }

        // Draw title
        using var titlePaint = new SKPaint
        {
            Color = _navBarTextColorSK,
            TextSize = 20f,
            IsAntialias = true,
            FakeBoldText = true
        };

        float titleX = (CanGoBack || (FlyoutBehavior == ShellFlyoutBehavior.Flyout && FlyoutBehavior != ShellFlyoutBehavior.Locked)) ? navBarBounds.Left + 56 : navBarBounds.Left + 16;
        float titleY = navBarBounds.MidY + 6;
        canvas.DrawText(Title, titleX, titleY, titlePaint);
    }

    private void DrawTabBar(SKCanvas canvas, SKRect bounds)
    {
        if (_selectedSectionIndex < 0 || _selectedSectionIndex >= _sections.Count) return;

        var section = _sections[_selectedSectionIndex];
        if (section.Items.Count <= 1) return;

        var tabBarBounds = new SKRect(
            bounds.Left,
            bounds.Bottom - TabBarHeight,
            bounds.Right,
            bounds.Bottom);

        // Draw background
        using var bgPaint = new SKPaint
        {
            Color = SkiaTheme.BackgroundWhiteSK,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRect(tabBarBounds, bgPaint);

        // Draw top border
        using var borderPaint = new SKPaint
        {
            Color = SkiaTheme.Gray300SK,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1
        };
        canvas.DrawLine(tabBarBounds.Left, tabBarBounds.Top, tabBarBounds.Right, tabBarBounds.Top, borderPaint);

        // Draw tabs
        float tabWidth = tabBarBounds.Width / section.Items.Count;

        using var textPaint = new SKPaint
        {
            TextSize = 12f,
            IsAntialias = true
        };

        for (int i = 0; i < section.Items.Count; i++)
        {
            var item = section.Items[i];
            bool isSelected = i == _selectedItemIndex;

            textPaint.Color = isSelected ? _navBarBackgroundColorSK : SkiaTheme.TextTertiarySK;

            var textBounds = new SKRect();
            textPaint.MeasureText(item.Title, ref textBounds);

            float textX = tabBarBounds.Left + i * tabWidth + tabWidth / 2 - textBounds.MidX;
            float textY = tabBarBounds.MidY - textBounds.MidY;

            canvas.DrawText(item.Title, textX, textY, textPaint);
        }
    }

    private void DrawFlyout(SKCanvas canvas, SKRect bounds)
    {
        bool isLocked = FlyoutBehavior == ShellFlyoutBehavior.Locked;

        // Draw scrim only for non-locked flyout (overlay mode)
        if (!isLocked)
        {
            using var scrimPaint = new SKPaint
            {
                Color = SkiaTheme.Shadow40SK.WithAlpha((byte)(100 * _flyoutAnimationProgress)),
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(bounds, scrimPaint);
        }

        // Draw flyout panel — locked mode uses fixed position, overlay mode uses animation
        float flyoutX = isLocked ? bounds.Left : bounds.Left - FlyoutWidth + (FlyoutWidth * _flyoutAnimationProgress);
        var flyoutBounds = new SKRect(
            flyoutX,
            bounds.Top,
            flyoutX + FlyoutWidth,
            bounds.Bottom);

        using var flyoutPaint = new SKPaint
        {
            Color = _flyoutBackgroundColorSK,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRect(flyoutBounds, flyoutPaint);

        // Calculate header and footer heights
        float headerHeight = FlyoutHeaderView != null ? FlyoutHeaderHeight : 0f;
        float footerHeight = FlyoutFooterView != null ? FlyoutFooterHeight :
                            (!string.IsNullOrEmpty(FlyoutFooterText) ? FlyoutFooterHeight : 0f);

        // Draw flyout header if present
        if (FlyoutHeaderView != null)
        {
            var headerBounds = new SKRect(flyoutBounds.Left, flyoutBounds.Top, flyoutBounds.Right, flyoutBounds.Top + headerHeight);
            FlyoutHeaderView.Measure(new Size(headerBounds.Width, headerBounds.Height));
            FlyoutHeaderView.Arrange(new Rect(headerBounds.Left, headerBounds.Top, headerBounds.Width, headerBounds.Height));
            FlyoutHeaderView.Draw(canvas);
        }

        // Draw flyout items with scrolling support
        float itemHeight = 48f;
        float itemsAreaTop = flyoutBounds.Top + headerHeight;
        float itemsAreaBottom = flyoutBounds.Bottom - footerHeight;

        // Clip to items area (between header and footer)
        canvas.Save();
        canvas.ClipRect(new SKRect(flyoutBounds.Left, itemsAreaTop, flyoutBounds.Right, itemsAreaBottom));

        // Apply scroll offset
        float itemY = itemsAreaTop - _flyoutScrollOffset;

        using var itemTextPaint = new SKPaint
        {
            TextSize = 14f,
            IsAntialias = true
        };

        for (int i = 0; i < _sections.Count; i++)
        {
            var section = _sections[i];
            bool isSelected = i == _selectedSectionIndex;

            // Skip items that are scrolled above the visible area
            if (itemY + itemHeight < itemsAreaTop)
            {
                itemY += itemHeight;
                continue;
            }

            // Stop if we're below the visible area
            if (itemY > itemsAreaBottom)
                break;

            // Draw selection background
            if (isSelected)
            {
                using var selectionPaint = new SKPaint
                {
                    Color = SkiaTheme.PrimarySelectionSK,
                    Style = SKPaintStyle.Fill
                };
                var selectionRect = new SKRect(flyoutBounds.Left, itemY, flyoutBounds.Right, itemY + itemHeight);
                canvas.DrawRect(selectionRect, selectionPaint);
            }

            itemTextPaint.Color = isSelected ? SKColors.White : _flyoutTextColorSK;

            // Draw icon if available
            float textStartX = flyoutBounds.Left + 16;
            var icon = GetFlyoutIcon(section.IconPath);
            if (icon != null)
            {
                float iconSize = 24f;
                float iconX = flyoutBounds.Left + 16;
                float iconY = itemY + (itemHeight - iconSize) / 2;
                canvas.DrawBitmap(icon, new SKRect(iconX, iconY, iconX + iconSize, iconY + iconSize));
                textStartX = iconX + iconSize + 12; // gap between icon and text
            }

            canvas.DrawText(section.Title, textStartX, itemY + 30, itemTextPaint);

            itemY += itemHeight;
        }

        canvas.Restore();

        // Draw flyout footer
        if (FlyoutFooterView != null)
        {
            var footerBounds = new SKRect(flyoutBounds.Left, flyoutBounds.Bottom - footerHeight, flyoutBounds.Right, flyoutBounds.Bottom);
            FlyoutFooterView.Measure(new Size(footerBounds.Width, footerBounds.Height));
            FlyoutFooterView.Arrange(new Rect(footerBounds.Left, footerBounds.Top, footerBounds.Width, footerBounds.Height));
            FlyoutFooterView.Draw(canvas);
        }
        else if (!string.IsNullOrEmpty(FlyoutFooterText))
        {
            // Fallback: draw simple text footer
            using var footerPaint = new SKPaint
            {
                TextSize = 12f,
                Color = _flyoutTextColorSK.WithAlpha(180),
                IsAntialias = true
            };
            var footerY = flyoutBounds.Bottom - footerHeight / 2 + 4;
            canvas.DrawText(FlyoutFooterText, flyoutBounds.Left + 16, footerY, footerPaint);
        }
    }

    public override SkiaView? HitTest(float x, float y)
    {
        if (!IsVisible || !Bounds.Contains(x, y)) return null;

        // Check flyout area
        bool isLockedHit = FlyoutBehavior == ShellFlyoutBehavior.Locked;
        if (isLockedHit || _flyoutAnimationProgress > 0)
        {
            float flyoutX = isLockedHit ? (float)Bounds.Left : (float)Bounds.Left - FlyoutWidth + (FlyoutWidth * _flyoutAnimationProgress);
            var flyoutBounds = new SKRect(flyoutX, (float)Bounds.Top, flyoutX + FlyoutWidth, (float)Bounds.Bottom);

            if (flyoutBounds.Contains(x, y))
            {
                return this; // Flyout handles its own hits
            }

            // Tap on scrim closes flyout (non-locked only)
            if (FlyoutIsPresented && !isLockedHit)
            {
                return this;
            }
        }

        // Check nav bar
        if (NavBarIsVisible && y < (float)Bounds.Top + NavBarHeight)
        {
            return this;
        }

        // Check tab bar
        if (TabBarIsVisible && y > (float)Bounds.Bottom - TabBarHeight)
        {
            return this;
        }

        // Check content
        if (_currentContent != null)
        {
            var hit = _currentContent.HitTest(x, y);
            if (hit != null) return hit;
        }

        return this;
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        // Check flyout tap
        bool isLocked = FlyoutBehavior == ShellFlyoutBehavior.Locked;
        if (isLocked || _flyoutAnimationProgress > 0)
        {
            float flyoutX = isLocked ? (float)Bounds.Left : (float)Bounds.Left - FlyoutWidth + (FlyoutWidth * _flyoutAnimationProgress);
            var flyoutBounds = new SKRect(flyoutX, (float)Bounds.Top, flyoutX + FlyoutWidth, (float)Bounds.Bottom);

            if (flyoutBounds.Contains(e.X, e.Y))
            {
                // Calculate header and footer heights
                float headerHeight = FlyoutHeaderView != null ? FlyoutHeaderHeight : 0f;
                float footerHeight = FlyoutFooterView != null ? FlyoutFooterHeight :
                                    (!string.IsNullOrEmpty(FlyoutFooterText) ? FlyoutFooterHeight : 0f);

                float itemsAreaTop = flyoutBounds.Top + headerHeight;
                float itemsAreaBottom = flyoutBounds.Bottom - footerHeight;

                // Only check items if tap is in items area
                if (e.Y >= itemsAreaTop && e.Y < itemsAreaBottom)
                {
                    // Apply scroll offset to find which item was tapped
                    float itemY = itemsAreaTop - _flyoutScrollOffset;
                    float itemHeight = 48f;

                    for (int i = 0; i < _sections.Count; i++)
                    {
                        if (e.Y >= itemY && e.Y < itemY + itemHeight)
                        {
                            NavigateToSection(i, 0);
                            if (!isLocked)
                            {
                                FlyoutIsPresented = false;
                                _flyoutScrollOffset = 0; // Reset scroll when closing
                            }
                            e.Handled = true;
                            return;
                        }
                        itemY += itemHeight;
                    }
                }
            }
            else if (FlyoutIsPresented && !isLocked)
            {
                // Tap on scrim (non-locked mode only)
                FlyoutIsPresented = false;
                e.Handled = true;
                return;
            }
        }

        // Check nav bar icon tap (back button or hamburger menu)
        if (NavBarIsVisible && e.Y < Bounds.Top + NavBarHeight && e.X < 56)
        {
            if (CanGoBack)
            {
                // Back button pressed
                PopAsync();
                e.Handled = true;
                return;
            }
            else if (FlyoutBehavior == ShellFlyoutBehavior.Flyout)
            {
                // Hamburger menu pressed
                FlyoutIsPresented = !FlyoutIsPresented;
                e.Handled = true;
                return;
            }
        }

        // Check tab bar tap
        if (TabBarIsVisible && e.Y > (float)Bounds.Bottom - TabBarHeight)
        {
            if (_selectedSectionIndex >= 0 && _selectedSectionIndex < _sections.Count)
            {
                var section = _sections[_selectedSectionIndex];
                float tabWidth = (float)Bounds.Width / section.Items.Count;
                int tappedIndex = (int)((e.X - (float)Bounds.Left) / tabWidth);
                tappedIndex = Math.Clamp(tappedIndex, 0, section.Items.Count - 1);

                if (tappedIndex != _selectedItemIndex)
                {
                    NavigateToSection(_selectedSectionIndex, tappedIndex);
                }
                e.Handled = true;
                return;
            }
        }

        base.OnPointerPressed(e);
    }

    public override void OnScroll(ScrollEventArgs e)
    {
        if (FlyoutIsPresented && _flyoutAnimationProgress > 0)
        {
            float flyoutX = (float)Bounds.Left - FlyoutWidth + (FlyoutWidth * _flyoutAnimationProgress);
            var flyoutBounds = new SKRect(flyoutX, (float)Bounds.Top, flyoutX + FlyoutWidth, (float)Bounds.Bottom);

            if (flyoutBounds.Contains(e.X, e.Y))
            {
                float headerHeight = FlyoutHeaderView != null ? FlyoutHeaderHeight : 0f;
                float footerHeight = FlyoutFooterView != null ? FlyoutFooterHeight :
                                    (!string.IsNullOrEmpty(FlyoutFooterText) ? FlyoutFooterHeight : 0f);
                float itemHeight = 48f;
                float totalItemsHeight = _sections.Count * itemHeight;
                float viewableHeight = flyoutBounds.Height - headerHeight - footerHeight;
                float maxScroll = Math.Max(0f, totalItemsHeight - viewableHeight);

                _flyoutScrollOffset += e.DeltaY * 30f;
                _flyoutScrollOffset = Math.Max(0f, Math.Min(_flyoutScrollOffset, maxScroll));
                Invalidate();
                e.Handled = true;
                return;
            }
        }
        base.OnScroll(e);
    }
}

/// <summary>
/// Shell flyout behavior options.
/// </summary>
public enum ShellFlyoutBehavior
{
    /// <summary>
    /// No flyout menu.
    /// </summary>
    Disabled,

    /// <summary>
    /// Flyout slides over content.
    /// </summary>
    Flyout,

    /// <summary>
    /// Flyout is always visible (side-by-side layout).
    /// </summary>
    Locked
}

/// <summary>
/// Represents a section in the shell (typically shown in flyout).
/// </summary>
public class ShellSection
{
    /// <summary>
    /// The route identifier for this section.
    /// </summary>
    public string Route { get; set; } = string.Empty;

    /// <summary>
    /// The display title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Optional icon path.
    /// </summary>
    public string? IconPath { get; set; }

    /// <summary>
    /// Items in this section.
    /// </summary>
    public List<ShellContent> Items { get; } = new();
}

/// <summary>
/// Represents content within a shell section.
/// </summary>
public class ShellContent
{
    /// <summary>
    /// The route identifier for this content.
    /// </summary>
    public string Route { get; set; } = string.Empty;

    /// <summary>
    /// The display title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Optional icon path.
    /// </summary>
    public string? IconPath { get; set; }

    /// <summary>
    /// The content view.
    /// </summary>
    public SkiaView? Content { get; set; }

    /// <summary>
    /// Reference to the MAUI ShellContent this represents.
    /// </summary>
    public Microsoft.Maui.Controls.ShellContent? MauiShellContent { get; set; }
}

/// <summary>
/// Event args for shell navigation events.
/// </summary>
public class ShellNavigationEventArgs : EventArgs
{
    public ShellSection Section { get; }
    public ShellContent Content { get; }

    public ShellNavigationEventArgs(ShellSection section, ShellContent content)
    {
        Section = section;
        Content = content;
    }
}
