using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Microsoft.Maui.ApplicationModel;

namespace Microsoft.Maui.Platform.Linux.Services;

public class VersionTrackingService : IVersionTracking
{
	private class VersionTrackingData
	{
		public string? CurrentVersion { get; set; }

		public string? CurrentBuild { get; set; }

		public string? PreviousVersion { get; set; }

		public string? PreviousBuild { get; set; }

		public string? FirstInstalledVersion { get; set; }

		public string? FirstInstalledBuild { get; set; }

		public List<string> VersionHistory { get; set; } = new List<string>();

		public List<string> BuildHistory { get; set; } = new List<string>();

		public bool IsFirstLaunchEver { get; set; }

		public bool IsFirstLaunchForCurrentVersion { get; set; }

		public bool IsFirstLaunchForCurrentBuild { get; set; }
	}

	private const string VersionTrackingFile = ".maui-version-tracking.json";

	private readonly string _trackingFilePath;

	private VersionTrackingData _data;

	private bool _isInitialized;

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

	public VersionTrackingService()
	{
		_trackingFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ".maui-version-tracking.json");
		_data = new VersionTrackingData();
	}

	private void EnsureInitialized()
	{
		if (!_isInitialized)
		{
			_isInitialized = true;
			LoadTrackingData();
			UpdateTrackingData();
		}
	}

	private void LoadTrackingData()
	{
		try
		{
			if (File.Exists(_trackingFilePath))
			{
				string json = File.ReadAllText(_trackingFilePath);
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
		string currentVersion = CurrentVersion;
		string currentBuild = CurrentBuild;
		if (_data.PreviousVersion != currentVersion || _data.PreviousBuild != currentBuild)
		{
			if (!string.IsNullOrEmpty(_data.CurrentVersion))
			{
				_data.PreviousVersion = _data.CurrentVersion;
				_data.PreviousBuild = _data.CurrentBuild;
			}
			_data.CurrentVersion = currentVersion;
			_data.CurrentBuild = currentBuild;
			if (!_data.VersionHistory.Contains(currentVersion))
			{
				_data.VersionHistory.Add(currentVersion);
			}
			if (!_data.BuildHistory.Contains(currentBuild))
			{
				_data.BuildHistory.Add(currentBuild);
			}
		}
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
		_data.IsFirstLaunchForCurrentVersion = _data.PreviousVersion != currentVersion;
		_data.IsFirstLaunchForCurrentBuild = _data.PreviousBuild != currentBuild;
		SaveTrackingData();
	}

	private void SaveTrackingData()
	{
		try
		{
			string directoryName = Path.GetDirectoryName(_trackingFilePath);
			if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			string contents = JsonSerializer.Serialize(_data, new JsonSerializerOptions
			{
				WriteIndented = true
			});
			File.WriteAllText(_trackingFilePath, contents);
		}
		catch
		{
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
		Version version = (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetName().Version;
		if (!(version != null))
		{
			return "1.0.0";
		}
		return $"{version.Major}.{version.Minor}.{version.Build}";
	}

	private static string GetAssemblyBuild()
	{
		return (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetName().Version?.Revision.ToString() ?? "0";
	}
}
