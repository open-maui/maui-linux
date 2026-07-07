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
    private Shell? _mauiShell;
    private SkiaShell? _skiaShell;

    /// <summary>
    /// Most recently created renderer's SkiaShell. Used by the theme-refresh
    /// hook and by sample apps for direct navigation; multi-window support
    /// will replace this with a per-window lookup.
    /// </summary>
    public static SkiaShell? CurrentSkiaShell => s_currentSkiaShell;
    private static SkiaShell? s_currentSkiaShell;
    private static LinuxViewRenderer? s_currentRenderer;

    /// <summary>
    /// Navigates the current shell to a route. Pass either a section route
    /// (e.g. "Buttons") or a leading-slash form ("//Buttons"). Returns true if
    /// a matching section was found.
    /// </summary>
    public static bool NavigateToRoute(string route)
    {
        var shell = s_currentSkiaShell;
        if (shell == null)
        {
            DiagnosticLog.Warn("LinuxViewRenderer", "NavigateToRoute: no current shell");
            return false;
        }

        var cleanRoute = route.TrimStart('/');
        for (int i = 0; i < shell.Sections.Count; i++)
        {
            var section = shell.Sections[i];
            if (section.Route.Equals(cleanRoute, StringComparison.OrdinalIgnoreCase) ||
                section.Title.Equals(cleanRoute, StringComparison.OrdinalIgnoreCase))
            {
                shell.NavigateToSection(i);
                return true;
            }
        }
        DiagnosticLog.Warn("LinuxViewRenderer", $"NavigateToRoute: route not found: {cleanRoute}");
        return false;
    }

    /// <summary>
    /// Renders a MAUI Page through the current renderer and pushes it onto
    /// the shell's navigation stack.
    /// </summary>
    public static bool PushPage(Page page)
    {
        var shell = s_currentSkiaShell;
        var renderer = s_currentRenderer;
        if (shell == null || renderer == null)
        {
            DiagnosticLog.Warn("LinuxViewRenderer", "PushPage: no current shell/renderer");
            return false;
        }

        try
        {
            var skiaPage = renderer.RenderPage(page);
            if (skiaPage == null) return false;
            shell.PushAsync(skiaPage, page.Title ?? "Detail", page);
            return true;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("LinuxViewRenderer", "PushPage failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Pops the top page from the current shell's navigation stack.
    /// </summary>
    public static bool PopPage()
    {
        var shell = s_currentSkiaShell;
        if (shell == null)
        {
            DiagnosticLog.Warn("LinuxViewRenderer", "PopPage: no current shell");
            return false;
        }
        return shell.PopAsync();
    }

    public LinuxViewRenderer(IMauiContext mauiContext)
    {
        _mauiContext = mauiContext ?? throw new ArgumentNullException(nameof(mauiContext));
        s_currentRenderer = this;
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
        // Per-instance: each renderer holds its own Shell pair so OnShellNavigated
        // captures the correct context even with multiple windows in the future.
        _mauiShell = shell;

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
                skiaShell.FlyoutFooterHeight = (float)(footerView.HeightRequest > 0 ? footerView.HeightRequest : 120.0);
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

        // Per-instance + the static "current" pointer for the legacy theme-refresh
        // hook in LinuxApplication. Last-write-wins is acceptable today (single
        // window) and explicit so it can be migrated to a per-window registry later.
        _skiaShell = skiaShell;
        s_currentSkiaShell = skiaShell;

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

        // The initial NavigateToSection ran mid-build, possibly before the
        // page's parent chain reached the Window — in which case MAUI's
        // SendAppearing no-op'd. Everything is parented now; re-issue (no-op
        // when the first attempt landed).
        skiaShell.ResendPendingAppearing();

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

        // NavBar background color - check resource, attached property, instance property
        Color? navBg = TryGetResourceColor(resources, isDark ? "ShellBackgroundDark" : "ShellBackgroundLight");
        navBg ??= Shell.GetBackgroundColor(shell);
        navBg ??= shell.BackgroundColor;
        if (navBg != null && navBg != Colors.Transparent)
        {
            skiaShell.NavBarBackgroundColor = navBg;
        }
        else
        {
            // Theme-aware default instead of hardcoded blue
            skiaShell.NavBarBackgroundColor = isDark
                ? Color.FromRgb(30, 30, 30)
                : Color.FromRgb(255, 255, 255);
        }

        // NavBar text color - prefer Shell.TitleColor attached property
        Color? titleColor = Shell.GetTitleColor(shell);
        if (titleColor != null && titleColor != Colors.Transparent)
        {
            skiaShell.NavBarTextColor = titleColor;
        }
        else if (fgColor != null && fgColor != Colors.Transparent)
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
    /// Instance-bound so the captured shell pair always matches the renderer that
    /// subscribed — multi-window safe by construction.
    /// </summary>
    private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        DiagnosticLog.Debug("LinuxViewRenderer", $"OnShellNavigated - Source: {e.Source}, Current: {e.Current?.Location}, Previous: {e.Previous?.Location}");

        var skiaShell = _skiaShell;
        var mauiShell = _mauiShell;
        if (skiaShell == null || mauiShell == null)
        {
            DiagnosticLog.Warn("LinuxViewRenderer", "Shell pair not initialized; ignoring navigation");
            return;
        }

        var location = mauiShell.CurrentState?.Location?.OriginalString ?? "";
        DiagnosticLog.Debug("LinuxViewRenderer", $"Navigation: Location: {location}, Sections: {skiaShell.Sections.Count}");

        for (int i = 0; i < skiaShell.Sections.Count; i++)
        {
            var section = skiaShell.Sections[i];
            if (!string.IsNullOrEmpty(section.Route) && location.Contains(section.Route, StringComparison.OrdinalIgnoreCase))
            {
                if (i != skiaShell.CurrentSectionIndex)
                    skiaShell.NavigateToSection(i);
                return;
            }
            if (!string.IsNullOrEmpty(section.Title) && location.Contains(section.Title, StringComparison.OrdinalIgnoreCase))
            {
                if (i != skiaShell.CurrentSectionIndex)
                    skiaShell.NavigateToSection(i);
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

    // Maps a MAUI ShellContent to the Page instance CreateShellContentPage
    // built for it. Weak on both sides so recycled shell items don't pin pages.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Controls.ShellContent, Page> s_shellContentPages = new();

    // ShellContent.ContentCache (internal) — cached PropertyInfo for parenting
    // created pages into the Shell hierarchy.
    private static System.Reflection.PropertyInfo? s_contentCacheProperty;

    /// <summary>
    /// The Page instance most recently rendered for <paramref name="content"/>,
    /// or null when the content has not been rendered yet.
    /// </summary>
    internal static Page? GetShellContentPage(Controls.ShellContent content) =>
        s_shellContentPages.TryGetValue(content, out var page) ? page : null;

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
                // Extract the type from the DataTemplate via reflection
                // DataTemplate stores the type in a private field
                var typeField = typeof(ElementTemplate).GetProperty("Type",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var templateType = typeField?.GetValue(content.ContentTemplate) as Type;

                // First try to resolve from DI (handles constructor injection)
                if (templateType != null)
                {
                    try
                    {
                        // Use ActivatorUtilities to create with DI - more robust than GetService
                        page = Microsoft.Extensions.DependencyInjection.ActivatorUtilities
                            .CreateInstance(_mauiContext.Services, templateType) as Page;
                    }
                    catch (Exception diEx)
                    {
                        DiagnosticLog.Debug("LinuxViewRenderer", $"DI resolution failed for {templateType.Name}: {diEx.Message}");
                    }
                }

                // Fallback to CreateContent() (uses Activator.CreateInstance)
                if (page == null)
                {
                    page = content.ContentTemplate.CreateContent() as Page;
                }
            }

            if (page == null && content.Content is Page contentPage)
            {
                page = contentPage;
            }

            // Record which Page instance renders this ShellContent. The page is
            // created here (DI/template), never through IShellContentController,
            // so MAUI's own Page cache stays null — SkiaShell resolves through
            // this table to send Appearing/Disappearing. Re-renders (theme
            // refresh) replace the entry so lifecycle always targets the live
            // instance.
            if (page != null)
            {
                // Parent the page into MAUI's Shell hierarchy the way Shell
                // itself does: ShellContent.ContentCache's setter calls
                // AddLogicalChild, which gives the page the Window-rooted
                // parent chain Page.SendAppearing requires — it silently
                // no-ops on unparented pages (Page.cs checks
                // FindParentOfType<IWindow>()?.Parent). Also makes
                // IShellContentController.Page resolve for lifecycle lookups.
                s_contentCacheProperty ??= typeof(Controls.ShellContent).GetProperty("ContentCache",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                try
                {
                    s_contentCacheProperty?.SetValue(content, page);
                }
                catch (Exception cacheEx)
                {
                    DiagnosticLog.Debug("LinuxViewRenderer", $"ContentCache set failed: {cacheEx.Message}");
                }

                s_shellContentPages.Remove(content);
                s_shellContentPages.Add(content, page);
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
        catch (Exception ex)
        {
            DiagnosticLog.Error("LinuxViewRenderer", $"CreateShellContentPage failed for route '{content.Route}': {ex.Message}\n{ex.StackTrace}");
        }

        return null;
    }

    /// <summary>
    /// Renders a MAUI view and returns the corresponding SkiaView.
    /// </summary>
    /// <summary>
    /// Set of view type names known to cause crashes on Linux.
    /// With SKCanvasView/SKGLView native hosting, ContentViewHandler, PathHandler,
    /// and alignment fixes, most third-party controls now render through normal handlers.
    /// Add entries here only as a last resort for controls that truly SIGSEGV.
    /// </summary>
    private static readonly HashSet<string> _unsupportedViewTypes = new(StringComparer.OrdinalIgnoreCase)
    {
    };

    public SkiaView? RenderView(IView view)
    {
        if (view == null)
            return null;

        // Skip known unsupported third-party controls that would segfault
        var typeName = view.GetType().Name;
        if (_unsupportedViewTypes.Contains(typeName))
        {
            DiagnosticLog.Error("LinuxViewRenderer", $"Skipping unsupported view type: {typeName}");
            return CreateFallbackView(view);
        }

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
        catch (Exception ex)
        {
            DiagnosticLog.Error("LinuxViewRenderer", $"RenderView failed for {typeName}: {ex.Message}");
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

