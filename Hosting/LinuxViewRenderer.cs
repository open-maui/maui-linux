// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
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
            Console.WriteLine($"[NavigateToRoute] CurrentSkiaShell is null");
            return false;
        }

        // Clean up the route - remove leading // or /
        var cleanRoute = route.TrimStart('/');
        Console.WriteLine($"[NavigateToRoute] Navigating to: {cleanRoute}");

        for (int i = 0; i < CurrentSkiaShell.Sections.Count; i++)
        {
            var section = CurrentSkiaShell.Sections[i];
            if (section.Route.Equals(cleanRoute, StringComparison.OrdinalIgnoreCase) ||
                section.Title.Equals(cleanRoute, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[NavigateToRoute] Found section {i}: {section.Title}");
                CurrentSkiaShell.NavigateToSection(i);
                return true;
            }
        }

        Console.WriteLine($"[NavigateToRoute] Route not found: {cleanRoute}");
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
        Console.WriteLine($"[PushPage] Pushing page: {page.GetType().Name}");

        if (CurrentSkiaShell == null)
        {
            Console.WriteLine($"[PushPage] CurrentSkiaShell is null");
            return false;
        }

        if (CurrentRenderer == null)
        {
            Console.WriteLine($"[PushPage] CurrentRenderer is null");
            return false;
        }

        try
        {
            // Render the page content
            SkiaView? pageContent = null;
            if (page is ContentPage contentPage && contentPage.Content != null)
            {
                pageContent = CurrentRenderer.RenderView(contentPage.Content);
            }

            if (pageContent == null)
            {
                Console.WriteLine($"[PushPage] Failed to render page content");
                return false;
            }

            // Wrap in ScrollView if needed
            if (pageContent is not SkiaScrollView)
            {
                var scrollView = new SkiaScrollView { Content = pageContent };
                pageContent = scrollView;
            }

            // Push onto SkiaShell's navigation stack
            CurrentSkiaShell.PushAsync(pageContent, page.Title ?? "Detail");
            Console.WriteLine($"[PushPage] Successfully pushed page");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PushPage] Error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Pops the current page from the navigation stack.
    /// </summary>
    /// <returns>True if successful</returns>
    public static bool PopPage()
    {
        Console.WriteLine($"[PopPage] Popping page");

        if (CurrentSkiaShell == null)
        {
            Console.WriteLine($"[PopPage] CurrentSkiaShell is null");
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

        if (handler.PlatformView is SkiaView skiaPage)
        {
            // For ContentPage, render the content
            if (page is ContentPage contentPage && contentPage.Content != null)
            {
                var contentView = RenderView(contentPage.Content);
                if (skiaPage is SkiaPage sp && contentView != null)
                {
                    sp.Content = contentView;
                }
            }

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
            }
        };

        // Process shell items into sections
        foreach (var item in shell.Items)
        {
            ProcessShellItem(skiaShell, item);
        }

        // Store reference to SkiaShell for navigation
        CurrentSkiaShell = skiaShell;

        // Subscribe to MAUI Shell navigation events to update SkiaShell
        shell.Navigated += OnShellNavigated;
        shell.Navigating += (s, e) => Console.WriteLine($"[Navigation] Navigating: {e.Target}");

        Console.WriteLine($"[Navigation] Shell navigation events subscribed. Sections: {skiaShell.Sections.Count}");
        for (int i = 0; i < skiaShell.Sections.Count; i++)
        {
            Console.WriteLine($"[Navigation] Section {i}: Route='{skiaShell.Sections[i].Route}', Title='{skiaShell.Sections[i].Title}'");
        }

        return skiaShell;
    }

    /// <summary>
    /// Handles MAUI Shell navigation events and updates SkiaShell accordingly.
    /// </summary>
    private static void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        Console.WriteLine($"[Navigation] OnShellNavigated called - Source: {e.Source}, Current: {e.Current?.Location}, Previous: {e.Previous?.Location}");

        if (CurrentSkiaShell == null || CurrentMauiShell == null)
        {
            Console.WriteLine($"[Navigation] CurrentSkiaShell or CurrentMauiShell is null");
            return;
        }

        // Get the current route from the Shell
        var currentState = CurrentMauiShell.CurrentState;
        var location = currentState?.Location?.OriginalString ?? "";
        Console.WriteLine($"[Navigation] Location: {location}, Sections: {CurrentSkiaShell.Sections.Count}");

        // Find the matching section in SkiaShell by route
        for (int i = 0; i < CurrentSkiaShell.Sections.Count; i++)
        {
            var section = CurrentSkiaShell.Sections[i];
            Console.WriteLine($"[Navigation] Checking section {i}: Route='{section.Route}', Title='{section.Title}'");
            if (!string.IsNullOrEmpty(section.Route) && location.Contains(section.Route, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[Navigation] Match found by route! Navigating to section {i}");
                if (i != CurrentSkiaShell.CurrentSectionIndex)
                {
                    CurrentSkiaShell.NavigateToSection(i);
                }
                return;
            }
            if (!string.IsNullOrEmpty(section.Title) && location.Contains(section.Title, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[Navigation] Match found by title! Navigating to section {i}");
                if (i != CurrentSkiaShell.CurrentSectionIndex)
                {
                    CurrentSkiaShell.NavigateToSection(i);
                }
                return;
            }
        }
        Console.WriteLine($"[Navigation] No matching section found for location: {location}");
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
                        Route = content.Route ?? ""
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
                        Route = content.Route ?? ""
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
                        Route = content.Route ?? ""
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
                    if (contentView is SkiaScrollView)
                    {
                        return contentView;
                    }
                    else
                    {
                        var scrollView = new SkiaScrollView
                        {
                            Content = contentView
                        };
                        return scrollView;
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
            var handler = view.ToHandler(_mauiContext);

            if (handler?.PlatformView is not SkiaView skiaView)
            {
                // If no Skia handler, create a fallback
                return CreateFallbackView(view);
            }

            // Recursively render children for layout views
            if (view is ILayout layout && skiaView is SkiaLayoutView layoutView)
            {

                // For StackLayout, copy orientation and spacing
                if (layoutView is SkiaStackLayout skiaStack)
                {
                    if (view is Controls.VerticalStackLayout)
                    {
                        skiaStack.Orientation = StackOrientation.Vertical;
                    }
                    else if (view is Controls.HorizontalStackLayout)
                    {
                        skiaStack.Orientation = StackOrientation.Horizontal;
                    }
                    else if (view is Controls.StackLayout sl)
                    {
                        skiaStack.Orientation = sl.Orientation == Microsoft.Maui.Controls.StackOrientation.Vertical
                            ? StackOrientation.Vertical : StackOrientation.Horizontal;
                    }

                    if (view is IStackLayout stackLayout)
                    {
                        skiaStack.Spacing = (float)stackLayout.Spacing;
                    }
                }

                // For Grid, set up row/column definitions
                if (view is Controls.Grid mauiGrid && layoutView is SkiaGrid skiaGrid)
                {
                    // Copy row definitions
                    foreach (var rowDef in mauiGrid.RowDefinitions)
                    {
                        skiaGrid.RowDefinitions.Add(new GridLength((float)rowDef.Height.Value,
                            rowDef.Height.IsAbsolute ? GridUnitType.Absolute :
                            rowDef.Height.IsStar ? GridUnitType.Star : GridUnitType.Auto));
                    }
                    // Copy column definitions
                    foreach (var colDef in mauiGrid.ColumnDefinitions)
                    {
                        skiaGrid.ColumnDefinitions.Add(new GridLength((float)colDef.Width.Value,
                            colDef.Width.IsAbsolute ? GridUnitType.Absolute :
                            colDef.Width.IsStar ? GridUnitType.Star : GridUnitType.Auto));
                    }
                    skiaGrid.RowSpacing = (float)mauiGrid.RowSpacing;
                    skiaGrid.ColumnSpacing = (float)mauiGrid.ColumnSpacing;
                }

                foreach (var child in layout)
                {
                    if (child is IView childViewElement)
                    {
                        var childView = RenderView(childViewElement);
                        if (childView != null)
                        {
                            // For Grid, get attached properties for position
                            if (layoutView is SkiaGrid grid && child is BindableObject bindable)
                            {
                                var row = Controls.Grid.GetRow(bindable);
                                var col = Controls.Grid.GetColumn(bindable);
                                var rowSpan = Controls.Grid.GetRowSpan(bindable);
                                var colSpan = Controls.Grid.GetColumnSpan(bindable);
                                grid.AddChild(childView, row, col, rowSpan, colSpan);
                            }
                            else
                            {
                                layoutView.AddChild(childView);
                            }
                        }
                    }
                }
            }
            else if (view is IContentView contentView && contentView.Content is IView contentElement)
            {
                var content = RenderView(contentElement);
                if (content != null)
                {
                    if (skiaView is SkiaBorder border)
                    {
                        border.AddChild(content);
                    }
                    else if (skiaView is SkiaFrame frame)
                    {
                        frame.AddChild(content);
                    }
                    else if (skiaView is SkiaScrollView scrollView)
                    {
                        scrollView.Content = content;
                    }
                }
            }

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
            TextColor = SKColors.Gray,
            FontSize = 12
        };
    }
}

/// <summary>
/// Extension methods for MAUI handler creation.
/// </summary>
public static class MauiHandlerExtensions
{
    /// <summary>
    /// Creates a handler for the view and returns it.
    /// </summary>
    public static IElementHandler ToHandler(this IElement element, IMauiContext mauiContext)
    {
        var handler = mauiContext.Handlers.GetHandler(element.GetType());
        if (handler != null)
        {
            handler.SetMauiContext(mauiContext);
            handler.SetVirtualView(element);
        }
        return handler!;
    }
}
