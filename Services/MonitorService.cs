// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Maui.Platform.Linux.Interop;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Service for querying and monitoring display configuration using XRandR.
/// </summary>
public class MonitorService : IDisposable
{
    private static MonitorService? _instance;
    private static readonly object _lock = new();

    private IntPtr _display;
    private IntPtr _rootWindow;
    private List<MonitorInfo> _monitors = new();
    private bool _initialized;
    private bool _disposed;
    private int _eventBase;
    private int _errorBase;

    /// <summary>
    /// Gets the singleton instance of the monitor service.
    /// </summary>
    public static MonitorService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new MonitorService();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Gets the list of connected monitors.
    /// </summary>
    public IReadOnlyList<MonitorInfo> Monitors
    {
        get
        {
            EnsureInitialized();
            return _monitors.AsReadOnly();
        }
    }

    /// <summary>
    /// Gets the primary monitor.
    /// </summary>
    public MonitorInfo? PrimaryMonitor
    {
        get
        {
            EnsureInitialized();
            return _monitors.FirstOrDefault(m => m.IsPrimary) ?? _monitors.FirstOrDefault();
        }
    }

    /// <summary>
    /// Gets the total virtual desktop bounds (union of all monitors).
    /// </summary>
    public (int X, int Y, int Width, int Height) VirtualDesktopBounds
    {
        get
        {
            EnsureInitialized();
            if (_monitors.Count == 0)
                return (0, 0, 1920, 1080); // Default fallback

            int minX = _monitors.Min(m => m.X);
            int minY = _monitors.Min(m => m.Y);
            int maxX = _monitors.Max(m => m.X + m.Width);
            int maxY = _monitors.Max(m => m.Y + m.Height);

            return (minX, minY, maxX - minX, maxY - minY);
        }
    }

    /// <summary>
    /// Event raised when monitor configuration changes.
    /// </summary>
    public event EventHandler<MonitorConfigurationChangedEventArgs>? ConfigurationChanged;

    private MonitorService()
    {
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;

        lock (_lock)
        {
            if (_initialized) return;

            try
            {
                _display = X11.XOpenDisplay(IntPtr.Zero);
                if (_display == IntPtr.Zero)
                {
                    Console.WriteLine("[MonitorService] Failed to open X11 display");
                    _initialized = true;
                    return;
                }

                int screen = X11.XDefaultScreen(_display);
                _rootWindow = X11.XRootWindow(_display, screen);

                // Check if XRandR is available
                if (XRandR.XRRQueryExtension(_display, out _eventBase, out _errorBase) == 0)
                {
                    Console.WriteLine("[MonitorService] XRandR extension not available");
                    _initialized = true;
                    return;
                }

                if (XRandR.XRRQueryVersion(_display, out int major, out int minor) == 0)
                {
                    Console.WriteLine("[MonitorService] Failed to query XRandR version");
                    _initialized = true;
                    return;
                }

                Console.WriteLine($"[MonitorService] XRandR {major}.{minor} available");

                RefreshMonitors();
                _initialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MonitorService] Initialization failed: {ex.Message}");
                _initialized = true;
            }
        }
    }

    /// <summary>
    /// Refreshes the monitor list from the system.
    /// </summary>
    public void RefreshMonitors()
    {
        if (_display == IntPtr.Zero) return;

        var newMonitors = new List<MonitorInfo>();

        IntPtr resources = IntPtr.Zero;
        try
        {
            resources = XRandR.XRRGetScreenResourcesCurrent(_display, _rootWindow);
            if (resources == IntPtr.Zero)
            {
                Console.WriteLine("[MonitorService] Failed to get screen resources");
                return;
            }

            var res = Marshal.PtrToStructure<XRRScreenResources>(resources);

            // Get array of output IDs
            var outputIds = new ulong[res.NOutput];
            for (int i = 0; i < res.NOutput; i++)
            {
                outputIds[i] = (ulong)Marshal.ReadInt64(res.Outputs, i * 8);
            }

            // Track which monitor is at 0,0 (likely primary)
            MonitorInfo? primaryCandidate = null;

            foreach (var outputId in outputIds)
            {
                IntPtr outputInfo = IntPtr.Zero;
                IntPtr crtcInfo = IntPtr.Zero;

                try
                {
                    outputInfo = XRandR.XRRGetOutputInfo(_display, resources, outputId);
                    if (outputInfo == IntPtr.Zero) continue;

                    var output = Marshal.PtrToStructure<XRROutputInfo>(outputInfo);

                    // Skip disconnected outputs
                    if (output.Connection != XRandR.RR_Connected)
                        continue;

                    // Skip outputs without a CRTC (not currently displaying)
                    if (output.Crtc == 0)
                        continue;

                    crtcInfo = XRandR.XRRGetCrtcInfo(_display, resources, output.Crtc);
                    if (crtcInfo == IntPtr.Zero) continue;

                    var crtc = Marshal.PtrToStructure<XRRCrtcInfo>(crtcInfo);

                    // Get output name
                    string name = output.Name != IntPtr.Zero && output.NameLen > 0
                        ? Marshal.PtrToStringAnsi(output.Name, output.NameLen) ?? $"Output-{outputId}"
                        : $"Output-{outputId}";

                    // Calculate refresh rate from mode info
                    double refreshRate = GetRefreshRate(resources, crtc.Mode);

                    var monitor = new MonitorInfo
                    {
                        Id = outputId,
                        Name = name,
                        X = crtc.X,
                        Y = crtc.Y,
                        Width = (int)crtc.Width,
                        Height = (int)crtc.Height,
                        PhysicalWidthMm = (int)output.MmWidth,
                        PhysicalHeightMm = (int)output.MmHeight,
                        RefreshRate = refreshRate,
                        IsPrimary = false // Will be set below
                    };

                    newMonitors.Add(monitor);

                    // Track the monitor at 0,0 as primary candidate
                    if (crtc.X == 0 && crtc.Y == 0)
                    {
                        primaryCandidate = monitor;
                    }
                }
                finally
                {
                    if (crtcInfo != IntPtr.Zero)
                        XRandR.XRRFreeCrtcInfo(crtcInfo);
                    if (outputInfo != IntPtr.Zero)
                        XRandR.XRRFreeOutputInfo(outputInfo);
                }
            }

            // Set primary monitor (the one at 0,0 or the first one)
            if (newMonitors.Count > 0)
            {
                var primary = primaryCandidate ?? newMonitors[0];
                var index = newMonitors.IndexOf(primary);
                if (index >= 0)
                {
                    newMonitors[index] = primary with { IsPrimary = true };
                }
            }

            var oldMonitors = _monitors;
            _monitors = newMonitors;

            // Log detected monitors
            Console.WriteLine($"[MonitorService] Detected {_monitors.Count} monitor(s):");
            foreach (var monitor in _monitors)
            {
                Console.WriteLine($"  {monitor}");
            }

            // Notify if configuration changed
            if (!MonitorListsEqual(oldMonitors, newMonitors))
            {
                ConfigurationChanged?.Invoke(this, new MonitorConfigurationChangedEventArgs(_monitors));
            }
        }
        finally
        {
            if (resources != IntPtr.Zero)
                XRandR.XRRFreeScreenResources(resources);
        }
    }

    private double GetRefreshRate(IntPtr resources, ulong modeId)
    {
        if (modeId == 0) return 60.0; // Default

        var res = Marshal.PtrToStructure<XRRScreenResources>(resources);

        for (int i = 0; i < res.NMode; i++)
        {
            var modePtr = res.Modes + i * Marshal.SizeOf<XRRModeInfo>();
            var mode = Marshal.PtrToStructure<XRRModeInfo>(modePtr);

            if (mode.Id == modeId)
            {
                if (mode.HTotal > 0 && mode.VTotal > 0 && mode.DotClock > 0)
                {
                    return (double)mode.DotClock / (mode.HTotal * mode.VTotal);
                }
                break;
            }
        }

        return 60.0; // Default fallback
    }

    private bool MonitorListsEqual(List<MonitorInfo> a, List<MonitorInfo> b)
    {
        if (a.Count != b.Count) return false;

        for (int i = 0; i < a.Count; i++)
        {
            if (a[i].Id != b[i].Id ||
                a[i].X != b[i].X ||
                a[i].Y != b[i].Y ||
                a[i].Width != b[i].Width ||
                a[i].Height != b[i].Height)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets the monitor containing the specified point.
    /// </summary>
    public MonitorInfo? GetMonitorAt(int x, int y)
    {
        EnsureInitialized();
        return _monitors.FirstOrDefault(m =>
            x >= m.X && x < m.X + m.Width &&
            y >= m.Y && y < m.Y + m.Height);
    }

    /// <summary>
    /// Gets the monitor containing the center of the specified rectangle.
    /// </summary>
    public MonitorInfo? GetMonitorFromRect(int x, int y, int width, int height)
    {
        int centerX = x + width / 2;
        int centerY = y + height / 2;
        return GetMonitorAt(centerX, centerY);
    }

    /// <summary>
    /// Gets the monitor by name (e.g., "HDMI-1", "DP-2").
    /// </summary>
    public MonitorInfo? GetMonitorByName(string name)
    {
        EnsureInitialized();
        return _monitors.FirstOrDefault(m =>
            m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_display != IntPtr.Zero)
            {
                X11.XCloseDisplay(_display);
                _display = IntPtr.Zero;
            }
            _disposed = true;
        }
    }
}

/// <summary>
/// Event args for monitor configuration changes.
/// </summary>
public class MonitorConfigurationChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the updated list of monitors.
    /// </summary>
    public IReadOnlyList<MonitorInfo> Monitors { get; }

    public MonitorConfigurationChangedEventArgs(IReadOnlyList<MonitorInfo> monitors)
    {
        Monitors = monitors;
    }
}
