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

    /// <summary>Right-to-left for a right-to-left UI language (Arabic, Hebrew, ...), as on the other platforms.</summary>
    public LayoutDirection RequestedLayoutDirection =>
        System.Globalization.CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft ? LayoutDirection.RightToLeft : LayoutDirection.LeftToRight;

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
            if (IsRunningFromAppImage())
            {
                return AppPackagingModel.Packaged;
            }
            return AppPackagingModel.Unpackaged;
        }
    }

    /// <summary>
    /// True when this process runs from inside an AppImage. $APPIMAGE alone is
    /// not proof: the runtime exports it to everything the AppImage starts, so
    /// a terminal opened from an AppImage app passes it on to any app launched
    /// there. The process must also live under the AppImage's mount ($APPDIR).
    /// </summary>
    internal static bool IsRunningFromAppImage() => IsRunningFromAppImage(
        Environment.GetEnvironmentVariable("APPIMAGE"),
        Environment.GetEnvironmentVariable("APPDIR"),
        Environment.ProcessPath);

    internal static bool IsRunningFromAppImage(string? appImage, string? appDir, string? processPath)
    {
        if (string.IsNullOrEmpty(appImage) || string.IsNullOrEmpty(appDir) || string.IsNullOrEmpty(processPath))
            return false;
        var root = appDir.EndsWith('/') ? appDir : appDir + "/";
        return processPath.StartsWith(root, StringComparison.Ordinal);
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
        // gnome-control-center first; any other desktop gets the x-settings:
        // handler through xdg-open.
        if (ExternalProcess.TryStart(new ProcessStartInfo { FileName = "gnome-control-center", UseShellExecute = false }))
            return;

        if (!ExternalProcess.TryStart(new ProcessStartInfo { FileName = "xdg-open", ArgumentList = { "x-settings:" }, UseShellExecute = false }))
            DiagnosticLog.Debug("AppInfoService", "Settings launch fallback failed");
    }
}
