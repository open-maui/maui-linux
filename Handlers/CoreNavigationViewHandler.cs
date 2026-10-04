// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// MAUI's NavigationViewHandler on Linux: the handler of a core
/// <see cref="IStackNavigationView"/> (a library's own stack navigation view; a Controls
/// NavigationPage keeps <see cref="NavigationPageHandler"/>). Its platform view is a
/// <see cref="SkiaNavigationPage"/> that shows exactly the stack each
/// <see cref="NavigationRequest"/> asks for (a page whose platform view is not a
/// <see cref="SkiaPage"/> is shown in one, which carries its bar), and the view is told
/// <see cref="IStackNavigation.NavigationFinished"/> once the transition is over (without one,
/// on the next iteration of the main loop), as MAUI's StackNavigationManager does. The back arrow (and Escape) asks the view for the stack
/// without its top page. A view given MAUI's own NavigationViewHandler gets this one.
/// </summary>
public partial class CoreNavigationViewHandler : LinuxViewHandler<IStackNavigationView, SkiaNavigationPage>
{
    private readonly ConditionalWeakTable<IView, SkiaPage> _pageHosts = new();
    private IReadOnlyList<IView> _stack = Array.Empty<IView>();

    public static IPropertyMapper<IStackNavigationView, CoreNavigationViewHandler> Mapper = new PropertyMapper<IStackNavigationView, CoreNavigationViewHandler>(ViewHandler.ViewMapper)
    {
    };

    public static CommandMapper<IStackNavigationView, CoreNavigationViewHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
        [nameof(IStackNavigation.RequestNavigation)] = MapRequestNavigation,
    };

    public CoreNavigationViewHandler() : base(Mapper, CommandMapper)
    {
    }

    public CoreNavigationViewHandler(IPropertyMapper? mapper)
        : base(mapper ?? Mapper, CommandMapper)
    {
    }

    public CoreNavigationViewHandler(IPropertyMapper? mapper, CommandMapper? commandMapper)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaNavigationPage CreatePlatformView() => new();

    protected override void ConnectHandler(SkiaNavigationPage platformView)
    {
        base.ConnectHandler(platformView);
        platformView.BackRequested = OnPlatformBackRequested;
    }

    protected override void DisconnectHandler(SkiaNavigationPage platformView)
    {
        platformView.BackRequested = null;
        _stack = Array.Empty<IView>();
        base.DisconnectHandler(platformView);
    }

    /// <summary>The navigation stack the view last asked for (bottom first).</summary>
    internal IReadOnlyList<IView> NavigationStack => _stack;

    public static void MapRequestNavigation(CoreNavigationViewHandler handler, IStackNavigationView view, object? args)
    {
        if (handler.PlatformView is not { } platform || handler.MauiContext is null || args is not NavigationRequest request)
            return;

        var requested = request.NavigationStack;
        var pages = new List<SkiaPage>(requested.Count);
        foreach (var page in requested)
        {
            try
            {
                if (page.Handler is null)
                    page.ToViewHandler(handler.MauiContext);
                if (handler.PlatformPageFor(page) is { } skiaPage)
                {
                    skiaPage.ShowNavigationBar = true;
                    skiaPage.Title = (page as ITitledElement)?.Title ?? skiaPage.Title;
                    pages.Add(skiaPage);
                }
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("CoreNavigationViewHandler", $"Failed to render page ({page.GetType().Name}): {ex.Message}", ex);
            }
        }

        handler._stack = requested;
        platform.SetNavigationStack(pages, request.Animated);

        void Finish() => view.NavigationFinished(requested);

        if (platform.IsTransitioning)
        {
            void OnCompleted(object? sender, EventArgs e)
            {
                platform.TransitionCompleted -= OnCompleted;
                Finish();
            }
            platform.TransitionCompleted += OnCompleted;
        }
        else
        {
            // As MAUI's platform navigation reports it: after the work that asked for it, on
            // the next iteration of the main loop (the caller sees the request return first).
            var dispatcher = handler.MauiContext.Services.GetService(typeof(Microsoft.Maui.Dispatching.IDispatcher)) as Microsoft.Maui.Dispatching.IDispatcher
                ?? Microsoft.Maui.Dispatching.Dispatcher.GetForCurrentThread();
            if (dispatcher is null || !dispatcher.Dispatch(Finish))
                Finish();
        }
    }

    private SkiaPage? PlatformPageFor(IView page)
    {
        if (page.Handler?.PlatformView is not SkiaView platformView)
            return null;
        if (platformView is SkiaPage skiaPage)
            return skiaPage;
        if (!_pageHosts.TryGetValue(page, out var host))
        {
            host = new SkiaPage();
            _pageHosts.Add(page, host);
        }
        if (!ReferenceEquals(host.Content, platformView))
            host.Content = platformView;
        return host;
    }

    private bool OnPlatformBackRequested()
    {
        if (VirtualView is not { } view || _stack.Count <= 1)
            return false;
        view.RequestNavigation(new NavigationRequest(_stack.Take(_stack.Count - 1).ToList(), true));
        return true;
    }
}
