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
        return ParseScaleFactor(Environment.GetEnvironmentVariable("GDK_SCALE"))
            ?? ParseScaleFactor(Environment.GetEnvironmentVariable("QT_SCALE_FACTOR"))
            ?? 1.0;
    }

    /// <summary>
    /// GDK_SCALE / QT_SCALE_FACTOR value parsed with the invariant culture (so
    /// "1.5" is 1.5 under any locale); null for empty, unparsable or
    /// non-positive values.
    /// </summary>
    internal static double? ParseScaleFactor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (!double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var result))
            return null;
        return result > 0 ? result : null;
    }

    private float GetRefreshRate()
    {
        return 60f;
    }

    private void SetScreenSaverInhibit(bool inhibit)
    {
        try
        {
            // xdg-screensaver requires an X11 window ID; on Wayland the call is skipped
            // (the compositor handles idle inhibit via the idle-inhibit-unstable-v1 protocol,
            // wired up in a follow-up).
            IntPtr windowHandle = (LinuxApplication.Current?.MainWindow as IX11Surface)?.Handle ?? IntPtr.Zero;
            if (windowHandle != IntPtr.Zero)
            {
                ExternalProcess.TryStart(BuildScreenSaverStartInfo(inhibit, windowHandle.ToInt64()));
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("DeviceDisplayService", "Screen saver inhibit failed", ex);
        }
    }

    /// <summary>xdg-screensaver suspend/resume for an X11 window id.</summary>
    internal static ProcessStartInfo BuildScreenSaverStartInfo(bool inhibit, long windowId)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "xdg-screensaver",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add(inhibit ? "suspend" : "resume");
        psi.ArgumentList.Add(windowId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return psi;
    }

    public void OnDisplayInfoChanged()
    {
        RefreshDisplayInfo();
        MainDisplayInfoChanged?.Invoke(this, new DisplayInfoChangedEventArgs(_mainDisplayInfo));
    }
}
