using System;

namespace Microsoft.Maui.Platform.Linux.Services;

public static class AccessibilityServiceFactory
{
	private static IAccessibilityService? _instance;

	private static readonly object _lock = new object();

	public static IAccessibilityService Instance
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

	private static IAccessibilityService CreateService()
	{
		try
		{
			AtSpi2AccessibilityService atSpi2AccessibilityService = new AtSpi2AccessibilityService();
			atSpi2AccessibilityService.Initialize();
			return atSpi2AccessibilityService;
		}
		catch (Exception ex)
		{
			Console.WriteLine("AccessibilityServiceFactory: Failed to create AT-SPI2 service - " + ex.Message);
			return new NullAccessibilityService();
		}
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
