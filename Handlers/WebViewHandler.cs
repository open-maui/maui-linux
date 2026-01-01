using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class WebViewHandler : ViewHandler<IWebView, SkiaWebView>
{
	public static IPropertyMapper<IWebView, WebViewHandler> Mapper = (IPropertyMapper<IWebView, WebViewHandler>)(object)new PropertyMapper<IWebView, WebViewHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)ViewHandler.ViewMapper }) { ["Source"] = MapSource };

	public static CommandMapper<IWebView, WebViewHandler> CommandMapper = new CommandMapper<IWebView, WebViewHandler>((CommandMapper)(object)ViewHandler.ViewCommandMapper)
	{
		["GoBack"] = MapGoBack,
		["GoForward"] = MapGoForward,
		["Reload"] = MapReload
	};

	public WebViewHandler()
		: base((IPropertyMapper)(object)Mapper, (CommandMapper)(object)CommandMapper)
	{
	}

	public WebViewHandler(IPropertyMapper? mapper = null, CommandMapper? commandMapper = null)
		: base((IPropertyMapper)(((object)mapper) ?? ((object)Mapper)), (CommandMapper)(((object)commandMapper) ?? ((object)CommandMapper)))
	{
	}

	protected override SkiaWebView CreatePlatformView()
	{
		return new SkiaWebView();
	}

	protected override void ConnectHandler(SkiaWebView platformView)
	{
		base.ConnectHandler(platformView);
		platformView.Navigating += OnNavigating;
		platformView.Navigated += OnNavigated;
	}

	protected override void DisconnectHandler(SkiaWebView platformView)
	{
		platformView.Navigating -= OnNavigating;
		platformView.Navigated -= OnNavigated;
		base.DisconnectHandler(platformView);
	}

	private void OnNavigating(object? sender, WebNavigatingEventArgs e)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Expected O, but got Unknown
		IWebView virtualView = base.VirtualView;
		IWebViewController val = (IWebViewController)(object)((virtualView is IWebViewController) ? virtualView : null);
		if (val != null)
		{
			WebNavigatingEventArgs e2 = new WebNavigatingEventArgs((WebNavigationEvent)3, (WebViewSource)null, e.Url);
			val.SendNavigating(e2);
		}
	}

	private void OnNavigated(object? sender, WebNavigatedEventArgs e)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Expected O, but got Unknown
		IWebView virtualView = base.VirtualView;
		IWebViewController val = (IWebViewController)(object)((virtualView is IWebViewController) ? virtualView : null);
		if (val != null)
		{
			WebNavigationResult val2 = (WebNavigationResult)(e.Success ? 1 : 4);
			WebNavigatedEventArgs e2 = new WebNavigatedEventArgs((WebNavigationEvent)3, (WebViewSource)null, e.Url, val2);
			val.SendNavigated(e2);
		}
	}

	public static void MapSource(WebViewHandler handler, IWebView webView)
	{
		Console.WriteLine("[WebViewHandler] MapSource called");
		if (((ViewHandler<IWebView, SkiaWebView>)(object)handler).PlatformView == null)
		{
			Console.WriteLine("[WebViewHandler] PlatformView is null!");
			return;
		}
		IWebViewSource source = webView.Source;
		Console.WriteLine("[WebViewHandler] Source type: " + (((object)source)?.GetType().Name ?? "null"));
		UrlWebViewSource val = (UrlWebViewSource)(object)((source is UrlWebViewSource) ? source : null);
		if (val != null)
		{
			Console.WriteLine("[WebViewHandler] Loading URL: " + val.Url);
			((ViewHandler<IWebView, SkiaWebView>)(object)handler).PlatformView.Source = val.Url ?? "";
			return;
		}
		HtmlWebViewSource val2 = (HtmlWebViewSource)(object)((source is HtmlWebViewSource) ? source : null);
		if (val2 != null)
		{
			Console.WriteLine($"[WebViewHandler] Loading HTML ({val2.Html?.Length ?? 0} chars)");
			Console.WriteLine("[WebViewHandler] HTML preview: " + val2.Html?.Substring(0, Math.Min(100, val2.Html?.Length ?? 0)) + "...");
			((ViewHandler<IWebView, SkiaWebView>)(object)handler).PlatformView.Html = val2.Html ?? "";
		}
		else
		{
			Console.WriteLine("[WebViewHandler] Unknown source type or null");
		}
	}

	public static void MapGoBack(WebViewHandler handler, IWebView webView, object? args)
	{
		((ViewHandler<IWebView, SkiaWebView>)(object)handler).PlatformView?.GoBack();
	}

	public static void MapGoForward(WebViewHandler handler, IWebView webView, object? args)
	{
		((ViewHandler<IWebView, SkiaWebView>)(object)handler).PlatformView?.GoForward();
	}

	public static void MapReload(WebViewHandler handler, IWebView webView, object? args)
	{
		((ViewHandler<IWebView, SkiaWebView>)(object)handler).PlatformView?.Reload();
	}
}
