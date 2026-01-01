using System;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Window;

namespace Microsoft.Maui.Platform.Linux.Services;

public class GtkHostService
{
	private static GtkHostService? _instance;

	private GtkHostWindow? _hostWindow;

	private GtkWebViewManager? _webViewManager;

	public static GtkHostService Instance => _instance ?? (_instance = new GtkHostService());

	public GtkHostWindow? HostWindow => _hostWindow;

	public GtkWebViewManager? WebViewManager => _webViewManager;

	public bool IsInitialized => _hostWindow != null;

	public event EventHandler<GtkHostWindow>? HostWindowCreated;

	public void Initialize(string title, int width, int height)
	{
		if (_hostWindow == null)
		{
			_hostWindow = new GtkHostWindow(title, width, height);
			_webViewManager = new GtkWebViewManager(_hostWindow);
			this.HostWindowCreated?.Invoke(this, _hostWindow);
		}
	}

	public GtkHostWindow GetOrCreateHostWindow(string title = "MAUI Application", int width = 800, int height = 600)
	{
		if (_hostWindow == null)
		{
			Initialize(title, width, height);
		}
		return _hostWindow;
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
