using System;
using System.Diagnostics;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Platform.Linux.Native;

namespace Microsoft.Maui.Platform.Linux.Services;

public class DeviceDisplayService : IDeviceDisplay
{
	private static readonly Lazy<DeviceDisplayService> _instance = new Lazy<DeviceDisplayService>(() => new DeviceDisplayService());

	private DisplayInfo _mainDisplayInfo;

	private bool _keepScreenOn;

	public static DeviceDisplayService Instance => _instance.Value;

	public bool KeepScreenOn
	{
		get
		{
			return _keepScreenOn;
		}
		set
		{
			if (_keepScreenOn != value)
			{
				_keepScreenOn = value;
				SetScreenSaverInhibit(value);
			}
		}
	}

	public DisplayInfo MainDisplayInfo
	{
		get
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			RefreshDisplayInfo();
			return _mainDisplayInfo;
		}
	}

	public event EventHandler<DisplayInfoChangedEventArgs>? MainDisplayInfoChanged;

	public DeviceDisplayService()
	{
		RefreshDisplayInfo();
	}

	private void RefreshDisplayInfo()
	{
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			IntPtr intPtr = GdkNative.gdk_screen_get_default();
			if (intPtr != IntPtr.Zero)
			{
				int num = GdkNative.gdk_screen_get_width(intPtr);
				int num2 = GdkNative.gdk_screen_get_height(intPtr);
				double scaleFactor = GetScaleFactor();
				_mainDisplayInfo = new DisplayInfo((double)num, (double)num2, scaleFactor, (DisplayOrientation)((num <= num2) ? 1 : 2), (DisplayRotation)1, GetRefreshRate());
			}
			else
			{
				_mainDisplayInfo = new DisplayInfo(1920.0, 1080.0, 1.0, (DisplayOrientation)2, (DisplayRotation)1, 60f);
			}
		}
		catch
		{
			_mainDisplayInfo = new DisplayInfo(1920.0, 1080.0, 1.0, (DisplayOrientation)2, (DisplayRotation)1, 60f);
		}
	}

	private double GetScaleFactor()
	{
		string environmentVariable = Environment.GetEnvironmentVariable("GDK_SCALE");
		if (!string.IsNullOrEmpty(environmentVariable) && double.TryParse(environmentVariable, out var result))
		{
			return result;
		}
		string environmentVariable2 = Environment.GetEnvironmentVariable("QT_SCALE_FACTOR");
		if (!string.IsNullOrEmpty(environmentVariable2) && double.TryParse(environmentVariable2, out result))
		{
			return result;
		}
		return 1.0;
	}

	private float GetRefreshRate()
	{
		return 60f;
	}

	private void SetScreenSaverInhibit(bool inhibit)
	{
		try
		{
			string value = (inhibit ? "suspend" : "resume");
			IntPtr intPtr = LinuxApplication.Current?.MainWindow?.Handle ?? IntPtr.Zero;
			if (intPtr != IntPtr.Zero)
			{
				long value2 = intPtr.ToInt64();
				Process.Start(new ProcessStartInfo
				{
					FileName = "xdg-screensaver",
					Arguments = $"{value} {value2}",
					UseShellExecute = false,
					CreateNoWindow = true
				});
			}
		}
		catch
		{
		}
	}

	public void OnDisplayInfoChanged()
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Expected O, but got Unknown
		RefreshDisplayInfo();
		this.MainDisplayInfoChanged?.Invoke(this, new DisplayInfoChangedEventArgs(_mainDisplayInfo));
	}
}
