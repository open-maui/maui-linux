using System;
using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Services;

public static class InputMethodServiceFactory
{
	private static IInputMethodService? _instance;

	private static readonly object _lock = new object();

	public static IInputMethodService Instance
	{
		get
		{
			if (_instance == null)
			{
				lock (_lock)
				{
					if (_instance == null)
					{
						_instance = CreateService();
					}
				}
			}
			return _instance;
		}
	}

	public static IInputMethodService CreateService()
	{
		string environmentVariable = Environment.GetEnvironmentVariable("MAUI_INPUT_METHOD");
		if (!string.IsNullOrEmpty(environmentVariable))
		{
			switch (environmentVariable.ToLowerInvariant())
			{
			case "ibus":
				return CreateIBusService();
			case "fcitx":
			case "fcitx5":
				return CreateFcitx5Service();
			case "xim":
				return CreateXIMService();
			case "none":
				return new NullInputMethodService();
			default:
				return CreateAutoService();
			}
		}
		return CreateAutoService();
	}

	private static IInputMethodService CreateAutoService()
	{
		string obj = Environment.GetEnvironmentVariable("GTK_IM_MODULE")?.ToLowerInvariant();
		if (obj != null && obj.Contains("fcitx") && Fcitx5InputMethodService.IsAvailable())
		{
			Console.WriteLine("InputMethodServiceFactory: Using Fcitx5");
			return CreateFcitx5Service();
		}
		if (IsIBusAvailable())
		{
			Console.WriteLine("InputMethodServiceFactory: Using IBus");
			return CreateIBusService();
		}
		if (Fcitx5InputMethodService.IsAvailable())
		{
			Console.WriteLine("InputMethodServiceFactory: Using Fcitx5");
			return CreateFcitx5Service();
		}
		if (IsXIMAvailable())
		{
			Console.WriteLine("InputMethodServiceFactory: Using XIM");
			return CreateXIMService();
		}
		Console.WriteLine("InputMethodServiceFactory: No IME available, using null service");
		return new NullInputMethodService();
	}

	private static IInputMethodService CreateIBusService()
	{
		try
		{
			return new IBusInputMethodService();
		}
		catch (Exception ex)
		{
			Console.WriteLine("InputMethodServiceFactory: Failed to create IBus service - " + ex.Message);
			return new NullInputMethodService();
		}
	}

	private static IInputMethodService CreateFcitx5Service()
	{
		try
		{
			return new Fcitx5InputMethodService();
		}
		catch (Exception ex)
		{
			Console.WriteLine("InputMethodServiceFactory: Failed to create Fcitx5 service - " + ex.Message);
			return new NullInputMethodService();
		}
	}

	private static IInputMethodService CreateXIMService()
	{
		try
		{
			return new X11InputMethodService();
		}
		catch (Exception ex)
		{
			Console.WriteLine("InputMethodServiceFactory: Failed to create XIM service - " + ex.Message);
			return new NullInputMethodService();
		}
	}

	private static bool IsIBusAvailable()
	{
		if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IBUS_ADDRESS")))
		{
			return true;
		}
		try
		{
			NativeLibrary.Free(NativeLibrary.Load("libibus-1.0.so.5"));
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static bool IsXIMAvailable()
	{
		string environmentVariable = Environment.GetEnvironmentVariable("XMODIFIERS");
		if (!string.IsNullOrEmpty(environmentVariable) && environmentVariable.Contains("@im="))
		{
			return true;
		}
		return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"));
	}

	public static void Reset()
	{
		lock (_lock)
		{
			_instance?.Shutdown();
			_instance = null;
		}
	}
}
