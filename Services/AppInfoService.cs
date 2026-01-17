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
            // Use SystemThemeService for consistent theme detection across the platform
            return SystemThemeService.Instance.CurrentTheme switch
            {
                SystemTheme.Dark => AppTheme.Dark,
                SystemTheme.Light => AppTheme.Light,
                _ => AppTheme.Unspecified
            };
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
