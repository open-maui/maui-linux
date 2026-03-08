// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Hosting;

/// <summary>
/// Renders MAUI views to Skia platform views.
/// Handles the conversion of the view hierarchy.
/// </summary>
public class LinuxViewRenderer
{
    private readonly IMauiContext _mauiContext;

    /// <summary>
    /// Static reference to the current MAUI Shell for navigation support.
    /// Used when Shell.Current is not available through normal lifecycle.
    /// </summary>
    public static Shell? CurrentMauiShell { get; private set; }

    /// <summary>
    /// Static reference to the current SkiaShell for navigation updates.
    /// </summary>
    public static SkiaShell? CurrentSkiaShell { get; private set; }

    /// <summary>
    /// Navigate to a route using the SkiaShell directly.
    /// Use this instead of Shell.Current.GoToAsync on Linux.
    /// </summary>
    /// <param name="route">The route to navigate to (e.g., "Buttons" or "//Buttons")</param>
    /// <returns>True if navigation succeeded</returns>
    public static bool NavigateToRoute(string route)
    {
        if (CurrentSkiaShell == null)
        {
            DiagnosticLog.Warn("LinuxViewRenderer", "CurrentSkiaShell is null");
            return false;
        }

        // Clean up the route - remove leading // or /
        var cleanRoute = route.TrimStart('/');
        DiagnosticLog.Debug("LinuxViewRenderer", $"NavigateToRoute: Navigating to: {cleanRoute}");

        for (int i = 0; i < CurrentSkiaShell.Sections.Count; i++)
        {
            var section = CurrentSkiaShell.Sections[i];
            if (section.Route.Equals(cleanRoute, StringComparison.OrdinalIgnoreCase) ||
                section.Title.Equals(cleanRoute, StringComparison.OrdinalIgnoreCase))
            {
                DiagnosticLog.Debug("LinuxViewRenderer", $"NavigateToRoute: Found section {i}: {section.Title}");
                CurrentSkiaShell.NavigateToSection(i);
                return true;
            }
        }

        DiagnosticLog.Warn("LinuxViewRenderer", $"NavigateToRoute: Route not found: {cleanRoute}");
        return false;
    }

    /// <summary>
    /// Current renderer instance for page rendering.
    /// </summary>
    public static LinuxViewRenderer? CurrentRenderer { get; set; }

    /// <summary>
    /// Pushes a page onto the navigation stack.
    /// </summary>
    /// <param name="page">The page to push</param>
    /// <returns>True if successful</returns>
    public static bool PushPage(Page page)
    {
        DiagnosticLog.Debug("LinuxViewRenderer", $"PushPage: Pushing page: {page.GetType().Name}");

        if (CurrentSkiaShell == null)
        {
            DiagnosticLog.Warn("LinuxViewRenderer", "PushPage: CurrentSkiaShell is null");
            return false;
        }

        if (CurrentRenderer == null)
        {
            DiagnosticLog.Warn("LinuxViewRenderer", "PushPage: CurrentRenderer is null");
            return false;
        }

        try
        {
            // Render the page through the proper handler system
            // This ensures all properties (including BackgroundColor via AppThemeBinding) are mapped
            var skiaPage = CurrentRenderer.RenderPage(page);

            if (skiaPage == null)
            {
                DiagnosticLog.Warn("LinuxViewRenderer", "PushPage: Failed to render page through handler");
                return false;
            }

            // Push onto SkiaShell's navigation stack
            CurrentSkiaShell.PushAsync(skiaPage, page.Title ?? "Detail");
            DiagnosticLog.Debug("LinuxViewRenderer", "PushPage: Successfully pushed page via handler system");
            return true;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("LinuxViewRenderer", "PushPage failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Pops the current page from the navigation stack.
    /// </summary>
    /// <returns>True if successful</returns>
    public static bool PopPage()
    {
        DiagnosticLog.Debug("LinuxViewRenderer", "PopPage: Popping page");

        if (CurrentSkiaShell == null)
        {
            DiagnosticLog.Warn("LinuxViewRenderer", "PopPage: CurrentSkiaShell is null");
            return false;
        }

        return CurrentSkiaShell.PopAsync();
    }

    public LinuxViewRenderer(IMauiContext mauiContext)
    {
        _mauiContext = mauiContext ?? throw new ArgumentNullException(nameof(mauiContext));
        // Store reference for push/pop navigation
        CurrentRenderer = this;
    }

    /// <summary>
    /// Renders a MAUI page and returns the corresponding SkiaView.
    /// </summary>
    public SkiaView? RenderPage(Page page)
    {
        if (page == null)
            return null;

        // Special handling for Shell - Shell is our navigation container
        if (page is Shell shell)
        {
            return RenderShell(shell);
        }

        // Set handler context
        page.Handler?.DisconnectHandler();
        var handler = page.ToHandler(_mauiContext);

        // The handler's property mappers (e.g., ContentPageHandler.MapContent)
        // already set up the content and child handlers - no need to re-render here.
        // Re-rendering would disconnect the existing handler hierarchy.
        if (handler.PlatformView is SkiaView skiaPage)
        {
            return skiaPage;
        }

        return null;
    }

    /// <summary>
    /// Renders a MAUI Shell with all its navigation structure.
    /// </summary>
    private SkiaShell RenderShell(Shell shell)
    {
        // Store reference for navigation - Shell.Current is computed from Application.Current.Windows
        // Our platform handles navigation through SkiaShell directly
        CurrentMauiShell = shell;

        var skiaShell = new SkiaShell
        {
            Title = shell.Title ?? "App",
            FlyoutBehavior = shell.FlyoutBehavior switch
            {
                FlyoutBehavior.Flyout => ShellFlyoutBehavior.Flyout,
                FlyoutBehavior.Locked => ShellFlyoutBehavior.Locked,
                FlyoutBehavior.Disabled => ShellFlyoutBehavior.Disabled,
                _ => ShellFlyoutBehavior.Flyout
            },
            MauiShell = shell
        };

        // Apply shell colors based on theme
        ApplyShellColors(skiaShell, shell);

        // Render flyout header if present
        if (shell.FlyoutHeader is View headerView)
        {
            var skiaHeader = RenderView(headerView);
            if (skiaHeader != null)
            {
                skiaShell.FlyoutHeaderView = skiaHeader;
                skiaShell.FlyoutHeaderHeight = (float)(headerView.HeightRequest > 0 ? headerView.HeightRequest : 140.0);
            }
        }

        // Render flyout footer if present, otherwise use version text
        if (shell.FlyoutFooter is View footerView)
        {
            var skiaFooter = RenderView(footerView);
            if (skiaFooter != null)
            {
                skiaShell.FlyoutFooterView = skiaFooter;
                skiaShell.FlyoutFooterHeight = (float)(footerView.HeightRequest > 0 ? footerView.HeightRequest : 40.0);
            }
        }
        else
        {
            // Fallback: use assembly version as footer text
            var version = Assembly.GetEntryAssembly()?.GetName().Version;
            skiaShell.FlyoutFooterText = $"Version {version?.Major ?? 1}.{version?.Minor ?? 0}.{version?.Build ?? 0}";
        }

        // Process shell items into sections
        foreach (var item in shell.Items)
        {
            ProcessShellItem(skiaShell, item);
        }

        // Store reference to SkiaShell for navigation
        CurrentSkiaShell = skiaShell;

        // Set up content renderer, color refresher, and icon syncer delegates
        skiaShell.ContentRenderer = CreateShellContentPage;
        skiaShell.ColorRefresher = ApplyShellColors;
        skiaShell.IconSyncer = s => SyncFlyoutIcons(s, shell);

        // Subscribe to MAUI Shell navigation events to update SkiaShell
        shell.Navigated += OnShellNavigated;
        shell.Navigating += (s, e) => DiagnosticLog.Debug("LinuxViewRenderer", $"Navigation: Navigating: {e.Target}");

        DiagnosticLog.Debug("LinuxViewRenderer", $"Shell navigation events subscribed. Sections: {skiaShell.Sections.Count}");
        for (int i = 0; i < skiaShell.Sections.Count; i++)
        {
            DiagnosticLog.Debug("LinuxViewRenderer", $"Section {i}: Route='{skiaShell.Sections[i].Route}', Title='{skiaShell.Sections[i].Title}'");
        }

        return skiaShell;
    }

    /// <summary>
    /// Applies shell colors based on the current theme (dark/light mode).
    /// </summary>
    private static void SyncFlyoutIcons(SkiaShell skiaShell, Shell shell)
    {
        int sectionIndex = 0;
        foreach (var item in shell.Items)
        {
            if (sectionIndex >= skiaShell.Sections.Count) break;

            string? iconPath = item.Icon?.ToString();
            skiaShell.Sections[sectionIndex].IconPath = iconPath;
            sectionIndex++;
        }
    }

    private static void ApplyShellColors(SkiaShell skiaShell, Shell shell)
    {
        bool isDark = Application.Current?.UserAppTheme == AppTheme.Dark;
        DiagnosticLog.Debug("LinuxViewRenderer", $"ApplyShellColors: Theme is: {(isDark ? "Dark" : "Light")}");

        // Look up theme resource colors from the Application's resources.
        // This is more reliable than reading shell.FlyoutBackgroundColor during
        // theme changes because AppThemeBinding may not have re-evaluated yet.
        var resources = Application.Current?.Resources;

        // Flyout background color
        Color? flyoutBg = TryGetResourceColor(resources, isDark ? "FlyoutBackgroundDark" : "FlyoutBackgroundLight");
        if (flyoutBg == null)
        {
            // Try the shell property (works for initial setup before theme changes)
            flyoutBg = shell.FlyoutBackgroundColor;
        }
        if (flyoutBg != null && flyoutBg != Colors.Transparent)
        {
            skiaShell.FlyoutBackgroundColor = flyoutBg;
        }
        else
        {
            skiaShell.FlyoutBackgroundColor = isDark
                ? Color.FromRgb(30, 30, 30)
                : Color.FromRgb(255, 255, 255);
        }
        DiagnosticLog.Debug("LinuxViewRenderer", $"ApplyShellColors: FlyoutBackgroundColor: {skiaShell.FlyoutBackgroundColor}");

        // Flyout text color
        Color? fgColor = TryGetResourceColor(resources, isDark ? "TextPrimaryDark" : "TextPrimaryLight");
        if (fgColor != null && fgColor != Colors.Transparent)
        {
            skiaShell.FlyoutTextColor = fgColor;
        }
        else
        {
            skiaShell.FlyoutTextColor = isDark
                ? Color.FromRgb(224, 224, 224)
                : Color.FromRgb(33, 33, 33);
        }
        DiagnosticLog.Debug("LinuxViewRenderer", $"ApplyShellColors: FlyoutTextColor: {skiaShell.FlyoutTextColor}");

        // Content background color
        skiaShell.ContentBackgroundColor = isDark
            ? Color.FromRgb(18, 18, 18)
            : Color.FromRgb(250, 250, 250);

        // NavBar background color
        Color? navBg = TryGetResourceColor(resources, isDark ? "ShellBackgroundDark" : "ShellBackgroundLight");
        if (navBg == null)
        {
            navBg = shell.BackgroundColor;
        }
        if (navBg != null && navBg != Colors.Transparent)
        {
            skiaShell.NavBarBackgroundColor = navBg;
        }
        else
        {
            skiaShell.NavBarBackgroundColor = Color.FromRgb(33, 150, 243); // Material blue
        }

        // NavBar text color
        if (fgColor != null && fgColor != Colors.Transparent)
        {
            skiaShell.NavBarTextColor = fgColor;
        }
    }

    /// <summary>
    /// Tries to resolve a Color from the app's merged resource dictionaries.
    /// </summary>
    private static Color? TryGetResourceColor(ResourceDictionary? resources, string key)
    {
        if (resources == null) return null;

        // Check main dictionary
        if (resources.TryGetValue(key, out var value) && value is Color color)
            return color;

        // Check merged dictionaries
        foreach (var merged in resources.MergedDictionaries)
        {
            if (merged.TryGetValue(key, out var mergedValue) && mergedValue is Color mergedColor)
                return mergedColor;
        }

        return null;
    }

    /// <summary>
    /// Handles MAUI Shell navigation events and updates SkiaShell accordingly.
    /// </summary>
    private static void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        DiagnosticLog.Debug("LinuxViewRenderer", $"OnShellNavigated called - Source: {e.Source}, Current: {e.Current?.Location}, Previous: {e.Previous?.Location}");

        if (CurrentSkiaShell == null || CurrentMauiShell == null)
        {
            DiagnosticLog.Warn("LinuxViewRenderer", "CurrentSkiaShell or CurrentMauiShell is null");
            return;
        }

        // Get the current route from the Shell
        var currentState = CurrentMauiShell.CurrentState;
        var location = currentState?.Location?.OriginalString ?? "";
        DiagnosticLog.Debug("LinuxViewRenderer", $"Navigation: Location: {location}, Sections: {CurrentSkiaShell.Sections.Count}");

        // Find the matching section in SkiaShell by route
        for (int i = 0; i < CurrentSkiaShell.Sections.Count; i++)
        {
            var section = CurrentSkiaShell.Sections[i];
            DiagnosticLog.Debug("LinuxViewRenderer", $"Navigation: Checking section {i}: Route='{section.Route}', Title='{section.Title}'");
            if (!string.IsNullOrEmpty(section.Route) && location.Contains(section.Route, StringComparison.OrdinalIgnoreCase))
            {
                DiagnosticLog.Debug("LinuxViewRenderer", $"Navigation: Match found by route! Navigating to section {i}");
                if (i != CurrentSkiaShell.CurrentSectionIndex)
                {
                    CurrentSkiaShell.NavigateToSection(i);
                }
                return;
            }
            if (!string.IsNullOrEmpty(section.Title) && location.Contains(section.Title, StringComparison.OrdinalIgnoreCase))
            {
                DiagnosticLog.Debug("LinuxViewRenderer", $"Navigation: Match found by title! Navigating to section {i}");
                if (i != CurrentSkiaShell.CurrentSectionIndex)
                {
                    CurrentSkiaShell.NavigateToSection(i);
                }
                return;
            }
        }
        DiagnosticLog.Warn("LinuxViewRenderer", $"Navigation: No matching section found for location: {location}");
    }

    /// <summary>
    /// Process a ShellItem (FlyoutItem, TabBar, etc.) into SkiaShell sections.
    /// </summary>
    private void ProcessShellItem(SkiaShell skiaShell, ShellItem item)
    {
        if (item is FlyoutItem flyoutItem)
        {
            // Each FlyoutItem becomes a section
            var section = new ShellSection
            {
                Title = flyoutItem.Title ?? "",
                Route = flyoutItem.Route ?? flyoutItem.Title ?? ""
            };

            // Process the items within the FlyoutItem
            foreach (var shellSection in flyoutItem.Items)
            {
                foreach (var content in shellSection.Items)
                {
                    var shellContent = new ShellContent
                    {
                        Title = content.Title ?? shellSection.Title ?? flyoutItem.Title ?? "",
                        Route = content.Route ?? "",
                        MauiShellContent = content
                    };

                    // Create the page content
                    var pageContent = CreateShellContentPage(content);
                    if (pageContent != null)
                    {
                        shellContent.Content = pageContent;
                    }

                    section.Items.Add(shellContent);
                }
            }

            // If there's only one item, use it as the main section content
            if (section.Items.Count == 1)
            {
                section.Title = section.Items[0].Title;
            }

            skiaShell.AddSection(section);
        }
        else if (item is TabBar tabBar)
        {
            // TabBar items get their own sections
            foreach (var tab in tabBar.Items)
            {
                var section = new ShellSection
                {
                    Title = tab.Title ?? "",
                    Route = tab.Route ?? ""
                };

                foreach (var content in tab.Items)
                {
                    var shellContent = new ShellContent
                    {
                        Title = content.Title ?? tab.Title ?? "",
                        Route = content.Route ?? "",
                        MauiShellContent = content
                    };

                    var pageContent = CreateShellContentPage(content);
                    if (pageContent != null)
                    {
                        shellContent.Content = pageContent;
                    }

                    section.Items.Add(shellContent);
                }

                skiaShell.AddSection(section);
            }
        }
        else
        {
            // Generic ShellItem
            var section = new ShellSection
            {
                Title = item.Title ?? "",
                Route = item.Route ?? ""
            };

            foreach (var shellSection in item.Items)
            {
                foreach (var content in shellSection.Items)
                {
                    var shellContent = new ShellContent
                    {
                        Title = content.Title ?? "",
                        Route = content.Route ?? "",
                        MauiShellContent = content
                    };

                    var pageContent = CreateShellContentPage(content);
                    if (pageContent != null)
                    {
                        shellContent.Content = pageContent;
                    }

                    section.Items.Add(shellContent);
                }
            }

            skiaShell.AddSection(section);
        }
    }

    /// <summary>
    /// Creates the page content for a ShellContent.
    /// </summary>
    private SkiaView? CreateShellContentPage(Controls.ShellContent content)
    {
        try
        {
            // Try to create the page from the content template
            Page? page = null;

            if (content.ContentTemplate != null)
            {
                page = content.ContentTemplate.CreateContent() as Page;
            }

            if (page == null && content.Content is Page contentPage)
            {
                page = contentPage;
            }

            if (page is ContentPage cp && cp.Content != null)
            {
                // Wrap in a scroll view if not already scrollable
                var contentView = RenderView(cp.Content);
                if (contentView != null)
                {
                    // Get page background color if set
                    Color? bgColor = null;
                    if (cp.BackgroundColor != null && cp.BackgroundColor != Colors.Transparent)
                    {
                        bgColor = cp.BackgroundColor;
                        DiagnosticLog.Debug("LinuxViewRenderer", $"CreateShellContentPage: Page BackgroundColor: {bgColor}");
                    }

                    if (contentView is SkiaScrollView scrollView)
                    {
                        if (bgColor != null)
                        {
                            scrollView.BackgroundColor = bgColor;
                        }
                        return scrollView;
                    }
                    else
                    {
                        var newScrollView = new SkiaScrollView
                        {
                            Content = contentView
                        };
                        if (bgColor != null)
                        {
                            newScrollView.BackgroundColor = bgColor;
                        }
                        return newScrollView;
                    }
                }
            }
        }
        catch (Exception)
        {
            // Silently handle template creation errors
        }

        return null;
    }

    /// <summary>
    /// Renders a MAUI view and returns the corresponding SkiaView.
    /// </summary>
    public SkiaView? RenderView(IView view)
    {
        if (view == null)
            return null;

        try
        {
            // Disconnect any existing handler
            if (view is Element element && element.Handler != null)
            {
                element.Handler.DisconnectHandler();
            }

            // Create handler for the view
            // The handler's ConnectHandler and property mappers handle child views automatically
            var handler = view.ToHandler(_mauiContext);

            if (handler?.PlatformView is not SkiaView skiaView)
            {
                // If no Skia handler, create a fallback
                return CreateFallbackView(view);
            }

            // Handlers manage their own children via ConnectHandler and property mappers
            // No manual child rendering needed here - that caused "View already has a parent" errors
            return skiaView;
        }
        catch (Exception)
        {
            return CreateFallbackView(view);
        }
    }

    /// <summary>
    /// Creates a fallback view for unsupported view types.
    /// </summary>
    private SkiaView CreateFallbackView(IView view)
    {
        // For views without handlers, create a placeholder
        return new SkiaLabel
        {
            Text = $"[{view.GetType().Name}]",
            TextColor = Colors.Gray,
            FontSize = 12
        };
    }
}

