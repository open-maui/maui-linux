// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
    private static readonly object _lock = new();

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
    public SystemColors Colors { get; private set; }

    private FileSystemWatcher? _settingsWatcher;

    private SystemThemeService()
    {
        DetectDesktopEnvironment();
        DetectTheme();
        UpdateColors();
        SetupWatcher();
    }

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
        var theme = Desktop switch
        {
            DesktopEnvironment.GNOME => DetectGnomeTheme(),
            DesktopEnvironment.KDE => DetectKdeTheme(),
            DesktopEnvironment.XFCE => DetectXfceTheme(),
            DesktopEnvironment.Cinnamon => DetectCinnamonTheme(),
            _ => DetectGtkTheme()
        };

        CurrentTheme = theme ?? SystemTheme.Light;

        // Try to get accent color
        AccentColor = Desktop switch
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
        catch { }

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
        catch { }

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
        catch { }

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
        catch { }

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
        catch { }

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
        catch { }

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
        catch { }
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
