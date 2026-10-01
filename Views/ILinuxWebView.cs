// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Native;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Views;

/// <summary>
/// A WebView's browser, the same on both of OpenMaui's backends (WPE WebKit and WebKitGTK): what a
/// library needs beyond MAUI's WebView, as WebView2 and WKWebView give it on the other platforms.
/// <see cref="LinuxWebViewHandler.Browser"/> is the WebView's; <see cref="NativeWebView"/> and
/// <see cref="Content"/> reach the rest of WebKit's GLib API.
/// </summary>
public interface ILinuxWebView
{
    /// <summary>The engine behind this view.</summary>
    WebViewBackend.Kind Backend { get; }

    /// <summary>The WebKitWebView, for WebKit calls this interface does not cover.</summary>
    IntPtr NativeWebView { get; }

    /// <summary>WebKit's GLib API for this view's library: scripts, script messages, URI schemes, policy decisions.</summary>
    WebKitContentApi Content { get; }

    string? Url { get; }
    string? Title { get; }
    bool CanGoBack { get; }
    bool CanGoForward { get; }
    bool IsLoading { get; }

    /// <summary>The user agent; null or empty restores WebKit's own.</summary>
    string? UserAgent { get; set; }

    /// <summary>The page zoom (1 is 100%).</summary>
    double ZoomLevel { get; set; }

    /// <summary>
    /// Ctrl+wheel and Ctrl +, -, 0 zoom the page, as WebView2 does by default (the default is on).
    /// </summary>
    bool ZoomGesturesEnabled { get; set; }

    void Navigate(string url);
    void LoadHtml(string html, string? baseUrl);
    void GoBack();
    void GoForward();
    void Reload();
    void StopLoading();

    /// <summary>Runs a script; completes with its result when it is a string, else null.</summary>
    Task<string?> EvaluateJavaScriptAsync(string script);

    /// <summary>The page as an image: what is in view, or the whole document. Null when it cannot be captured.</summary>
    Task<SKBitmap?> CaptureAsync(bool fullDocument = false);

    /// <summary>
    /// Writes the page to <paramref name="path"/> as a PDF: printed by WebKitGTK, or from the whole
    /// document's image on WPE, which has no print engine. False when it cannot be written.
    /// </summary>
    Task<bool> SaveAsPdfAsync(string path);

    /// <summary>A navigation, or a new window (target=_blank, window.open), is about to start. Cancellable.</summary>
    event EventHandler<LinuxWebNavigationStartingEventArgs>? NavigationStarting;

    /// <summary>A main-frame load finished or failed, with its HTTP status.</summary>
    event EventHandler<LinuxWebNavigationFinishedEventArgs>? NavigationFinished;

    /// <summary>The page's URL changed (also within a page, as single-page apps change it).</summary>
    event EventHandler<string>? UrlChanged;

    /// <summary>The page's title changed.</summary>
    event EventHandler<string>? TitleChanged;

    /// <summary>A response arrived; the handler can show it, ignore it or download it.</summary>
    event EventHandler<LinuxWebResponseEventArgs>? ResponseReceived;

    /// <summary>A download is starting; the handler can cancel it or choose where it is saved.</summary>
    event EventHandler<LinuxWebDownloadEventArgs>? DownloadStarting;

    /// <summary>A download (not cancelled) finished or failed.</summary>
    event EventHandler<LinuxWebDownloadFinishedEventArgs>? DownloadFinished;
}

/// <summary>What started a navigation.</summary>
public enum LinuxWebNavigationType
{
    LinkClicked,
    FormSubmitted,
    BackForward,
    Reload,
    FormResubmitted,
    Other,
}

/// <summary>A navigation or new window about to start.</summary>
public sealed class LinuxWebNavigationStartingEventArgs : EventArgs
{
    public LinuxWebNavigationStartingEventArgs(string url, LinuxWebNavigationType navigationType, bool isNewWindow)
    {
        Url = url;
        NavigationType = navigationType;
        IsNewWindow = isNewWindow;
    }

    public string Url { get; }
    public LinuxWebNavigationType NavigationType { get; }

    /// <summary>The page asked for a new window (target=_blank, window.open).</summary>
    public bool IsNewWindow { get; }

    /// <summary>Stops the navigation (or the new window).</summary>
    public bool Cancel { get; set; }

    /// <summary>For a new window: load its URL in this view (the default), as an embedded browser has no other window.</summary>
    public bool OpenNewWindowInPlace { get; set; } = true;
}

/// <summary>A main-frame load that finished or failed.</summary>
public sealed class LinuxWebNavigationFinishedEventArgs : EventArgs
{
    public LinuxWebNavigationFinishedEventArgs(string url, bool success, int httpStatusCode, string? error)
    {
        Url = url;
        Success = success;
        HttpStatusCode = httpStatusCode;
        Error = error;
    }

    public string Url { get; }

    /// <summary>The page loaded (an HTTP error page that loaded counts: see <see cref="HttpStatusCode"/>).</summary>
    public bool Success { get; }

    /// <summary>The main resource's HTTP status, 0 when there is none (a failed connection, HTML loaded from a string).</summary>
    public int HttpStatusCode { get; }

    /// <summary>Why the load failed, when it did.</summary>
    public string? Error { get; }
}

/// <summary>What to do with a response.</summary>
public enum LinuxWebResponseAction
{
    /// <summary>WebKit's choice: show what it can, download what it cannot.</summary>
    Default,
    Show,
    Ignore,
    Download,
}

/// <summary>A response to a request the view made.</summary>
public sealed class LinuxWebResponseEventArgs : EventArgs
{
    public LinuxWebResponseEventArgs(string url, string? mimeType, int httpStatusCode, bool canShow, bool isMainFrame, string? suggestedFileName)
    {
        Url = url;
        MimeType = mimeType;
        HttpStatusCode = httpStatusCode;
        CanShow = canShow;
        IsMainFrame = isMainFrame;
        SuggestedFileName = suggestedFileName;
    }

    public string Url { get; }
    public string? MimeType { get; }
    public int HttpStatusCode { get; }

    /// <summary>WebKit can display this MIME type itself.</summary>
    public bool CanShow { get; }

    /// <summary>The main frame's document, not a subresource or an iframe.</summary>
    public bool IsMainFrame { get; }

    public string? SuggestedFileName { get; }

    public LinuxWebResponseAction Action { get; set; } = LinuxWebResponseAction.Default;
}

/// <summary>A download about to start.</summary>
public sealed class LinuxWebDownloadEventArgs : EventArgs
{
    public LinuxWebDownloadEventArgs(string url, string? suggestedFileName)
    {
        Url = url;
        SuggestedFileName = suggestedFileName;
    }

    public string Url { get; }
    public string? SuggestedFileName { get; }

    /// <summary>Stops the download.</summary>
    public bool Cancel { get; set; }

    /// <summary>Where to save it; null keeps the default (the Downloads folder, under a unique name).</summary>
    public string? DestinationPath { get; set; }

    /// <summary>
    /// Asks where to save it with the desktop's Save dialog, under the name the server suggests,
    /// as WebView2 and browsers do: the file downloads while the dialog is open and is moved where
    /// the user chooses; cancelling the dialog cancels the download. Ignored when
    /// <see cref="DestinationPath"/> is set. <see cref="LinuxWebViewHandler"/> asks by default.
    /// </summary>
    public bool AskWhereToSave { get; set; }
}

/// <summary>A download that finished or failed.</summary>
public sealed class LinuxWebDownloadFinishedEventArgs : EventArgs
{
    public LinuxWebDownloadFinishedEventArgs(string url, string? path, bool success, string? error)
    {
        Url = url;
        Path = path;
        Success = success;
        Error = error;
    }

    public string Url { get; }

    /// <summary>Where it was saved.</summary>
    public string? Path { get; }

    public bool Success { get; }

    /// <summary>Why it failed, when it did.</summary>
    public string? Error { get; }
}
