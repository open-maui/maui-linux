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

            if (page.Handler?.PlatformView is SkiaPage skiaPage)
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
            }
        }
    }

    private readonly Dictionary<Page, (SkiaPage, INotifyCollectionChanged)> _toolbarSubscriptions = new();

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
                notifyCollection.CollectionChanged += (s, e) =>
                {
                    DiagnosticLog.Debug("NavigationPageHandler", $"ToolbarItems changed for '{page.Title}', action={e.Action}");
                    MapToolbarItems(skiaPage, page);
                    skiaPage.Invalidate();
                };
                _toolbarSubscriptions[page] = (skiaPage, notifyCollection);
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
        int currentDepth = handler.PlatformView.StackDepth;

        DiagnosticLog.Debug("NavigationPageHandler", $"MapRequestNavigation: requested={requestedStack.Count} pages, current platform depth={currentDepth}");

        if (requestedStack.Count < currentDepth)
        {
            // Pop: the requested stack is shorter than current
            int popCount = currentDepth - requestedStack.Count;
            DiagnosticLog.Debug("NavigationPageHandler", $"Popping {popCount} pages");
            for (int i = 0; i < popCount; i++)
            {
                handler.PlatformView.Pop(request.Animated);
            }
        }
        else if (requestedStack.Count > currentDepth || currentDepth == 0)
        {
            // Push: new pages on the stack
            // Only push pages that aren't already on the platform stack
            int startIndex = Math.Max(0, currentDepth);
            for (int i = startIndex; i < requestedStack.Count; i++)
            {
                if (requestedStack[i] is not Page page) continue;

                // Ensure handler exists
                if (page.Handler == null)
                {
                    page.Handler = page.ToViewHandler(handler.MauiContext);
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

                    DiagnosticLog.Debug("NavigationPageHandler", $"Pushed page [{i}]: {page.Title ?? page.GetType().Name}");
                }
            }
        }

        // Signal to MAUI that navigation is complete.
        // Without this, PushAsync/PopAsync Tasks never complete and the Pushed/Popped events never fire.
        ((IStackNavigation)navigationPage).NavigationFinished(requestedStack);
        DiagnosticLog.Debug("NavigationPageHandler", "NavigationFinished called");
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
