using System;
using Microsoft.Maui.Platform.Linux.Native;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public sealed class GtkWebViewPlatformView : IDisposable
{
	private IntPtr _widget;

	private bool _disposed;

	private string? _currentUri;

	private ulong _loadChangedSignalId;

	private WebKitNative.LoadChangedCallback? _loadChangedCallback;

	public IntPtr Widget => _widget;

	public string? CurrentUri => _currentUri;

	public event EventHandler<string>? NavigationStarted;

	public event EventHandler<(string Url, bool Success)>? NavigationCompleted;

	public event EventHandler<string>? TitleChanged;

	public GtkWebViewPlatformView()
	{
		if (!WebKitNative.Initialize())
		{
			throw new InvalidOperationException("Failed to initialize WebKitGTK. Is libwebkit2gtk-4.x installed?");
		}
		_widget = WebKitNative.WebViewNew();
		if (_widget == IntPtr.Zero)
		{
			throw new InvalidOperationException("Failed to create WebKitWebView widget");
		}
		WebKitNative.ConfigureSettings(_widget);
		_loadChangedCallback = OnLoadChanged;
		_loadChangedSignalId = WebKitNative.ConnectLoadChanged(_widget, _loadChangedCallback);
		Console.WriteLine("[GtkWebViewPlatformView] Created WebKitWebView widget");
	}

	private void OnLoadChanged(IntPtr webView, int loadEvent, IntPtr userData)
	{
		try
		{
			string text = WebKitNative.GetUri(webView) ?? _currentUri ?? "";
			switch ((WebKitNative.WebKitLoadEvent)loadEvent)
			{
			case WebKitNative.WebKitLoadEvent.Started:
				Console.WriteLine("[GtkWebViewPlatformView] Load started: " + text);
				this.NavigationStarted?.Invoke(this, text);
				break;
			case WebKitNative.WebKitLoadEvent.Finished:
				_currentUri = text;
				Console.WriteLine("[GtkWebViewPlatformView] Load finished: " + text);
				this.NavigationCompleted?.Invoke(this, (text, true));
				break;
			case WebKitNative.WebKitLoadEvent.Committed:
				_currentUri = text;
				Console.WriteLine("[GtkWebViewPlatformView] Load committed: " + text);
				break;
			case WebKitNative.WebKitLoadEvent.Redirected:
				break;
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("[GtkWebViewPlatformView] Error in OnLoadChanged: " + ex.Message);
			Console.WriteLine("[GtkWebViewPlatformView] Stack trace: " + ex.StackTrace);
		}
	}

	public void Navigate(string uri)
	{
		if (_widget != IntPtr.Zero)
		{
			WebKitNative.LoadUri(_widget, uri);
			Console.WriteLine("[GtkWebViewPlatformView] Navigate to: " + uri);
		}
	}

	public void LoadHtml(string html, string? baseUri = null)
	{
		if (_widget != IntPtr.Zero)
		{
			WebKitNative.LoadHtml(_widget, html, baseUri);
			Console.WriteLine("[GtkWebViewPlatformView] Load HTML content");
		}
	}

	public void GoBack()
	{
		if (_widget != IntPtr.Zero)
		{
			WebKitNative.GoBack(_widget);
		}
	}

	public void GoForward()
	{
		if (_widget != IntPtr.Zero)
		{
			WebKitNative.GoForward(_widget);
		}
	}

	public bool CanGoBack()
	{
		if (_widget == IntPtr.Zero)
		{
			return false;
		}
		return WebKitNative.CanGoBack(_widget);
	}

	public bool CanGoForward()
	{
		if (_widget == IntPtr.Zero)
		{
			return false;
		}
		return WebKitNative.CanGoForward(_widget);
	}

	public void Reload()
	{
		if (_widget != IntPtr.Zero)
		{
			WebKitNative.Reload(_widget);
		}
	}

	public void Stop()
	{
		if (_widget != IntPtr.Zero)
		{
			WebKitNative.StopLoading(_widget);
		}
	}

	public string? GetTitle()
	{
		if (_widget == IntPtr.Zero)
		{
			return null;
		}
		return WebKitNative.GetTitle(_widget);
	}

	public string? GetUri()
	{
		if (_widget == IntPtr.Zero)
		{
			return null;
		}
		return WebKitNative.GetUri(_widget);
	}

	public void SetJavascriptEnabled(bool enabled)
	{
		if (_widget != IntPtr.Zero)
		{
			WebKitNative.SetJavascriptEnabled(_widget, enabled);
		}
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
			if (_widget != IntPtr.Zero)
			{
				WebKitNative.DisconnectLoadChanged(_widget);
			}
			_widget = IntPtr.Zero;
			_loadChangedCallback = null;
		}
	}
}
