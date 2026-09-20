// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.Maui.Storage;
using MauiAppInfo = Microsoft.Maui.ApplicationModel.AppInfo;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux implementation of <see cref="IFileSystem"/> following the XDG Base
/// Directory specification: <see cref="AppDataDirectory"/> is
/// <c>$XDG_DATA_HOME/&lt;app&gt;</c> (default <c>~/.local/share/&lt;app&gt;</c>)
/// and <see cref="CacheDirectory"/> is <c>$XDG_CACHE_HOME/&lt;app&gt;</c>
/// (default <c>~/.cache/&lt;app&gt;</c>). App package files resolve relative to
/// <see cref="AppContext.BaseDirectory"/> (the published output), then the
/// <c>Resources/Raw</c> convention used by <c>MauiAsset</c>, then embedded
/// resources of the entry assembly.
/// </summary>
public class FileSystemService : IFileSystem
{
    private readonly string? _appNameOverride;
    private readonly string? _dataRootOverride;
    private readonly string? _cacheRootOverride;
    private readonly string _packageRoot;
    private readonly Assembly? _packageAssembly;
    private string? _appDataDirectory;
    private string? _cacheDirectory;

    /// <summary>
    /// Creates the service using the XDG environment, the running app's name
    /// and <see cref="AppContext.BaseDirectory"/> as the package root.
    /// </summary>
    public FileSystemService()
        : this(null, null, null, null, null)
    {
    }

    /// <summary>
    /// Creates a service with explicit roots. Any null argument falls back to
    /// the production default; used by tests and embedded hosts.
    /// </summary>
    /// <param name="appName">Directory name under the XDG roots.</param>
    /// <param name="dataRoot">Replaces <c>$XDG_DATA_HOME</c>.</param>
    /// <param name="cacheRoot">Replaces <c>$XDG_CACHE_HOME</c>.</param>
    /// <param name="packageRoot">Replaces <see cref="AppContext.BaseDirectory"/>.</param>
    /// <param name="packageAssembly">Assembly searched for embedded package files; defaults to the entry assembly.</param>
    internal FileSystemService(string? appName, string? dataRoot, string? cacheRoot, string? packageRoot, Assembly? packageAssembly)
    {
        _appNameOverride = appName;
        _dataRootOverride = dataRoot;
        _cacheRootOverride = cacheRoot;
        _packageRoot = packageRoot ?? AppContext.BaseDirectory;
        _packageAssembly = packageAssembly;
    }

    /// <inheritdoc />
    public string AppDataDirectory
    {
        get
        {
            if (_appDataDirectory != null) return _appDataDirectory;

            var root = _dataRootOverride ?? Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (string.IsNullOrEmpty(root))
                root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");

            var dir = Path.Combine(root, AppName);
            TryCreateDirectory(dir);
            return _appDataDirectory = dir;
        }
    }

    /// <inheritdoc />
    public string CacheDirectory
    {
        get
        {
            if (_cacheDirectory != null) return _cacheDirectory;

            var root = _cacheRootOverride ?? Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
            if (string.IsNullOrEmpty(root))
                root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache");

            var dir = Path.Combine(root, AppName);
            TryCreateDirectory(dir);
            return _cacheDirectory = dir;
        }
    }

    /// <inheritdoc />
    public Task<bool> AppPackageFileExistsAsync(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return Task.FromResult(false);

        return Task.FromResult(ResolvePackageFile(filename) != null || FindEmbeddedResource(filename) != null);
    }

    /// <inheritdoc />
    public Task<Stream> OpenAppPackageFileAsync(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            throw new ArgumentNullException(nameof(filename));

        var path = ResolvePackageFile(filename);
        if (path != null)
        {
            Stream file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
            return Task.FromResult(file);
        }

        var resource = FindEmbeddedResource(filename);
        if (resource != null)
        {
            var stream = resource.Value.Assembly.GetManifestResourceStream(resource.Value.Name);
            if (stream != null)
                return Task.FromResult(stream);
        }

        throw new FileNotFoundException($"App package file '{filename}' was not found under '{_packageRoot}'.", filename);
    }

    /// <summary>
    /// Directory name used under the XDG roots: <c>AppInfo.Current.Name</c>
    /// when the Essentials facade is wired up, otherwise the entry assembly
    /// name, otherwise <c>"MauiApp"</c>.
    /// </summary>
    internal string AppName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_appNameOverride))
                return _appNameOverride;

            string? name = null;
            try { name = MauiAppInfo.Current?.Name; }
            catch { /* portable AppInfo stub throws until EssentialsPatches runs */ }

            if (string.IsNullOrWhiteSpace(name))
            {
                try { name = Assembly.GetEntryAssembly()?.GetName().Name; }
                catch { }
            }

            return string.IsNullOrWhiteSpace(name) ? "MauiApp" : SanitizeDirectoryName(name);
        }
    }

    /// <summary>
    /// Resolves <paramref name="filename"/> against the package root and the
    /// <c>Resources/Raw</c> asset folder. Absolute paths are honoured as-is.
    /// Rejects paths that escape the package root.
    /// </summary>
    internal string? ResolvePackageFile(string filename)
    {
        if (Path.IsPathRooted(filename))
            return File.Exists(filename) ? filename : null;

        var normalized = filename.Replace('\\', '/');
        var rootFull = Path.GetFullPath(_packageRoot);

        foreach (var candidate in new[]
                 {
                     Path.Combine(_packageRoot, normalized),
                     Path.Combine(_packageRoot, "Resources", "Raw", normalized),
                     Path.Combine(_packageRoot, "Resources", "Raw", Path.GetFileName(normalized)),
                     Path.Combine(_packageRoot, Path.GetFileName(normalized)),
                 })
        {
            var full = Path.GetFullPath(candidate);
            if (!full.StartsWith(rootFull, StringComparison.Ordinal))
                continue;
            if (File.Exists(full))
                return full;
        }
        return null;
    }

    private (Assembly Assembly, string Name)? FindEmbeddedResource(string filename)
    {
        var assembly = _packageAssembly;
        if (assembly == null)
        {
            try { assembly = Assembly.GetEntryAssembly(); } catch { }
        }
        if (assembly == null) return null;

        var baseName = Path.GetFileName(filename);
        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (name.Equals(filename, StringComparison.OrdinalIgnoreCase)
                || name.EndsWith("." + baseName, StringComparison.OrdinalIgnoreCase)
                || name.EndsWith("/" + baseName, StringComparison.OrdinalIgnoreCase)
                || name.Equals(baseName, StringComparison.OrdinalIgnoreCase))
            {
                return (assembly, name);
            }
        }
        return null;
    }

    private static string SanitizeDirectoryName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }

    private static void TryCreateDirectory(string dir)
    {
        try { Directory.CreateDirectory(dir); }
        catch (Exception ex) { DiagnosticLog.Warn("FileSystemService", $"Could not create '{dir}': {ex.Message}"); }
    }
}
