// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.Maui.Storage;
using MauiAppInfo = Microsoft.Maui.ApplicationModel.AppInfo;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux preferences implementation using JSON file storage.
/// Follows XDG Base Directory Specification.
/// </summary>
public class PreferencesService : IPreferences
{
    private readonly string _preferencesPath;
    private readonly object _lock = new();
    private Dictionary<string, Dictionary<string, object?>> _preferences = new();
    private bool _loaded;

    public PreferencesService()
    {
        // Use XDG config directory
        var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (string.IsNullOrEmpty(configHome))
        {
            configHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        }

        var appName = MauiAppInfo.Current?.Name ?? "MauiApp";
        var appDir = Path.Combine(configHome, appName);
        Directory.CreateDirectory(appDir);

        _preferencesPath = Path.Combine(appDir, "preferences.json");
    }

    private void EnsureLoaded()
    {
        if (_loaded) return;

        lock (_lock)
        {
            if (_loaded) return;

            try
            {
                if (File.Exists(_preferencesPath))
                {
                    var json = File.ReadAllText(_preferencesPath);
                    _preferences = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object?>>>(json)
                                   ?? new();
                }
            }
            catch
            {
                _preferences = new();
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
                var json = JsonSerializer.Serialize(_preferences, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(_preferencesPath, json);
            }
            catch
            {
                // Silently fail save operations
            }
        }
    }

    private Dictionary<string, object?> GetContainer(string? sharedName)
    {
        var key = sharedName ?? "__default__";

        EnsureLoaded();

        if (!_preferences.TryGetValue(key, out var container))
        {
            container = new Dictionary<string, object?>();
            _preferences[key] = container;
        }

        return container;
    }

    public bool ContainsKey(string key, string? sharedName = null)
    {
        var container = GetContainer(sharedName);
        return container.ContainsKey(key);
    }

    public void Remove(string key, string? sharedName = null)
    {
        lock (_lock)
        {
            var container = GetContainer(sharedName);
            if (container.Remove(key))
            {
                Save();
            }
        }
    }

    public void Clear(string? sharedName = null)
    {
        lock (_lock)
        {
            var container = GetContainer(sharedName);
            container.Clear();
            Save();
        }
    }

    public void Set<T>(string key, T value, string? sharedName = null)
    {
        lock (_lock)
        {
            var container = GetContainer(sharedName);
            container[key] = value;
            Save();
        }
    }

    public T Get<T>(string key, T defaultValue, string? sharedName = null)
    {
        var container = GetContainer(sharedName);

        if (!container.TryGetValue(key, out var value))
            return defaultValue;

        if (value == null)
            return defaultValue;

        try
        {
            // Handle JsonElement conversion (from deserialization)
            if (value is JsonElement element)
            {
                return ConvertJsonElement<T>(element, defaultValue);
            }

            // Direct conversion
            if (value is T typedValue)
                return typedValue;

            // Try Convert.ChangeType for primitive types
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    private T ConvertJsonElement<T>(JsonElement element, T defaultValue)
    {
        var targetType = typeof(T);

        try
        {
            if (targetType == typeof(string))
                return (T)(object)element.GetString()!;

            if (targetType == typeof(int))
                return (T)(object)element.GetInt32();

            if (targetType == typeof(long))
                return (T)(object)element.GetInt64();

            if (targetType == typeof(float))
                return (T)(object)element.GetSingle();

            if (targetType == typeof(double))
                return (T)(object)element.GetDouble();

            if (targetType == typeof(bool))
                return (T)(object)element.GetBoolean();

            if (targetType == typeof(DateTime))
                return (T)(object)element.GetDateTime();

            // For complex types, deserialize
            return element.Deserialize<T>() ?? defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }
}
