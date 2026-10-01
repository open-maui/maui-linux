// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Maui.Platform.Linux.Native;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Views;

/// <summary>
/// The <see cref="ILinuxWebView"/> events for one WebKitWebView, from the signals both WebKit
/// ports share: load-changed and load-failed (with the main resource's HTTP status), the URL
/// and title, decide-policy (navigation, new window, response), create (window.open) and
/// download-started. Signals arrive on the GLib main loop, OpenMaui's UI thread; policy
/// decisions are answered synchronously, as WebKit requires.
/// </summary>
internal sealed class WebKitBrowserController : IDisposable
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void LoadChangedCallback(IntPtr webView, int loadEvent, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int LoadFailedCallback(IntPtr webView, int loadEvent, IntPtr failingUri, IntPtr error, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void NotifyCallback(IntPtr instance, IntPtr paramSpec, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DecidePolicyCallback(IntPtr webView, IntPtr decision, int decisionType, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr CreateCallback(IntPtr webView, IntPtr navigationAction, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void DownloadStartedCallback(IntPtr source, IntPtr download, IntPtr userData);

    private readonly WebKitContentApi _api;
    private readonly IntPtr _webView;
    private readonly ILinuxWebView _owner;
    private readonly IntPtr _downloadSource;

    // Kept alive while connected; dropped (and collectable) after Dispose.
    private readonly LoadChangedCallback _onLoadChanged;
    private readonly LoadFailedCallback _onLoadFailed;
    private readonly NotifyCallback _onUri;
    private readonly NotifyCallback _onTitle;
    private readonly DecidePolicyCallback _onDecidePolicy;
    private readonly CreateCallback _onCreate;
    private readonly DownloadStartedCallback _onDownloadStarted;
    private readonly List<(IntPtr Instance, ulong Id)> _connections = new();

    private bool _loadFailed;
    private string? _errorPageFor; // a failed load's URI, until WebKit's error page for it has started
    private int _mainFrameStatus;
    private bool _disposed;

    internal event EventHandler<LinuxWebNavigationStartingEventArgs>? NavigationStarting;
    internal event EventHandler<LinuxWebNavigationFinishedEventArgs>? NavigationFinished;
    internal event EventHandler<string>? UrlChanged;
    internal event EventHandler<string>? TitleChanged;
    internal event EventHandler<LinuxWebResponseEventArgs>? ResponseReceived;
    internal event EventHandler<LinuxWebDownloadEventArgs>? DownloadStarting;
    internal event EventHandler<LinuxWebDownloadFinishedEventArgs>? DownloadFinished;

    internal WebKitBrowserController(WebKitContentApi api, IntPtr webView, ILinuxWebView owner)
    {
        _api = api;
        _webView = webView;
        _owner = owner;
        _onLoadChanged = OnLoadChanged;
        _onLoadFailed = OnLoadFailed;
        _onUri = (_, _, _) => Raise(UrlChanged, _api.GetUri(_webView) ?? string.Empty, "UrlChanged");
        _onTitle = (_, _, _) => Raise(TitleChanged, _api.GetTitle(_webView) ?? string.Empty, "TitleChanged");
        _onDecidePolicy = OnDecidePolicy;
        _onCreate = OnCreate;
        _onDownloadStarted = OnDownloadStarted;

        Connect(_webView, "load-changed", _onLoadChanged);
        Connect(_webView, "load-failed", _onLoadFailed);
        Connect(_webView, "notify::uri", _onUri);
        Connect(_webView, "notify::title", _onTitle);
        Connect(_webView, "decide-policy", _onDecidePolicy);
        Connect(_webView, "create", _onCreate);
        _downloadSource = _api.GetDownloadSource(_webView);
        Connect(_downloadSource, "download-started", _onDownloadStarted);
        InstallZoomGestures();
    }

    private const string ZoomMessageHandler = "openmauiZoom";
    private const double ZoomStep = 1.1, MinZoom = 0.25, MaxZoom = 5.0;

    // WebKit has no Ctrl+wheel zoom of its own: the page sees the events and reports them.
    private const string ZoomScript = @"(function() {
  if (window.__openmauiZoom) return; window.__openmauiZoom = true;
  function post(m) { try { window.webkit.messageHandlers.openmauiZoom.postMessage(m); } catch (e) {} }
  window.addEventListener('wheel', function(e) {
    if (!e.ctrlKey || e.deltaY === 0) return;
    e.preventDefault();
    post(e.deltaY < 0 ? 'in' : 'out');
  }, { passive: false, capture: true });
  window.addEventListener('keydown', function(e) {
    if (!e.ctrlKey || e.altKey) return;
    var k = e.key;
    if (k === '+' || k === '=') { post('in'); e.preventDefault(); }
    else if (k === '-' || k === '_') { post('out'); e.preventDefault(); }
    else if (k === '0') { post('reset'); e.preventDefault(); }
  }, true);
})();";

    /// <summary>Ctrl+wheel and Ctrl +, -, 0 zoom the page while <see cref="ZoomGesturesEnabled"/>.</summary>
    internal bool ZoomGesturesEnabled { get; set; } = true;

    private void InstallZoomGestures()
    {
        try
        {
            _api.AddUserScript(_webView, ZoomScript, WebKitContentApi.InjectAllFrames, WebKitContentApi.InjectAtDocumentStart);
            _api.RegisterScriptMessageHandler(_webView, ZoomMessageHandler, message =>
            {
                if (!ZoomGesturesEnabled || _disposed)
                    return;
                double zoom = _api.GetZoomLevel(_webView);
                zoom = message switch
                {
                    "in" => zoom * ZoomStep,
                    "out" => zoom / ZoomStep,
                    "reset" => 1.0,
                    _ => zoom,
                };
                _api.SetZoomLevel(_webView, Math.Clamp(Math.Round(zoom, 3), MinZoom, MaxZoom));
            });
        }
        catch (Exception ex)
        {
            DiagnosticLog.Warn("WebKitBrowser", $"Zoom gestures are unavailable: {ex.Message}");
        }
    }

    private void Connect(IntPtr instance, string signal, Delegate handler)
    {
        var id = _api.Connect(instance, signal, handler, keepAlive: false);
        if (id != 0)
            _connections.Add((instance, id));
    }

    private void Raise<T>(EventHandler<T>? handler, T args, string what)
    {
        if (handler == null || _disposed) return;
        try
        {
            handler(_owner, args);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("WebKitBrowser", $"A {what} handler failed", ex);
        }
    }

    private void OnLoadChanged(IntPtr webView, int loadEvent, IntPtr userData)
    {
        switch (loadEvent)
        {
            case WebKitContentApi.LoadStarted:
                // After a failure WebKit loads its own error page, a load of its own for the same
                // URI: it belongs to the failed load, which was reported already.
                if (_errorPageFor != null && _errorPageFor == _api.GetUri(_webView))
                {
                    _errorPageFor = null;
                    break;
                }
                _errorPageFor = null;
                _loadFailed = false;
                _mainFrameStatus = 0;
                break;
            case WebKitContentApi.LoadFinished:
                // A failed load is followed by FINISHED; it was reported as failed already.
                if (_loadFailed)
                    break;
                var status = _api.GetMainResourceStatus(_webView);
                if (status == 0)
                    status = _mainFrameStatus;
                Raise(NavigationFinished, new LinuxWebNavigationFinishedEventArgs(_api.GetUri(_webView) ?? string.Empty, true, status, null), "NavigationFinished");
                break;
        }
    }

    private int OnLoadFailed(IntPtr webView, int loadEvent, IntPtr failingUri, IntPtr error, IntPtr userData)
    {
        _loadFailed = true;
        var uri = Marshal.PtrToStringUTF8(failingUri) ?? _api.GetUri(_webView) ?? string.Empty;
        _errorPageFor = uri;
        var status = _api.GetMainResourceStatus(_webView);
        if (status == 0)
            status = _mainFrameStatus;
        Raise(NavigationFinished, new LinuxWebNavigationFinishedEventArgs(uri, false, status, WebKitContentApi.GErrorMessage(error)), "NavigationFinished");
        return 0; // WebKit shows its own error page, as WebView2 and WKWebView do
    }

    private int OnDecidePolicy(IntPtr webView, IntPtr decision, int decisionType, IntPtr userData)
    {
        try
        {
            switch (decisionType)
            {
                case WebKitContentApi.PolicyNavigationAction:
                case WebKitContentApi.PolicyNewWindowAction:
                {
                    var uri = _api.GetNavigationDecisionUri(decision);
                    if (uri == null)
                        return 0;
                    bool newWindow = decisionType == WebKitContentApi.PolicyNewWindowAction;
                    var args = new LinuxWebNavigationStartingEventArgs(uri, (LinuxWebNavigationType)_api.GetNavigationDecisionType(decision), newWindow);
                    Raise(NavigationStarting, args, "NavigationStarting");
                    if (args.Cancel)
                    {
                        _api.PolicyIgnore(decision);
                        return 1;
                    }
                    if (newWindow)
                    {
                        // No second window to open: the page's new window loads here, or nowhere.
                        _api.PolicyIgnore(decision);
                        if (args.OpenNewWindowInPlace && IsLoadable(uri))
                            _owner.Navigate(uri);
                        return 1;
                    }
                    return 0;
                }
                case WebKitContentApi.PolicyResponse:
                {
                    var response = _api.GetResponse(decision);
                    bool mainFrame = _api.IsMainFrameResponse(decision);
                    int status = _api.GetResponseStatus(response);
                    if (mainFrame)
                        _mainFrameStatus = status;
                    var args = new LinuxWebResponseEventArgs(
                        _api.GetResponseUri(response) ?? string.Empty,
                        _api.GetResponseMimeType(response),
                        status,
                        _api.IsResponseMimeTypeSupported(decision),
                        mainFrame,
                        _api.GetResponseSuggestedFilename(response));
                    Raise(ResponseReceived, args, "ResponseReceived");
                    switch (args.Action)
                    {
                        case LinuxWebResponseAction.Show: _api.PolicyUse(decision); return 1;
                        case LinuxWebResponseAction.Ignore: _api.PolicyIgnore(decision); return 1;
                        case LinuxWebResponseAction.Download: _api.PolicyDownload(decision); return 1;
                        default: return 0;
                    }
                }
                default:
                    return 0;
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("WebKitBrowser", "decide-policy failed", ex);
            return 0;
        }
    }

    /// <summary>
    /// window.open and target=_blank that reach "create" (a new-window decision WebKit made itself):
    /// the URL loads here, the same as <see cref="OnDecidePolicy"/> does, and no new view is made.
    /// </summary>
    private IntPtr OnCreate(IntPtr webView, IntPtr navigationAction, IntPtr userData)
    {
        try
        {
            var uri = _api.GetNavigationActionUri(navigationAction);
            if (uri != null)
            {
                var args = new LinuxWebNavigationStartingEventArgs(uri, LinuxWebNavigationType.Other, isNewWindow: true);
                Raise(NavigationStarting, args, "NavigationStarting");
                if (!args.Cancel && args.OpenNewWindowInPlace && IsLoadable(uri))
                    _owner.Navigate(uri);
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("WebKitBrowser", "create failed", ex);
        }
        return IntPtr.Zero;
    }

    private void OnDownloadStarted(IntPtr source, IntPtr download, IntPtr userData)
    {
        try
        {
            // The session (or context) is shared by every view: only this view's downloads.
            if (_api.GetDownloadWebView(download) != _webView)
                return;
            var args = new LinuxWebDownloadEventArgs(_api.GetDownloadUri(download) ?? string.Empty, null);
            Raise(DownloadStarting, args, "DownloadStarting");
            if (args.Cancel)
            {
                _api.CancelDownload(download);
                return;
            }
            if (!string.IsNullOrEmpty(args.DestinationPath))
            {
                // WebKit takes the destination set during decide-destination (its default handler
                // would put it in the Downloads folder): set it now and again then.
                var destination = Path.GetFullPath(args.DestinationPath);
                _api.SetDownloadDestination(download, destination);
                s_chosenDestinations[download] = (_api, destination);
                _api.Connect(download, "decide-destination", s_onDecideDestination);
            }
            else if (args.AskWhereToSave)
            {
                // The name is known at decide-destination, where the dialog is opened.
                s_asks[download] = new PendingSave(this, args.Url);
                _api.Connect(download, "decide-destination", s_onDecideAsk);
            }
            s_downloads[download] = (this, args.Url);
            _api.Connect(download, "failed", s_onDownloadFailed);
            _api.Connect(download, "finished", s_onDownloadDone);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("WebKitBrowser", "download-started failed", ex);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void DownloadFailedCallback(IntPtr download, IntPtr error, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void DownloadFinishedCallback(IntPtr download, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DecideDestinationCallback(IntPtr download, IntPtr suggestedFilename, IntPtr userData);

    // Downloads whose destination the app chose, until they end.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<IntPtr, (WebKitContentApi Api, string Path)> s_chosenDestinations = new();

    private static readonly DecideDestinationCallback s_onDecideDestination = (download, _, _) =>
    {
        if (!s_chosenDestinations.TryGetValue(download, out var chosen))
            return 0;
        chosen.Api.SetDownloadDestination(download, chosen.Path);
        return 1;
    };

    // Downloads in flight, for DownloadFinished; removed when reported (WebKit emits "finished"
    // after "failed" too, so each is reported once).
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<IntPtr, (WebKitBrowserController Owner, string Url)> s_downloads = new();

    private static readonly DownloadFinishedCallback s_onDownloadDone = (download, userData) =>
    {
        s_chosenDestinations.TryRemove(download, out var ignored);
        if (s_asks.TryGetValue(download, out var ask))
        {
            s_downloads.TryRemove(download, out _);
            if (!ask.Failed)
                ask.Finished = true;
            if (ask.Decided)
                ask.Owner.CompleteAsk(download, ask);
            return;
        }
        if (s_downloads.TryRemove(download, out var entry))
            entry.Owner.ReportDownload(download, entry.Url, true, null);
    };

    private static readonly DownloadFailedCallback s_onDownloadFailed = (download, error, userData) =>
    {
        s_chosenDestinations.TryRemove(download, out var ignored);
        var message = WebKitContentApi.GErrorMessage(error);
        if (s_asks.TryGetValue(download, out var ask))
        {
            s_downloads.TryRemove(download, out _);
            ask.Failed = true;
            ask.Error = message;
            if (ask.Decided)
                ask.Owner.CompleteAsk(download, ask);
            return;
        }
        DiagnosticLog.Warn("WebKitBrowser", $"A download failed: {message}");
        if (s_downloads.TryRemove(download, out var entry))
            entry.Owner.ReportDownload(download, entry.Url, false, message);
    };

    private void ReportDownload(IntPtr download, string url, bool success, string? error) =>
        Raise(DownloadFinished, new LinuxWebDownloadFinishedEventArgs(url, _api.GetDownloadDestination(download), success, error), "DownloadFinished");

    // ---- Save dialog (AskWhereToSave) ------------------------------------------------------

    /// <summary>A download whose destination the user is choosing; it is written to a temporary file meanwhile.</summary>
    private sealed class PendingSave(WebKitBrowserController owner, string url)
    {
        public WebKitBrowserController Owner { get; } = owner;
        public string Url { get; } = url;
        public string TempPath { get; set; } = string.Empty;
        public bool Finished { get; set; }
        public bool Failed { get; set; }
        public string? Error { get; set; }
        public bool Decided { get; set; }
        public string? Target { get; set; }
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<IntPtr, PendingSave> s_asks = new();

    /// <summary>True while the user is asked where <paramref name="download"/> goes (other handlers leave it alone).</summary>
    internal static bool AsksWhereToSave(IntPtr download) => s_asks.ContainsKey(download);

    /// <summary>Stands in for the desktop's Save dialog (tests): the suggested name in, the chosen path or null out.</summary>
    internal static Func<string, Task<string?>>? SaveDialogOverride { get; set; }

    private static readonly DecideDestinationCallback s_onDecideAsk = (download, suggestedFilename, userData) =>
    {
        if (!s_asks.TryGetValue(download, out var ask))
            return 0;
        try
        {
            var name = SafeFileName(Marshal.PtrToStringUTF8(suggestedFilename));
            var folder = Path.Combine(Path.GetTempPath(), "openmaui-downloads", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            ask.TempPath = Path.Combine(folder, name);
            ask.Owner._api.SetDownloadDestination(download, ask.TempPath);
            ask.Owner.AskWhereToSave(download, ask, name);
            return 1;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("WebKitBrowser", "Preparing a download's Save dialog failed", ex);
            s_asks.TryRemove(download, out _);
            return 0; // WebKit's default: the Downloads folder
        }
    };

    private static string SafeFileName(string? suggested)
    {
        var name = Path.GetFileName(suggested ?? string.Empty);
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) || name is "." or ".." ? "download" : name;
    }

    private async void AskWhereToSave(IntPtr download, PendingSave ask, string suggestedName)
    {
        string? target = null;
        try
        {
            var dialog = SaveDialogOverride?.Invoke(suggestedName)
                ?? ToolkitFileSaver.PickSavePathAsync(DownloadsFolder(), suggestedName, CancellationToken.None);
            target = await dialog;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("WebKitBrowser", "The download's Save dialog failed", ex);
        }
        OnMainThread(() =>
        {
            ask.Decided = true;
            ask.Target = target;
            if (target == null && !ask.Finished && !ask.Failed)
            {
                _api.CancelDownload(download); // "failed" (cancelled) completes it
                return;
            }
            if (ask.Finished || ask.Failed)
                CompleteAsk(download, ask);
        });
    }

    /// <summary>Both the download and the dialog are done: the file goes where the user chose.</summary>
    private void CompleteAsk(IntPtr download, PendingSave ask)
    {
        if (!s_asks.TryRemove(download, out _))
            return;
        var folder = Path.GetDirectoryName(ask.TempPath);
        try
        {
            if (ask.Target == null)
                return; // the user cancelled: not reported, as a cancelled download is not
            if (ask.Failed)
            {
                Raise(DownloadFinished, new LinuxWebDownloadFinishedEventArgs(ask.Url, null, false, ask.Error), "DownloadFinished");
                return;
            }
            File.Move(ask.TempPath, ask.Target, overwrite: true); // the dialog confirmed replacing it
            Raise(DownloadFinished, new LinuxWebDownloadFinishedEventArgs(ask.Url, ask.Target, true, null), "DownloadFinished");
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("WebKitBrowser", $"Saving a download to {ask.Target} failed", ex);
            Raise(DownloadFinished, new LinuxWebDownloadFinishedEventArgs(ask.Url, null, false, ex.Message), "DownloadFinished");
        }
        finally
        {
            try
            {
                if (folder != null && Directory.Exists(folder))
                    Directory.Delete(folder, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static string? DownloadsFolder()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var downloads = Path.Combine(home, "Downloads");
        return Directory.Exists(downloads) ? downloads : home;
    }

    private static void OnMainThread(Action action)
    {
        var main = Microsoft.Maui.Platform.Linux.Dispatching.LinuxDispatcher.Main;
        if (main == null || Microsoft.Maui.Platform.Linux.Dispatching.LinuxDispatcher.IsMainThread)
            action();
        else
            main.Dispatch(action);
    }

    private static bool IsLoadable(string uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var parsed)
        && parsed.Scheme is "http" or "https" or "file" or "data" or "about";

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var (instance, id) in _connections)
            _api.Disconnect(instance, id);
        _connections.Clear();
    }
}
