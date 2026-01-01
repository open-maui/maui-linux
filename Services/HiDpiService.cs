// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Provides HiDPI and display scaling detection for Linux.
/// </summary>
public class HiDpiService
{
    private const float DefaultDpi = 96f;
    private float _scaleFactor = 1.0f;
    private float _dpi = DefaultDpi;
    private bool _initialized;

    /// <summary>
    /// Gets the current scale factor.
    /// </summary>
    public float ScaleFactor => _scaleFactor;

    /// <summary>
    /// Gets the current DPI.
    /// </summary>
    public float Dpi => _dpi;

    /// <summary>
    /// Event raised when scale factor changes.
    /// </summary>
    public event EventHandler<ScaleChangedEventArgs>? ScaleChanged;

    /// <summary>
    /// Initializes the HiDPI detection service.
    /// </summary>
    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        DetectScaleFactor();
    }

    /// <summary>
    /// Detects the current scale factor using multiple methods.
    /// </summary>
    public void DetectScaleFactor()
    {
        float scale = 1.0f;
        float dpi = DefaultDpi;

        // Try multiple detection methods in order of preference
        if (TryGetEnvironmentScale(out float envScale))
        {
            scale = envScale;
        }
        else if (TryGetGnomeScale(out float gnomeScale, out float gnomeDpi))
        {
            scale = gnomeScale;
            dpi = gnomeDpi;
        }
        else if (TryGetKdeScale(out float kdeScale))
        {
            scale = kdeScale;
        }
        else if (TryGetX11Scale(out float x11Scale, out float x11Dpi))
        {
            scale = x11Scale;
            dpi = x11Dpi;
        }
        else if (TryGetXrandrScale(out float xrandrScale))
        {
            scale = xrandrScale;
        }

        UpdateScale(scale, dpi);
    }

    private void UpdateScale(float scale, float dpi)
    {
        if (Math.Abs(_scaleFactor - scale) > 0.01f || Math.Abs(_dpi - dpi) > 0.01f)
        {
            var oldScale = _scaleFactor;
            _scaleFactor = scale;
            _dpi = dpi;
            ScaleChanged?.Invoke(this, new ScaleChangedEventArgs(oldScale, scale, dpi));
        }
    }

    /// <summary>
    /// Gets scale from environment variables.
    /// </summary>
    private static bool TryGetEnvironmentScale(out float scale)
    {
        scale = 1.0f;

        // GDK_SCALE (GTK3/4)
        var gdkScale = Environment.GetEnvironmentVariable("GDK_SCALE");
        if (!string.IsNullOrEmpty(gdkScale) && float.TryParse(gdkScale, out float gdk))
        {
            scale = gdk;
            return true;
        }

        // GDK_DPI_SCALE (GTK3/4)
        var gdkDpiScale = Environment.GetEnvironmentVariable("GDK_DPI_SCALE");
        if (!string.IsNullOrEmpty(gdkDpiScale) && float.TryParse(gdkDpiScale, out float gdkDpi))
        {
            scale = gdkDpi;
            return true;
        }

        // QT_SCALE_FACTOR
        var qtScale = Environment.GetEnvironmentVariable("QT_SCALE_FACTOR");
        if (!string.IsNullOrEmpty(qtScale) && float.TryParse(qtScale, out float qt))
        {
            scale = qt;
            return true;
        }

        // QT_SCREEN_SCALE_FACTORS (can be per-screen)
        var qtScreenScales = Environment.GetEnvironmentVariable("QT_SCREEN_SCALE_FACTORS");
        if (!string.IsNullOrEmpty(qtScreenScales))
        {
            // Format: "screen1=1.5;screen2=2.0" or just "1.5"
            var first = qtScreenScales.Split(';')[0];
            if (first.Contains('='))
            {
                first = first.Split('=')[1];
            }
            if (float.TryParse(first, out float qtScreen))
            {
                scale = qtScreen;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets scale from GNOME settings.
    /// </summary>
    private static bool TryGetGnomeScale(out float scale, out float dpi)
    {
        scale = 1.0f;
        dpi = DefaultDpi;

        try
        {
            // Try gsettings for GNOME
            var result = RunCommand("gsettings", "get org.gnome.desktop.interface scaling-factor");
            if (!string.IsNullOrEmpty(result))
            {
                var match = Regex.Match(result, @"uint32\s+(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int gnomeScale))
                {
                    if (gnomeScale > 0)
                    {
                        scale = gnomeScale;
                    }
                }
            }

            // Also check text-scaling-factor for fractional scaling
            result = RunCommand("gsettings", "get org.gnome.desktop.interface text-scaling-factor");
            if (!string.IsNullOrEmpty(result) && float.TryParse(result.Trim(), out float textScale))
            {
                if (textScale > 0.5f)
                {
                    scale = Math.Max(scale, textScale);
                }
            }

            // Check for GNOME 40+ experimental fractional scaling
            result = RunCommand("gsettings", "get org.gnome.mutter experimental-features");
            if (result != null && result.Contains("scale-monitor-framebuffer"))
            {
                // Fractional scaling is enabled, try to get actual scale
                result = RunCommand("gdbus", "call --session --dest org.gnome.Mutter.DisplayConfig --object-path /org/gnome/Mutter/DisplayConfig --method org.gnome.Mutter.DisplayConfig.GetCurrentState");
                if (result != null)
                {
                    // Parse for scale value
                    var scaleMatch = Regex.Match(result, @"'scale':\s*<(\d+\.?\d*)>");
                    if (scaleMatch.Success && float.TryParse(scaleMatch.Groups[1].Value, out float mutterScale))
                    {
                        scale = mutterScale;
                    }
                }
            }

            return scale > 1.0f || Math.Abs(scale - 1.0f) < 0.01f;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets scale from KDE settings.
    /// </summary>
    private static bool TryGetKdeScale(out float scale)
    {
        scale = 1.0f;

        try
        {
            // Try kreadconfig5 for KDE Plasma 5
            var result = RunCommand("kreadconfig5", "--file kdeglobals --group KScreen --key ScaleFactor");
            if (!string.IsNullOrEmpty(result) && float.TryParse(result.Trim(), out float kdeScale))
            {
                if (kdeScale > 0)
                {
                    scale = kdeScale;
                    return true;
                }
            }

            // Try KDE Plasma 6
            result = RunCommand("kreadconfig6", "--file kdeglobals --group KScreen --key ScaleFactor");
            if (!string.IsNullOrEmpty(result) && float.TryParse(result.Trim(), out float kde6Scale))
            {
                if (kde6Scale > 0)
                {
                    scale = kde6Scale;
                    return true;
                }
            }

            // Check kdeglobals config file directly
            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config", "kdeglobals");

            if (File.Exists(configPath))
            {
                var lines = File.ReadAllLines(configPath);
                bool inKScreenSection = false;
                foreach (var line in lines)
                {
                    if (line.Trim() == "[KScreen]")
                    {
                        inKScreenSection = true;
                        continue;
                    }
                    if (inKScreenSection && line.StartsWith("["))
                    {
                        break;
                    }
                    if (inKScreenSection && line.StartsWith("ScaleFactor="))
                    {
                        var value = line.Substring("ScaleFactor=".Length);
                        if (float.TryParse(value, out float fileScale))
                        {
                            scale = fileScale;
                            return true;
                        }
                    }
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets scale from X11 Xresources.
    /// </summary>
    private bool TryGetX11Scale(out float scale, out float dpi)
    {
        scale = 1.0f;
        dpi = DefaultDpi;

        try
        {
            // Try xrdb query
            var result = RunCommand("xrdb", "-query");
            if (!string.IsNullOrEmpty(result))
            {
                // Look for Xft.dpi
                var match = Regex.Match(result, @"Xft\.dpi:\s*(\d+)");
                if (match.Success && float.TryParse(match.Groups[1].Value, out float xftDpi))
                {
                    dpi = xftDpi;
                    scale = xftDpi / DefaultDpi;
                    return true;
                }
            }

            // Try reading .Xresources directly
            var xresourcesPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".Xresources");

            if (File.Exists(xresourcesPath))
            {
                var content = File.ReadAllText(xresourcesPath);
                var match = Regex.Match(content, @"Xft\.dpi:\s*(\d+)");
                if (match.Success && float.TryParse(match.Groups[1].Value, out float fileDpi))
                {
                    dpi = fileDpi;
                    scale = fileDpi / DefaultDpi;
                    return true;
                }
            }

            // Try X11 directly
            return TryGetX11DpiDirect(out scale, out dpi);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets DPI directly from X11 server.
    /// </summary>
    private bool TryGetX11DpiDirect(out float scale, out float dpi)
    {
        scale = 1.0f;
        dpi = DefaultDpi;

        try
        {
            var display = XOpenDisplay(IntPtr.Zero);
            if (display == IntPtr.Zero) return false;

            try
            {
                int screen = XDefaultScreen(display);

                // Get physical dimensions
                int widthMm = XDisplayWidthMM(display, screen);
                int heightMm = XDisplayHeightMM(display, screen);
                int widthPx = XDisplayWidth(display, screen);
                int heightPx = XDisplayHeight(display, screen);

                if (widthMm > 0 && heightMm > 0)
                {
                    float dpiX = widthPx * 25.4f / widthMm;
                    float dpiY = heightPx * 25.4f / heightMm;
                    dpi = (dpiX + dpiY) / 2;
                    scale = dpi / DefaultDpi;
                    return true;
                }

                return false;
            }
            finally
            {
                XCloseDisplay(display);
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets scale from xrandr output.
    /// </summary>
    private static bool TryGetXrandrScale(out float scale)
    {
        scale = 1.0f;

        try
        {
            var result = RunCommand("xrandr", "--query");
            if (string.IsNullOrEmpty(result)) return false;

            // Look for connected displays with scaling
            // Format: "eDP-1 connected primary 2560x1440+0+0 (normal left inverted right x axis y axis) 309mm x 174mm"
            var lines = result.Split('\n');
            foreach (var line in lines)
            {
                if (!line.Contains("connected") || line.Contains("disconnected")) continue;

                // Try to find resolution and physical size
                var resMatch = Regex.Match(line, @"(\d+)x(\d+)\+\d+\+\d+");
                var mmMatch = Regex.Match(line, @"(\d+)mm x (\d+)mm");

                if (resMatch.Success && mmMatch.Success)
                {
                    if (int.TryParse(resMatch.Groups[1].Value, out int widthPx) &&
                        int.TryParse(mmMatch.Groups[1].Value, out int widthMm) &&
                        widthMm > 0)
                    {
                        float dpi = widthPx * 25.4f / widthMm;
                        scale = dpi / DefaultDpi;
                        return true;
                    }
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static string? RunCommand(string command, string arguments)
    {
        try
        {
            using var process = new System.Diagnostics.Process();
            process.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(1000);
            return output;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Converts logical pixels to physical pixels.
    /// </summary>
    public float ToPhysicalPixels(float logicalPixels)
    {
        return logicalPixels * _scaleFactor;
    }

    /// <summary>
    /// Converts physical pixels to logical pixels.
    /// </summary>
    public float ToLogicalPixels(float physicalPixels)
    {
        return physicalPixels / _scaleFactor;
    }

    /// <summary>
    /// Gets the recommended font scale factor.
    /// </summary>
    public float GetFontScaleFactor()
    {
        // Some desktop environments use a separate text scaling factor
        try
        {
            var result = RunCommand("gsettings", "get org.gnome.desktop.interface text-scaling-factor");
            if (!string.IsNullOrEmpty(result) && float.TryParse(result.Trim(), out float textScale))
            {
                return textScale;
            }
        }
        catch { }

        return _scaleFactor;
    }

    #region X11 Interop

    [DllImport("libX11.so.6")]
    private static extern nint XOpenDisplay(nint display);

    [DllImport("libX11.so.6")]
    private static extern void XCloseDisplay(nint display);

    [DllImport("libX11.so.6")]
    private static extern int XDefaultScreen(nint display);

    [DllImport("libX11.so.6")]
    private static extern int XDisplayWidth(nint display, int screen);

    [DllImport("libX11.so.6")]
    private static extern int XDisplayHeight(nint display, int screen);

    [DllImport("libX11.so.6")]
    private static extern int XDisplayWidthMM(nint display, int screen);

    [DllImport("libX11.so.6")]
    private static extern int XDisplayHeightMM(nint display, int screen);

    #endregion
}

/// <summary>
/// Event args for scale change events.
/// </summary>
public class ScaleChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the old scale factor.
    /// </summary>
    public float OldScale { get; }

    /// <summary>
    /// Gets the new scale factor.
    /// </summary>
    public float NewScale { get; }

    /// <summary>
    /// Gets the new DPI.
    /// </summary>
    public float NewDpi { get; }

    public ScaleChangedEventArgs(float oldScale, float newScale, float newDpi)
    {
        OldScale = oldScale;
        NewScale = newScale;
        NewDpi = newDpi;
    }
}
