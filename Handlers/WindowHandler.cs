using System;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class WindowHandler : ElementHandler<IWindow, SkiaWindow>
{
	public static IPropertyMapper<IWindow, WindowHandler> Mapper = (IPropertyMapper<IWindow, WindowHandler>)(object)new PropertyMapper<IWindow, WindowHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)ElementHandler.ElementMapper })
	{
		["Title"] = MapTitle,
		["Content"] = MapContent,
		["X"] = MapX,
		["Y"] = MapY,
		["Width"] = MapWidth,
		["Height"] = MapHeight,
		["MinimumWidth"] = MapMinimumWidth,
		["MinimumHeight"] = MapMinimumHeight,
		["MaximumWidth"] = MapMaximumWidth,
		["MaximumHeight"] = MapMaximumHeight
	};

	public static CommandMapper<IWindow, WindowHandler> CommandMapper = new CommandMapper<IWindow, WindowHandler>((CommandMapper)(object)ElementHandler.ElementCommandMapper);

	public WindowHandler()
		: base((IPropertyMapper)(object)Mapper, (CommandMapper)(object)CommandMapper)
	{
	}

	public WindowHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
		: base((IPropertyMapper)(((object)mapper) ?? ((object)Mapper)), (CommandMapper)(((object)commandMapper) ?? ((object)CommandMapper)))
	{
	}

	protected override SkiaWindow CreatePlatformElement()
	{
		return new SkiaWindow();
	}

	protected override void ConnectHandler(SkiaWindow platformView)
	{
		base.ConnectHandler(platformView);
		platformView.CloseRequested += OnCloseRequested;
		platformView.SizeChanged += OnSizeChanged;
	}

	protected override void DisconnectHandler(SkiaWindow platformView)
	{
		platformView.CloseRequested -= OnCloseRequested;
		platformView.SizeChanged -= OnSizeChanged;
		base.DisconnectHandler(platformView);
	}

	private void OnCloseRequested(object? sender, EventArgs e)
	{
		IWindow virtualView = base.VirtualView;
		if (virtualView != null)
		{
			virtualView.Destroying();
		}
	}

	private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		IWindow virtualView = base.VirtualView;
		if (virtualView != null)
		{
			virtualView.FrameChanged(new Rect(0.0, 0.0, (double)e.Width, (double)e.Height));
		}
	}

	public static void MapTitle(WindowHandler handler, IWindow window)
	{
		if (((ElementHandler<IWindow, SkiaWindow>)(object)handler).PlatformView != null)
		{
			((ElementHandler<IWindow, SkiaWindow>)(object)handler).PlatformView.Title = ((ITitledElement)window).Title ?? "MAUI Application";
		}
	}

	public static void MapContent(WindowHandler handler, IWindow window)
	{
		if (((ElementHandler<IWindow, SkiaWindow>)(object)handler).PlatformView != null)
		{
			IView content = window.Content;
			object obj;
			if (content == null)
			{
				obj = null;
			}
			else
			{
				IViewHandler handler2 = content.Handler;
				obj = ((handler2 != null) ? ((IElementHandler)handler2).PlatformView : null);
			}
			if (obj is SkiaView content2)
			{
				((ElementHandler<IWindow, SkiaWindow>)(object)handler).PlatformView.Content = content2;
			}
		}
	}

	public static void MapX(WindowHandler handler, IWindow window)
	{
		if (((ElementHandler<IWindow, SkiaWindow>)(object)handler).PlatformView != null)
		{
			((ElementHandler<IWindow, SkiaWindow>)(object)handler).PlatformView.X = (int)window.X;
		}
	}

	public static void MapY(WindowHandler handler, IWindow window)
	{
		if (((ElementHandler<IWindow, SkiaWindow>)(object)handler).PlatformView != null)
		{
			((ElementHandler<IWindow, SkiaWindow>)(object)handler).PlatformView.Y = (int)window.Y;
		}
	}

	public static void MapWidth(WindowHandler handler, IWindow window)
	{
		if (((ElementHandler<IWindow, SkiaWindow>)(object)handler).PlatformView != null)
		{
			((ElementHandler<IWindow, SkiaWindow>)(object)handler).PlatformView.Width = (int)window.Width;
		}
	}

	public static void MapHeight(WindowHandler handler, IWindow window)
	{
		if (((ElementHandler<IWindow, SkiaWindow>)(object)handler).PlatformView != null)
		{
			((ElementHandler<IWindow, SkiaWindow>)(object)handler).PlatformView.Height = (int)window.Height;
		}
	}

	public static void MapMinimumWidth(WindowHandler handler, IWindow window)
	{
		if (((ElementHandler<IWindow, SkiaWindow>)(object)handler).PlatformView != null)
		{
			((ElementHandler<IWindow, SkiaWindow>)(object)handler).PlatformView.MinWidth = (int)window.MinimumWidth;
		}
	}

	public static void MapMinimumHeight(WindowHandler handler, IWindow window)
	{
		if (((ElementHandler<IWindow, SkiaWindow>)(object)handler).PlatformView != null)
		{
			((ElementHandler<IWindow, SkiaWindow>)(object)handler).PlatformView.MinHeight = (int)window.MinimumHeight;
		}
	}

	public static void MapMaximumWidth(WindowHandler handler, IWindow window)
	{
		if (((ElementHandler<IWindow, SkiaWindow>)(object)handler).PlatformView != null)
		{
			((ElementHandler<IWindow, SkiaWindow>)(object)handler).PlatformView.MaxWidth = (int)window.MaximumWidth;
		}
	}

	public static void MapMaximumHeight(WindowHandler handler, IWindow window)
	{
		if (((ElementHandler<IWindow, SkiaWindow>)(object)handler).PlatformView != null)
		{
			((ElementHandler<IWindow, SkiaWindow>)(object)handler).PlatformView.MaxHeight = (int)window.MaximumHeight;
		}
	}
}
