using System;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Window;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Singleton service that manages the GTK host window and WebView manager.
/// Provides centralized access to the GTK infrastructure for MAUI applications.
/// </summary>
public class GtkHostService
{
    private static GtkHostService? _instance;
    private GtkHostWindow? _hostWindow;
    private GtkWebViewManager? _webViewManager;

    public static GtkHostService Instance => _instance ??= new GtkHostService();

    public GtkHostWindow? HostWindow => _hostWindow;
    public GtkWebViewManager? WebViewManager => _webViewManager;
    public bool IsInitialized => _hostWindow != null;

    public event EventHandler<GtkHostWindow>? HostWindowCreated;

    private string _title = "MAUI Application";
    private int _width = 800;
    private int _height = 600;

    /// <summary>
    /// Records the title and size the host window is created with. The window itself is created
    /// on first use (<see cref="EnsureHostWindow"/>: a WebView shown in the page, or GTK mode),
    /// since creating it initializes GTK's widgets and loads the theme CSS, which an app without
    /// them should not pay for at startup.
    /// </summary>
    public void Configure(string title, int width, int height)
    {
        _title = title;
        _width = width;
        _height = height;
    }

    /// <summary>
    /// The host window, created with the configured title and size on first call; null when GTK
    /// cannot create it (logged).
    /// </summary>
    internal GtkHostWindow? EnsureHostWindow()
    {
        if (_hostWindow != null)
            return _hostWindow;
        try
        {
            Initialize(_title, _width, _height);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("GtkHostService", "Could not create the GTK host window", ex);
        }
        return _hostWindow;
    }

    public void Initialize(string title, int width, int height)
    {
        if (_hostWindow == null)
        {
            _hostWindow = new GtkHostWindow(title, width, height);
            _webViewManager = new GtkWebViewManager(_hostWindow);
            HostWindowCreated?.Invoke(this, _hostWindow);
        }
    }

    public GtkHostWindow GetOrCreateHostWindow(string title = "MAUI Application", int width = 800, int height = 600)
    {
        if (_hostWindow == null)
        {
            Initialize(title, width, height);
        }
        return _hostWindow!;
    }

    public void SetWindowIcon(string iconPath)
    {
        _hostWindow?.SetIcon(iconPath);
    }

    public void Shutdown()
    {
        _webViewManager?.Clear();
        _webViewManager = null;
        _hostWindow?.Dispose();
        _hostWindow = null;
    }
}
