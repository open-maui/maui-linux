using System.Collections.Generic;
using Microsoft.Maui.Platform.Linux.Window;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public sealed class GtkWebViewManager
{
	private readonly GtkHostWindow _host;

	private readonly Dictionary<object, GtkWebViewPlatformView> _webViews = new Dictionary<object, GtkWebViewPlatformView>();

	public GtkWebViewManager(GtkHostWindow host)
	{
		_host = host;
	}

	public GtkWebViewPlatformView CreateWebView(object key, int x, int y, int width, int height)
	{
		GtkWebViewPlatformView gtkWebViewPlatformView = new GtkWebViewPlatformView();
		_webViews[key] = gtkWebViewPlatformView;
		_host.AddWebView(gtkWebViewPlatformView.Widget, x, y, width, height);
		return gtkWebViewPlatformView;
	}

	public void UpdateLayout(object key, int x, int y, int width, int height)
	{
		if (_webViews.TryGetValue(key, out GtkWebViewPlatformView value))
		{
			_host.MoveResizeWebView(value.Widget, x, y, width, height);
		}
	}

	public GtkWebViewPlatformView? GetWebView(object key)
	{
		if (!_webViews.TryGetValue(key, out GtkWebViewPlatformView value))
		{
			return null;
		}
		return value;
	}

	public void RemoveWebView(object key)
	{
		if (_webViews.TryGetValue(key, out GtkWebViewPlatformView value))
		{
			_host.RemoveWebView(value.Widget);
			value.Dispose();
			_webViews.Remove(key);
		}
	}

	public void Clear()
	{
		foreach (KeyValuePair<object, GtkWebViewPlatformView> webView in _webViews)
		{
			_host.RemoveWebView(webView.Value.Widget);
			webView.Value.Dispose();
		}
		_webViews.Clear();
	}
}
