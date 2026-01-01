using System;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class GtkWebViewProxy : SkiaView
{
	private readonly GtkWebViewHandler _handler;

	private readonly GtkWebViewPlatformView _platformView;

	public GtkWebViewPlatformView PlatformView => _platformView;

	public bool CanGoBack => _platformView.CanGoBack();

	public bool CanGoForward => _platformView.CanGoForward();

	public GtkWebViewProxy(GtkWebViewHandler handler, GtkWebViewPlatformView platformView)
	{
		_handler = handler;
		_platformView = platformView;
	}

	public override void Arrange(SKRect bounds)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		base.Arrange(bounds);
		SKRect bounds2 = TransformToWindow(bounds);
		_handler.RegisterWithHost(bounds2);
	}

	private SKRect TransformToWindow(SKRect localBounds)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		float num = ((SKRect)(ref localBounds)).Left;
		float num2 = ((SKRect)(ref localBounds)).Top;
		for (SkiaView parent = base.Parent; parent != null; parent = parent.Parent)
		{
			float num3 = num;
			SKRect bounds = parent.Bounds;
			num = num3 + ((SKRect)(ref bounds)).Left;
			float num4 = num2;
			bounds = parent.Bounds;
			num2 = num4 + ((SKRect)(ref bounds)).Top;
		}
		return new SKRect(num, num2, num + ((SKRect)(ref localBounds)).Width, num2 + ((SKRect)(ref localBounds)).Height);
	}

	public override void Draw(SKCanvas canvas)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Expected O, but got Unknown
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		SKPaint val = new SKPaint
		{
			Color = new SKColor((byte)0, (byte)0, (byte)0, (byte)0),
			Style = (SKPaintStyle)0
		};
		try
		{
			canvas.DrawRect(base.Bounds, val);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	public void Navigate(string url)
	{
		_platformView.Navigate(url);
	}

	public void LoadHtml(string html, string? baseUrl = null)
	{
		_platformView.LoadHtml(html, baseUrl);
	}

	public void GoBack()
	{
		_platformView.GoBack();
	}

	public void GoForward()
	{
		_platformView.GoForward();
	}

	public void Reload()
	{
		_platformView.Reload();
	}
}
