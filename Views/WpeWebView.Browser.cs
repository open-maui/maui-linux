// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Native;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Views;

/// <summary><see cref="ILinuxWebView"/> on WPE WebKit.</summary>
public partial class WpeWebView : ILinuxWebView
{
    /// <summary>The tallest whole-document capture, in device pixels (a GPU texture limit).</summary>
    private const int MaxCaptureHeight = 16384;

    private WebKitBrowserController? _browser;
    private bool _capturing;
    private TaskCompletionSource<SKBitmap?>? _captureWaiter;
    private int _captureWidth, _captureHeight; // device pixels the capture frame has

    public WebViewBackend.Kind Backend => WebViewBackend.Kind.Wpe;

    public string? Url => CurrentUri;

    public bool IsLoading => _webView != IntPtr.Zero && Content.IsLoading(_webView);

    public double ZoomLevel
    {
        get => _webView == IntPtr.Zero ? 1.0 : Content.GetZoomLevel(_webView);
        set { if (_webView != IntPtr.Zero) Content.SetZoomLevel(_webView, value); }
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

    private void HookBrowserEvents()
    {
        if (_browser == null) return;
        _browser.NavigationStarting += (s, e) => NavigationStarting?.Invoke(this, e);
        _browser.NavigationFinished += (s, e) => NavigationFinished?.Invoke(this, e);
        _browser.UrlChanged += (s, e) => UrlChanged?.Invoke(this, e);
        _browser.TitleChanged += (s, e) => TitleChanged?.Invoke(this, e);
        _browser.ResponseReceived += (s, e) => ResponseReceived?.Invoke(this, e);
        _browser.DownloadStarting += (s, e) => DownloadStarting?.Invoke(this, e);
        _browser.DownloadFinished += (s, e) => DownloadFinished?.Invoke(this, e);
    }

    public async Task<SKBitmap?> CaptureAsync(bool fullDocument = false)
    {
        if (_webView == IntPtr.Zero || _wpeView == IntPtr.Zero)
            return null;
        if (!fullDocument)
            return CopyCurrentFrame();
        if (_capturing)
            return null; // one at a time

        // The document's size in CSS pixels; the width stays the view's, so the page does not reflow.
        var json = await EvaluateJavaScriptAsync(
            "JSON.stringify([Math.max(document.documentElement.scrollHeight, document.body ? document.body.scrollHeight : 0)])");
        int documentHeight = 0;
        try
        {
            if (json != null)
                documentHeight = JsonSerializer.Deserialize<int[]>(json)?.FirstOrDefault() ?? 0;
        }
        catch (JsonException)
        {
        }

        double scale = Scale <= 0 ? 1 : Scale;
        int width = Math.Max(1, _requestedWidth);
        int height = Math.Min(documentHeight, (int)(MaxCaptureHeight / scale));
        if (height <= _requestedHeight)
            return CopyCurrentFrame(); // it all fits in view already

        var waiter = new TaskCompletionSource<SKBitmap?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _captureWidth = (int)Math.Round(width * scale);
        _captureHeight = (int)Math.Round(height * scale);
        _captureWaiter = waiter;
        _capturing = true;
        try
        {
            var toplevel = WpeNative.wpe_view_get_toplevel(_wpeView);
            if (toplevel != IntPtr.Zero)
                WpeNative.wpe_toplevel_resized(toplevel, width, height);
            WpeNative.wpe_view_resized(_wpeView, width, height);

            var done = await Task.WhenAny(waiter.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            return done == waiter.Task ? waiter.Task.Result : null;
        }
        finally
        {
            _captureWaiter = null;
            _capturing = false;
            // Back to the view's size.
            _requestedWidth = _requestedHeight = -1;
            SyncSize();
            Invalidate();
        }
    }

    /// <summary>
    /// While capturing, frames are not shown (the view is the document's height for a moment); the
    /// one at the capture size is copied for <see cref="CaptureAsync"/>.
    /// </summary>
    private void TryCompleteCapture(IntPtr buffer)
    {
        var waiter = _captureWaiter;
        if (waiter == null)
            return;
        int w = WpeNative.wpe_buffer_get_width(buffer);
        int h = WpeNative.wpe_buffer_get_height(buffer);
        if (Math.Abs(w - _captureWidth) > 1 || Math.Abs(h - _captureHeight) > 1)
            return;
        var bytes = WpeNative.wpe_buffer_import_to_pixels(buffer, out var err);
        if (bytes == IntPtr.Zero)
        {
            DiagnosticLog.Warn("WpeWebView", $"Capture import failed: {WpeNative.ConsumeError(err, "unknown")}");
            waiter.TrySetResult(null);
            return;
        }
        var data = WpeNative.g_bytes_get_data(bytes, out var size);
        long needed = (long)w * h * 4;
        if (data == IntPtr.Zero || (long)size < needed)
        {
            waiter.TrySetResult(null);
            return;
        }
        var bitmap = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul));
        unsafe
        {
            Buffer.MemoryCopy((void*)data, (void*)bitmap.GetPixels(), needed, needed);
        }
        bitmap.NotifyPixelsChanged();
        waiter.TrySetResult(bitmap);
    }

    /// <summary>The newest frame, brought up to date from the GPU buffer when it is only there.</summary>
    private SKBitmap? CopyCurrentFrame()
    {
        if (_bitmapStale)
        {
            var newest = _pendingBuffer != IntPtr.Zero ? _pendingBuffer : _gpuFrame?.Buffer ?? IntPtr.Zero;
            if (newest != IntPtr.Zero && CopyFramePixels(newest))
            {
                _bitmapStale = false;
                if (newest == _pendingBuffer) _pendingCopied = true;
            }
        }
        lock (_frameLock)
            return _frame?.Copy();
    }

    public async Task<bool> SaveAsPdfAsync(string path)
    {
        using var page = await CaptureAsync(fullDocument: true);
        if (page == null)
            return false;
        try
        {
            return BitmapPdfWriter.Write(page, (float)(Scale <= 0 ? 1 : Scale), path);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("WpeWebView", "Saving the page as PDF failed", ex);
            return false;
        }
    }

    // ILinuxWebView members WpeWebView has under its own names.
    void ILinuxWebView.StopLoading() => StopLoading();
}
