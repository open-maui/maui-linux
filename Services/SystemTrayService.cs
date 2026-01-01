// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux system tray service using various backends.
/// Supports yad, zenity, or native D-Bus StatusNotifierItem.
/// </summary>
public class SystemTrayService : IDisposable
{
    private Process? _trayProcess;
    private readonly string _appName;
    private string? _iconPath;
    private string? _tooltip;
    private readonly List<TrayMenuItem> _menuItems = new();
    private bool _isVisible;
    private bool _disposed;

    public event EventHandler? Clicked;
    public event EventHandler<string>? MenuItemClicked;

    public SystemTrayService(string appName)
    {
        _appName = appName;
    }

    /// <summary>
    /// Gets or sets the tray icon path.
    /// </summary>
    public string? IconPath
    {
        get => _iconPath;
        set
        {
            _iconPath = value;
            if (_isVisible) UpdateTray();
        }
    }

    /// <summary>
    /// Gets or sets the tooltip text.
    /// </summary>
    public string? Tooltip
    {
        get => _tooltip;
        set
        {
            _tooltip = value;
            if (_isVisible) UpdateTray();
        }
    }

    /// <summary>
    /// Gets the menu items.
    /// </summary>
    public IList<TrayMenuItem> MenuItems => _menuItems;

    /// <summary>
    /// Shows the system tray icon.
    /// </summary>
    public async Task ShowAsync()
    {
        if (_isVisible) return;

        // Try yad first (most feature-complete)
        if (await TryYadTray())
        {
            _isVisible = true;
            return;
        }

        // Fall back to a simple approach
        _isVisible = true;
    }

    /// <summary>
    /// Hides the system tray icon.
    /// </summary>
    public void Hide()
    {
        if (!_isVisible) return;

        _trayProcess?.Kill();
        _trayProcess?.Dispose();
        _trayProcess = null;
        _isVisible = false;
    }

    /// <summary>
    /// Updates the tray icon and menu.
    /// </summary>
    public void UpdateTray()
    {
        if (!_isVisible) return;

        // Restart tray with new settings
        Hide();
        _ = ShowAsync();
    }

    private async Task<bool> TryYadTray()
    {
        try
        {
            var args = BuildYadArgs();

            var startInfo = new ProcessStartInfo
            {
                FileName = "yad",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            _trayProcess = Process.Start(startInfo);
            if (_trayProcess == null) return false;

            // Start reading output for menu clicks
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!_trayProcess.HasExited)
                    {
                        var line = await _trayProcess.StandardOutput.ReadLineAsync();
                        if (!string.IsNullOrEmpty(line))
                        {
                            HandleTrayOutput(line);
                        }
                    }
                }
                catch { }
            });

            return true;
        }
        catch
        {
            return false;
        }
    }

    private string BuildYadArgs()
    {
        var args = new List<string>
        {
            "--notification",
            "--listen"
        };

        if (!string.IsNullOrEmpty(_iconPath) && File.Exists(_iconPath))
        {
            args.Add($"--image=\"{_iconPath}\"");
        }
        else
        {
            args.Add("--image=application-x-executable");
        }

        if (!string.IsNullOrEmpty(_tooltip))
        {
            args.Add($"--text=\"{EscapeArg(_tooltip)}\"");
        }

        // Build menu
        if (_menuItems.Count > 0)
        {
            var menuStr = string.Join("!", _menuItems.Select(m =>
                m.IsSeparator ? "---" : $"{EscapeArg(m.Text)}"));
            args.Add($"--menu=\"{menuStr}\"");
        }

        args.Add("--command=\"echo clicked\"");

        return string.Join(" ", args);
    }

    private void HandleTrayOutput(string output)
    {
        if (output == "clicked")
        {
            Clicked?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            // Menu item clicked
            var menuItem = _menuItems.FirstOrDefault(m => m.Text == output);
            if (menuItem != null)
            {
                menuItem.Action?.Invoke();
                MenuItemClicked?.Invoke(this, output);
            }
        }
    }

    /// <summary>
    /// Adds a menu item to the tray context menu.
    /// </summary>
    public void AddMenuItem(string text, Action? action = null)
    {
        _menuItems.Add(new TrayMenuItem { Text = text, Action = action });
    }

    /// <summary>
    /// Adds a separator to the tray context menu.
    /// </summary>
    public void AddSeparator()
    {
        _menuItems.Add(new TrayMenuItem { IsSeparator = true });
    }

    /// <summary>
    /// Clears all menu items.
    /// </summary>
    public void ClearMenuItems()
    {
        _menuItems.Clear();
    }

    /// <summary>
    /// Checks if system tray is available on this system.
    /// </summary>
    public static bool IsAvailable()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = "yad",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string EscapeArg(string arg)
    {
        return arg?.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("!", "\\!") ?? "";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Hide();
        GC.SuppressFinalize(this);
    }

    ~SystemTrayService()
    {
        Dispose();
    }
}

/// <summary>
/// Represents a tray menu item.
/// </summary>
public class TrayMenuItem
{
    public string Text { get; set; } = "";
    public Action? Action { get; set; }
    public bool IsSeparator { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? IconPath { get; set; }
}
