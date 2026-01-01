using System;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Maui.ApplicationModel;

namespace Microsoft.Maui.Platform.Linux.Services;

public class AppInfoService : IAppInfo
{
    private static readonly Lazy<AppInfoService> _instance = new Lazy<AppInfoService>(() => new AppInfoService());

    private readonly Assembly _entryAssembly;

    private readonly string _packageName;

    private readonly string _name;

    private readonly string _versionString;

    private readonly Version _version;

    private readonly string _buildString;

    public static AppInfoService Instance => _instance.Value;

    public string PackageName => _packageName;

    public string Name => _name;

    public string VersionString => _versionString;

    public Version Version => _version;

    public string BuildString => _buildString;

    public LayoutDirection RequestedLayoutDirection => LayoutDirection.LeftToRight;

    public AppTheme RequestedTheme
    {
        get
        {
            try
            {
                var environmentVariable = Environment.GetEnvironmentVariable("GTK_THEME");
                if (!string.IsNullOrEmpty(environmentVariable) && environmentVariable.Contains("dark", StringComparison.OrdinalIgnoreCase))
                {
                    return AppTheme.Dark;
                }
                if (GetGnomeColorScheme().Contains("dark", StringComparison.OrdinalIgnoreCase))
                {
                    return AppTheme.Dark;
                }
                return AppTheme.Light;
            }
            catch
            {
                return AppTheme.Light;
            }
        }
    }

    public AppPackagingModel PackagingModel
    {
        get
        {
            if (Environment.GetEnvironmentVariable("FLATPAK_ID") != null)
            {
                return AppPackagingModel.Packaged;
            }
            if (Environment.GetEnvironmentVariable("SNAP") != null)
            {
                return AppPackagingModel.Packaged;
            }
            if (Environment.GetEnvironmentVariable("APPIMAGE") != null)
            {
                return AppPackagingModel.Packaged;
            }
            return AppPackagingModel.Unpackaged;
        }
    }

    public AppInfoService()
    {
        _entryAssembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        _packageName = _entryAssembly.GetName().Name ?? "Unknown";
        _name = _entryAssembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? _packageName;
        _versionString = (_version = _entryAssembly.GetName().Version ?? new Version(1, 0)).ToString();
        _buildString = _entryAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? _versionString;
    }

    private string GetGnomeColorScheme()
    {
        try
        {
            using Process? process = Process.Start(new ProcessStartInfo
            {
                FileName = "gsettings",
                Arguments = "get org.gnome.desktop.interface color-scheme",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            if (process != null)
            {
                string text = process.StandardOutput.ReadToEnd();
                process.WaitForExit(1000);
                return text.Trim().Trim('\'');
            }
        }
        catch
        {
        }
        return "";
    }

    public void ShowSettingsUI()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "gnome-control-center",
                UseShellExecute = true
            });
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = "x-settings:",
                    UseShellExecute = true
                });
            }
            catch
            {
            }
        }
    }
}
