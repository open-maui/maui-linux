// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Maui.Platform.Linux.Services.Portal;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Provides HiDPI and display scaling detection for Linux.
/// </summary>
public partial class HiDpiService
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
    /// Which detection method produced <see cref="ScaleFactor"/> on the last
    /// <see cref="DetectScaleFactor"/> call (for diagnostics / openmaui doctor).
    /// </summary>
    internal string DetectionSource { get; private set; } = "default (none matched)";

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
        string source = "default (none matched)";

        // Try multiple detection methods in order of preference
        if (TryGetEnvironmentScale(out float envScale))
        {
            scale = envScale;
            source = "environment (GDK_SCALE / GDK_DPI_SCALE / QT_SCALE_FACTOR / QT_SCREEN_SCALE_FACTORS)";
        }
        else if (TryGetGnomeScale(out float gnomeScale, out float gnomeDpi))
        {
            scale = gnomeScale;
            dpi = gnomeDpi;
            source = "GNOME (gsettings / Mutter DisplayConfig)";
        }
        else if (TryGetKdeScale(out float kdeScale))
        {
            scale = kdeScale;
            source = "KDE (kdeglobals KScreen/ScaleFactor)";
        }
        else if (TryGetX11Scale(out float x11Scale, out float x11Dpi))
        {
            scale = x11Scale;
            dpi = x11Dpi;
            source = "X11 (Xft.dpi / .Xresources / X server DPI)";
        }
        else if (TryGetXrandrScale(out float xrandrScale))
        {
            scale = xrandrScale;
            source = "xrandr (output resolution / physical size)";
        }

        DetectionSource = source;

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
        if (!string.IsNullOrEmpty(gdkScale) && float.TryParse(gdkScale, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float gdk))
        {
            scale = gdk;
            return true;
        }

        // GDK_DPI_SCALE (GTK3/4)
        var gdkDpiScale = Environment.GetEnvironmentVariable("GDK_DPI_SCALE");
        if (!string.IsNullOrEmpty(gdkDpiScale) && float.TryParse(gdkDpiScale, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float gdkDpi))
        {
            scale = gdkDpi;
            return true;
        }

        // QT_SCALE_FACTOR
        var qtScale = Environment.GetEnvironmentVariable("QT_SCALE_FACTOR");
        if (!string.IsNullOrEmpty(qtScale) && float.TryParse(qtScale, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float qt))
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
            if (float.TryParse(first, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float qtScreen))
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
            // org.gnome.desktop.interface through the Settings portal when it
            // exposes that namespace (xdg-desktop-portal-gnome/-gtk; the KDE
            // backend mirrors defaults), gsettings otherwise.
            var portal = ReadGnomeInterfaceFromPortal();

            var gnomeScale = portal.ScalingFactor;
            if (gnomeScale == null)
            {
                var result = RunCommand("gsettings", "get org.gnome.desktop.interface scaling-factor");
                gnomeScale = ParseGsettingsScalingFactor(result);
            }
            if (gnomeScale is > 0)
            {
                scale = (float)gnomeScale.Value;
            }

            // Also check text-scaling-factor for fractional scaling
            var textScale = portal.TextScalingFactor ?? ParseGsettingsDouble(RunCommand("gsettings", "get org.gnome.desktop.interface text-scaling-factor"));
            if (textScale is > 0.5)
            {
                scale = Math.Max(scale, (float)textScale.Value);
            }

            // Check for GNOME 40+ experimental fractional scaling
            var features = RunCommand("gsettings", "get org.gnome.mutter experimental-features");
            if (features != null && features.Contains("scale-monitor-framebuffer"))
            {
                // Fractional scaling is enabled: the primary logical monitor's
                // scale from Mutter's DisplayConfig (native D-Bus).
                if (TryGetMutterPrimaryScale(out float mutterScale))
                {
                    scale = mutterScale;
                }
            }

            return scale > 1.01f;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>scaling-factor / text-scaling-factor as read through the Settings portal (null when absent).</summary>
    internal readonly record struct GnomeInterfaceScaling(double? ScalingFactor, double? TextScalingFactor);

    private static GnomeInterfaceScaling ReadGnomeInterfaceFromPortal()
    {
        if (!DesktopPortal.ShouldTry(PortalUse.Always))
            return default;
        PortalSync.TryRun(ct => ReadGnomeInterfaceAsync(DesktopPortal.Current, ct), TimeSpan.FromSeconds(1), out var values);
        return values;
    }

    internal static async Task<GnomeInterfaceScaling> ReadGnomeInterfaceAsync(IDesktopPortal portal, CancellationToken cancellationToken)
    {
        if (await portal.GetVersionAsync(PortalInterfaces.Settings, cancellationToken).ConfigureAwait(false) == 0)
            return default;
        try
        {
            var scaling = await portal.ReadSettingAsync(PortalAppearance.GnomeInterfaceNamespace, "scaling-factor", cancellationToken).ConfigureAwait(false);
            var text = await portal.ReadSettingAsync(PortalAppearance.GnomeInterfaceNamespace, "text-scaling-factor", cancellationToken).ConfigureAwait(false);
            return new GnomeInterfaceScaling(PortalAppearance.ParsePositiveNumber(scaling), PortalAppearance.ParsePositiveNumber(text));
        }
        catch (PortalUnavailableException)
        {
            return default;
        }
    }

    /// <summary>"uint32 2" (gsettings output) to 2; null when unparsable.</summary>
    internal static double? ParseGsettingsScalingFactor(string? output)
    {
        if (string.IsNullOrEmpty(output))
            return null;
        var match = Regex.Match(output, @"uint32\s+(\d+)");
        return match.Success && int.TryParse(match.Groups[1].Value, out int value) ? value : null;
    }

    /// <summary>"1.25" (gsettings output) to 1.25 with the invariant culture; null when unparsable.</summary>
    internal static double? ParseGsettingsDouble(string? output)
    {
        if (string.IsNullOrEmpty(output))
            return null;
        return double.TryParse(output.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double value) ? value : null;
    }

    private static bool TryGetMutterPrimaryScale(out float scale)
    {
        scale = 1.0f;
        if (!PortalSync.TryRun(async ct =>
        {
            var bus = await SessionBus.GetAsync(ct).ConfigureAwait(false);
            var config = bus.Connection.CreateProxy<IMutterDisplayConfigProxy>("org.gnome.Mutter.DisplayConfig", new Tmds.DBus.ObjectPath("/org/gnome/Mutter/DisplayConfig"));
            var state = await config.GetCurrentStateAsync().WaitAsync(ct).ConfigureAwait(false);
            return PrimaryLogicalScale(state.logicalMonitors.Select(m => (m.scale, m.primary)));
        }, TimeSpan.FromSeconds(1), out var found) || found is not > 0)
        {
            return false;
        }

        scale = (float)found.Value;
        return true;
    }

    /// <summary>The primary logical monitor's scale, else the first one's; null for none.</summary>
    internal static double? PrimaryLogicalScale(IEnumerable<(double scale, bool primary)> logicalMonitors)
    {
        var list = logicalMonitors.ToList();
        if (list.Count == 0)
            return null;
        var primary = list.FirstOrDefault(m => m.primary);
        return primary.primary ? primary.scale : list[0].scale;
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
            if (!string.IsNullOrEmpty(result) && float.TryParse(result.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float kdeScale))
            {
                if (kdeScale > 0)
                {
                    scale = kdeScale;
                    return true;
                }
            }

            // Try KDE Plasma 6
            result = RunCommand("kreadconfig6", "--file kdeglobals --group KScreen --key ScaleFactor");
            if (!string.IsNullOrEmpty(result) && float.TryParse(result.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float kde6Scale))
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
                        if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float fileScale))
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
                if (match.Success && float.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float xftDpi))
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
                if (match.Success && float.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float fileDpi))
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

    internal static string? RunCommand(string command, string arguments)
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
        var portalText = ReadGnomeInterfaceFromPortal().TextScalingFactor;
        if (portalText is > 0)
            return (float)portalText.Value;

        try
        {
            var result = RunCommand("gsettings", "get org.gnome.desktop.interface text-scaling-factor");
            if (!string.IsNullOrEmpty(result) && float.TryParse(result.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float textScale))
            {
                return textScale;
            }
        }
        catch (Exception ex) { DiagnosticLog.Debug("HiDpiService", "Font scale factor detection failed", ex); }

        return _scaleFactor;
    }

    #region X11 Interop

    [LibraryImport("libX11.so.6")]
    private static partial nint XOpenDisplay(nint display);

    [LibraryImport("libX11.so.6")]
    private static partial void XCloseDisplay(nint display);

    [LibraryImport("libX11.so.6")]
    private static partial int XDefaultScreen(nint display);

    [LibraryImport("libX11.so.6")]
    private static partial int XDisplayWidth(nint display, int screen);

    [LibraryImport("libX11.so.6")]
    private static partial int XDisplayHeight(nint display, int screen);

    [LibraryImport("libX11.so.6")]
    private static partial int XDisplayWidthMM(nint display, int screen);

    [LibraryImport("libX11.so.6")]
    private static partial int XDisplayHeightMM(nint display, int screen);

    #endregion
}
