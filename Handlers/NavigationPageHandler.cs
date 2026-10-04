// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using System.Collections.Specialized;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for NavigationPage on Linux using Skia rendering.
/// </summary>
public partial class NavigationPageHandler : LinuxViewHandler<NavigationPage, SkiaNavigationPage>
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
        // The page's MAUI frame follows the platform view's arrange.
        platformView.HostedPage = VirtualView as Microsoft.Maui.Controls.Page;
        platformView.Pushed += OnPushed;
        platformView.Popped += OnPopped;
        platformView.PoppedToRoot += OnPoppedToRoot;
        // The back arrow (and Escape) pop MAUI's NavigationPage, which then
        // navigates this view: MAUI's stack and the shown one stay the same.
        platformView.BackRequested = OnPlatformBackRequested;

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
        platformView.BackRequested = null;
        foreach (var page in _barSubscriptions)
            page.PropertyChanged -= OnPagePropertyChanged;
        _barSubscriptions.Clear();
        foreach (var toolbar in _toolbarSubscriptions.Values)
            toolbar.Items.CollectionChanged -= toolbar.Handler;
        _toolbarSubscriptions.Clear();

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

        // MapRequestNavigation handles the actual navigation stack setup.
        // This method only runs as a fallback if the platform has no pages yet
        // (e.g., if ConnectHandler fires before the initial RequestNavigation command).
        if (PlatformView.StackDepth > 0)
        {
            DiagnosticLog.Debug("NavigationPageHandler", $"SetupNavigationStack skipped - platform already has {PlatformView.StackDepth} pages");
            return;
        }

        var pages = VirtualView.Navigation.NavigationStack.ToList();
        DiagnosticLog.Debug("NavigationPageHandler", $"SetupNavigationStack: {pages.Count} pages");

        if (pages.Count == 0 && VirtualView.CurrentPage != null)
        {
            pages.Add(VirtualView.CurrentPage);
        }

        foreach (var page in pages)
        {
            if (page.Handler == null)
            {
                page.Handler = page.ToViewHandler(MauiContext);
            }

            if (PlatformPageFor(page) is SkiaPage skiaPage)
            {
                skiaPage.ShowNavigationBar = true;
                skiaPage.TitleBarColor = PlatformView.BarBackgroundColor;
                skiaPage.TitleTextColor = PlatformView.BarTextColor;
                skiaPage.Title = page.Title ?? "";

                if (skiaPage.Content == null && page is ContentPage contentPage && contentPage.Content != null)
                {
                    if (contentPage.Content.Handler == null)
                    {
                        contentPage.Content.Handler = contentPage.Content.ToViewHandler(MauiContext);
                    }
                    if (contentPage.Content.Handler?.PlatformView is SkiaView skiaContent)
                    {
                        skiaPage.Content = skiaContent;
                    }
                }

                MapToolbarItems(skiaPage, page);

                if (PlatformView.StackDepth == 0)
                {
                    PlatformView.SetRootPage(skiaPage);
                }
                else
                {
                    PlatformView.Push(skiaPage, false);
                }
                TrackPageBar(page);
            }
        }
    }

    private readonly Dictionary<Page, (SkiaPage Page, INotifyCollectionChanged Items, NotifyCollectionChangedEventHandler Handler)> _toolbarSubscriptions = new();

    private void MapToolbarItems(SkiaPage skiaPage, Page page)
    {
        if (skiaPage is SkiaContentPage contentPage)
        {
            DiagnosticLog.Debug("NavigationPageHandler", $"MapToolbarItems for '{page.Title}', count={page.ToolbarItems.Count}");

            ReleaseToolbarIcons(contentPage);
            contentPage.ToolbarItems.Clear();
            foreach (var item in page.ToolbarItems)
            {
                DiagnosticLog.Debug("NavigationPageHandler", $"Adding toolbar item: '{item.Text}', IconImageSource={item.IconImageSource}, Order={item.Order}");
                // Default and Primary should both be treated as Primary (shown in toolbar)
                // Only Secondary goes to overflow menu
                var order = item.Order == ToolbarItemOrder.Secondary
                    ? SkiaToolbarItemOrder.Secondary
                    : SkiaToolbarItemOrder.Primary;

                // Create a command that invokes the Clicked event
                var toolbarItem = item; // Capture for closure
                var clickCommand = new RelayCommand(() =>
                {
                    DiagnosticLog.Debug("NavigationPageHandler", $"ToolbarItem '{toolbarItem.Text}' clicked, invoking...");
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

                var skiaItem = new SkiaToolbarItem
                {
                    Text = item.Text ?? "",
                    Order = order,
                    Command = clickCommand
                };
                contentPage.ToolbarItems.Add(skiaItem);
                if (item.IconImageSource is { IsEmpty: false } iconSource)
                    _ = LoadToolbarIconAsync(contentPage, skiaItem, iconSource);
            }

            // Subscribe to ToolbarItems changes if not already subscribed
            if (page.ToolbarItems is INotifyCollectionChanged notifyCollection && !_toolbarSubscriptions.ContainsKey(page))
            {
                DiagnosticLog.Debug("NavigationPageHandler", $"Subscribing to ToolbarItems changes for '{page.Title}'");
                NotifyCollectionChangedEventHandler onChanged = (s, e) =>
                {
                    DiagnosticLog.Debug("NavigationPageHandler", $"ToolbarItems changed for '{page.Title}', action={e.Action}");
                    MapToolbarItems(skiaPage, page);
                    skiaPage.Invalidate();
                };
                notifyCollection.CollectionChanged += onChanged;
                _toolbarSubscriptions[page] = (skiaPage, notifyCollection, onChanged);
            }
        }
    }

    /// <summary>The icon loads in flight and loaded, per page, released when its items are mapped again.</summary>
    private readonly Dictionary<SkiaContentPage, List<(CancellationTokenSource Load, SkiaToolbarItem Item)>> _toolbarIconLoads = new();
    private readonly Dictionary<SkiaToolbarItem, IImageSourceServiceResult<SKBitmap>> _toolbarIconResults = new();

    /// <summary>
    /// Loads a toolbar icon through its image-source service (file, font, URI, stream, or an
    /// app's own source), as Image does, at the bar's 24-pixel icon size and the screen's density.
    /// </summary>
    private async Task LoadToolbarIconAsync(SkiaContentPage page, SkiaToolbarItem skiaItem, ImageSource source)
    {
        var load = new CancellationTokenSource();
        if (!_toolbarIconLoads.TryGetValue(page, out var loads))
            _toolbarIconLoads[page] = loads = new();
        loads.Add((load, skiaItem));
        try
        {
            var result = await LinuxImageSourceServices.LoadAsync(
                MauiContext?.Services ?? LinuxImageSourceServices.AppServices, source, Math.Max(1f, page.DeviceScale), new Size(24, 24), load.Token);
            if (load.IsCancellationRequested)
            {
                result?.Dispose();
                return;
            }
            if (result != null)
            {
                _toolbarIconResults[skiaItem] = result;
                skiaItem.Icon = result.Value;
                page.Invalidate();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("NavigationPageHandler", $"Loading the toolbar icon of '{skiaItem.Text}' failed", ex);
        }
    }

    private void ReleaseToolbarIcons(SkiaContentPage page)
    {
        if (!_toolbarIconLoads.Remove(page, out var loads))
            return;
        foreach (var (load, item) in loads)
        {
            load.Cancel();
            if (_toolbarIconResults.Remove(item, out var result))
                result.Dispose();
            item.Icon = null;
        }
    }

    private void OnVirtualViewPushed(object? sender, Microsoft.Maui.Controls.NavigationEventArgs e)
    {
        // This event fires after NavigationFinished() is called in MapRequestNavigation.
        // The actual platform push is already handled there, so we only log here.
        DiagnosticLog.Debug("NavigationPageHandler", $"VirtualView Pushed confirmed: {e.Page?.Title}");
    }

    private void OnVirtualViewPopped(object? sender, Microsoft.Maui.Controls.NavigationEventArgs e)
    {
        // This event fires after NavigationFinished() is called in MapRequestNavigation.
        // The actual platform pop is already handled there, so we only log here.
        DiagnosticLog.Debug("NavigationPageHandler", $"VirtualView Popped confirmed: {e.Page?.Title}");
    }

    private void OnVirtualViewPoppedToRoot(object? sender, Microsoft.Maui.Controls.NavigationEventArgs e)
    {
        // This event fires after NavigationFinished() is called in MapRequestNavigation.
        DiagnosticLog.Debug("NavigationPageHandler", "VirtualView PoppedToRoot confirmed");
    }

    private void OnPushed(object? sender, NavigationEventArgs e)
    {
        // Navigation was completed on platform side
    }

    private void OnPopped(object? sender, NavigationEventArgs e)
    {
        // Platform pop events are handled by MapRequestNavigation, which drives navigation.
        // No need to sync back — MAUI's stack is already correct when this fires.
        DiagnosticLog.Debug("NavigationPageHandler", $"Platform Popped: {e.Page?.GetType().Name}");
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
            handler.PlatformView.BarBackgroundColor = navigationPage.BarBackgroundColor;
        }
    }

    public static void MapBarBackground(NavigationPageHandler handler, NavigationPage navigationPage)
    {
        if (handler.PlatformView is null) return;

        if (navigationPage.BarBackground is SolidColorBrush solidBrush)
        {
            handler.PlatformView.BarBackgroundColor = solidBrush.Color;
        }
    }

    public static void MapBarTextColor(NavigationPageHandler handler, NavigationPage navigationPage)
    {
        if (handler.PlatformView is null) return;

        if (navigationPage.BarTextColor is not null)
        {
            handler.PlatformView.BarTextColor = navigationPage.BarTextColor;
        }
    }

    public static void MapBackground(NavigationPageHandler handler, NavigationPage navigationPage)
    {
        if (handler.PlatformView is null) return;

        if (navigationPage.Background is SolidColorBrush solidBrush)
        {
            handler.PlatformView.BackgroundColor = solidBrush.Color;
        }
    }

    public static void MapRequestNavigation(NavigationPageHandler handler, NavigationPage navigationPage, object? args)
    {
        if (handler.PlatformView is null || handler.MauiContext is null || args is not NavigationRequest request)
            return;

        var requestedStack = request.NavigationStack;
        DiagnosticLog.Debug("NavigationPageHandler", $"MapRequestNavigation: requested={requestedStack.Count} pages, current platform depth={handler.PlatformView.StackDepth}");

        // The platform shows exactly the requested stack: pushes, pops, pages inserted
        // or removed beneath the current one, and whole-stack swaps (MAUI's
        // StackNavigationManager does the same on every platform).
        var skiaPages = new List<SkiaPage>(requestedStack.Count);
        foreach (var view in requestedStack)
        {
            if (view is not Page page)
                continue;
            if (page.Handler == null)
                page.Handler = page.ToViewHandler(handler.MauiContext);
            if (handler.PlatformPageFor(page) is SkiaPage skiaPage)
            {
                skiaPage.TitleBarColor = handler.PlatformView.BarBackgroundColor;
                skiaPage.TitleTextColor = handler.PlatformView.BarTextColor;
                handler.MapToolbarItems(skiaPage, page);
                skiaPages.Add(skiaPage);
            }
        }

        handler.PlatformView.SetNavigationStack(skiaPages, request.Animated);

        // Bars after the stack changed (pushing configures a page's bar); pages that
        // left the stack are let go (the handler must not keep popped pages alive).
        handler.UntrackPagesNotIn(requestedStack);
        foreach (var view in requestedStack)
            if (view is Page page)
                handler.TrackPageBar(page);

        // Signal to MAUI that navigation is complete once the page is on screen:
        // PushAsync/PopAsync return (and Pushed/Popped fire) after the transition,
        // as on MAUI's platforms, so the next navigation finds the page current.
        void Finish()
        {
            ((IStackNavigation)navigationPage).NavigationFinished(requestedStack);
            DiagnosticLog.Debug("NavigationPageHandler", "NavigationFinished called");
        }

        if (handler.PlatformView.IsTransitioning)
        {
            var platformView = handler.PlatformView;
            void OnCompleted(object? sender, EventArgs e)
            {
                platformView.TransitionCompleted -= OnCompleted;
                Finish();
            }
            platformView.TransitionCompleted += OnCompleted;
        }
        else
        {
            Finish();
        }
    }

    private readonly HashSet<Page> _barSubscriptions = new();

    // Pages whose platform view is not a SkiaPage (a TabbedPage or FlyoutPage pushed onto
    // the stack) are shown in a SkiaPage that hosts it and carries its navigation bar.
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<Page, SkiaPage> _pageHosts = new();

    private SkiaPage? PlatformPageFor(Page page)
    {
        var platformView = page.Handler?.PlatformView as SkiaView;
        if (platformView is SkiaPage skiaPage)
            return skiaPage;
        if (platformView is null)
            return null;
        if (!_pageHosts.TryGetValue(page, out var host))
        {
            // The hosted view gives the page its frame (SkiaView.HostedPage); the host
            // only adds the bar.
            host = new SkiaPage();
            _pageHosts.Add(page, host);
        }
        if (!ReferenceEquals(host.Content, platformView))
            host.Content = platformView;
        return host;
    }

    /// <summary>
    /// A page's bar follows NavigationPage.HasNavigationBar / HasBackButton and its
    /// Title while it is on the stack.
    /// </summary>
    private void TrackPageBar(Page page)
    {
        ApplyPageBar(page);
        if (_barSubscriptions.Add(page))
            page.PropertyChanged += OnPagePropertyChanged;
    }

    private void UntrackPagesNotIn(IReadOnlyList<IView> stack)
    {
        foreach (var page in _barSubscriptions.ToList())
        {
            if (stack.Contains(page))
                continue;
            page.PropertyChanged -= OnPagePropertyChanged;
            _barSubscriptions.Remove(page);
            if (_toolbarSubscriptions.Remove(page, out var toolbar))
                toolbar.Items.CollectionChanged -= toolbar.Handler;
        }
    }

    private void OnPagePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not Page page)
            return;
        if (e.PropertyName == NavigationPage.HasNavigationBarProperty.PropertyName
            || e.PropertyName == NavigationPage.HasBackButtonProperty.PropertyName
            || e.PropertyName == Page.TitleProperty.PropertyName)
        {
            ApplyPageBar(page);
        }
    }

    private void ApplyPageBar(Page page)
    {
        if (PlatformPageFor(page) is not SkiaPage skiaPage)
            return;
        skiaPage.ShowNavigationBar = NavigationPage.GetHasNavigationBar(page);
        skiaPage.HasBackButton = NavigationPage.GetHasBackButton(page);
        skiaPage.Title = page.Title ?? string.Empty;
        skiaPage.InvalidateMeasure();
        skiaPage.Invalidate();
        PlatformView?.Invalidate();
    }

    private bool OnPlatformBackRequested()
    {
        if (VirtualView is not { } navigationPage || navigationPage.Navigation.NavigationStack.Count <= 1)
            return false;
        _ = navigationPage.PopAsync();
        return true;
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
