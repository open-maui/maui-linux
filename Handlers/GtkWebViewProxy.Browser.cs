// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Native;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Views;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary><see cref="ILinuxWebView"/> on WebKitGTK: the shared WebKit API, plus GTK's snapshots and printing.</summary>
public partial class GtkWebViewProxy : ILinuxWebView
{
    private WebKitContentApi? _content;
    private WebKitBrowserController? _browser;

    private string LibraryName => WebKitNative.LoadedLibraryName ?? "libwebkit2gtk-4.1.so.0";

    public WebViewBackend.Kind Backend => WebViewBackend.Kind.WebKitGtk;

    public IntPtr NativeWebView => _platformView.Widget;

    public WebKitContentApi Content => _content ??= WebKitContentApi.For(LibraryName, WebKitContentApi.Flavor.WebKitGtk41);

    public string? Url => _platformView.GetUri();
    public string? Title => _platformView.GetTitle();
    public bool IsLoading => NativeWebView != IntPtr.Zero && Content.IsLoading(NativeWebView);

    public string? UserAgent
    {
        get => NativeWebView == IntPtr.Zero ? null : Content.GetUserAgent(NativeWebView);
        set { if (NativeWebView != IntPtr.Zero) Content.SetUserAgent(NativeWebView, value); }
    }

    public double ZoomLevel
    {
        get => NativeWebView == IntPtr.Zero ? 1.0 : Content.GetZoomLevel(NativeWebView);
        set { if (NativeWebView != IntPtr.Zero) Content.SetZoomLevel(NativeWebView, value); }
    }

    public event EventHandler<LinuxWebNavigationStartingEventArgs>? NavigationStarting;
    public event EventHandler<LinuxWebNavigationFinishedEventArgs>? NavigationFinished;
    public event EventHandler<string>? UrlChanged;
    public event EventHandler<string>? TitleChanged;
    public event EventHandler<LinuxWebResponseEventArgs>? ResponseReceived;
    public event EventHandler<LinuxWebDownloadEventArgs>? DownloadStarting;
    public event EventHandler<LinuxWebDownloadFinishedEventArgs>? DownloadFinished;

    public bool ZoomGesturesEnabled
    {
        get => _browser?.ZoomGesturesEnabled ?? true;
        set { if (_browser != null) _browser.ZoomGesturesEnabled = value; }
    }

    private void AttachBrowser()
    {
        if (NativeWebView == IntPtr.Zero)
            return;
        try
        {
            _browser = new WebKitBrowserController(Content, NativeWebView, this);
            _browser.NavigationStarting += (s, e) => NavigationStarting?.Invoke(this, e);
            _browser.NavigationFinished += (s, e) => NavigationFinished?.Invoke(this, e);
            _browser.UrlChanged += (s, e) => UrlChanged?.Invoke(this, e);
            _browser.TitleChanged += (s, e) => TitleChanged?.Invoke(this, e);
            _browser.ResponseReceived += (s, e) => ResponseReceived?.Invoke(this, e);
            _browser.DownloadStarting += (s, e) => DownloadStarting?.Invoke(this, e);
            _browser.DownloadFinished += (s, e) => DownloadFinished?.Invoke(this, e);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("GtkWebViewProxy", "The WebKitGTK browser API is unavailable", ex);
        }
    }

    internal void DetachBrowser()
    {
        _browser?.Dispose();
        _browser = null;
    }

    public void StopLoading() => _platformView.Stop();

    public Task<string?> EvaluateJavaScriptAsync(string script) =>
        NativeWebView == IntPtr.Zero ? Task.FromResult<string?>(null) : Content.EvaluateJavaScriptAsync(NativeWebView, script);

    public Task<SKBitmap?> CaptureAsync(bool fullDocument = false) =>
        WebKitGtkExtras.SnapshotAsync(LibraryName, NativeWebView, fullDocument);

    public async Task<bool> SaveAsPdfAsync(string path)
    {
        // A real (vector) print first; the whole document's image when GTK cannot print to a file.
        var printing = WebKitGtkExtras.PrintToPdfAsync(LibraryName, NativeWebView, path);
        var done = await Task.WhenAny(printing, Task.Delay(TimeSpan.FromSeconds(30)));
        if (done == printing && printing.Result)
            return true;

        using var page = await CaptureAsync(fullDocument: true);
        if (page == null)
            return false;
        try
        {
            return BitmapPdfWriter.Write(page, 1f, path);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("GtkWebViewProxy", "Saving the page as PDF failed", ex);
            return false;
        }
    }
}
