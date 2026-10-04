// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using Svg.Skia;
using Microsoft.Maui.Platform.Linux.Rendering;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Shell provides a common navigation experience for MAUI applications.
/// Supports flyout menu, tabs, and URI-based navigation.
/// </summary>
public partial class SkiaShell : SkiaLayoutView
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
            // MAUI's -1 means "platform default"; any other width is honoured
            // (a 64 px rail included).
            coerceValue: (b, v) => (float)v < 0 ? 280f : (float)v,
            // The content beside a locked flyout moves with its width (a rail
            // collapsing to 64 px), so lay out again, not just repaint.
            propertyChanged: (b, o, n) => { var shell = (SkiaShell)b; shell.InvalidateMeasure(); shell.Invalidate(); });

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

    private void ReadShellThemeColors()
    {
        if (MauiShell == null) return;
        var titleColor = Shell.GetTitleColor(MauiShell) ?? Shell.GetForegroundColor(MauiShell);
        if (titleColor != null)
            NavBarTextColor = titleColor;
        var bgColor = Shell.GetBackgroundColor(MauiShell);
        if (bgColor != null)
            NavBarBackgroundColor = bgColor;
    }

    /// <summary>
    /// Theme-refresh walker hook: yield every content tree this shell owns —
    /// the active section's content, every section's pre-rendered content, and
    /// any pages on the navigation stack. These live in private fields rather
    /// than the standard Children chain.
    /// </summary>
    public override IEnumerable<SkiaView> ExtraContentRoots
    {
        get
        {
            if (_currentContent != null)
                yield return _currentContent;
            foreach (var entry in _navigationStack)
                if (entry.Content != null)
                    yield return entry.Content;
            foreach (var section in _sections)
            {
                foreach (var item in section.Items)
                {
                    if (item.Content != null && !ReferenceEquals(item.Content, _currentContent))
                        yield return item.Content;
                }
            }
        }
    }
    private SkiaView? _pressedTarget;
    private float _flyoutAnimationProgress = 0f;
    private int _selectedSectionIndex = 0;
    private int _selectedItemIndex = 0;

    // Navigation stack for push/pop navigation. Ordered bottom (the section
    // root that was covered first) to top (the entry directly under the
    // current content); the current content itself is not on the stack.
    private readonly List<NavigationEntry> _navigationStack = new();

    private readonly record struct NavigationEntry(SkiaView Content, string Title, Microsoft.Maui.Controls.Page? MauiPage);

    // The MAUI page the shell presents (the one whose Appearing has been sent
    // and Disappearing hasn't). Without a MAUI Shell, SkiaShell owns page
    // transitions and sends the lifecycle events itself — pages commonly
    // subscribe/unsubscribe event handlers in OnAppearing/OnDisappearing. With
    // one, MAUI's ShellSection sends them too, and SkiaShell only sends
    // Appearing to the page MAUI presents (MauiPresents).
    private Microsoft.Maui.Controls.Page? _lifecyclePage;

    private void SendPageLifecycle(Microsoft.Maui.Controls.Page? newPage)
    {
        if (ReferenceEquals(_lifecyclePage, newPage))
        {
            ApplyPresentedPageTitle(); // a navigation back to the same page reset the title
            return;
        }
        try
        {
            DiagnosticLog.Debug("SkiaShell", $"lifecycle: {_lifecyclePage?.GetType().Name ?? "(none)"} -> {newPage?.GetType().Name ?? "(null)"}");
            (_lifecyclePage as Microsoft.Maui.Controls.IPageController)?.SendDisappearing();
            if (_lifecyclePage != null)
                _lifecyclePage.PropertyChanged -= OnPresentedPagePropertyChanged;
            _lifecyclePage = newPage;
            if (newPage != null)
            {
                newPage.PropertyChanged += OnPresentedPagePropertyChanged;
                ApplyPresentedPageTitle();
            }
            TrackBackButtonBehavior();
            TrackToolbarItems(newPage);
            UpdateTitleView();
            if (MauiPresents(newPage))
                (newPage as Microsoft.Maui.Controls.IPageController)?.SendAppearing();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaShell", "Page lifecycle handler threw", ex);
        }
    }

    /// <summary>
    /// The navigation bar shows the presented page's Title whenever the page sets one, and
    /// follows it as it changes (MAUI's ShellToolbar); otherwise the title the navigation gave
    /// (the ShellContent's at the root) stays.
    /// </summary>
    private void ApplyPresentedPageTitle()
    {
        if (_lifecyclePage is { } page && page.IsSet(Microsoft.Maui.Controls.Page.TitleProperty))
            Title = page.Title ?? string.Empty;
    }

    private void OnPresentedPagePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == Microsoft.Maui.Controls.Page.TitleProperty.PropertyName && ReferenceEquals(sender, _lifecyclePage))
        {
            ApplyPresentedPageTitle();
            Invalidate();
        }
        else if (e.PropertyName == Shell.BackButtonBehaviorProperty.PropertyName && ReferenceEquals(sender, _lifecyclePage))
        {
            TrackBackButtonBehavior();
            Invalidate();
        }
        else if (e.PropertyName == Shell.TitleViewProperty.PropertyName && ReferenceEquals(sender, _lifecyclePage))
        {
            UpdateTitleView();
        }
        else if (e.PropertyName == Shell.TabBarIsVisibleProperty.PropertyName && ReferenceEquals(sender, _lifecyclePage))
        {
            // The page shows or hides the tab bar; the content takes or gives back its room.
            InvalidateMeasure();
            Invalidate();
        }
    }

    private BackButtonBehavior? _backButtonBehavior;

    /// <summary>The presented page's Shell.BackButtonBehavior, followed as it changes.</summary>
    private void TrackBackButtonBehavior()
    {
        var behavior = _lifecyclePage != null ? Shell.GetBackButtonBehavior(_lifecyclePage) : null;
        if (ReferenceEquals(behavior, _backButtonBehavior))
            return;
        if (_backButtonBehavior != null)
            _backButtonBehavior.PropertyChanged -= OnBackButtonBehaviorChanged;
        _backButtonBehavior = behavior;
        if (behavior != null)
            behavior.PropertyChanged += OnBackButtonBehaviorChanged;
    }

    private void OnBackButtonBehaviorChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => Invalidate();

    /// <summary>
    /// The back arrow is shown: there is a page to go back to and the presented page's
    /// Shell.BackButtonBehavior does not hide it (MAUI's ShellToolbar).
    /// </summary>
    public bool IsBackButtonVisible => CanGoBack && (_backButtonBehavior?.IsVisible ?? true);

    /// <summary>
    /// Re-issues SendAppearing for the current lifecycle page. MAUI's
    /// Page.SendAppearing silently no-ops until the page's parent chain
    /// reaches an Application-attached Window, and the initial
    /// NavigateToSection can run before that chain is complete — the host
    /// calls this once the shell is fully attached. Safe to call repeatedly
    /// (MAUI guards with _hasAppeared).
    /// </summary>
    public void ResendPendingAppearing()
    {
        if (MauiPresents(_lifecyclePage))
            (_lifecyclePage as Microsoft.Maui.Controls.IPageController)?.SendAppearing();
    }

    /// <summary>
    /// True when <paramref name="page"/> is the page the attached MAUI Shell presents and no
    /// modal page covers the window. MAUI's ShellSection sends Appearing and Disappearing for
    /// the pages it presents; the mirror lags it (it follows Navigated), so an Appearing sent
    /// for a page MAUI has already moved past (a push from the page's NavigatedTo) or that a
    /// modal covers fired its Appearing a second time. Without a MAUI Shell the platform is
    /// the only source of the lifecycle.
    /// </summary>
    private bool MauiPresents(Microsoft.Maui.Controls.Page? page)
    {
        if (page == null || _mauiShell == null)
            return page != null;
        if (page.Window is { } window && window.Navigation.ModalStack.Count > 0)
            return false;
        return ReferenceEquals(_mauiShell.CurrentPage, page);
    }

    private static Microsoft.Maui.Controls.Page? ResolveMauiPage(ShellContent item)
    {
        if (item.MauiShellContent == null) return null;
        // MAUI's own cache is only populated when the page was created through
        // IShellContentController — our renderer creates pages itself, so fall
        // through to its ShellContent → Page table.
        return (item.MauiShellContent as Microsoft.Maui.Controls.IShellContentController)?.Page as Microsoft.Maui.Controls.Page
            ?? Microsoft.Maui.Platform.Linux.Hosting.LinuxViewRenderer.GetShellContentPage(item.MauiShellContent);
    }

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
        ReportFlyoutPresentedToMaui(newValue);
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
    /// True when <see cref="FlyoutHeaderHeight"/> was set for this header (its MAUI view's
    /// HeightRequest); otherwise the header is as tall as its content, as MAUI sizes it.
    /// </summary>
    public bool FlyoutHeaderHeightExplicit { get; set; }

    /// <summary>
    /// The header's height at <paramref name="width"/>: its explicit height, else its
    /// content's, at most the flyout's height. A fixed 140 cut the hit area of a taller
    /// header (Strikeline's whole menu) at 140, so rows drawn below it took no clicks.
    /// </summary>
    private float ResolveFlyoutHeaderHeight(float width, float maxHeight)
    {
        if (FlyoutHeaderView == null)
            return 0f;
        float h;
        if (FlyoutHeaderHeightExplicit)
            h = FlyoutHeaderHeight;
        else
        {
            var desired = FlyoutHeaderView.Measure(new Size(width, double.PositiveInfinity));
            h = double.IsNaN(desired.Height) || double.IsInfinity(desired.Height) ? FlyoutHeaderHeight : (float)desired.Height;
        }
        // A collapsing header never gets shorter than an app bar, on every MAUI platform.
        if (FlyoutHeaderBehavior == Microsoft.Maui.Controls.FlyoutHeaderBehavior.CollapseOnScroll)
            h = Math.Max(h, CollapsedFlyoutHeaderHeight);
        return Math.Min(h, Math.Max(0f, maxHeight));
    }

    /// <summary>The height a CollapseOnScroll header keeps (MAUI's 56, Android's action bar).</summary>
    private const float CollapsedFlyoutHeaderHeight = 56f;

    private Microsoft.Maui.Controls.FlyoutHeaderBehavior _flyoutHeaderBehavior;

    /// <summary>
    /// Shell.FlyoutHeaderBehavior. A CollapseOnScroll header is at least
    /// <c>56</c> tall, as MAUI keeps it on every platform.
    /// </summary>
    public Microsoft.Maui.Controls.FlyoutHeaderBehavior FlyoutHeaderBehavior
    {
        get => _flyoutHeaderBehavior;
        set
        {
            if (_flyoutHeaderBehavior == value)
                return;
            _flyoutHeaderBehavior = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Where the flyout panel is now, in the coordinates of <see cref="SkiaView.Bounds"/> (its
    /// header, content and footer views are arranged inside it). Off to the left of the shell
    /// while the flyout is closed; the shell's left edge when it is open or locked.
    /// </summary>
    public Rect FlyoutBounds
    {
        get
        {
            bool locked = FlyoutBehavior == ShellFlyoutBehavior.Locked;
            float x = locked ? (float)Bounds.Left : (float)Bounds.Left - FlyoutWidth + FlyoutWidth * _flyoutAnimationProgress;
            return new Rect(x, Bounds.Top, FlyoutWidth, Bounds.Height);
        }
    }

    /// <summary>
    /// The footer's height at <paramref name="width"/>: at least <see cref="FlyoutFooterHeight"/>,
    /// and as tall as its content, as the flyout draws it; input uses the same value (it used the
    /// fixed height, so rows of a taller footer took no clicks). The version-text fallback belongs
    /// to the default item list only.
    /// </summary>
    private float ResolveFlyoutFooterHeight(float width)
    {
        if (FlyoutFooterView != null)
        {
            var desired = FlyoutFooterView.Measure(new Size(width, double.PositiveInfinity));
            return Math.Max(FlyoutFooterHeight, double.IsNaN(desired.Height) || double.IsInfinity(desired.Height) ? 0f : (float)desired.Height);
        }
        return !string.IsNullOrEmpty(FlyoutFooterText) && FlyoutContentView == null ? FlyoutFooterHeight : 0f;
    }

    private SkiaView? _flyoutContentView;

    /// <summary>
    /// Shell.FlyoutContent (or FlyoutContentTemplate): a view that replaces the
    /// flyout's item list, between the header and footer, as on every platform.
    /// Apps with their own navigation rail hide the items and put it here.
    /// </summary>
    public SkiaView? FlyoutContentView
    {
        get => _flyoutContentView;
        set
        {
            if (ReferenceEquals(_flyoutContentView, value))
                return;
            if (_flyoutContentView != null && ReferenceEquals(_flyoutContentView.Parent, this))
                _flyoutContentView.Parent = null;
            _flyoutContentView = value;
            if (value != null)
                value.Parent = this; // invalidations reach the window
            InvalidateMeasure();
            Invalidate();
        }
    }

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
    /// Gets the index of the selected content (tab) within the current section.
    /// </summary>
    public int CurrentItemIndex => _selectedItemIndex;

    /// <summary>
    /// The view presented in the content area: the selected content's view,
    /// or the top pushed page's view when the navigation stack is not empty.
    /// </summary>
    public SkiaView? CurrentContent => _currentContent;

    /// <summary>
    /// The MAUI page currently presented (the one that last received
    /// Appearing), or null when no MAUI page is attached.
    /// </summary>
    public Microsoft.Maui.Controls.Page? CurrentMauiPage => _lifecyclePage;

    /// <summary>
    /// Reference to the MAUI Shell this view represents.
    /// </summary>
    private Shell? _mauiShell;
    private bool _appThemeSubscribed;
    public Shell? MauiShell
    {
        get => _mauiShell;
        set
        {
            if (ReferenceEquals(_mauiShell, value)) return;
            if (_mauiShell != null)
                DetachMauiShell(_mauiShell);
            _mauiShell = value;
            if (_mauiShell != null)
                AttachMauiShell(_mauiShell);
            // Subscribe to Application.RequestedThemeChanged once so the Shell's
            // attached colors (TitleColor, ForegroundColor, BackgroundColor —
            // which are AppThemeBinding-bound from XAML) get pulled into the
            // navbar cache after MAUI finishes propagating the new theme. The
            // standard PropertyMapper doesn't reliably refire for attached props
            // on theme change, and the cache otherwise lags a toggle behind.
            if (!_appThemeSubscribed && Application.Current != null)
            {
                Application.Current.RequestedThemeChanged += OnApplicationRequestedThemeChanged;
                _appThemeSubscribed = true;
            }
        }
    }

    private void OnApplicationRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        ReadShellThemeColors();
        Invalidate();
    }

    #region MAUI Shell navigation bridge

    // MAUI's Shell owns routing: Shell.GoToAsync runs through
    // ShellNavigationManager, which resolves the route, applies
    // [QueryProperty] / IQueryAttributable, raises Navigating/Navigated,
    // updates CurrentState and mutates each ShellSection's page stack.
    // SkiaShell never routes on its own when a MAUI Shell is attached — it
    // mirrors MAUI's state: the current ShellContent, and the pages MAUI has
    // pushed on the current section. Two signals drive the mirror:
    //   * IShellSectionController.NavigationRequested (per section) fires
    //     synchronously on every push/pop/insert/remove, after MAUI's stack
    //     has been mutated;
    //   * Shell.Navigated fires when a navigation completes (section switch,
    //     push, pop) and is the public, stable signal.
    // Both call SyncFromMauiShell, which is idempotent.

    private readonly HashSet<Microsoft.Maui.Controls.ShellSection> _observedSections = new();
    private bool _syncingFromMaui;

    /// <summary>
    /// Renders a MAUI page that MAUI pushed onto a section's navigation
    /// stack. Set by the host (handler or LinuxViewRenderer). Pages are
    /// rendered once and the platform view is reused on later syncs.
    /// </summary>
    public Func<Microsoft.Maui.Controls.Page, SkiaView?>? PageRenderer { get; set; }

    private bool _syncingFlyoutPresented;

    /// <summary>
    /// The MAUI shell's FlyoutIsPresented, both ways: code that opens or closes the drawer
    /// (<c>Shell.Current.FlyoutIsPresented = true</c>) reaches the platform whatever handler
    /// hosts the shell (the renderer-hosted handler maps no properties), and the hamburger
    /// and scrim, which the platform handles itself, report back so MAUI's value is current.
    /// </summary>
    private void OnMauiShellPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // The selection, as MAUI moves it (GoToAsync to another item, code setting
        // CurrentItem): the platform follows whatever handler hosts the shell. MAUI raises
        // no Navigated for an item switch the platform has not performed.
        if (e.PropertyName == nameof(Shell.CurrentItem))
        {
            SyncFromMauiShell();
            return;
        }
        if (e.PropertyName == Shell.TitleViewProperty.PropertyName)
        {
            OnMauiShellTitleViewChanged();
            return;
        }
        if (e.PropertyName != Shell.FlyoutIsPresentedProperty.PropertyName || _syncingFlyoutPresented || _mauiShell == null)
            return;
        _syncingFlyoutPresented = true;
        try { FlyoutIsPresented = _mauiShell.FlyoutIsPresented; }
        finally { _syncingFlyoutPresented = false; }
    }

    private void ReportFlyoutPresentedToMaui(bool presented)
    {
        if (_mauiShell == null || _syncingFlyoutPresented || _mauiShell.FlyoutIsPresented == presented)
            return;
        _syncingFlyoutPresented = true;
        try { _mauiShell.SetValueFromRenderer(Shell.FlyoutIsPresentedProperty, presented); }
        finally { _syncingFlyoutPresented = false; }
    }

    private void AttachMauiShell(Shell shell)
    {
        shell.PropertyChanged += OnMauiShellPropertyChanged;
        ObserveAppearance(shell);
        if (FlyoutBehavior != ShellFlyoutBehavior.Locked)
            OnMauiShellPropertyChanged(shell, new System.ComponentModel.PropertyChangedEventArgs(Shell.FlyoutIsPresentedProperty.PropertyName));
        shell.Navigated += OnMauiShellNavigated;
        if (shell is Microsoft.Maui.Controls.IShellController controller)
            controller.StructureChanged += OnMauiShellStructureChanged;
        ObserveMauiSections();
    }

    private void DetachMauiShell(Shell shell)
    {
        shell.PropertyChanged -= OnMauiShellPropertyChanged;
        StopObservingAppearance(shell);
        shell.Navigated -= OnMauiShellNavigated;
        if (shell is Microsoft.Maui.Controls.IShellController controller)
            controller.StructureChanged -= OnMauiShellStructureChanged;
        foreach (var section in _observedSections)
        {
            ((Microsoft.Maui.Controls.IShellSectionController)section).NavigationRequested -= OnMauiNavigationRequested;
            section.PropertyChanged -= OnMauiSelectionPropertyChanged;
        }
        _observedSections.Clear();
        foreach (var item in _observedItems)
            item.PropertyChanged -= OnMauiSelectionPropertyChanged;
        _observedItems.Clear();
    }

    private readonly HashSet<Microsoft.Maui.Controls.ShellItem> _observedItems = new();

    private void ObserveMauiSections()
    {
        if (_mauiShell == null) return;
        foreach (var item in _mauiShell.Items)
        {
            if (_observedItems.Add(item))
                item.PropertyChanged += OnMauiSelectionPropertyChanged;
            foreach (var section in item.Items)
            {
                if (_observedSections.Add(section))
                {
                    ((Microsoft.Maui.Controls.IShellSectionController)section).NavigationRequested += OnMauiNavigationRequested;
                    section.PropertyChanged += OnMauiSelectionPropertyChanged;
                }
            }
        }
    }

    // A tab or content switch inside an item or section (their CurrentItem).
    private void OnMauiSelectionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "CurrentItem")
            SyncFromMauiShell();
    }

    private void OnMauiShellStructureChanged(object? sender, EventArgs e) => ObserveMauiSections();

    private void OnMauiNavigationRequested(object? sender, Microsoft.Maui.Controls.Internals.NavigationRequestedEventArgs e)
    {
        // MAUI mutates the section's stack before raising this, so the
        // mirror can be rebuilt from Stack directly. Completing the task
        // synchronously tells MAUI the platform transition is done.
        SyncFromMauiShell();
        e.Task ??= Task.FromResult(true);
    }

    private void OnMauiShellNavigated(object? sender, ShellNavigatedEventArgs e) => SyncFromMauiShell();

    /// <summary>
    /// The MAUI ShellSection currently presented (null when no MAUI Shell is
    /// attached or it has no items).
    /// </summary>
    private Microsoft.Maui.Controls.ShellSection? CurrentMauiSection => _mauiShell?.CurrentItem?.CurrentItem;

    /// <summary>
    /// Brings the platform selection and navigation stack in line with the
    /// attached MAUI Shell: selects the section/content MAUI reports as
    /// current, then makes the pushed pages match the MAUI section's
    /// <see cref="Microsoft.Maui.Controls.ShellSection.Stack"/>. Safe to call
    /// at any time; a no-op when already in sync or when no Shell is attached.
    /// </summary>
    public void SyncFromMauiShell()
    {
        if (_mauiShell == null || _syncingFromMaui) return;
        _syncingFromMaui = true;
        try
        {
            ObserveMauiSections();

            var mauiSection = CurrentMauiSection;
            var mauiContent = mauiSection?.CurrentItem;
            if (mauiContent != null && TryFindContent(mauiContent, out int sectionIndex, out int itemIndex)
                && (sectionIndex != _selectedSectionIndex || itemIndex != _selectedItemIndex))
            {
                NavigateToSection(sectionIndex, itemIndex);
            }

            SyncNavigationStack(mauiSection);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaShell", "SyncFromMauiShell failed", ex);
        }
        finally
        {
            _syncingFromMaui = false;
        }
    }

    private bool TryFindContent(Microsoft.Maui.Controls.ShellContent content, out int sectionIndex, out int itemIndex)
    {
        for (int i = 0; i < _sections.Count; i++)
        {
            var items = _sections[i].Items;
            for (int j = 0; j < items.Count; j++)
            {
                if (ReferenceEquals(items[j].MauiShellContent, content))
                {
                    sectionIndex = i;
                    itemIndex = j;
                    return true;
                }
            }
        }
        sectionIndex = -1;
        itemIndex = -1;
        return false;
    }

    private void SyncNavigationStack(Microsoft.Maui.Controls.ShellSection? mauiSection)
    {
        // MAUI's Stack[0] is a null placeholder for the section root.
        var target = new List<Microsoft.Maui.Controls.Page>();
        if (mauiSection != null)
        {
            foreach (var page in mauiSection.Stack)
                if (page != null) target.Add(page);
        }

        // Pages currently mirrored: stack entries above the root, then the
        // presented page (when something is pushed).
        var mirrored = new List<Microsoft.Maui.Controls.Page?>();
        for (int i = 1; i < _navigationStack.Count; i++)
            mirrored.Add(_navigationStack[i].MauiPage);
        if (_navigationStack.Count > 0)
            mirrored.Add(_lifecyclePage);

        if (mirrored.Count == target.Count)
        {
            bool same = true;
            for (int i = 0; i < target.Count && same; i++)
                same = ReferenceEquals(mirrored[i], target[i]);
            if (same) return;
        }

        // Find the longest common prefix, pop everything above it, then push
        // the remainder, so an unaffected page keeps its live platform view.
        int common = 0;
        while (common < mirrored.Count && common < target.Count && ReferenceEquals(mirrored[common], target[common]))
            common++;

        while (mirrored.Count > common)
        {
            PopAsync();
            mirrored.RemoveAt(mirrored.Count - 1);
        }

        for (int i = common; i < target.Count; i++)
        {
            var page = target[i];
            var view = PageRenderer?.Invoke(page);
            if (view == null)
            {
                DiagnosticLog.Warn("SkiaShell", $"No platform view for pushed page {page.GetType().Name}; skipping");
                continue;
            }
            PushAsync(view, page.Title ?? string.Empty, page);
        }
    }

    /// <summary>
    /// Handles the navigation bar back affordance. With a MAUI Shell attached
    /// the pop is requested from MAUI (so Navigating/Navigated fire and its
    /// stack stays authoritative); otherwise the platform stack is popped.
    /// </summary>
    public void GoBack()
    {
        var section = CurrentMauiSection;
        if (section != null && section.Stack.Count > 1)
        {
            _ = section.Navigation.PopAsync();
            return;
        }
        PopAsync();
    }

    /// <summary>
    /// Selects a section (flyout item) and content (tab). With a MAUI Shell
    /// attached the selection is proposed to MAUI exactly as a flyout tap is,
    /// and the platform follows through Navigated; otherwise the platform
    /// navigates directly.
    /// </summary>
    public void SelectSection(int sectionIndex, int itemIndex = 0)
    {
        if (sectionIndex < 0 || sectionIndex >= _sections.Count) return;
        var section = _sections[sectionIndex];
        if (itemIndex < 0 || itemIndex >= section.Items.Count) return;

        if (_mauiShell is Microsoft.Maui.Controls.IShellController controller)
        {
            var mauiContent = section.Items[itemIndex].MauiShellContent;
            if (mauiContent != null && ReferenceEquals(_mauiShell, FindShell(mauiContent)))
            {
                _ = controller.OnFlyoutItemSelectedAsync(mauiContent);
                return;
            }
        }

        NavigateToSection(sectionIndex, itemIndex);
    }

    private static Shell? FindShell(Element element)
    {
        var parent = element.Parent;
        while (parent != null && parent is not Shell)
            parent = parent.Parent;
        return parent as Shell;
    }

    #endregion

    /// <summary>
    /// Callback to render content from a ShellContent.
    /// </summary>
    public Func<Microsoft.Maui.Controls.ShellContent, SkiaView?>? ContentRenderer
    {
        get => _contentRenderer;
        set
        {
            _contentRenderer = value;
            // The first section is selected as soon as it is added, which can be before the
            // host supplies the renderer: build and show its page now.
            if (value != null && _navigationStack.Count == 0
                && _selectedSectionIndex >= 0 && _selectedSectionIndex < _sections.Count
                && _selectedItemIndex >= 0 && _selectedItemIndex < _sections[_selectedSectionIndex].Items.Count
                && _sections[_selectedSectionIndex].Items[_selectedItemIndex].Content == null)
            {
                NavigateToSection(_selectedSectionIndex, _selectedItemIndex);
            }
        }
    }

    private Func<Microsoft.Maui.Controls.ShellContent, SkiaView?>? _contentRenderer;

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
        // A ShellContent's page is built the first time it is shown, as MAUI builds a
        // templated one: building every page up front ran the constructors of pages the
        // app never visits (a sign-out page that needs a signed-in account threw at start).
        if (item.Content == null && item.MauiShellContent != null && ContentRenderer != null)
            item.Content = ContentRenderer(item.MauiShellContent);
        SetCurrentContent(item.Content);
        Title = item.Title;
        SendPageLifecycle(ResolveMauiPage(item));

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
        ReRenderContentTrees();

        // Clear icon cache so icons reload with new theme paths
        ClearIconCache();

        // Re-sync flyout item icon paths from MAUI Shell
        IconSyncer?.Invoke(this);

        InvalidateMeasure();
        Invalidate();
    }

    /// <summary>
    /// Rebuilds every section's content tree from its MAUI ShellContent and
    /// re-swaps the active page. Each rebuild goes through ContentRenderer
    /// (CreateShellContentPage), which creates a fresh Page instance — so a
    /// fresh <c>InitializeComponent</c> runs and any hot-reloaded XAML/C# is
    /// picked up. Shared by <see cref="RefreshTheme"/> (theme flip) and the
    /// hot-reload re-render path.
    /// </summary>
    public void ReRenderContentTrees()
    {
        if (ContentRenderer != null)
        {
            foreach (var section in _sections)
            {
                foreach (var item in section.Items)
                {
                    // Only pages already built: the others are built fresh when first shown.
                    if (item.MauiShellContent != null && item.Content != null)
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
                // Re-render created a fresh Page instance; move the lifecycle
                // to it so its OnAppearing subscriptions are live.
                SendPageLifecycle(ResolveMauiPage(item));
            }
        }

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
        foreach (var entry in _flyoutIcons.Values)
            entry.Release();
        _flyoutIcons.Clear();
        _tabIconCache?.Clear(); // tab icons reload too (a theme's icon set)
    }

    /// <summary>A flyout icon loaded through its image-source service, and the load in flight.</summary>
    private sealed class FlyoutIconEntry
    {
        public IImageSourceServiceResult<SKBitmap>? Result;
        public CancellationTokenSource? Load;

        public void Release()
        {
            Load?.Cancel();
            Load = null;
            Result?.Dispose();
            Result = null;
        }
    }

    private readonly Dictionary<Microsoft.Maui.Controls.ImageSource, FlyoutIconEntry> _flyoutIcons = new();

    /// <summary>The loaded flyout icon for <paramref name="source"/>, null while loading (tests).</summary>
    internal SKBitmap? LoadedFlyoutIcon(Microsoft.Maui.Controls.ImageSource source) =>
        _flyoutIcons.TryGetValue(source, out var entry) ? entry.Result?.Value : null;

    /// <summary>
    /// A flyout item's icon, loaded through its image-source service (file, font, URI, stream,
    /// or an app's own source) at the row's icon size and the screen's density, as on the other
    /// platforms. Null while it loads; the flyout repaints when it arrives.
    /// </summary>
    private SKBitmap? GetFlyoutIcon(Microsoft.Maui.Controls.ImageSource source)
    {
        if (source.IsEmpty)
            return null;
        if (_flyoutIcons.TryGetValue(source, out var entry))
            return entry.Result?.Value;
        entry = new FlyoutIconEntry();
        _flyoutIcons[source] = entry;
        _ = LoadFlyoutIconAsync(source, entry);
        return null;
    }

    private async Task LoadFlyoutIconAsync(Microsoft.Maui.Controls.ImageSource source, FlyoutIconEntry entry)
    {
        var load = entry.Load = new CancellationTokenSource();
        try
        {
            var result = await LinuxImageSourceServices.LoadAsync(
                LinuxImageSourceServices.ServicesFor(this), source, Math.Max(1f, DeviceScale), new Size(24, 24), load.Token);
            if (load.IsCancellationRequested)
            {
                result?.Dispose();
                return;
            }
            entry.Result = result;
            entry.Load = null;
            if (result == null)
                DiagnosticLog.Warn("SkiaShell", $"Flyout icon not found: {source}");
            Invalidate();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaShell", $"Loading a flyout icon failed: {source}", ex);
        }
    }

    /// <summary>
    /// Loads a flyout icon from file (SVG or PNG), with caching.
    /// </summary>
    private SKBitmap? GetFlyoutIcon(string? iconPath)
    {
        if (string.IsNullOrEmpty(iconPath)) return null;

        if (_iconCache.TryGetValue(iconPath, out var cached))
            return cached;

        // App-relative names and the .png -> .svg fallback, rendered sharp for
        // the 24-logical-pixel icon at up to 2x.
        var bitmap = Microsoft.Maui.Platform.Linux.Services.ImageFileResolver.LoadBitmap(iconPath, 48);
        if (bitmap == null)
            DiagnosticLog.Warn("SkiaShell", $"Flyout icon not found: {iconPath}");

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
    /// Navigates using a URI route with parameters. With a MAUI Shell
    /// attached the request is handed to <see cref="Shell.GoToAsync(ShellNavigationState, IDictionary{string, object})"/>
    /// so MAUI resolves the route (registered routes, "..", "//absolute"),
    /// applies query attributes and raises its navigation events; the
    /// platform then mirrors the result. The built-in router below only
    /// serves shells assembled without a MAUI Shell.
    /// </summary>
    public void GoToAsync(string route, IDictionary<string, object>? parameters)
    {
        if (string.IsNullOrEmpty(route)) return;

        if (_mauiShell != null)
        {
            var state = new ShellNavigationState(route);
            var task = parameters != null
                ? _mauiShell.GoToAsync(state, parameters)
                : _mauiShell.GoToAsync(state);
            task.ContinueWith(
                t => DiagnosticLog.Error("SkiaShell", $"GoToAsync('{route}') failed", t.Exception!),
                TaskContinuationOptions.OnlyOnFaulted);
            return;
        }

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
    /// Pushes a new page onto the navigation stack. <paramref name="mauiPage"/>
    /// is the MAUI page the view renders (when known) — it receives Appearing,
    /// and the page it covers receives Disappearing.
    /// </summary>
    public void PushAsync(SkiaView page, string title, Microsoft.Maui.Controls.Page? mauiPage = null)
    {
        // Save current content to stack
        if (_currentContent != null)
        {
            _navigationStack.Add(new NavigationEntry(_currentContent, Title, _lifecyclePage));
        }

        // Set new content
        SetCurrentContent(page);
        Title = title;
        SendPageLifecycle(mauiPage);
        Invalidate();
    }

    /// <summary>
    /// Pops the current page from the navigation stack.
    /// </summary>
    public bool PopAsync()
    {
        if (_navigationStack.Count == 0) return false;

        var previous = _navigationStack[^1];
        _navigationStack.RemoveAt(_navigationStack.Count - 1);
        SetCurrentContent(previous.Content);
        Title = previous.Title;
        SendPageLifecycle(previous.MauiPage);
        Invalidate();
        return true;
    }

    /// <summary>
    /// Pops all pages from the navigation stack, returning to the root.
    /// </summary>
    public void PopToRootAsync()
    {
        if (_navigationStack.Count == 0) return;

        var root = _navigationStack[0];
        _navigationStack.Clear();

        SetCurrentContent(root.Content);
        Title = root.Title ?? string.Empty;
        SendPageLifecycle(root.MauiPage);
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
            float contentBottom = IsTabBarShown ? TabBarHeight : 0;
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
            float contentBottom = (float)bounds.Bottom - (IsTabBarShown ? TabBarHeight : 0) - ContentPadding;
            var contentBounds = new Rect(
                bounds.Left + flyoutOffset + ContentPadding,
                contentTop,
                bounds.Width - flyoutOffset - ContentPadding * 2,
                contentBottom - contentTop);
            _currentContent.Arrange(contentBounds);
        }

        return bounds;
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        canvas.Save();
        canvas.ClipRect(bounds);

        bool isLocked = FlyoutBehavior == ShellFlyoutBehavior.Locked;

        // Fill the content area with a theme-appropriate background BEFORE drawing
        // the page content. The Skia surface is cleared to transparent each frame,
        // so without this any pixels the page doesn't paint (e.g. ContentPage with
        // no explicit BackgroundColor) end up transparent and the compositor blends
        // through to the desktop / windows behind. Pages that DO set a background
        // simply paint over the fill, so this is non-destructive.
        var contentBg = SkiaTheme.IsDarkMode ? SkiaTheme.DarkBackgroundSK : _contentBackgroundColorSK;
        using (var bgPaint = new SKPaint { Color = contentBg, Style = SKPaintStyle.Fill })
        {
            canvas.DrawRect(bounds, bgPaint);
        }

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

        // Draw tab bar: MAUI's sections with a MAUI Shell attached, else the section's contents.
        if (IsTabBarShown)
        {
            if (_mauiShell != null)
                DrawMauiTabBar(canvas, bounds);
            else
                DrawTabBar(canvas, bounds);
        }
        else
        {
            _tabHits.Clear();
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

        if (IsBackButtonVisible)
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
        using var titleFont = SkiaFontFactory.Create(20f);
        titleFont.Embolden = true;
        using var titlePaint = new SKPaint
        {
            Color = _navBarTextColorSK,
            IsAntialias = true
        };

        float titleRight = DrawToolbarItems(canvas, navBarBounds);

        float titleX = (IsBackButtonVisible || (FlyoutBehavior == ShellFlyoutBehavior.Flyout && FlyoutBehavior != ShellFlyoutBehavior.Locked)) ? navBarBounds.Left + 56 : navBarBounds.Left + 16;
        float titleY = navBarBounds.MidY + 6;
        // The title stops short of the toolbar items, as on the other platforms; a TitleView
        // takes the title's place.
        var titleSlot = new SKRect(titleX, navBarBounds.Top, Math.Max(titleX, titleRight - 8), navBarBounds.Bottom);
        if (DrawTitleView(canvas, titleSlot))
            return;
        canvas.Save();
        canvas.ClipRect(titleSlot);
        canvas.DrawText(Title, titleX, titleY, titleFont, titlePaint);
        canvas.Restore();
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

        using var textFont = SkiaFontFactory.Create(12f);
        using var textPaint = new SKPaint
        {
            IsAntialias = true
        };

        for (int i = 0; i < section.Items.Count; i++)
        {
            var item = section.Items[i];
            bool isSelected = i == _selectedItemIndex;

            textPaint.Color = isSelected ? _navBarBackgroundColorSK : SkiaTheme.TextTertiarySK;

            textFont.MeasureText(item.Title, out var textBounds);

            float textX = tabBarBounds.Left + i * tabWidth + tabWidth / 2 - textBounds.MidX;
            float textY = TextRenderingHelper.BaselineForVerticalCenter(textFont, tabBarBounds.MidY);

            canvas.DrawText(item.Title, textX, textY, textFont, textPaint);
        }
    }

    private const float DefaultFlyoutRowHeight = 48f;

    /// <summary>A flyout row's height: a templated row's measured height, else the built-in 48.</summary>
    private static float FlyoutRowHeight(ShellSection section, float width)
    {
        if (section.TemplateView is not { } view)
            return DefaultFlyoutRowHeight;
        var desired = view.Measure(new Size(width, double.PositiveInfinity));
        return desired.Height > 0 && !double.IsInfinity(desired.Height) ? (float)desired.Height : DefaultFlyoutRowHeight;
    }

    private float FlyoutItemsHeight(float width)
    {
        float total = 0;
        foreach (var section in _sections)
            if (section.IsVisibleInFlyout)
                total += FlyoutRowHeight(section, width);
        return total;
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
        float headerHeight = ResolveFlyoutHeaderHeight(flyoutBounds.Width, flyoutBounds.Height);
        float footerHeight = ResolveFlyoutFooterHeight(flyoutBounds.Width);

        // Draw flyout header if present
        if (FlyoutHeaderView != null)
        {
            var headerBounds = new SKRect(flyoutBounds.Left, flyoutBounds.Top, flyoutBounds.Right, flyoutBounds.Top + headerHeight);
            FlyoutHeaderView.Measure(new Size(headerBounds.Width, headerBounds.Height));
            FlyoutHeaderView.Arrange(new Rect(headerBounds.Left, headerBounds.Top, headerBounds.Width, headerBounds.Height));

            // If the header view has a BackgroundColor, draw it over the flyout background
            // to ensure it covers the default flyout color (e.g. white) in the header region
            if (FlyoutHeaderView.BackgroundColor != null && FlyoutHeaderView.BackgroundColor != Colors.Transparent)
            {
                using var headerBgPaint = new SKPaint
                {
                    Color = FlyoutHeaderView.BackgroundColor.ToSKColor(),
                    Style = SKPaintStyle.Fill
                };
                canvas.DrawRect(headerBounds, headerBgPaint);
            }

            FlyoutHeaderView.Draw(canvas);
        }

        // Draw flyout items with scrolling support
        float itemsAreaTop = flyoutBounds.Top + headerHeight;
        float itemsAreaBottom = flyoutBounds.Bottom - footerHeight;

        // Flyout content replaces the item list.
        if (FlyoutContentView is { } flyoutContent)
        {
            var contentRect = new Rect(flyoutBounds.Left, itemsAreaTop, flyoutBounds.Width, Math.Max(0, itemsAreaBottom - itemsAreaTop));
            flyoutContent.Measure(new Size(contentRect.Width, contentRect.Height));
            flyoutContent.Arrange(contentRect);
            canvas.Save();
            canvas.ClipRect(new SKRect(flyoutBounds.Left, itemsAreaTop, flyoutBounds.Right, itemsAreaBottom));
            flyoutContent.Draw(canvas);
            canvas.Restore();
            DrawFlyoutFooter(canvas, flyoutBounds, footerHeight);
            return;
        }

        // Clip to items area (between header and footer)
        canvas.Save();
        canvas.ClipRect(new SKRect(flyoutBounds.Left, itemsAreaTop, flyoutBounds.Right, itemsAreaBottom));

        // Apply scroll offset
        float itemY = itemsAreaTop - _flyoutScrollOffset;

        using var itemTextFont = SkiaFontFactory.Create(14f);
        using var itemTextPaint = new SKPaint
        {
            IsAntialias = true
        };

        for (int i = 0; i < _sections.Count; i++)
        {
            var section = _sections[i];
            if (!section.IsVisibleInFlyout) continue;
            bool isSelected = i == _selectedSectionIndex;
            float itemHeight = FlyoutRowHeight(section, flyoutBounds.Width);

            // Skip items that are scrolled above the visible area
            if (itemY + itemHeight < itemsAreaTop)
            {
                itemY += itemHeight;
                continue;
            }

            // Stop if we're below the visible area
            if (itemY > itemsAreaBottom)
                break;

            // A templated row draws itself, selection included (it binds IsChecked).
            if (section.TemplateView is { } rowView)
            {
                rowView.Arrange(new Rect(flyoutBounds.Left, itemY, flyoutBounds.Width, itemHeight));
                rowView.Draw(canvas);
                itemY += itemHeight;
                continue;
            }

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
            var icon = section.IconSource != null ? GetFlyoutIcon(section.IconSource) : GetFlyoutIcon(section.IconPath);
            if (icon != null)
            {
                float iconSize = 24f;
                float iconX = flyoutBounds.Left + 16;
                float iconY = itemY + (itemHeight - iconSize) / 2;
                canvas.DrawBitmap(icon, new SKRect(iconX, iconY, iconX + iconSize, iconY + iconSize));
                textStartX = iconX + iconSize + 12; // gap between icon and text
            }

            canvas.DrawText(section.Title, textStartX, itemY + 30, itemTextFont, itemTextPaint);

            itemY += itemHeight;
        }

        canvas.Restore();

        DrawFlyoutFooter(canvas, flyoutBounds, footerHeight);
    }

    private void DrawFlyoutFooter(SKCanvas canvas, SKRect flyoutBounds, float footerHeight)
    {
        // Draw flyout footer (footerHeight already measured to natural size above)
        if (FlyoutFooterView != null)
        {
            var footerBounds = new SKRect(flyoutBounds.Left, flyoutBounds.Bottom - footerHeight, flyoutBounds.Right, flyoutBounds.Bottom);
            FlyoutFooterView.Arrange(new Rect(footerBounds.Left, footerBounds.Top, footerBounds.Width, footerHeight));
            FlyoutFooterView.Draw(canvas);
        }
        else if (!string.IsNullOrEmpty(FlyoutFooterText) && FlyoutContentView == null)
        {
            // Fallback: draw simple text footer
            using var footerFont = SkiaFontFactory.Create(12f);
            using var footerPaint = new SKPaint
            {
                Color = _flyoutTextColorSK.WithAlpha(180),
                IsAntialias = true
            };
            var footerY = flyoutBounds.Bottom - footerHeight / 2 + 4;
            canvas.DrawText(FlyoutFooterText, flyoutBounds.Left + 16, footerY, footerFont, footerPaint);
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
                // Check footer view for hit testing (buttons, etc.)
                if (FlyoutFooterView != null)
                {
                    var footerHit = FlyoutFooterView.HitTestAt(x, y);
                    if (footerHit != null) return footerHit;
                }

                // Check header view for hit testing
                if (FlyoutHeaderView != null)
                {
                    var headerHit = FlyoutHeaderView.HitTestAt(x, y);
                    if (headerHit != null) return headerHit;
                }

                // Flyout content takes the item list's place, and its input.
                if (FlyoutContentView != null)
                {
                    var contentHit = FlyoutContentView.HitTestAt(x, y);
                    if (contentHit != null) return contentHit;
                }

                return this; // Flyout handles its own hits (menu items)
            }

            // Tap on scrim closes flyout (non-locked only)
            if (FlyoutIsPresented && !isLockedHit)
            {
                return this;
            }
        }

        // Check nav bar: its TitleView takes its own input.
        if (NavBarIsVisible && y < (float)Bounds.Top + NavBarHeight)
        {
            if (_titleView != null && _titleViewBounds.Contains(x, y) && _titleView.HitTestAt(x, y) is { } titleHit)
                return titleHit;
            return this;
        }

        // Check tab bar
        if (IsTabBarShown && y > (float)Bounds.Bottom - TabBarHeight)
        {
            return this;
        }

        // Check content
        if (_currentContent != null)
        {
            var hit = _currentContent.HitTestAt(x, y);
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
                float headerHeight = ResolveFlyoutHeaderHeight(flyoutBounds.Width, flyoutBounds.Height);
                float footerHeight = ResolveFlyoutFooterHeight(flyoutBounds.Width);

                float itemsAreaTop = flyoutBounds.Top + headerHeight;
                float itemsAreaBottom = flyoutBounds.Bottom - footerHeight;

                // Check footer area — dispatch to footer view for button clicks
                if (e.Y >= itemsAreaBottom && FlyoutFooterView != null)
                {
                    var footerHit = FlyoutFooterView.HitTestAt(e.X, e.Y);
                    if (footerHit != null)
                    {
                        _pressedTarget = footerHit;
                        footerHit.OnPointerPressed(e);
                        return;
                    }
                }

                // Check header area — dispatch to header view
                if (e.Y < itemsAreaTop && FlyoutHeaderView != null)
                {
                    var headerHit = FlyoutHeaderView.HitTestAt(e.X, e.Y);
                    if (headerHit != null)
                    {
                        _pressedTarget = headerHit;
                        headerHit.OnPointerPressed(e);
                        return;
                    }
                }

                // Only check items if tap is in items area
                if (FlyoutContentView == null && e.Y >= itemsAreaTop && e.Y < itemsAreaBottom)
                {
                    // Apply scroll offset to find which item was tapped
                    float itemY = itemsAreaTop - _flyoutScrollOffset;

                    for (int i = 0; i < _sections.Count; i++)
                    {
                        if (!_sections[i].IsVisibleInFlyout) continue;
                        float itemHeight = FlyoutRowHeight(_sections[i], flyoutBounds.Width);
                        if (e.Y >= itemY && e.Y < itemY + itemHeight)
                        {
                            SelectSection(i, 0);
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

        if (NavBarIsVisible && e.Y < Bounds.Top + NavBarHeight && TryPressToolbarItem(e.X, e.Y))
        {
            e.Handled = true;
            return;
        }

        // Check nav bar icon tap (back button or hamburger menu)
        // The nav bar starts after a locked flyout (a rail), as DrawNavBar draws it; the
        // back arrow's hit area moved with it (it only ever matched with no rail).
        float navLeft = (float)Bounds.Left + (FlyoutBehavior == ShellFlyoutBehavior.Locked ? FlyoutWidth : 0);
        if (NavBarIsVisible && e.Y < Bounds.Top + NavBarHeight && e.X >= navLeft && e.X < navLeft + 56)
        {
            if (IsBackButtonVisible)
            {
                // Back button pressed: the page's BackButtonBehavior.Command replaces
                // the navigation when it has one (MAUI's ShellToolbar).
                if (_backButtonBehavior?.Command is { } command)
                {
                    if (_backButtonBehavior.IsEnabled && command.CanExecute(_backButtonBehavior.CommandParameter))
                        command.Execute(_backButtonBehavior.CommandParameter);
                }
                else
                {
                    GoBack();
                }
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
        if (_mauiShell != null && IsTabBarShown && e.Y > (float)Bounds.Bottom - TabBarHeight)
        {
            TryPressMauiTab(e.X, e.Y);
            e.Handled = true;
            return;
        }
        if (_mauiShell == null && TabBarIsVisible && e.Y > (float)Bounds.Bottom - TabBarHeight)
        {
            if (_selectedSectionIndex >= 0 && _selectedSectionIndex < _sections.Count)
            {
                var section = _sections[_selectedSectionIndex];
                float tabWidth = (float)Bounds.Width / section.Items.Count;
                int tappedIndex = (int)((e.X - (float)Bounds.Left) / tabWidth);
                tappedIndex = Math.Clamp(tappedIndex, 0, section.Items.Count - 1);

                if (tappedIndex != _selectedItemIndex)
                {
                    SelectSection(_selectedSectionIndex, tappedIndex);
                }
                e.Handled = true;
                return;
            }
        }

        base.OnPointerPressed(e);
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        if (_pressedTarget != null)
        {
            _pressedTarget.OnPointerReleased(e);
            _pressedTarget = null;
            return;
        }

        base.OnPointerReleased(e);
    }

    public override void OnScroll(ScrollEventArgs e)
    {
        if (FlyoutIsPresented && _flyoutAnimationProgress > 0)
        {
            float flyoutX = (float)Bounds.Left - FlyoutWidth + (FlyoutWidth * _flyoutAnimationProgress);
            var flyoutBounds = new SKRect(flyoutX, (float)Bounds.Top, flyoutX + FlyoutWidth, (float)Bounds.Bottom);

            if (flyoutBounds.Contains(e.X, e.Y))
            {
                float headerHeight = ResolveFlyoutHeaderHeight(flyoutBounds.Width, flyoutBounds.Height);
                float footerHeight = ResolveFlyoutFooterHeight(flyoutBounds.Width);
                float totalItemsHeight = FlyoutItemsHeight(flyoutBounds.Width);
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
    /// The item's icon as MAUI declares it (a file, font, URI or stream image, or an app's own
    /// source), loaded through its image-source service; when null, <see cref="IconPath"/> is used.
    /// </summary>
    public Microsoft.Maui.Controls.ImageSource? IconSource { get; set; }

    /// <summary>
    /// False for items declared with <c>FlyoutItemIsVisible="False"</c>: still
    /// navigable by route, not listed in the flyout.
    /// </summary>
    public bool IsVisibleInFlyout { get; set; } = true;

    /// <summary>
    /// The flyout row realised from <c>Shell.ItemTemplate</c> (bound to the
    /// shell item), drawn in place of the built-in icon and title; null for the
    /// built-in row.
    /// </summary>
    public SkiaView? TemplateView { get; set; }

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
