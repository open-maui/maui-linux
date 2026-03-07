// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for Shell on Linux using Skia rendering.
/// </summary>
public partial class ShellHandler : ViewHandler<Shell, SkiaShell>
{
    private bool _isUpdatingFlyoutPresented;

    public static IPropertyMapper<Shell, ShellHandler> Mapper = new PropertyMapper<Shell, ShellHandler>(ViewHandler.ViewMapper)
    {
        [nameof(Shell.FlyoutIsPresented)] = MapFlyoutIsPresented,
        [nameof(Shell.FlyoutBehavior)] = MapFlyoutBehavior,
        [nameof(Shell.FlyoutWidth)] = MapFlyoutWidth,
        [nameof(Shell.FlyoutBackgroundColor)] = MapFlyoutBackgroundColor,
        [nameof(Shell.FlyoutBackground)] = MapFlyoutBackground,
        [nameof(Shell.BackgroundColor)] = MapBackgroundColor,
        [nameof(Shell.FlyoutHeaderBehavior)] = MapFlyoutHeaderBehavior,
        [nameof(Shell.FlyoutHeader)] = MapFlyoutHeader,
        [nameof(Shell.FlyoutFooter)] = MapFlyoutFooter,
        [nameof(Shell.Items)] = MapItems,
        [nameof(Shell.CurrentItem)] = MapCurrentItem,
        [nameof(Shell.Title)] = MapTitle,
    };

    public static CommandMapper<Shell, ShellHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
        ["GoToAsync"] = MapGoToAsync,
    };

    public ShellHandler() : base(Mapper, CommandMapper)
    {
    }

    public ShellHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaShell CreatePlatformView()
    {
        DiagnosticLog.Debug("ShellHandler", "CreatePlatformView - creating SkiaShell");
        return new SkiaShell();
    }

    protected override void ConnectHandler(SkiaShell platformView)
    {
        DiagnosticLog.Debug("ShellHandler", "ConnectHandler - connecting to SkiaShell");
        base.ConnectHandler(platformView);
        platformView.FlyoutIsPresentedChanged += OnFlyoutIsPresentedChanged;
        platformView.Navigated += OnNavigated;

        // Store reference to MAUI Shell for callbacks
        platformView.MauiShell = VirtualView;

        // Set up content renderer
        platformView.ContentRenderer = RenderShellContent;
        platformView.ColorRefresher = RefreshShellColors;

        // Subscribe to Shell navigation events
        if (VirtualView != null)
        {
            VirtualView.Navigating += OnShellNavigating;
            VirtualView.Navigated += OnShellNavigated;

            // Initial sync of shell items
            SyncShellItems();
        }
    }

    protected override void DisconnectHandler(SkiaShell platformView)
    {
        platformView.FlyoutIsPresentedChanged -= OnFlyoutIsPresentedChanged;
        platformView.Navigated -= OnNavigated;
        platformView.MauiShell = null;
        platformView.ContentRenderer = null;
        platformView.ColorRefresher = null;

        if (VirtualView != null)
        {
            VirtualView.Navigating -= OnShellNavigating;
            VirtualView.Navigated -= OnShellNavigated;
        }

        base.DisconnectHandler(platformView);
    }

    private void OnFlyoutIsPresentedChanged(object? sender, EventArgs e)
    {
        if (VirtualView is null || PlatformView is null || _isUpdatingFlyoutPresented) return;

        try
        {
            _isUpdatingFlyoutPresented = true;
            VirtualView.FlyoutIsPresented = PlatformView.FlyoutIsPresented;
        }
        finally
        {
            _isUpdatingFlyoutPresented = false;
        }
    }

    private void OnNavigated(object? sender, Platform.ShellNavigationEventArgs e)
    {
        // Handle platform navigation events
    }

    private void OnShellNavigating(object? sender, ShellNavigatingEventArgs e)
    {
        DiagnosticLog.Debug("ShellHandler", $"Shell Navigating to: {e.Target?.Location}");

        // Route to platform view
        if (PlatformView != null && e.Target?.Location != null)
        {
            var route = e.Target.Location.ToString().TrimStart('/');
            DiagnosticLog.Debug("ShellHandler", $"Routing to: {route}");
            PlatformView.GoToAsync(route);
        }
    }

    private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        DiagnosticLog.Debug("ShellHandler", $"Shell Navigated to: {e.Current?.Location}");
    }

    private void SyncShellItems()
    {
        if (PlatformView is null || VirtualView is null || MauiContext is null) return;

        // Clear existing sections
        foreach (var section in PlatformView.Sections.ToList())
        {
            PlatformView.RemoveSection(section);
        }

        // Add shell items as sections
        foreach (var item in VirtualView.Items)
        {
            if (item is FlyoutItem flyoutItem)
            {
                var section = new Platform.ShellSection
                {
                    Route = flyoutItem.Route ?? flyoutItem.Title ?? "",
                    Title = flyoutItem.Title ?? "",
                    IconPath = flyoutItem.Icon?.ToString()
                };

                // Add shell contents as items
                foreach (var shellSection in flyoutItem.Items)
                {
                    foreach (var content in shellSection.Items)
                    {
                        var contentItem = new Platform.ShellContent
                        {
                            Route = content.Route ?? content.Title ?? "",
                            Title = content.Title ?? "",
                            IconPath = content.Icon?.ToString(),
                            MauiShellContent = content,
                            Content = RenderShellContent(content)
                        };
                        section.Items.Add(contentItem);
                    }
                }

                PlatformView.AddSection(section);
            }
            else if (item is ShellItem shellItem)
            {
                var section = new Platform.ShellSection
                {
                    Route = shellItem.Route ?? shellItem.Title ?? "",
                    Title = shellItem.Title ?? "",
                    IconPath = shellItem.Icon?.ToString()
                };

                foreach (var shellSection in shellItem.Items)
                {
                    foreach (var content in shellSection.Items)
                    {
                        var contentItem = new Platform.ShellContent
                        {
                            Route = content.Route ?? content.Title ?? "",
                            Title = content.Title ?? "",
                            IconPath = content.Icon?.ToString(),
                            MauiShellContent = content,
                            Content = RenderShellContent(content)
                        };
                        section.Items.Add(contentItem);
                    }
                }

                PlatformView.AddSection(section);
            }
        }
    }

    private SkiaView? RenderShellContent(Microsoft.Maui.Controls.ShellContent content)
    {
        if (MauiContext is null) return null;

        try
        {
            var page = content.Content as Page;
            if (page == null && content.ContentTemplate != null)
            {
                page = content.ContentTemplate.CreateContent() as Page;
            }

            if (page != null)
            {
                if (page.Handler == null)
                {
                    page.Handler = page.ToViewHandler(MauiContext);
                }

                if (page.Handler?.PlatformView is SkiaView skiaView)
                {
                    return skiaView;
                }
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("ShellHandler", $"Error rendering content: {ex.Message}", ex);
        }

        return null;
    }

    private static void RefreshShellColors(SkiaShell platformView, Shell shell)
    {
        // Sync flyout colors
        if (shell.FlyoutBackgroundColor is Color flyoutBgColor)
        {
            platformView.FlyoutBackgroundColor = flyoutBgColor;
        }
        else if (shell.FlyoutBackground is SolidColorBrush flyoutBrush)
        {
            platformView.FlyoutBackgroundColor = flyoutBrush.Color;
        }

        // Sync nav bar colors
        if (shell.BackgroundColor is Color bgColor)
        {
            platformView.NavBarBackgroundColor = bgColor;
        }
    }

    public static void MapFlyoutIsPresented(ShellHandler handler, Shell shell)
    {
        if (handler.PlatformView is null || handler._isUpdatingFlyoutPresented) return;

        try
        {
            handler._isUpdatingFlyoutPresented = true;
            handler.PlatformView.FlyoutIsPresented = shell.FlyoutIsPresented;
        }
        finally
        {
            handler._isUpdatingFlyoutPresented = false;
        }
    }

    public static void MapFlyoutBehavior(ShellHandler handler, Shell shell)
    {
        if (handler.PlatformView is null) return;

        handler.PlatformView.FlyoutBehavior = shell.FlyoutBehavior switch
        {
            Microsoft.Maui.FlyoutBehavior.Disabled => ShellFlyoutBehavior.Disabled,
            Microsoft.Maui.FlyoutBehavior.Flyout => ShellFlyoutBehavior.Flyout,
            Microsoft.Maui.FlyoutBehavior.Locked => ShellFlyoutBehavior.Locked,
            _ => ShellFlyoutBehavior.Flyout
        };
    }

    public static void MapFlyoutWidth(ShellHandler handler, Shell shell)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.FlyoutWidth = (float)shell.FlyoutWidth;
    }

    public static void MapFlyoutBackgroundColor(ShellHandler handler, Shell shell)
    {
        if (handler.PlatformView is null) return;

        if (shell.FlyoutBackgroundColor is Color color)
        {
            handler.PlatformView.FlyoutBackgroundColor = color;
        }
    }

    public static void MapFlyoutBackground(ShellHandler handler, Shell shell)
    {
        if (handler.PlatformView is null) return;

        if (shell.FlyoutBackground is SolidColorBrush solidBrush)
        {
            handler.PlatformView.FlyoutBackgroundColor = solidBrush.Color;
        }
    }

    public static void MapBackgroundColor(ShellHandler handler, Shell shell)
    {
        if (handler.PlatformView is null) return;

        if (shell.BackgroundColor is Color color)
        {
            handler.PlatformView.NavBarBackgroundColor = color;
        }
    }

    public static void MapFlyoutHeaderBehavior(ShellHandler handler, Shell shell)
    {
        // Flyout header behavior - handled by platform view
    }

    public static void MapFlyoutHeader(ShellHandler handler, Shell shell)
    {
        if (handler.PlatformView is null || handler.MauiContext is null) return;

        var header = shell.FlyoutHeader;
        if (header == null)
        {
            handler.PlatformView.FlyoutHeaderView = null;
            return;
        }

        if (header is View headerView)
        {
            if (headerView.Handler == null)
            {
                headerView.Handler = headerView.ToViewHandler(handler.MauiContext);
            }

            if (headerView.Handler?.PlatformView is SkiaView skiaHeader)
            {
                handler.PlatformView.FlyoutHeaderView = skiaHeader;
            }
        }
    }

    public static void MapFlyoutFooter(ShellHandler handler, Shell shell)
    {
        if (handler.PlatformView is null || handler.MauiContext is null) return;

        var footer = shell.FlyoutFooter;
        if (footer == null)
        {
            handler.PlatformView.FlyoutFooterText = null;
            return;
        }

        // Simple text footer support
        if (footer is Label label)
        {
            handler.PlatformView.FlyoutFooterText = label.Text;
        }
        else if (footer is string text)
        {
            handler.PlatformView.FlyoutFooterText = text;
        }
    }

    public static void MapItems(ShellHandler handler, Shell shell)
    {
        handler.SyncShellItems();
    }

    public static void MapCurrentItem(ShellHandler handler, Shell shell)
    {
        if (handler.PlatformView is null) return;

        // Sync current item selection
        var currentItem = shell.CurrentItem;
        if (currentItem != null)
        {
            // Find matching section index
            for (int i = 0; i < handler.PlatformView.Sections.Count; i++)
            {
                var section = handler.PlatformView.Sections[i];
                if (section.Route == (currentItem.Route ?? currentItem.Title))
                {
                    handler.PlatformView.NavigateToSection(i);
                    break;
                }
            }
        }
    }

    public static void MapTitle(ShellHandler handler, Shell shell)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Title = shell.Title ?? "";
    }

    public static void MapGoToAsync(ShellHandler handler, Shell shell, object? args)
    {
        if (handler.PlatformView is null || args is null) return;

        if (args is ShellNavigationState state)
        {
            handler.PlatformView.GoToAsync(state.Location.ToString());
        }
        else if (args is string route)
        {
            handler.PlatformView.GoToAsync(route);
        }
    }
}
