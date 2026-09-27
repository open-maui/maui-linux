// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Services.Portal;
using SkiaSharp;
using System.Diagnostics;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Detects and monitors system theme settings (dark/light mode, accent colors).
/// Supports GNOME, KDE, and GTK-based environments.
/// </summary>
public class SystemThemeService
{
    private static SystemThemeService? _instance;
    private static readonly Lock _lock = new();

    /// <summary>
    /// Gets the singleton instance of the system theme service.
    /// </summary>
    public static SystemThemeService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new SystemThemeService();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// The current system theme.
    /// </summary>
    public SystemTheme CurrentTheme { get; private set; } = SystemTheme.Light;

    /// <summary>
    /// The system accent color (if available).
    /// </summary>
    public SKColor AccentColor { get; private set; } = new SKColor(0x21, 0x96, 0xF3); // Default blue

    /// <summary>
    /// The detected desktop environment.
    /// </summary>
    public DesktopEnvironment Desktop { get; private set; } = DesktopEnvironment.Unknown;

    /// <summary>
    /// Event raised when the theme changes.
    /// </summary>
    public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    /// <summary>
    /// System colors based on the current theme.
    /// </summary>
    public SystemColors Colors { get; private set; } = new SystemColors
    {
        Background = SKColors.White,
        Surface = new SKColor(0xF5, 0xF5, 0xF5),
        Primary = new SKColor(0x21, 0x96, 0xF3),
        OnPrimary = SKColors.White,
        Text = new SKColor(0x21, 0x21, 0x21),
        TextSecondary = new SKColor(0x75, 0x75, 0x75),
        Border = new SKColor(0xE0, 0xE0, 0xE0),
        Divider = new SKColor(0xE0, 0xE0, 0xE0),
        Error = new SKColor(0xF4, 0x43, 0x36),
        Success = new SKColor(0x4C, 0xAF, 0x50)
    };

    private FileSystemWatcher? _settingsWatcher;
    private Timer? _pollTimer;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    // xdg-desktop-portal Settings (org.freedesktop.appearance): read at start
    // and kept current through the SettingChanged signal, so dark-mode and
    // accent changes are live without polling gsettings.
    private static readonly TimeSpan PortalStartupWait = TimeSpan.FromMilliseconds(1500);
    private PortalColorScheme? _portalColorScheme;
    private (byte R, byte G, byte B)? _portalAccent;
    private IDisposable? _portalSettingWatch;
    private volatile bool _constructed;

    private SystemThemeService()
    {
        DetectDesktopEnvironment();
        StartPortalSettings();
        DetectTheme();
        UpdateColors();
        SetupWatcher();
        SetupPolling();
        _constructed = true;
    }

    /// <summary>True while the portal SettingChanged subscription is active.</summary>
    internal bool IsPortalSettingsLive => _portalSettingWatch != null;

    private void StartPortalSettings()
    {
        if (!DesktopPortal.ShouldTry(PortalUse.Always))
            return;

        try
        {
            var init = Task.Run(() => InitializePortalSettingsAsync(DesktopPortal.Current));
            if (!init.Wait(PortalStartupWait))
                DiagnosticLog.Debug("SystemThemeService", "Settings portal slow to answer; using desktop detection until it does");
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("SystemThemeService", "Settings portal init failed", ex);
        }
    }

    private async Task InitializePortalSettingsAsync(IDesktopPortal portal)
    {
        if (await portal.GetVersionAsync(PortalInterfaces.Settings).ConfigureAwait(false) == 0)
            return;

        try
        {
            // Subscribe first so a change between the read and the watch is not lost.
            _portalSettingWatch = await portal.WatchSettingChangedAsync(OnPortalSettingChanged).ConfigureAwait(false);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            ApplyPortalSetting(PortalAppearance.ColorSchemeKey,
                await portal.ReadSettingAsync(PortalAppearance.Namespace, PortalAppearance.ColorSchemeKey, cts.Token).ConfigureAwait(false));
            ApplyPortalSetting(PortalAppearance.AccentColorKey,
                await portal.ReadSettingAsync(PortalAppearance.Namespace, PortalAppearance.AccentColorKey, cts.Token).ConfigureAwait(false));

            DiagnosticLog.Debug("SystemThemeService", $"Settings portal: color-scheme={_portalColorScheme?.ToString() ?? "unset"}, accent={(_portalAccent.HasValue ? "set" : "unset")}");

            // Finished after the constructor gave up waiting: apply now.
            if (_constructed)
                RefreshTheme();
        }
        catch (PortalUnavailableException ex)
        {
            DiagnosticLog.Debug("SystemThemeService", $"Settings portal unavailable: {ex.Message}");
        }
    }

    private void OnPortalSettingChanged(string @namespace, string key, object value)
    {
        if (@namespace != PortalAppearance.Namespace)
            return;
        if (!ApplyPortalSetting(key, value))
            return;

        try
        {
            DiagnosticLog.Debug("SystemThemeService", $"Settings portal change: {key}");
            RefreshTheme();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SystemThemeService", $"Error applying portal setting change: {ex.Message}", ex);
        }
    }

    /// <summary>Stores a portal appearance value; true when it was a key this service uses.</summary>
    private bool ApplyPortalSetting(string key, object? value)
    {
        switch (key)
        {
            case PortalAppearance.ColorSchemeKey:
                _portalColorScheme = PortalAppearance.ParseColorScheme(value);
                return true;
            case PortalAppearance.AccentColorKey:
                _portalAccent = PortalAppearance.ParseAccentColor(value);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Theme from the portal color-scheme: dark/light when the desktop states a
    /// preference, null (use desktop-specific detection) otherwise.
    /// </summary>
    internal static SystemTheme? ThemeFromPortal(PortalColorScheme? scheme) => scheme switch
    {
        PortalColorScheme.PreferDark => SystemTheme.Dark,
        PortalColorScheme.PreferLight => SystemTheme.Light,
        _ => null,
    };

    private void DetectDesktopEnvironment()
    {
        var xdgDesktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP")?.ToLowerInvariant() ?? "";
        var desktopSession = Environment.GetEnvironmentVariable("DESKTOP_SESSION")?.ToLowerInvariant() ?? "";

        if (xdgDesktop.Contains("gnome") || desktopSession.Contains("gnome"))
        {
            Desktop = DesktopEnvironment.GNOME;
        }
        else if (xdgDesktop.Contains("kde") || xdgDesktop.Contains("plasma") || desktopSession.Contains("plasma"))
        {
            Desktop = DesktopEnvironment.KDE;
        }
        else if (xdgDesktop.Contains("xfce") || desktopSession.Contains("xfce"))
        {
            Desktop = DesktopEnvironment.XFCE;
        }
        else if (xdgDesktop.Contains("mate") || desktopSession.Contains("mate"))
        {
            Desktop = DesktopEnvironment.MATE;
        }
        else if (xdgDesktop.Contains("cinnamon") || desktopSession.Contains("cinnamon"))
        {
            Desktop = DesktopEnvironment.Cinnamon;
        }
        else if (xdgDesktop.Contains("lxqt"))
        {
            Desktop = DesktopEnvironment.LXQt;
        }
        else if (xdgDesktop.Contains("lxde"))
        {
            Desktop = DesktopEnvironment.LXDE;
        }
        else
        {
            Desktop = DesktopEnvironment.Unknown;
        }
    }

    private void DetectTheme()
    {
        var theme = ThemeFromPortal(_portalColorScheme) ?? Desktop switch
        {
            DesktopEnvironment.GNOME => DetectGnomeTheme(),
            DesktopEnvironment.KDE => DetectKdeTheme(),
            DesktopEnvironment.XFCE => DetectXfceTheme(),
            DesktopEnvironment.Cinnamon => DetectCinnamonTheme(),
            _ => DetectGtkTheme()
        };

        CurrentTheme = theme ?? SystemTheme.Light;

        // Try to get accent color (the portal's accent-color wins when set)
        AccentColor = _portalAccent is { } accent ? new SKColor(accent.R, accent.G, accent.B) : Desktop switch
        {
            DesktopEnvironment.GNOME => GetGnomeAccentColor(),
            DesktopEnvironment.KDE => GetKdeAccentColor(),
            _ => new SKColor(0x21, 0x96, 0xF3)
        };
    }

    private SystemTheme? DetectGnomeTheme()
    {
        try
        {
            // gsettings get org.gnome.desktop.interface color-scheme
            var output = RunCommand("gsettings", "get org.gnome.desktop.interface color-scheme");
            if (output.Contains("prefer-dark"))
                return SystemTheme.Dark;
            if (output.Contains("prefer-light") || output.Contains("default"))
                return SystemTheme.Light;

            // Fallback: check GTK theme name
            output = RunCommand("gsettings", "get org.gnome.desktop.interface gtk-theme");
            if (output.ToLowerInvariant().Contains("dark"))
                return SystemTheme.Dark;
        }
        catch (Exception ex) { DiagnosticLog.Debug("SystemThemeService", "GNOME theme detection failed", ex); }

        return null;
    }

    private SystemTheme? DetectKdeTheme()
    {
        try
        {
            // Read ~/.config/kdeglobals
            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config", "kdeglobals");

            if (File.Exists(configPath))
            {
                var content = File.ReadAllText(configPath);

                // Look for ColorScheme or LookAndFeelPackage
                if (content.Contains("BreezeDark", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("Dark", StringComparison.OrdinalIgnoreCase))
                {
                    return SystemTheme.Dark;
                }
            }
        }
        catch (Exception ex) { DiagnosticLog.Debug("SystemThemeService", "KDE theme detection failed", ex); }

        return null;
    }

    private SystemTheme? DetectXfceTheme()
    {
        try
        {
            var output = RunCommand("xfconf-query", "-c xsettings -p /Net/ThemeName");
            if (output.ToLowerInvariant().Contains("dark"))
                return SystemTheme.Dark;
        }
        catch (Exception ex) { DiagnosticLog.Debug("SystemThemeService", "XFCE theme detection failed", ex); }

        return DetectGtkTheme();
    }

    private SystemTheme? DetectCinnamonTheme()
    {
        try
        {
            var output = RunCommand("gsettings", "get org.cinnamon.desktop.interface gtk-theme");
            if (output.ToLowerInvariant().Contains("dark"))
                return SystemTheme.Dark;
        }
        catch (Exception ex) { DiagnosticLog.Debug("SystemThemeService", "Cinnamon theme detection failed", ex); }

        return null;
    }

    private SystemTheme? DetectGtkTheme()
    {
        try
        {
            // Try GTK3 settings
            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config", "gtk-3.0", "settings.ini");

            if (File.Exists(configPath))
            {
                var content = File.ReadAllText(configPath);
                var lines = content.Split('\n');
                foreach (var line in lines)
                {
                    if (line.StartsWith("gtk-theme-name=", StringComparison.OrdinalIgnoreCase))
                    {
                        var themeName = line.Substring("gtk-theme-name=".Length).Trim();
                        if (themeName.Contains("dark", StringComparison.OrdinalIgnoreCase))
                            return SystemTheme.Dark;
                    }
                    if (line.StartsWith("gtk-application-prefer-dark-theme=", StringComparison.OrdinalIgnoreCase))
                    {
                        var value = line.Substring("gtk-application-prefer-dark-theme=".Length).Trim();
                        if (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase))
                            return SystemTheme.Dark;
                    }
                }
            }
        }
        catch (Exception ex) { DiagnosticLog.Debug("SystemThemeService", "GTK theme file read failed", ex); }

        return null;
    }

    private SKColor GetGnomeAccentColor()
    {
        try
        {
            var output = RunCommand("gsettings", "get org.gnome.desktop.interface accent-color");
            // Returns something like 'blue', 'teal', 'green', etc.
            return output.Trim().Trim('\'') switch
            {
                "blue" => new SKColor(0x35, 0x84, 0xe4),
                "teal" => new SKColor(0x2a, 0xc3, 0xde),
                "green" => new SKColor(0x3a, 0x94, 0x4a),
                "yellow" => new SKColor(0xf6, 0xd3, 0x2d),
                "orange" => new SKColor(0xff, 0x78, 0x00),
                "red" => new SKColor(0xe0, 0x1b, 0x24),
                "pink" => new SKColor(0xd6, 0x56, 0x8c),
                "purple" => new SKColor(0x91, 0x41, 0xac),
                "slate" => new SKColor(0x5e, 0x5c, 0x64),
                _ => new SKColor(0x21, 0x96, 0xF3)
            };
        }
        catch
        {
            return new SKColor(0x21, 0x96, 0xF3);
        }
    }

    private SKColor GetKdeAccentColor()
    {
        try
        {
            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config", "kdeglobals");

            if (File.Exists(configPath))
            {
                var content = File.ReadAllText(configPath);
                var lines = content.Split('\n');
                bool inColorsHeader = false;

                foreach (var line in lines)
                {
                    if (line.StartsWith("[Colors:Header]"))
                    {
                        inColorsHeader = true;
                        continue;
                    }
                    if (line.StartsWith("[") && inColorsHeader)
                    {
                        break;
                    }
                    if (inColorsHeader && line.StartsWith("BackgroundNormal="))
                    {
                        var rgb = line.Substring("BackgroundNormal=".Length).Split(',');
                        if (rgb.Length >= 3 &&
                            byte.TryParse(rgb[0], out var r) &&
                            byte.TryParse(rgb[1], out var g) &&
                            byte.TryParse(rgb[2], out var b))
                        {
                            return new SKColor(r, g, b);
                        }
                    }
                }
            }
        }
        catch (Exception ex) { DiagnosticLog.Debug("SystemThemeService", "KDE accent color parsing failed", ex); }

        return new SKColor(0x21, 0x96, 0xF3);
    }

    private void UpdateColors()
    {
        Colors = CurrentTheme == SystemTheme.Dark
            ? new SystemColors
            {
                Background = new SKColor(0x1e, 0x1e, 0x1e),
                Surface = new SKColor(0x2d, 0x2d, 0x2d),
                Primary = AccentColor,
                OnPrimary = SKColors.White,
                Text = new SKColor(0xf0, 0xf0, 0xf0),
                TextSecondary = new SKColor(0xa0, 0xa0, 0xa0),
                Border = new SKColor(0x40, 0x40, 0x40),
                Divider = new SKColor(0x3a, 0x3a, 0x3a),
                Error = new SKColor(0xcf, 0x66, 0x79),
                Success = new SKColor(0x81, 0xc9, 0x95)
            }
            : new SystemColors
            {
                Background = new SKColor(0xfa, 0xfa, 0xfa),
                Surface = SKColors.White,
                Primary = AccentColor,
                OnPrimary = SKColors.White,
                Text = new SKColor(0x21, 0x21, 0x21),
                TextSecondary = new SKColor(0x75, 0x75, 0x75),
                Border = new SKColor(0xe0, 0xe0, 0xe0),
                Divider = new SKColor(0xee, 0xee, 0xee),
                Error = new SKColor(0xb0, 0x00, 0x20),
                Success = new SKColor(0x2e, 0x7d, 0x32)
            };
    }

    private void SetupWatcher()
    {
        try
        {
            var configDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config");

            if (Directory.Exists(configDir))
            {
                _settingsWatcher = new FileSystemWatcher(configDir)
                {
                    NotifyFilter = NotifyFilters.LastWrite,
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true
                };

                _settingsWatcher.Changed += OnSettingsChanged;
            }
        }
        catch (Exception ex) { DiagnosticLog.Debug("SystemThemeService", "Settings watcher setup failed", ex); }
    }

    private void SetupPolling()
    {
        // With the Settings portal live and stating a preference, changes
        // arrive as SettingChanged signals; no polling needed.
        if (IsPortalSettingsLive && ThemeFromPortal(_portalColorScheme) != null)
            return;

        // For GNOME and other desktops that use dconf/gsettings,
        // file watching doesn't work. Use periodic polling instead.
        _pollTimer = new Timer(OnPollTimer, null, PollInterval, PollInterval);
    }

    private void OnPollTimer(object? state)
    {
        try
        {
            var oldTheme = CurrentTheme;
            DetectTheme();

            if (oldTheme != CurrentTheme)
            {
                DiagnosticLog.Debug("SystemThemeService", $"Theme change detected via polling: {oldTheme} -> {CurrentTheme}");
                UpdateColors();
                ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(CurrentTheme));
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SystemThemeService", $"Error in poll timer: {ex.Message}", ex);
        }
    }

    private void OnSettingsChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce and check relevant files
        if (e.Name?.Contains("kdeglobals") == true ||
            e.Name?.Contains("gtk") == true ||
            e.Name?.Contains("settings") == true)
        {
            // Re-detect theme after a short delay
            Task.Delay(500).ContinueWith(_ =>
            {
                var oldTheme = CurrentTheme;
                DetectTheme();
                UpdateColors();

                if (oldTheme != CurrentTheme)
                {
                    ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(CurrentTheme));
                }
            });
        }
    }

    private string RunCommand(string command, string arguments)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(1000);
            return output;
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// Forces a theme refresh.
    /// </summary>
    public void RefreshTheme()
    {
        var oldTheme = CurrentTheme;
        DetectTheme();
        UpdateColors();

        if (oldTheme != CurrentTheme)
        {
            ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(CurrentTheme));
        }
    }
}
