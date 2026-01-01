using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Native;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class GtkWebViewHandler : ViewHandler<IWebView, GtkWebViewProxy>
{
	private GtkWebViewPlatformView? _platformWebView;

	private bool _isRegisteredWithHost;

	private SKRect _lastBounds;

	public static IPropertyMapper<IWebView, GtkWebViewHandler> Mapper = (IPropertyMapper<IWebView, GtkWebViewHandler>)(object)new PropertyMapper<IWebView, GtkWebViewHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)ViewHandler.ViewMapper }) { ["Source"] = MapSource };

	public static CommandMapper<IWebView, GtkWebViewHandler> CommandMapper = new CommandMapper<IWebView, GtkWebViewHandler>((CommandMapper)(object)ViewHandler.ViewCommandMapper)
	{
		["GoBack"] = MapGoBack,
		["GoForward"] = MapGoForward,
		["Reload"] = MapReload
	};

	public GtkWebViewHandler()
		: base((IPropertyMapper)(object)Mapper, (CommandMapper)(object)CommandMapper)
	{
	}

	public GtkWebViewHandler(IPropertyMapper? mapper = null, CommandMapper? commandMapper = null)
		: base((IPropertyMapper)(((object)mapper) ?? ((object)Mapper)), (CommandMapper)(((object)commandMapper) ?? ((object)CommandMapper)))
	{
	}

	protected override GtkWebViewProxy CreatePlatformView()
	{
		_platformWebView = new GtkWebViewPlatformView();
		return new GtkWebViewProxy(this, _platformWebView);
	}

	protected override void ConnectHandler(GtkWebViewProxy platformView)
	{
		base.ConnectHandler(platformView);
		if (_platformWebView != null)
		{
			_platformWebView.NavigationStarted += OnNavigationStarted;
			_platformWebView.NavigationCompleted += OnNavigationCompleted;
		}
		Console.WriteLine("[GtkWebViewHandler] ConnectHandler - WebView ready");
	}

	protected override void DisconnectHandler(GtkWebViewProxy platformView)
	{
		if (_platformWebView != null)
		{
			_platformWebView.NavigationStarted -= OnNavigationStarted;
			_platformWebView.NavigationCompleted -= OnNavigationCompleted;
			UnregisterFromHost();
			_platformWebView.Dispose();
			_platformWebView = null;
		}
		base.DisconnectHandler(platformView);
	}

	private void OnNavigationStarted(object? sender, string uri)
	{
		Console.WriteLine("[GtkWebViewHandler] Navigation started: " + uri);
		try
		{
			GLibNative.IdleAdd(delegate
			{
				//IL_001c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0022: Expected O, but got Unknown
				try
				{
					IWebView virtualView = base.VirtualView;
					IWebViewController val = (IWebViewController)(object)((virtualView is IWebViewController) ? virtualView : null);
					if (val != null)
					{
						WebNavigatingEventArgs e = new WebNavigatingEventArgs((WebNavigationEvent)3, (WebViewSource)null, uri);
						val.SendNavigating(e);
						Console.WriteLine("[GtkWebViewHandler] Sent Navigating event to VirtualView");
					}
				}
				catch (Exception ex2)
				{
					Console.WriteLine("[GtkWebViewHandler] Error in SendNavigating: " + ex2.Message);
				}
				return false;
			});
		}
		catch (Exception ex)
		{
			Console.WriteLine("[GtkWebViewHandler] Error dispatching navigation started: " + ex.Message);
		}
	}

	private void OnNavigationCompleted(object? sender, (string Url, bool Success) e)
	{
		Console.WriteLine($"[GtkWebViewHandler] Navigation completed: {e.Url} (Success: {e.Success})");
		try
		{
			GLibNative.IdleAdd(delegate
			{
				//IL_0028: Unknown result type (might be due to invalid IL or missing references)
				//IL_0036: Unknown result type (might be due to invalid IL or missing references)
				//IL_0037: Unknown result type (might be due to invalid IL or missing references)
				//IL_003d: Expected O, but got Unknown
				try
				{
					IWebView virtualView = base.VirtualView;
					IWebViewController val = (IWebViewController)(object)((virtualView is IWebViewController) ? virtualView : null);
					if (val != null)
					{
						WebNavigationResult val2 = (WebNavigationResult)(e.Success ? 1 : 4);
						WebNavigatedEventArgs e2 = new WebNavigatedEventArgs((WebNavigationEvent)3, (WebViewSource)null, e.Url, val2);
						val.SendNavigated(e2);
						bool flag = _platformWebView?.CanGoBack() ?? false;
						bool flag2 = _platformWebView?.CanGoForward() ?? false;
						val.CanGoBack = flag;
						val.CanGoForward = flag2;
						Console.WriteLine($"[GtkWebViewHandler] Sent Navigated, CanGoBack={flag}, CanGoForward={flag2}");
					}
				}
				catch (Exception ex2)
				{
					Console.WriteLine("[GtkWebViewHandler] Error in SendNavigated: " + ex2.Message);
				}
				return false;
			});
		}
		catch (Exception ex)
		{
			Console.WriteLine("[GtkWebViewHandler] Error dispatching navigation completed: " + ex.Message);
		}
	}

	internal void RegisterWithHost(SKRect bounds)
	{
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
		if (_platformWebView == null)
		{
			return;
		}
		GtkHostService instance = GtkHostService.Instance;
		if (instance.HostWindow == null || instance.WebViewManager == null)
		{
			Console.WriteLine("[GtkWebViewHandler] Warning: GTK host not initialized, cannot register WebView");
			return;
		}
		int num = (int)((SKRect)(ref bounds)).Left;
		int num2 = (int)((SKRect)(ref bounds)).Top;
		int num3 = (int)((SKRect)(ref bounds)).Width;
		int num4 = (int)((SKRect)(ref bounds)).Height;
		if (num3 <= 0 || num4 <= 0)
		{
			Console.WriteLine($"[GtkWebViewHandler] Skipping invalid bounds: {bounds}");
			return;
		}
		if (!_isRegisteredWithHost)
		{
			instance.HostWindow.AddWebView(_platformWebView.Widget, num, num2, num3, num4);
			_isRegisteredWithHost = true;
			Console.WriteLine($"[GtkWebViewHandler] Registered WebView at ({num}, {num2}) size {num3}x{num4}");
		}
		else if (bounds != _lastBounds)
		{
			instance.HostWindow.MoveResizeWebView(_platformWebView.Widget, num, num2, num3, num4);
			Console.WriteLine($"[GtkWebViewHandler] Updated WebView to ({num}, {num2}) size {num3}x{num4}");
		}
		_lastBounds = bounds;
	}

	private void UnregisterFromHost()
	{
		if (_isRegisteredWithHost && _platformWebView != null)
		{
			GtkHostService instance = GtkHostService.Instance;
			if (instance.HostWindow != null)
			{
				instance.HostWindow.RemoveWebView(_platformWebView.Widget);
				Console.WriteLine("[GtkWebViewHandler] Unregistered WebView from host");
			}
			_isRegisteredWithHost = false;
		}
	}

	public static void MapSource(GtkWebViewHandler handler, IWebView webView)
	{
		if (handler._platformWebView == null)
		{
			return;
		}
		IWebViewSource source = webView.Source;
		Console.WriteLine("[GtkWebViewHandler] MapSource: " + (((object)source)?.GetType().Name ?? "null"));
		UrlWebViewSource val = (UrlWebViewSource)(object)((source is UrlWebViewSource) ? source : null);
		if (val != null)
		{
			string url = val.Url;
			if (!string.IsNullOrEmpty(url))
			{
				handler._platformWebView.Navigate(url);
			}
			return;
		}
		HtmlWebViewSource val2 = (HtmlWebViewSource)(object)((source is HtmlWebViewSource) ? source : null);
		if (val2 != null)
		{
			string html = val2.Html;
			if (!string.IsNullOrEmpty(html))
			{
				handler._platformWebView.LoadHtml(html, val2.BaseUrl);
			}
		}
	}

	public static void MapGoBack(GtkWebViewHandler handler, IWebView webView, object? args)
	{
		Console.WriteLine($"[GtkWebViewHandler] MapGoBack called, CanGoBack={handler._platformWebView?.CanGoBack()}");
		handler._platformWebView?.GoBack();
	}

	public static void MapGoForward(GtkWebViewHandler handler, IWebView webView, object? args)
	{
		Console.WriteLine($"[GtkWebViewHandler] MapGoForward called, CanGoForward={handler._platformWebView?.CanGoForward()}");
		handler._platformWebView?.GoForward();
	}

	public static void MapReload(GtkWebViewHandler handler, IWebView webView, object? args)
	{
		Console.WriteLine("[GtkWebViewHandler] MapReload called");
		handler._platformWebView?.Reload();
	}
}
