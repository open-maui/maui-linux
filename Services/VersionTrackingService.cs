// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Text.Json;
using Microsoft.Maui.ApplicationModel;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux version tracking implementation.
/// </summary>
public class VersionTrackingService : IVersionTracking
{
    private const string VersionTrackingFile = ".maui-version-tracking.json";
    private readonly string _trackingFilePath;
    private VersionTrackingData _data;
    private bool _isInitialized;

    public VersionTrackingService()
    {
        _trackingFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            VersionTrackingFile);
        _data = new VersionTrackingData();
    }

    private void EnsureInitialized()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        LoadTrackingData();
        UpdateTrackingData();
    }

    private void LoadTrackingData()
    {
        try
        {
            if (File.Exists(_trackingFilePath))
            {
                var json = File.ReadAllText(_trackingFilePath);
                _data = JsonSerializer.Deserialize<VersionTrackingData>(json) ?? new VersionTrackingData();
            }
        }
        catch
        {
            _data = new VersionTrackingData();
        }
    }

    private void UpdateTrackingData()
    {
        var currentVersion = CurrentVersion;
        var currentBuild = CurrentBuild;

        // Check if this is a new version
        if (_data.PreviousVersion != currentVersion || _data.PreviousBuild != currentBuild)
        {
            // Store previous version info
            if (!string.IsNullOrEmpty(_data.CurrentVersion))
            {
                _data.PreviousVersion = _data.CurrentVersion;
                _data.PreviousBuild = _data.CurrentBuild;
            }

            _data.CurrentVersion = currentVersion;
            _data.CurrentBuild = currentBuild;

            // Add to version history
            if (!_data.VersionHistory.Contains(currentVersion))
            {
                _data.VersionHistory.Add(currentVersion);
            }

            // Add to build history
            if (!_data.BuildHistory.Contains(currentBuild))
            {
                _data.BuildHistory.Add(currentBuild);
            }
        }

        // Track first launch
        if (_data.FirstInstalledVersion == null)
        {
            _data.FirstInstalledVersion = currentVersion;
            _data.FirstInstalledBuild = currentBuild;
            _data.IsFirstLaunchEver = true;
        }
        else
        {
            _data.IsFirstLaunchEver = false;
        }

        // Check if first launch for current version
        _data.IsFirstLaunchForCurrentVersion = _data.PreviousVersion != currentVersion;
        _data.IsFirstLaunchForCurrentBuild = _data.PreviousBuild != currentBuild;

        SaveTrackingData();
    }

    private void SaveTrackingData()
    {
        try
        {
            var directory = Path.GetDirectoryName(_trackingFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_trackingFilePath, json);
        }
        catch
        {
            // Silently fail if we can't save
        }
    }

    public bool IsFirstLaunchEver
    {
        get
        {
            EnsureInitialized();
            return _data.IsFirstLaunchEver;
        }
    }

    public bool IsFirstLaunchForCurrentVersion
    {
        get
        {
            EnsureInitialized();
            return _data.IsFirstLaunchForCurrentVersion;
        }
    }

    public bool IsFirstLaunchForCurrentBuild
    {
        get
        {
            EnsureInitialized();
            return _data.IsFirstLaunchForCurrentBuild;
        }
    }

    public string CurrentVersion => GetAssemblyVersion();
    public string CurrentBuild => GetAssemblyBuild();

    public string? PreviousVersion
    {
        get
        {
            EnsureInitialized();
            return _data.PreviousVersion;
        }
    }

    public string? PreviousBuild
    {
        get
        {
            EnsureInitialized();
            return _data.PreviousBuild;
        }
    }

    public string? FirstInstalledVersion
    {
        get
        {
            EnsureInitialized();
            return _data.FirstInstalledVersion;
        }
    }

    public string? FirstInstalledBuild
    {
        get
        {
            EnsureInitialized();
            return _data.FirstInstalledBuild;
        }
    }

    public IReadOnlyList<string> VersionHistory
    {
        get
        {
            EnsureInitialized();
            return _data.VersionHistory.AsReadOnly();
        }
    }

    public IReadOnlyList<string> BuildHistory
    {
        get
        {
            EnsureInitialized();
            return _data.BuildHistory.AsReadOnly();
        }
    }

    public bool IsFirstLaunchForVersion(string version)
    {
        EnsureInitialized();
        return !_data.VersionHistory.Contains(version);
    }

    public bool IsFirstLaunchForBuild(string build)
    {
        EnsureInitialized();
        return !_data.BuildHistory.Contains(build);
    }

    public void Track()
    {
        EnsureInitialized();
    }

    private static string GetAssemblyVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
    }

    private static string GetAssemblyBuild()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version?.Revision.ToString() ?? "0";
    }

    private class VersionTrackingData
    {
        public string? CurrentVersion { get; set; }
        public string? CurrentBuild { get; set; }
        public string? PreviousVersion { get; set; }
        public string? PreviousBuild { get; set; }
        public string? FirstInstalledVersion { get; set; }
        public string? FirstInstalledBuild { get; set; }
        public List<string> VersionHistory { get; set; } = new();
        public List<string> BuildHistory { get; set; } = new();
        public bool IsFirstLaunchEver { get; set; }
        public bool IsFirstLaunchForCurrentVersion { get; set; }
        public bool IsFirstLaunchForCurrentBuild { get; set; }
    }
}
