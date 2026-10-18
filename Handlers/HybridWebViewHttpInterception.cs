// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Platform.Linux.Native;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Views;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// <c>HybridWebView.WebResourceRequested</c> for http and https, as on Windows: a HybridWebView
/// whose app handles the event when its handler creates the view gets a network of its own,
/// routed through a <see cref="HybridWebViewHttpProxy"/> on 127.0.0.1, and every http and https
/// request its pages make is offered to the app before it goes out. WebKit has no API to answer
/// a network request itself; it has per-network proxy settings, a per-network list of
/// certificates to accept for a host, and an authenticate signal for proxy credentials, which
/// is all this uses. The network is the view's own (WebKit applies proxy settings and accepted
/// certificates to a whole network), so other views, and this view's TLS to the real servers,
/// are unaffected. Requests go through the proxy only while the app is subscribed: routing is
/// checked whenever a navigation starts and whenever the proxy is asked about a request.
/// <para>
/// The network keeps cookies and storage of its own, in the app's data directory
/// (<c>WebKit/hybridwebview-network-N</c>), so they persist as WebKit's default network's do. A
/// network is reused by the next view that needs one once its view is gone (WebKit allows one
/// network per directory), so N stays 0 while one such view exists at a time.
/// </para>
/// </summary>
internal sealed class HybridWebViewHttpInterception : IHybridWebViewHttpProxyHost, IDisposable
{
    private const string Tag = "HybridWebView";

    private static readonly FieldInfo? s_handlersField =
        typeof(Microsoft.Maui.Controls.HybridWebView).GetField("WebResourceRequested", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>A network (and the proxy certificate it accepts) that views take in turn.</summary>
    private sealed class NetworkSlot
    {
        public NetworkSlot(WebKitContentApi api, int index, IntPtr network, X509Certificate2 certificate, IntPtr tlsCertificate)
        {
            Api = api;
            Index = index;
            Network = network;
            Certificate = certificate;
            TlsCertificate = tlsCertificate;
        }

        public WebKitContentApi Api { get; }
        public int Index { get; }
        public IntPtr Network { get; }
        public X509Certificate2 Certificate { get; }
        public IntPtr TlsCertificate { get; }
        public bool InUse { get; set; }
    }

    private static readonly List<NetworkSlot> s_slots = new();

    private readonly IHybridWebView _view;
    private readonly WebKitContentApi _api;
    private readonly NetworkSlot _slot;
    private HybridWebViewHttpProxy? _proxy;
    private IDispatcher? _dispatcher;
    private SynchronizationContext? _uiContext;
    private ILinuxWebView? _browser;
    private WebKitContentApi.ProxyAuthentication? _authentication;
    private bool _routed;
    private volatile bool _disposed;

    private HybridWebViewHttpInterception(IHybridWebView view, WebKitContentApi api, NetworkSlot slot)
    {
        _view = view;
        _api = api;
        _slot = slot;
    }

    /// <summary>The network the view must be created on (WebKitNetworkSession* on WPE, WebKitWebContext* on WebKitGTK).</summary>
    public IntPtr Network => _slot.Network;

    /// <summary>True when the app handles <c>WebResourceRequested</c> on <paramref name="view"/> right now.</summary>
    internal static bool HasSubscribers(IHybridWebView? view) =>
        view is Microsoft.Maui.Controls.HybridWebView hybrid && s_handlersField?.GetValue(hybrid) is Delegate;

    /// <summary>
    /// The interception for a view about to be created, when its app handles
    /// <c>WebResourceRequested</c> and this WebKit can do it; else null, and the view is created
    /// as before (requests to the app origin are still offered to the app).
    /// </summary>
    internal static HybridWebViewHttpInterception? TryCreate(IHybridWebView? view, WebKitContentApi api)
    {
        if (view == null || !HasSubscribers(view))
            return null;
        if (!api.SupportsProxyNetworks)
        {
            DiagnosticLog.Warn(Tag, "This WebKit cannot route a view through a proxy: WebResourceRequested is raised for the app origin only");
            return null;
        }
        try
        {
            var slot = AcquireSlot(api);
            return slot == null ? null : new HybridWebViewHttpInterception(view, api, slot);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error(Tag, "Setting up http and https interception failed: WebResourceRequested is raised for the app origin only", ex);
            return null;
        }
    }

    private static NetworkSlot? AcquireSlot(WebKitContentApi api)
    {
        lock (s_slots)
        {
            var free = s_slots.FirstOrDefault(s => !s.InUse && s.Api == api);
            if (free != null)
            {
                free.InUse = true;
                return free;
            }

            int index = s_slots.Count(s => s.Api == api);
            var (data, cache) = Directories(index);
            var network = api.CreateProxyNetwork(data, cache);
            if (network == IntPtr.Zero)
                return null;
            var certificate = HybridWebViewHttpProxy.CreateCertificate();
            var tls = api.CreateTlsCertificate(certificate.ExportCertificatePem());
            if (tls == IntPtr.Zero)
            {
                certificate.Dispose();
                return null;
            }
            var slot = new NetworkSlot(api, index, network, certificate, tls) { InUse = true };
            s_slots.Add(slot);
            DiagnosticLog.Debug(Tag, $"Created interception network {index} ({(data == null ? "ephemeral" : data)})");
            return slot;
        }
    }

    private static (string? Data, string? Cache) Directories(int index)
    {
        try
        {
            var files = Microsoft.Maui.Storage.FileSystem.Current;
            var name = $"hybridwebview-network-{index}";
            var data = Path.Combine(files.AppDataDirectory, "WebKit", name);
            var cache = Path.Combine(files.CacheDirectory, "WebKit", name);
            Directory.CreateDirectory(data);
            Directory.CreateDirectory(cache);
            return (data, cache);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Warn(Tag, $"No data directory for the interception network ({ex.Message}); its cookies and storage are not kept");
            return (null, null);
        }
    }

    /// <summary>Starts the proxy and routes the view through it while the app is subscribed. UI thread, once.</summary>
    public void Attach(ILinuxWebView browser)
    {
        // WebKit and the app's handler both run on the UI thread; the proxy runs on the pool.
        _dispatcher = Dispatcher.GetForCurrentThread();
        _uiContext = SynchronizationContext.Current;
        if (_dispatcher == null && _uiContext == null)
            throw new InvalidOperationException("No dispatcher for the UI thread");
        _browser = browser;
        var proxy = new HybridWebViewHttpProxy(this, _slot.Certificate);
        proxy.Start();
        _proxy = proxy;
        _authentication = _api.AnswerProxyAuthentication(browser.NativeWebView, (host, port) =>
            !_disposed && host == "127.0.0.1" && port == proxy.Port ? (HybridWebViewHttpProxy.UserName, proxy.Password) : null);
        browser.NavigationStarting += OnNavigationStarting;
        UpdateRouting();
    }

    private void OnNavigationStarting(object? sender, LinuxWebNavigationStartingEventArgs e) => UpdateRouting();

    /// <summary>Through the proxy while the app is subscribed, else straight out. UI thread.</summary>
    private void UpdateRouting()
    {
        if (_disposed || _proxy == null)
            return;
        bool route = HasSubscribers(_view);
        if (route == _routed)
            return;
        _routed = route;
        _api.SetNetworkProxy(_slot.Network, route ? _proxy.ProxyUri : null);
        DiagnosticLog.Debug(Tag, route
            ? $"http and https requests go through the interception proxy (127.0.0.1:{_proxy.Port})"
            : "WebResourceRequested has no handler: http and https requests go straight out");
    }

    Task IHybridWebViewHttpProxyHost.AllowCertificateForHostAsync(string host) => RunOnUiThreadAsync(() =>
    {
        if (!_disposed)
            _api.AllowTlsCertificateForHost(_slot.Network, _slot.TlsCertificate, host);
        return true;
    });

    async Task<HybridWebResponse?> IHybridWebViewHttpProxyHost.InterceptAsync(HybridWebRequest request)
    {
        var pending = await RunOnUiThreadAsync<Task<HybridWebResponse>?>(() =>
        {
            if (_disposed)
                return null;
            if (!HasSubscribers(_view))
            {
                UpdateRouting();
                return null;
            }
            return HybridWebViewResourceInterception.TryIntercept(_view, request, out var response) ? response : null;
        }).ConfigureAwait(false);
        return pending == null ? null : await pending.ConfigureAwait(false);
    }

    private Task<T> RunOnUiThreadAsync<T>(Func<T> action)
    {
        var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Run()
        {
            try { result.TrySetResult(action()); }
            catch (Exception ex) { result.TrySetException(ex); }
        }

        if (_dispatcher is { } dispatcher)
        {
            if (!dispatcher.IsDispatchRequired)
                Run();
            else if (!dispatcher.Dispatch(Run))
                result.TrySetException(new InvalidOperationException("The UI thread is not running"));
        }
        else
        {
            _uiContext!.Post(_ => Run(), null);
        }
        return result.Task;
    }

    /// <summary>Stops the proxy and gives the network back. UI thread, while the view is still alive.</summary>
    public void Dispose() => Dispose(viewAlive: true);

    /// <summary>As <see cref="Dispose()"/>; <paramref name="viewAlive"/> false when the native view is already destroyed.</summary>
    public void Dispose(bool viewAlive)
    {
        if (_disposed)
            return;
        _disposed = true;
        if (_browser != null)
            _browser.NavigationStarting -= OnNavigationStarting;
        if (viewAlive)
            _authentication?.Dispose();
        else
            _authentication?.Abandon();
        _proxy?.Dispose();
        if (_routed)
            _api.SetNetworkProxy(_slot.Network, null);
        _routed = false;
        lock (s_slots)
            _slot.InUse = false;
    }
}
