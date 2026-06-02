// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Net.Http;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Maps.Services;

/// <summary>
/// Raster-tile fetcher for an OpenStreetMap-style tile server. Caches each
/// tile to <c>$XDG_CACHE_HOME/openmaui/osm-tiles/{zoom}/{x}/{y}.png</c> so
/// repeated zoom/pan operations don't re-hit the network. In-memory layer on
/// top of the disk cache keeps the most recently decoded SKImages around so
/// the same tile in successive frames doesn't pay disk I/O either.
///
/// <para>OSM's <a href="https://operations.osmfoundation.org/policies/tiles/">tile usage policy</a>
/// requires:</para>
/// <list type="bullet">
///   <item>A descriptive User-Agent (we send "OpenMaui-Linux/{version}").</item>
///   <item>Caching on the client — done; one disk hit per (zoom, x, y).</item>
///   <item>Attribution displayed prominently — SkiaMap renders the credit overlay.</item>
/// </list>
///
/// Apps that produce significant load are expected to host their own tile
/// server (or use a commercial provider) and point <see cref="UrlTemplate"/> at
/// it. The template uses <c>{z}/{x}/{y}</c> placeholders, matching every
/// raster service that exists.
/// </summary>
public sealed class OsmTileService : IDisposable
{
    private static readonly Lazy<OsmTileService> s_default = new(() => new OsmTileService());
    public static OsmTileService Default => s_default.Value;

    public string UrlTemplate { get; set; } = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";

    /// <summary>
    /// Disk cache root. Defaults to <c>$XDG_CACHE_HOME/openmaui/osm-tiles</c>
    /// (falling back to <c>~/.cache/openmaui/osm-tiles</c>).
    /// </summary>
    public string CacheRoot { get; }

    /// <summary>Upper bound on the in-memory SKImage cache (tile count, ~50–200 tiles fits a typical viewport at multiple zooms).</summary>
    public int MemoryCacheLimit { get; set; } = 256;

    private readonly HttpClient _http;
    private readonly ConcurrentDictionary<(int Z, int X, int Y), SKImage> _memory = new();
    private readonly ConcurrentQueue<(int Z, int X, int Y)> _memoryOrder = new();
    private readonly ConcurrentDictionary<(int Z, int X, int Y), Task<SKImage?>> _inflight = new();

    public OsmTileService()
    {
        var xdg = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        if (string.IsNullOrEmpty(xdg))
            xdg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache");
        CacheRoot = Path.Combine(xdg, "openmaui", "osm-tiles");
        Directory.CreateDirectory(CacheRoot);

        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("OpenMaui-Linux/10.0 (+https://github.com/open-maui/maui-linux)");
        _http.Timeout = TimeSpan.FromSeconds(10);
    }

    /// <summary>
    /// Fetch tile (zoom, x, y). Returns the cached SKImage when present,
    /// otherwise reads from disk, otherwise fetches from the network and
    /// caches both. Returns null when offline and the tile isn't in any cache.
    /// </summary>
    public async Task<SKImage?> GetTileAsync(int zoom, int x, int y, CancellationToken ct = default)
    {
        var key = (zoom, x, y);

        if (_memory.TryGetValue(key, out var cached))
            return cached;

        // Coalesce duplicate concurrent requests for the same tile (very
        // common during a pan: the same key is requested from multiple draw
        // calls before the first fetch finishes).
        var task = _inflight.GetOrAdd(key, _ => FetchAsync(zoom, x, y, ct));
        var image = await task.ConfigureAwait(false);
        _inflight.TryRemove(key, out _);
        return image;
    }

    private async Task<SKImage?> FetchAsync(int zoom, int x, int y, CancellationToken ct)
    {
        var key = (zoom, x, y);
        var path = Path.Combine(CacheRoot, zoom.ToString(), x.ToString(), $"{y}.png");

        SKImage? image = null;
        if (File.Exists(path))
        {
            try
            {
                using var data = SKData.Create(path);
                image = SKImage.FromEncodedData(data);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Warn("OsmTileService", $"Cached tile decode failed ({path}): {ex.Message}");
                try { File.Delete(path); } catch { }
            }
        }

        if (image == null)
        {
            try
            {
                var url = UrlTemplate
                    .Replace("{z}", zoom.ToString())
                    .Replace("{x}", x.ToString())
                    .Replace("{y}", y.ToString());
                using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    DiagnosticLog.Warn("OsmTileService", $"Tile {zoom}/{x}/{y} HTTP {(int)response.StatusCode}");
                    return null;
                }
                var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);

                // Persist to disk before decoding so the next process run can reuse it.
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                try { File.WriteAllBytes(path, bytes); }
                catch (Exception ex)
                {
                    DiagnosticLog.Warn("OsmTileService", $"Could not write tile cache file {path}: {ex.Message}");
                }

                image = SKImage.FromEncodedData(SKData.CreateCopy(bytes));
            }
            catch (OperationCanceledException) { return null; }
            catch (Exception ex)
            {
                DiagnosticLog.Warn("OsmTileService", $"Tile {zoom}/{x}/{y} fetch failed: {ex.Message}");
                return null;
            }
        }

        if (image != null)
        {
            _memory[key] = image;
            _memoryOrder.Enqueue(key);
            EvictIfNecessary();
        }
        return image;
    }

    private void EvictIfNecessary()
    {
        while (_memory.Count > MemoryCacheLimit && _memoryOrder.TryDequeue(out var oldest))
        {
            if (_memory.TryRemove(oldest, out var img))
                img.Dispose();
        }
    }

    public void Dispose()
    {
        _http.Dispose();
        foreach (var img in _memory.Values) img.Dispose();
        _memory.Clear();
    }
}
