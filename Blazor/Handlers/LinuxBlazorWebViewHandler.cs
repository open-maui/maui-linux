// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.FileProviders;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Views;

namespace Microsoft.Maui.Platform.Linux.Blazor.Handlers;

/// <summary>
/// Linux handler for <see cref="BlazorWebView"/>: hosts the Blazor Hybrid app
/// in a <see cref="WpeWebView"/> (WPE WebKit composited in the Skia tree),
/// mirroring the shape of MAUI's own platform handlers. HostPage and
/// RootComponents start the web view core; UrlLoading, BlazorWebViewInitializing
/// and BlazorWebViewInitialized are raised like on the other platforms.
/// </summary>
public class LinuxBlazorWebViewHandler : ViewHandler<IBlazorWebView, WpeWebView>
{
    public static IPropertyMapper<IBlazorWebView, LinuxBlazorWebViewHandler> Mapper = new PropertyMapper<IBlazorWebView, LinuxBlazorWebViewHandler>(ViewHandler.ViewMapper)
    {
        [nameof(IBlazorWebView.HostPage)] = MapHostPage,
        [nameof(IBlazorWebView.RootComponents)] = MapRootComponents,
    };

    private LinuxWebViewManager? _manager;
    private string? _hostPage;

    public LinuxBlazorWebViewHandler() : base(Mapper)
    {
    }

    public LinuxBlazorWebViewHandler(IPropertyMapper? mapper) : base(mapper ?? Mapper)
    {
    }

    protected override WpeWebView CreatePlatformView()
    {
        if (!WpeWebView.IsSupported)
            throw new InvalidOperationException(
                "BlazorWebView on Linux requires WPE WebKit 2.54+ (libWPEWebKit-2.0). " +
                "Debian/Ubuntu: apt install libwpewebkit-2.0-1; Fedora: dnf copr enable philn/wpewebkit && dnf install wpewebkit");
        return new WpeWebView();
    }

    protected override void ConnectHandler(WpeWebView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.NavigationDecision += OnNavigationDecision;
    }

    protected override void DisconnectHandler(WpeWebView platformView)
    {
        platformView.NavigationDecision -= OnNavigationDecision;
        var manager = _manager;
        _manager = null;
        if (manager != null)
        {
            // Blazor tears down the renderer asynchronously; do not block the UI thread.
            _ = manager.DisposeAsync().AsTask().ContinueWith(
                t => DiagnosticLog.Error("LinuxBlazorWebViewHandler", "WebViewManager dispose failed", t.Exception!),
                TaskContinuationOptions.OnlyOnFaulted);
        }
        platformView.Dispose();
        base.DisconnectHandler(platformView);
    }

    public static void MapHostPage(LinuxBlazorWebViewHandler handler, IBlazorWebView webView)
    {
        handler._hostPage = webView.HostPage;
        handler.StartWebViewCoreIfPossible();
    }

    public static void MapRootComponents(LinuxBlazorWebViewHandler handler, IBlazorWebView webView)
    {
        handler.StartWebViewCoreIfPossible();
    }

    private void StartWebViewCoreIfPossible()
    {
        if (_manager != null || string.IsNullOrEmpty(_hostPage) || PlatformView == null || MauiContext == null)
            return;

        var virtualView = VirtualView;
        virtualView.BlazorWebViewInitializing(new BlazorWebViewInitializingEventArgs());

        // HostPage is app-relative, e.g. "wwwroot/index.html": content root is
        // its directory, the host page path is relative to that root.
        var contentRootDir = Path.GetDirectoryName(_hostPage) ?? string.Empty;
        var hostPageRelativePath = Path.GetRelativePath(contentRootDir, _hostPage);
        var fileProvider = CreateFileProvider(virtualView, contentRootDir);

        var mauiDispatcher = Microsoft.Maui.Dispatching.Dispatcher.GetForCurrentThread()
            ?? MauiContext.Services.GetService(typeof(IDispatcher)) as IDispatcher
            ?? throw new InvalidOperationException("No MAUI dispatcher for the UI thread");

        _manager = new LinuxWebViewManager(
            PlatformView,
            MauiContext.Services,
            new LinuxBlazorDispatcher(mauiDispatcher),
            fileProvider,
            virtualView.JSComponents,
            hostPageRelativePath);

        foreach (var rootComponent in virtualView.RootComponents)
        {
            if (rootComponent.ComponentType == null || string.IsNullOrEmpty(rootComponent.Selector))
                continue;
            var parameters = rootComponent.Parameters == null
                ? ParameterView.Empty
                : ParameterView.FromDictionary(rootComponent.Parameters);
            _ = _manager.AddRootComponentAsync(rootComponent.ComponentType, rootComponent.Selector, parameters);
        }

        virtualView.BlazorWebViewInitialized(new BlazorWebViewInitializedEventArgs());
        DiagnosticLog.Debug("LinuxBlazorWebViewHandler", $"Starting Blazor: host={_hostPage} root={contentRootDir} start={virtualView.StartPath ?? "/"} components={virtualView.RootComponents.Count}");
        _manager.Navigate(virtualView.StartPath ?? "/");
    }

    private static IFileProvider CreateFileProvider(IBlazorWebView view, string contentRootDir)
    {
        // The generic-TFM BlazorWebView cannot know where a Linux app keeps its
        // wwwroot; resolve it next to the executable and let the view's own
        // provider (if it supplies one) take precedence.
        var physicalRoot = Path.Combine(AppContext.BaseDirectory, contentRootDir);
        IFileProvider? custom = null;
        try
        {
            custom = view.CreateFileProvider(contentRootDir);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("LinuxBlazorWebViewHandler", $"CreateFileProvider fell back to physical: {ex.Message}");
        }

        IFileProvider physical = Directory.Exists(physicalRoot)
            ? new PhysicalFileProvider(physicalRoot)
            : new NullFileProvider();

        if (custom == null || custom is NullFileProvider)
            return physical;
        return new CompositeFileProvider(custom, physical);
    }

    private void OnNavigationDecision(object? sender, WpeWebView.NavigationDecisionEventArgs e)
    {
        if (!Uri.TryCreate(e.Url, UriKind.Absolute, out var uri))
            return;
        if (LinuxWebViewManager.AppOriginUri.IsBaseOf(uri))
            return; // in-app

        var args = CreateUrlLoadingEventArgs(uri);
        if (args == null)
        {
            // Cannot consult the app: keep external links out of the app view.
            e.Cancel = true;
            _ = Launcher.Default.OpenAsync(uri);
            return;
        }

        VirtualView.UrlLoading(args);
        switch (args.UrlLoadingStrategy)
        {
            case UrlLoadingStrategy.OpenExternally:
                e.Cancel = true;
                _ = Launcher.Default.OpenAsync(uri);
                break;
            case UrlLoadingStrategy.CancelLoad:
                e.Cancel = true;
                break;
            case UrlLoadingStrategy.OpenInWebView:
                break;
        }
    }

    private static readonly MethodInfo? s_createUrlLoadingEventArgs = typeof(UrlLoadingEventArgs)
        .GetMethod("CreateWithDefaultLoadingStrategy", BindingFlags.Static | BindingFlags.NonPublic, new[] { typeof(Uri), typeof(Uri) });

    /// <summary>
    /// UrlLoadingEventArgs has an internal factory (default strategy: external
    /// for other origins, in-view for the app origin), identical on every platform.
    /// </summary>
    private static UrlLoadingEventArgs? CreateUrlLoadingEventArgs(Uri uri)
    {
        try
        {
            return s_createUrlLoadingEventArgs?.Invoke(null, new object[] { uri, LinuxWebViewManager.AppOriginUri }) as UrlLoadingEventArgs;
        }
        catch
        {
            return null;
        }
    }
}
