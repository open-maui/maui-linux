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
        : this(Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly())
    {
    }

    /// <summary>
    /// Reads the app's identity from <paramref name="entryAssembly"/>: the AppInfo metadata
    /// OpenMaui's build targets write from ApplicationId, ApplicationTitle,
    /// ApplicationDisplayVersion and ApplicationVersion (the MAUI single-project properties,
    /// under the keys MAUI's Windows build uses), else the assembly's name, title and version.
    /// </summary>
    internal AppInfoService(Assembly entryAssembly)
    {
        _entryAssembly = entryAssembly;
        var assemblyName = _entryAssembly.GetName().Name ?? "Unknown";
        var title = _entryAssembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title;
        StorageName = string.IsNullOrWhiteSpace(title) ? assemblyName : title;

        _packageName = GetMetadata(_entryAssembly, "PackageName") ?? assemblyName;
        _name = GetMetadata(_entryAssembly, "Name") ?? StorageName;

        var displayVersion = GetMetadata(_entryAssembly, "Version");
        if (displayVersion != null && TryParseVersion(displayVersion, out var parsed))
        {
            _version = parsed;
            _versionString = displayVersion;
        }
        else
        {
            _version = _entryAssembly.GetName().Version ?? new Version(1, 0);
            _versionString = _version.ToString();
        }

        _buildString = GetMetadata(_entryAssembly, "Build")
            ?? _entryAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? _versionString;
    }

    /// <summary>
    /// The directory name the app's preferences and data live under (the entry assembly's
    /// title, else its name). It is what <see cref="Name"/> was before ApplicationTitle was
    /// honoured, and stays so: changing it would move existing apps' data.
    /// </summary>
    internal string StorageName { get; }

    /// <summary>The value of the <c>Microsoft.Maui.ApplicationModel.AppInfo.&lt;key&gt;</c> assembly metadata, or null.</summary>
    internal static string? GetMetadata(Assembly assembly, string key)
    {
        var fullKey = "Microsoft.Maui.ApplicationModel.AppInfo." + key;
        foreach (var attribute in assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
        {
            if (attribute.Key == fullKey && !string.IsNullOrWhiteSpace(attribute.Value))
                return attribute.Value.Trim();
        }
        return null;
    }

    /// <summary>"1.0", "2.1.3", "1.0.0-beta.2" (the pre-release and build suffixes are not part of the Version).</summary>
    internal static bool TryParseVersion(string value, out Version version)
    {
        var core = value.Split('-', '+')[0].Trim();
        if (!core.Contains('.', StringComparison.Ordinal) && int.TryParse(core, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var major))
        {
            version = new Version(major, 0);
            return true;
        }
        return Version.TryParse(core, out version!);
    }

    /// <summary>The storage directory name of the current AppInfo (see <see cref="StorageName"/>), or null.</summary>
    internal static string? CurrentStorageName()
    {
        try
        {
            var current = Microsoft.Maui.ApplicationModel.AppInfo.Current;
            return current is AppInfoService linux ? linux.StorageName : current?.Name;
        }
        catch
        {
            // The portable AppInfo stub throws until EssentialsPatches has run.
            return null;
        }
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
