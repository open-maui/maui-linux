using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace Microsoft.Maui.Platform.Linux.Services;

public class PreferencesService : IPreferences
{
	private readonly string _preferencesPath;

	private readonly object _lock = new object();

	private Dictionary<string, Dictionary<string, object?>> _preferences = new Dictionary<string, Dictionary<string, object>>();

	private bool _loaded;

	public PreferencesService()
	{
		string text = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
		if (string.IsNullOrEmpty(text))
		{
			text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
		}
		IAppInfo current = AppInfo.Current;
		string path = ((current != null) ? current.Name : null) ?? "MauiApp";
		string text2 = Path.Combine(text, path);
		Directory.CreateDirectory(text2);
		_preferencesPath = Path.Combine(text2, "preferences.json");
	}

	private void EnsureLoaded()
	{
		if (_loaded)
		{
			return;
		}
		lock (_lock)
		{
			if (_loaded)
			{
				return;
			}
			try
			{
				if (File.Exists(_preferencesPath))
				{
					string json = File.ReadAllText(_preferencesPath);
					_preferences = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(json) ?? new Dictionary<string, Dictionary<string, object>>();
				}
			}
			catch
			{
				_preferences = new Dictionary<string, Dictionary<string, object>>();
			}
			_loaded = true;
		}
	}

	private void Save()
	{
		lock (_lock)
		{
			try
			{
				string contents = JsonSerializer.Serialize(_preferences, new JsonSerializerOptions
				{
					WriteIndented = true
				});
				File.WriteAllText(_preferencesPath, contents);
			}
			catch
			{
			}
		}
	}

	private Dictionary<string, object?> GetContainer(string? sharedName)
	{
		string key = sharedName ?? "__default__";
		EnsureLoaded();
		if (!_preferences.TryGetValue(key, out Dictionary<string, object> value))
		{
			value = new Dictionary<string, object>();
			_preferences[key] = value;
		}
		return value;
	}

	public bool ContainsKey(string key, string? sharedName = null)
	{
		return GetContainer(sharedName).ContainsKey(key);
	}

	public void Remove(string key, string? sharedName = null)
	{
		lock (_lock)
		{
			if (GetContainer(sharedName).Remove(key))
			{
				Save();
			}
		}
	}

	public void Clear(string? sharedName = null)
	{
		lock (_lock)
		{
			GetContainer(sharedName).Clear();
			Save();
		}
	}

	public void Set<T>(string key, T value, string? sharedName = null)
	{
		lock (_lock)
		{
			GetContainer(sharedName)[key] = value;
			Save();
		}
	}

	public T Get<T>(string key, T defaultValue, string? sharedName = null)
	{
		if (!GetContainer(sharedName).TryGetValue(key, out object value))
		{
			return defaultValue;
		}
		if (value == null)
		{
			return defaultValue;
		}
		try
		{
			if (value is JsonElement element)
			{
				return ConvertJsonElement(element, defaultValue);
			}
			if (value is T result)
			{
				return result;
			}
			return (T)Convert.ChangeType(value, typeof(T));
		}
		catch
		{
			return defaultValue;
		}
	}

	private T ConvertJsonElement<T>(JsonElement element, T defaultValue)
	{
		Type typeFromHandle = typeof(T);
		try
		{
			if (typeFromHandle == typeof(string))
			{
				return (T)(object)element.GetString();
			}
			if (typeFromHandle == typeof(int))
			{
				return (T)(object)element.GetInt32();
			}
			if (typeFromHandle == typeof(long))
			{
				return (T)(object)element.GetInt64();
			}
			if (typeFromHandle == typeof(float))
			{
				return (T)(object)element.GetSingle();
			}
			if (typeFromHandle == typeof(double))
			{
				return (T)(object)element.GetDouble();
			}
			if (typeFromHandle == typeof(bool))
			{
				return (T)(object)element.GetBoolean();
			}
			if (typeFromHandle == typeof(DateTime))
			{
				return (T)(object)element.GetDateTime();
			}
			T val = element.Deserialize<T>();
			return (T)((val != null) ? ((object)val) : ((object)defaultValue));
		}
		catch
		{
			return defaultValue;
		}
	}
}
