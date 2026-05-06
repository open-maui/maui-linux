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
        try
        {
            // Try to use MonitorService for accurate XRandR-based info
            var primaryMonitor = MonitorService.Instance.PrimaryMonitor;
            if (primaryMonitor != null)
            {
                double scaleFactor = GetScaleFactor();
                // If scale factor not set via env, use monitor's DPI-based scale
                if (scaleFactor == 1.0 && primaryMonitor.ScaleFactor > 1.0)
                {
                    scaleFactor = Math.Round(primaryMonitor.ScaleFactor * 4) / 4; // Round to nearest 0.25
                }

                DisplayOrientation orientation = (primaryMonitor.Width <= primaryMonitor.Height)
                    ? DisplayOrientation.Portrait
                    : DisplayOrientation.Landscape;

                _mainDisplayInfo = new DisplayInfo(
                    primaryMonitor.Width,
                    primaryMonitor.Height,
                    scaleFactor,
                    orientation,
                    DisplayRotation.Rotation0,
                    (float)primaryMonitor.RefreshRate);
                return;
            }

            // Fall back to GDK
            IntPtr screen = GdkNative.gdk_screen_get_default();
            if (screen != IntPtr.Zero)
            {
                int width = GdkNative.gdk_screen_get_width(screen);
                int height = GdkNative.gdk_screen_get_height(screen);
                double scaleFactor = GetScaleFactor();
                DisplayOrientation orientation = (width <= height) ? DisplayOrientation.Portrait : DisplayOrientation.Landscape;
                _mainDisplayInfo = new DisplayInfo(width, height, scaleFactor, orientation, DisplayRotation.Rotation0, GetRefreshRate());
            }
            else
            {
                _mainDisplayInfo = new DisplayInfo(1920.0, 1080.0, 1.0, DisplayOrientation.Landscape, DisplayRotation.Rotation0, 60f);
            }
        }
        catch
        {
            _mainDisplayInfo = new DisplayInfo(1920.0, 1080.0, 1.0, DisplayOrientation.Landscape, DisplayRotation.Rotation0, 60f);
        }
    }

    private double GetScaleFactor()
    {
        var gdkScale = Environment.GetEnvironmentVariable("GDK_SCALE");
        if (!string.IsNullOrEmpty(gdkScale) && double.TryParse(gdkScale, out var result))
        {
            return result;
        }

        var qtScale = Environment.GetEnvironmentVariable("QT_SCALE_FACTOR");
        if (!string.IsNullOrEmpty(qtScale) && double.TryParse(qtScale, out result))
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
            string action = inhibit ? "suspend" : "resume";
            IntPtr windowHandle = LinuxApplication.Current?.MainWindow?.Handle ?? IntPtr.Zero;
            if (windowHandle != IntPtr.Zero)
            {
                long windowId = windowHandle.ToInt64();
                Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-screensaver",
                    Arguments = $"{action} {windowId}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("DeviceDisplayService", "Display info refresh failed", ex);
        }
    }

    public void OnDisplayInfoChanged()
    {
        RefreshDisplayInfo();
        MainDisplayInfoChanged?.Invoke(this, new DisplayInfoChangedEventArgs(_mainDisplayInfo));
    }
}
