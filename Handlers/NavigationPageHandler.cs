// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using SkiaSharp;
using System.Collections.Specialized;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for NavigationPage on Linux using Skia rendering.
/// </summary>
public partial class NavigationPageHandler : ViewHandler<NavigationPage, SkiaNavigationPage>
{
    public static IPropertyMapper<NavigationPage, NavigationPageHandler> Mapper =
        new PropertyMapper<NavigationPage, NavigationPageHandler>(ViewHandler.ViewMapper)
        {
            [nameof(NavigationPage.BarBackgroundColor)] = MapBarBackgroundColor,
            [nameof(NavigationPage.BarBackground)] = MapBarBackground,
            [nameof(NavigationPage.BarTextColor)] = MapBarTextColor,
            [nameof(IView.Background)] = MapBackground,
        };

    public static CommandMapper<NavigationPage, NavigationPageHandler> CommandMapper =
        new(ViewHandler.ViewCommandMapper)
        {
            [nameof(IStackNavigationView.RequestNavigation)] = MapRequestNavigation,
        };

    public NavigationPageHandler() : base(Mapper, CommandMapper)
    {
    }

    public NavigationPageHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaNavigationPage CreatePlatformView()
    {
        return new SkiaNavigationPage();
    }

    protected override void ConnectHandler(SkiaNavigationPage platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Pushed += OnPushed;
        platformView.Popped += OnPopped;
        platformView.PoppedToRoot += OnPoppedToRoot;

        // Subscribe to navigation events from virtual view
        if (VirtualView != null)
        {
            VirtualView.Pushed += OnVirtualViewPushed;
            VirtualView.Popped += OnVirtualViewPopped;
            VirtualView.PoppedToRoot += OnVirtualViewPoppedToRoot;

            // Set up initial navigation stack
            SetupNavigationStack();
        }
    }

    protected override void DisconnectHandler(SkiaNavigationPage platformView)
    {
        platformView.Pushed -= OnPushed;
        platformView.Popped -= OnPopped;
        platformView.PoppedToRoot -= OnPoppedToRoot;

        if (VirtualView != null)
        {
            VirtualView.Pushed -= OnVirtualViewPushed;
            VirtualView.Popped -= OnVirtualViewPopped;
            VirtualView.PoppedToRoot -= OnVirtualViewPoppedToRoot;
        }

        base.DisconnectHandler(platformView);
    }

    private void SetupNavigationStack()
    {
        if (VirtualView == null || PlatformView == null || MauiContext == null) return;

        // Get all pages in the navigation stack
        var pages = VirtualView.Navigation.NavigationStack.ToList();
        Console.WriteLine($"[NavigationPageHandler] Setting up {pages.Count} pages");

        // If no pages in stack, check CurrentPage
        if (pages.Count == 0 && VirtualView.CurrentPage != null)
        {
            Console.WriteLine($"[NavigationPageHandler] No pages in stack, using CurrentPage: {VirtualView.CurrentPage.Title}");
            pages.Add(VirtualView.CurrentPage);
        }

        foreach (var page in pages)
        {
            // Ensure the page has a handler
            if (page.Handler == null)
            {
                Console.WriteLine($"[NavigationPageHandler] Creating handler for: {page.Title}");
                page.Handler = page.ToHandler(MauiContext);
            }

            Console.WriteLine($"[NavigationPageHandler] Page handler type: {page.Handler?.GetType().Name}");
            Console.WriteLine($"[NavigationPageHandler] Page PlatformView type: {page.Handler?.PlatformView?.GetType().Name}");

            if (page.Handler?.PlatformView is SkiaPage skiaPage)
            {
                // Set navigation bar properties
                skiaPage.ShowNavigationBar = true;
                skiaPage.TitleBarColor = PlatformView.BarBackgroundColor;
                skiaPage.TitleTextColor = PlatformView.BarTextColor;
                skiaPage.Title = page.Title ?? "";

                Console.WriteLine($"[NavigationPageHandler] SkiaPage content: {skiaPage.Content?.GetType().Name ?? "null"}");

                // If content is null, try to get it from ContentPage
                if (skiaPage.Content == null && page is ContentPage contentPage && contentPage.Content != null)
                {
                    Console.WriteLine($"[NavigationPageHandler] Content is null, manually creating handler for: {contentPage.Content.GetType().Name}");
                    if (contentPage.Content.Handler == null)
                    {
                        contentPage.Content.Handler = contentPage.Content.ToHandler(MauiContext);
                    }
                    if (contentPage.Content.Handler?.PlatformView is SkiaView skiaContent)
                    {
                        skiaPage.Content = skiaContent;
                        Console.WriteLine($"[NavigationPageHandler] Set content to: {skiaContent.GetType().Name}");
                    }
                }

                // Map toolbar items
                MapToolbarItems(skiaPage, page);

                if (PlatformView.StackDepth == 0)
                {
                    Console.WriteLine($"[NavigationPageHandler] Setting root page: {page.Title}");
                    PlatformView.SetRootPage(skiaPage);
                }
                else
                {
                    Console.WriteLine($"[NavigationPageHandler] Pushing page: {page.Title}");
                    PlatformView.Push(skiaPage, false);
                }
            }
            else
            {
                Console.WriteLine($"[NavigationPageHandler] Failed to get SkiaPage for: {page.Title}");
            }
        }
    }

    private readonly Dictionary<Page, (SkiaPage, INotifyCollectionChanged)> _toolbarSubscriptions = new();

    private void MapToolbarItems(SkiaPage skiaPage, Page page)
    {
        if (skiaPage is SkiaContentPage contentPage)
        {
            Console.WriteLine($"[NavigationPageHandler] MapToolbarItems for '{page.Title}', count={page.ToolbarItems.Count}");

            contentPage.ToolbarItems.Clear();
            foreach (var item in page.ToolbarItems)
            {
                Console.WriteLine($"[NavigationPageHandler] Adding toolbar item: '{item.Text}', Order={item.Order}");
                // Default and Primary should both be treated as Primary (shown in toolbar)
                // Only Secondary goes to overflow menu
                var order = item.Order == ToolbarItemOrder.Secondary
                    ? SkiaToolbarItemOrder.Secondary
                    : SkiaToolbarItemOrder.Primary;

                // Create a command that invokes the Clicked event
                var toolbarItem = item; // Capture for closure
                var clickCommand = new RelayCommand(() =>
                {
                    Console.WriteLine($"[NavigationPageHandler] ToolbarItem '{toolbarItem.Text}' clicked, invoking...");
                    // Use IMenuItemController to send the click
                    if (toolbarItem is IMenuItemController menuController)
                    {
                        menuController.Activate();
                    }
                    else
                    {
                        // Fallback: invoke Command if set
                        toolbarItem.Command?.Execute(toolbarItem.CommandParameter);
                    }
                });

                contentPage.ToolbarItems.Add(new SkiaToolbarItem
                {
                    Text = item.Text ?? "",
                    Order = order,
                    Command = clickCommand
                });
            }

            // Subscribe to ToolbarItems changes if not already subscribed
            if (page.ToolbarItems is INotifyCollectionChanged notifyCollection && !_toolbarSubscriptions.ContainsKey(page))
            {
                Console.WriteLine($"[NavigationPageHandler] Subscribing to ToolbarItems changes for '{page.Title}'");
                notifyCollection.CollectionChanged += (s, e) =>
                {
                    Console.WriteLine($"[NavigationPageHandler] ToolbarItems changed for '{page.Title}', action={e.Action}");
                    MapToolbarItems(skiaPage, page);
                    skiaPage.Invalidate();
                };
                _toolbarSubscriptions[page] = (skiaPage, notifyCollection);
            }
        }
    }

    private void OnVirtualViewPushed(object? sender, Microsoft.Maui.Controls.NavigationEventArgs e)
    {
        try
        {
            Console.WriteLine($"[NavigationPageHandler] VirtualView Pushed: {e.Page?.Title}");
            if (e.Page == null || PlatformView == null || MauiContext == null) return;

            // Ensure the page has a handler
            if (e.Page.Handler == null)
            {
                Console.WriteLine($"[NavigationPageHandler] Creating handler for page: {e.Page.GetType().Name}");
                e.Page.Handler = e.Page.ToHandler(MauiContext);
                Console.WriteLine($"[NavigationPageHandler] Handler created: {e.Page.Handler?.GetType().Name}");
            }

            if (e.Page.Handler?.PlatformView is SkiaPage skiaPage)
            {
                Console.WriteLine($"[NavigationPageHandler] Setting up skiaPage, content: {skiaPage.Content?.GetType().Name ?? "null"}");
                skiaPage.ShowNavigationBar = true;
                skiaPage.TitleBarColor = PlatformView.BarBackgroundColor;
                skiaPage.TitleTextColor = PlatformView.BarTextColor;
                Console.WriteLine($"[NavigationPageHandler] Mapping toolbar items");
                MapToolbarItems(skiaPage, e.Page);
                Console.WriteLine($"[NavigationPageHandler] Pushing page to platform");
                PlatformView.Push(skiaPage, true);
                Console.WriteLine($"[NavigationPageHandler] Push complete");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NavigationPageHandler] EXCEPTION in OnVirtualViewPushed: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"[NavigationPageHandler] Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    private void OnVirtualViewPopped(object? sender, Microsoft.Maui.Controls.NavigationEventArgs e)
    {
        Console.WriteLine($"[NavigationPageHandler] VirtualView Popped: {e.Page?.Title}");
        // Pop on the platform side to sync with MAUI navigation
        PlatformView?.Pop(true);
    }

    private void OnVirtualViewPoppedToRoot(object? sender, Microsoft.Maui.Controls.NavigationEventArgs e)
    {
        Console.WriteLine($"[NavigationPageHandler] VirtualView PoppedToRoot");
        PlatformView?.PopToRoot(true);
    }

    private void OnPushed(object? sender, NavigationEventArgs e)
    {
        // Navigation was completed on platform side
    }

    private void OnPopped(object? sender, NavigationEventArgs e)
    {
        // Sync back to virtual view - pop from MAUI navigation stack
        if (VirtualView?.Navigation.NavigationStack.Count > 1)
        {
            // Don't trigger another pop on platform side
            VirtualView.Navigation.RemovePage(VirtualView.Navigation.NavigationStack.Last());
        }
    }

    private void OnPoppedToRoot(object? sender, NavigationEventArgs e)
    {
        // Navigation was reset
    }

    public static void MapBarBackgroundColor(NavigationPageHandler handler, NavigationPage navigationPage)
    {
        if (handler.PlatformView is null) return;

        if (navigationPage.BarBackgroundColor is not null)
        {
            handler.PlatformView.BarBackgroundColor = navigationPage.BarBackgroundColor.ToSKColor();
        }
    }

    public static void MapBarBackground(NavigationPageHandler handler, NavigationPage navigationPage)
    {
        if (handler.PlatformView is null) return;

        if (navigationPage.BarBackground is SolidColorBrush solidBrush)
        {
            handler.PlatformView.BarBackgroundColor = solidBrush.Color.ToSKColor();
        }
    }

    public static void MapBarTextColor(NavigationPageHandler handler, NavigationPage navigationPage)
    {
        if (handler.PlatformView is null) return;

        if (navigationPage.BarTextColor is not null)
        {
            handler.PlatformView.BarTextColor = navigationPage.BarTextColor.ToSKColor();
        }
    }

    public static void MapBackground(NavigationPageHandler handler, NavigationPage navigationPage)
    {
        if (handler.PlatformView is null) return;

        if (navigationPage.Background is SolidColorBrush solidBrush)
        {
            handler.PlatformView.BackgroundColor = solidBrush.Color.ToSKColor();
        }
    }

    public static void MapRequestNavigation(NavigationPageHandler handler, NavigationPage navigationPage, object? args)
    {
        if (handler.PlatformView is null || handler.MauiContext is null || args is not NavigationRequest request)
            return;

        Console.WriteLine($"[NavigationPageHandler] MapRequestNavigation: {request.NavigationStack.Count} pages");

        // Handle navigation request
        foreach (var view in request.NavigationStack)
        {
            if (view is not Page page) continue;

            // Ensure handler exists
            if (page.Handler == null)
            {
                page.Handler = page.ToHandler(handler.MauiContext);
            }

            if (page.Handler?.PlatformView is SkiaPage skiaPage)
            {
                skiaPage.ShowNavigationBar = true;
                skiaPage.TitleBarColor = handler.PlatformView.BarBackgroundColor;
                skiaPage.TitleTextColor = handler.PlatformView.BarTextColor;
                handler.MapToolbarItems(skiaPage, page);

                if (handler.PlatformView.StackDepth == 0)
                {
                    handler.PlatformView.SetRootPage(skiaPage);
                }
                else
                {
                    handler.PlatformView.Push(skiaPage, request.Animated);
                }
            }
        }
    }
}

/// <summary>
/// Simple relay command for invoking actions.
/// </summary>
internal class RelayCommand : System.Windows.Input.ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
