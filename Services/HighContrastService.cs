// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Provides high contrast mode detection and theme support for accessibility.
/// </summary>
public class HighContrastService
{
    private bool _isHighContrastEnabled;
    private HighContrastTheme _currentTheme = HighContrastTheme.None;
    private bool _initialized;

    /// <summary>
    /// Gets whether high contrast mode is enabled.
    /// </summary>
    public bool IsHighContrastEnabled => _isHighContrastEnabled;

    /// <summary>
    /// Gets the current high contrast theme.
    /// </summary>
    public HighContrastTheme CurrentTheme => _currentTheme;

    /// <summary>
    /// Event raised when high contrast mode changes.
    /// </summary>
    public event EventHandler<HighContrastChangedEventArgs>? HighContrastChanged;

    /// <summary>
    /// Initializes the high contrast service.
    /// </summary>
    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        DetectHighContrast();
    }

    /// <summary>
    /// Detects current high contrast mode settings.
    /// </summary>
    public void DetectHighContrast()
    {
        bool isEnabled = false;
        var theme = HighContrastTheme.None;

        // Try GNOME settings
        if (TryGetGnomeHighContrast(out bool gnomeEnabled, out string? gnomeTheme))
        {
            isEnabled = gnomeEnabled;
            if (gnomeEnabled)
            {
                theme = ParseThemeName(gnomeTheme);
            }
        }
        // Try KDE settings
        else if (TryGetKdeHighContrast(out bool kdeEnabled, out string? kdeTheme))
        {
            isEnabled = kdeEnabled;
            if (kdeEnabled)
            {
                theme = ParseThemeName(kdeTheme);
            }
        }
        // Try GTK settings
        else if (TryGetGtkHighContrast(out bool gtkEnabled, out string? gtkTheme))
        {
            isEnabled = gtkEnabled;
            if (gtkEnabled)
            {
                theme = ParseThemeName(gtkTheme);
            }
        }
        // Check environment variables
        else if (TryGetEnvironmentHighContrast(out bool envEnabled))
        {
            isEnabled = envEnabled;
            theme = HighContrastTheme.WhiteOnBlack; // Default
        }

        UpdateHighContrast(isEnabled, theme);
    }

    private void UpdateHighContrast(bool isEnabled, HighContrastTheme theme)
    {
        if (_isHighContrastEnabled != isEnabled || _currentTheme != theme)
        {
            _isHighContrastEnabled = isEnabled;
            _currentTheme = theme;
            HighContrastChanged?.Invoke(this, new HighContrastChangedEventArgs(isEnabled, theme));
        }
    }

    private static bool TryGetGnomeHighContrast(out bool isEnabled, out string? themeName)
    {
        isEnabled = false;
        themeName = null;

        try
        {
            // Check if high contrast is enabled via gsettings
            var result = RunCommand("gsettings", "get org.gnome.desktop.a11y.interface high-contrast");
            if (!string.IsNullOrEmpty(result))
            {
                isEnabled = result.Trim().ToLower() == "true";
            }

            // Get the current GTK theme
            result = RunCommand("gsettings", "get org.gnome.desktop.interface gtk-theme");
            if (!string.IsNullOrEmpty(result))
            {
                themeName = result.Trim().Trim('\'');

                // Check if theme name indicates high contrast
                if (!isEnabled && themeName != null)
                {
                    var lowerTheme = themeName.ToLower();
                    isEnabled = lowerTheme.Contains("highcontrast") ||
                                lowerTheme.Contains("high-contrast") ||
                                lowerTheme.Contains("hc");
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetKdeHighContrast(out bool isEnabled, out string? themeName)
    {
        isEnabled = false;
        themeName = null;

        try
        {
            // Check kdeglobals for color scheme
            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config", "kdeglobals");

            if (!File.Exists(configPath)) return false;

            var lines = File.ReadAllLines(configPath);
            foreach (var line in lines)
            {
                if (line.StartsWith("ColorScheme="))
                {
                    themeName = line.Substring("ColorScheme=".Length);
                    var lowerTheme = themeName.ToLower();
                    isEnabled = lowerTheme.Contains("highcontrast") ||
                                lowerTheme.Contains("high-contrast") ||
                                lowerTheme.Contains("breeze-high-contrast");
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetGtkHighContrast(out bool isEnabled, out string? themeName)
    {
        isEnabled = false;
        themeName = null;

        try
        {
            // Check GTK settings.ini
            var gtkConfigPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config", "gtk-3.0", "settings.ini");

            if (!File.Exists(gtkConfigPath))
            {
                gtkConfigPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".config", "gtk-4.0", "settings.ini");
            }

            if (!File.Exists(gtkConfigPath)) return false;

            var lines = File.ReadAllLines(gtkConfigPath);
            foreach (var line in lines)
            {
                if (line.StartsWith("gtk-theme-name="))
                {
                    themeName = line.Substring("gtk-theme-name=".Length);
                    var lowerTheme = themeName.ToLower();
                    isEnabled = lowerTheme.Contains("highcontrast") ||
                                lowerTheme.Contains("high-contrast");
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetEnvironmentHighContrast(out bool isEnabled)
    {
        isEnabled = false;

        // Check GTK_THEME environment variable
        var gtkTheme = Environment.GetEnvironmentVariable("GTK_THEME");
        if (!string.IsNullOrEmpty(gtkTheme))
        {
            var lower = gtkTheme.ToLower();
            isEnabled = lower.Contains("highcontrast") || lower.Contains("high-contrast");
            if (isEnabled) return true;
        }

        // Check accessibility-related env vars
        var forceA11y = Environment.GetEnvironmentVariable("GTK_A11Y");
        if (forceA11y?.ToLower() == "atspi" || forceA11y == "1")
        {
            // A11y is forced, but doesn't necessarily mean high contrast
        }

        return isEnabled;
    }

    private static HighContrastTheme ParseThemeName(string? themeName)
    {
        if (string.IsNullOrEmpty(themeName))
            return HighContrastTheme.WhiteOnBlack;

        var lower = themeName.ToLower();

        if (lower.Contains("inverse") || lower.Contains("dark") || lower.Contains("white-on-black"))
            return HighContrastTheme.WhiteOnBlack;

        if (lower.Contains("light") || lower.Contains("black-on-white"))
            return HighContrastTheme.BlackOnWhite;

        // Default to white on black (more common high contrast choice)
        return HighContrastTheme.WhiteOnBlack;
    }

    /// <summary>
    /// Gets the appropriate colors for the current high contrast theme.
    /// </summary>
    public HighContrastColors GetColors()
    {
        return _currentTheme switch
        {
            HighContrastTheme.WhiteOnBlack => new HighContrastColors
            {
                Background = SKColors.Black,
                Foreground = SKColors.White,
                Accent = new SKColor(0, 255, 255), // Cyan
                Border = SKColors.White,
                Error = new SKColor(255, 100, 100),
                Success = new SKColor(100, 255, 100),
                Warning = SKColors.Yellow,
                Link = new SKColor(100, 200, 255),
                LinkVisited = new SKColor(200, 150, 255),
                Selection = new SKColor(0, 120, 215),
                SelectionText = SKColors.White,
                DisabledText = new SKColor(160, 160, 160),
                DisabledBackground = new SKColor(40, 40, 40)
            },
            HighContrastTheme.BlackOnWhite => new HighContrastColors
            {
                Background = SKColors.White,
                Foreground = SKColors.Black,
                Accent = new SKColor(0, 0, 200), // Dark blue
                Border = SKColors.Black,
                Error = new SKColor(180, 0, 0),
                Success = new SKColor(0, 130, 0),
                Warning = new SKColor(180, 120, 0),
                Link = new SKColor(0, 0, 180),
                LinkVisited = new SKColor(80, 0, 120),
                Selection = new SKColor(0, 120, 215),
                SelectionText = SKColors.White,
                DisabledText = new SKColor(100, 100, 100),
                DisabledBackground = new SKColor(220, 220, 220)
            },
            _ => GetDefaultColors()
        };
    }

    private static HighContrastColors GetDefaultColors()
    {
        return new HighContrastColors
        {
            Background = SKColors.White,
            Foreground = new SKColor(33, 33, 33),
            Accent = new SKColor(33, 150, 243),
            Border = new SKColor(200, 200, 200),
            Error = new SKColor(244, 67, 54),
            Success = new SKColor(76, 175, 80),
            Warning = new SKColor(255, 152, 0),
            Link = new SKColor(33, 150, 243),
            LinkVisited = new SKColor(156, 39, 176),
            Selection = new SKColor(33, 150, 243),
            SelectionText = SKColors.White,
            DisabledText = new SKColor(158, 158, 158),
            DisabledBackground = new SKColor(238, 238, 238)
        };
    }

    /// <summary>
    /// Forces a specific high contrast mode (for testing or user preference override).
    /// </summary>
    public void ForceHighContrast(bool enabled, HighContrastTheme theme = HighContrastTheme.WhiteOnBlack)
    {
        UpdateHighContrast(enabled, theme);
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
}

/// <summary>
/// High contrast theme types.
/// </summary>
public enum HighContrastTheme
{
    None,
    WhiteOnBlack,
    BlackOnWhite
}

/// <summary>
/// Color palette for high contrast mode.
/// </summary>
public class HighContrastColors
{
    public SKColor Background { get; set; }
    public SKColor Foreground { get; set; }
    public SKColor Accent { get; set; }
    public SKColor Border { get; set; }
    public SKColor Error { get; set; }
    public SKColor Success { get; set; }
    public SKColor Warning { get; set; }
    public SKColor Link { get; set; }
    public SKColor LinkVisited { get; set; }
    public SKColor Selection { get; set; }
    public SKColor SelectionText { get; set; }
    public SKColor DisabledText { get; set; }
    public SKColor DisabledBackground { get; set; }
}

/// <summary>
/// Event args for high contrast mode changes.
/// </summary>
public class HighContrastChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets whether high contrast mode is enabled.
    /// </summary>
    public bool IsEnabled { get; }

    /// <summary>
    /// Gets the current theme.
    /// </summary>
    public HighContrastTheme Theme { get; }

    public HighContrastChangedEventArgs(bool isEnabled, HighContrastTheme theme)
    {
        IsEnabled = isEnabled;
        Theme = theme;
    }
}
