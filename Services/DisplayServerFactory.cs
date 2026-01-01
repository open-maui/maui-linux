using System;

namespace Microsoft.Maui.Platform.Linux.Services;

public static class DisplayServerFactory
{
	private static DisplayServerType? _cachedServerType;

	public static DisplayServerType DetectDisplayServer()
	{
		if (_cachedServerType.HasValue)
		{
			return _cachedServerType.Value;
		}
		if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
		{
			string? environmentVariable = Environment.GetEnvironmentVariable("DISPLAY");
			string environmentVariable2 = Environment.GetEnvironmentVariable("MAUI_PREFER_X11");
			if (!string.IsNullOrEmpty(environmentVariable) && !string.IsNullOrEmpty(environmentVariable2))
			{
				Console.WriteLine("[DisplayServer] XWayland detected, using X11 backend (MAUI_PREFER_X11 set)");
				_cachedServerType = DisplayServerType.X11;
				return DisplayServerType.X11;
			}
			Console.WriteLine("[DisplayServer] Wayland display detected");
			_cachedServerType = DisplayServerType.Wayland;
			return DisplayServerType.Wayland;
		}
		if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")))
		{
			Console.WriteLine("[DisplayServer] X11 display detected");
			_cachedServerType = DisplayServerType.X11;
			return DisplayServerType.X11;
		}
		Console.WriteLine("[DisplayServer] No display server detected, defaulting to X11");
		_cachedServerType = DisplayServerType.X11;
		return DisplayServerType.X11;
	}

	public static IDisplayWindow CreateWindow(string title, int width, int height, DisplayServerType serverType = DisplayServerType.Auto)
	{
		if (serverType == DisplayServerType.Auto)
		{
			serverType = DetectDisplayServer();
		}
		return serverType switch
		{
			DisplayServerType.X11 => CreateX11Window(title, width, height), 
			DisplayServerType.Wayland => CreateWaylandWindow(title, width, height), 
			_ => CreateX11Window(title, width, height), 
		};
	}

	private static IDisplayWindow CreateX11Window(string title, int width, int height)
	{
		try
		{
			Console.WriteLine($"[DisplayServer] Creating X11 window: {title} ({width}x{height})");
			return new X11DisplayWindow(title, width, height);
		}
		catch (Exception ex)
		{
			Console.WriteLine("[DisplayServer] Failed to create X11 window: " + ex.Message);
			throw;
		}
	}

	private static IDisplayWindow CreateWaylandWindow(string title, int width, int height)
	{
		try
		{
			Console.WriteLine($"[DisplayServer] Creating Wayland window: {title} ({width}x{height})");
			return new WaylandDisplayWindow(title, width, height);
		}
		catch (Exception ex)
		{
			Console.WriteLine("[DisplayServer] Failed to create Wayland window: " + ex.Message);
			if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")))
			{
				Console.WriteLine("[DisplayServer] Falling back to X11 (XWayland)");
				return CreateX11Window(title, width, height);
			}
			throw;
		}
	}

	public static string GetDisplayServerName(DisplayServerType serverType = DisplayServerType.Auto)
	{
		if (serverType == DisplayServerType.Auto)
		{
			serverType = DetectDisplayServer();
		}
		return serverType switch
		{
			DisplayServerType.X11 => "X11", 
			DisplayServerType.Wayland => "Wayland", 
			_ => "Unknown", 
		};
	}
}
