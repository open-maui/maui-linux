// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Maui.Platform.Linux.Dispatching;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Maps.Services;

/// <summary>
/// Raster-tile fetcher for an OpenStreetMap-style tile server. Caches each
/// tile to <c>$XDG_CACHE_HOME/openmaui/osm-tiles/{template-hash}/{zoom}/{x}/{y}.png</c>
/// so repeated zoom/pan operations don't re-hit the network. An LRU in-memory
/// layer on top of the disk cache keeps the most recently used decoded
/// SKImages around so the same tile in successive frames doesn't pay disk I/O
/// either. Cache keys include a hash of <see cref="UrlTemplate"/> so switching
/// tile providers can never serve tiles cached from the previous one.
///
/// <para>OSM's <a href="https://operations.osmfoundation.org/policies/tiles/">tile usage policy</a>
/// requires:</para>
/// <list type="bullet">
///   <item>A descriptive User-Agent (we send "OpenMaui-Linux/{version}").</item>
///   <item>Caching on the client — done; one disk hit per (zoom, x, y).</item>
///   <item>Limited concurrency — downloads are capped at 2 in parallel.</item>
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

    private const string DefaultUrlTemplate = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";

    private string _urlTemplate = DefaultUrlTemplate;
    private volatile string _templateHash = HashTemplate(DefaultUrlTemplate);

    public string UrlTemplate
    {
        get => _urlTemplate;
        set
        {
            if (_urlTemplate == value) return;
            _urlTemplate = value;
            // Cache keys embed this hash, so tiles fetched from the previous
            // provider are simply never matched again — no eviction needed.
            _templateHash = HashTemplate(value);
        }
    }

    /// <summary>
    /// Disk cache root. Defaults to <c>$XDG_CACHE_HOME/openmaui/osm-tiles</c>
    /// (falling back to <c>~/.cache/openmaui/osm-tiles</c>). When the
    /// directory can't be created the service degrades to memory-only caching.
    /// </summary>
    public string CacheRoot { get; }

    /// <summary>Upper bound on the in-memory SKImage cache (tile count, ~50–200 tiles fits a typical viewport at multiple zooms).</summary>
    public int MemoryCacheLimit { get; set; } = 256;

    /// <summary>Disk cache size cap; a background sweep prunes oldest tiles when exceeded.</summary>
    public long DiskCacheLimitBytes { get; set; } = 256L * 1024 * 1024;

    /// <summary>How long a failed tile fetch is remembered before it may be retried.</summary>
    public TimeSpan FailureRetryWindow { get; set; } = TimeSpan.FromSeconds(15);

    private readonly HttpClient _http;
    private readonly bool _diskCacheAvailable;

    // OSM tile policy: keep parallel downloads limited (2 is the documented cap).
    private readonly SemaphoreSlim _downloadGate = new(2, 2);

    // In-memory LRU: dictionary for O(1) lookup, linked list for recency order
    // (head = most recent). Guarded by _memLock.
    private readonly object _memLock = new();
    private readonly Dictionary<(string T, int Z, int X, int Y), LinkedListNode<((string T, int Z, int X, int Y) Key, SKImage Image)>> _memory = new();
    private readonly LinkedList<((string T, int Z, int X, int Y) Key, SKImage Image)> _lru = new();

    // Lazy so the fetch factory runs exactly once per key even when GetOrAdd
    // races (a bare GetOrAdd may invoke competing factories).
    private readonly ConcurrentDictionary<(string T, int Z, int X, int Y), Lazy<Task<SKImage?>>> _inflight = new();

    // Negative cache: tiles that failed to fetch and when they may be retried.
    private readonly ConcurrentDictionary<(string T, int Z, int X, int Y), DateTime> _failedUntil = new();

    private int _sweepRunning;       // 1 while a disk sweep is in progress
    private int _writesSinceSweep;

    // Negative-cache hits happen once per tile per frame — avoid allocating
    // a fresh completed task each time.
    private static readonly Task<SKImage?> s_nullTile = Task.FromResult<SKImage?>(null);

    public OsmTileService()
    {
        var xdg = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        if (string.IsNullOrEmpty(xdg))
            xdg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache");
        CacheRoot = Path.Combine(xdg, "openmaui", "osm-tiles");
        try
        {
            Directory.CreateDirectory(CacheRoot);
            _diskCacheAvailable = true;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Warn("OsmTileService", $"Disk tile cache unavailable ({CacheRoot}): {ex.Message}; falling back to memory-only caching");
        }

        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("OpenMaui-Linux/10.0 (+https://github.com/open-maui/maui-linux)");
        _http.Timeout = TimeSpan.FromSeconds(10);

        if (_diskCacheAvailable)
            ScheduleDiskSweep();
    }

    /// <summary>
    /// Fetch tile (zoom, x, y). Returns the cached SKImage when present,
    /// otherwise reads from disk, otherwise fetches from the network and
    /// caches both. Returns null when offline and the tile isn't in any cache;
    /// failures are negatively cached for <see cref="FailureRetryWindow"/> so
    /// callers redrawing every frame don't hammer the server.
    /// </summary>
    public Task<SKImage?> GetTileAsync(int zoom, int x, int y, CancellationToken ct = default)
    {
        var key = (_templateHash, zoom, x, y);

        if (TryGetFromMemory(key) is { } cached)
            return Task.FromResult<SKImage?>(cached);

        if (_failedUntil.TryGetValue(key, out var retryAt))
        {
            if (DateTime.UtcNow < retryAt)
                return s_nullTile;
            _failedUntil.TryRemove(key, out _);
        }

        // Coalesce duplicate concurrent requests for the same tile (very
        // common during a pan). The shared fetch deliberately ignores the
        // caller's token — it serves every awaiter, so the first caller
        // cancelling must not fail the rest; WaitAsync detaches just this
        // caller instead.
        var task = _inflight.GetOrAdd(key, k => new Lazy<Task<SKImage?>>(
            () => FetchAsync(k, zoom, x, y),
            LazyThreadSafetyMode.ExecutionAndPublication)).Value;
        return ct.CanBeCanceled ? task.WaitAsync(ct) : task;
    }

    private SKImage? TryGetFromMemory((string T, int Z, int X, int Y) key)
    {
        lock (_memLock)
        {
            if (_memory.TryGetValue(key, out var node))
            {
                // Refresh recency so genuinely hot tiles stay resident.
                _lru.Remove(node);
                _lru.AddFirst(node);
                return node.Value.Image;
            }
        }
        return null;
    }

    private void AddToMemory((string T, int Z, int X, int Y) key, SKImage image)
    {
        List<SKImage>? evicted = null;
        lock (_memLock)
        {
            if (_memory.Remove(key, out var existing))
            {
                _lru.Remove(existing);
                (evicted ??= new()).Add(existing.Value.Image);
            }

            _memory[key] = _lru.AddFirst((key, image));

            while (_memory.Count > MemoryCacheLimit && _lru.Last is { } oldest)
            {
                _lru.RemoveLast();
                _memory.Remove(oldest.Value.Key);
                (evicted ??= new()).Add(oldest.Value.Image);
            }
        }
        if (evicted != null)
            DisposeDeferred(evicted);
    }

    /// <summary>
    /// Dispose evicted images on a FUTURE main-loop iteration. Rendering runs
    /// on the GLib main thread and may still hold a reference it obtained from
    /// the cache earlier in the current draw pass, so disposal must never run
    /// concurrently with — or inside — a draw. DispatchDelayed(0) defers even
    /// when already on the main thread (Dispatch would run inline there).
    /// </summary>
    private static void DisposeDeferred(List<SKImage> images)
    {
        if (LinuxDispatcher.Main is { } main)
        {
            main.DispatchDelayed(TimeSpan.Zero, () =>
            {
                foreach (var img in images) img.Dispose();
            });
        }
        else
        {
            // No main loop (unit tests / standalone tooling) — nothing to race with.
            foreach (var img in images) img.Dispose();
        }
    }

    private async Task<SKImage?> FetchAsync((string T, int Z, int X, int Y) key, int zoom, int x, int y)
    {
        try
        {
            var path = _diskCacheAvailable
                ? Path.Combine(CacheRoot, key.T, zoom.ToString(), x.ToString(), $"{y}.png")
                : null;

            SKImage? image = null;
            if (path != null && File.Exists(path))
            {
                try
                {
                    using var data = SKData.Create(path);
                    // FromEncodedData returns null (rather than throwing) on
                    // corrupt data; null falls through to a re-download that
                    // overwrites the bad file.
                    image = data != null ? SKImage.FromEncodedData(data) : null;
                }
                catch (Exception ex)
                {
                    DiagnosticLog.Warn("OsmTileService", $"Cached tile decode failed ({path}): {ex.Message}");
                    try { File.Delete(path); } catch { }
                }
            }

            if (image == null)
            {
                var url = UrlTemplate
                    .Replace("{z}", zoom.ToString())
                    .Replace("{x}", x.ToString())
                    .Replace("{y}", y.ToString());

                byte[]? bytes = null;
                await _downloadGate.WaitAsync().ConfigureAwait(false);
                try
                {
                    using var response = await _http.GetAsync(url).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                        bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    else
                        DiagnosticLog.Warn("OsmTileService", $"Tile {zoom}/{x}/{y} HTTP {(int)response.StatusCode}");
                }
                catch (Exception ex)
                {
                    DiagnosticLog.Warn("OsmTileService", $"Tile {zoom}/{x}/{y} fetch failed: {ex.Message}");
                }
                finally
                {
                    _downloadGate.Release();
                }

                if (bytes != null)
                {
                    if (path != null)
                    {
                        // Persist to disk so the next process run can reuse it.
                        try
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                            File.WriteAllBytes(path, bytes);
                            if (Interlocked.Increment(ref _writesSinceSweep) >= 128)
                            {
                                Interlocked.Exchange(ref _writesSinceSweep, 0);
                                ScheduleDiskSweep();
                            }
                        }
                        catch (Exception ex)
                        {
                            DiagnosticLog.Warn("OsmTileService", $"Could not write tile cache file {path}: {ex.Message}");
                        }
                    }

                    image = SKImage.FromEncodedData(SKData.CreateCopy(bytes));
                }
            }

            if (image != null)
            {
                AddToMemory(key, image);
                _failedUntil.TryRemove(key, out _);
            }
            else
            {
                _failedUntil[key] = DateTime.UtcNow + FailureRetryWindow;
            }
            return image;
        }
        finally
        {
            // Result is in the memory cache (or negatively cached) by now, so
            // dropping the inflight entry cannot cause duplicate fetches.
            _inflight.TryRemove(key, out _);
        }
    }

    private void ScheduleDiskSweep()
    {
        if (Interlocked.CompareExchange(ref _sweepRunning, 1, 0) != 0) return;
        _ = Task.Run(() =>
        {
            try { SweepDiskCache(); }
            catch (Exception ex) { DiagnosticLog.Warn("OsmTileService", $"Disk cache sweep failed: {ex.Message}"); }
            finally { Volatile.Write(ref _sweepRunning, 0); }
        });
    }

    private void SweepDiskCache()
    {
        var root = new DirectoryInfo(CacheRoot);
        if (!root.Exists) return;

        CleanupLegacyCacheLayout(root);

        var files = root.EnumerateFiles("*.png", SearchOption.AllDirectories).ToList();
        var total = files.Sum(f => f.Length);
        if (total <= DiskCacheLimitBytes) return;

        // Prune oldest-by-mtime down to 75% of the cap so sweeps stay rare.
        var target = DiskCacheLimitBytes * 3 / 4;
        foreach (var file in files.OrderBy(f => f.LastWriteTimeUtc))
        {
            if (total <= target) break;
            try
            {
                var len = file.Length;
                file.Delete();
                total -= len;
            }
            catch { /* file in use or already gone — skip */ }
        }
    }

    /// <summary>
    /// Delete tiles cached by the pre-provider-hash layout, which stored zoom
    /// directories directly under the root (<c>osm-tiles/{z}/{x}/{y}.png</c>).
    /// The current layout nests everything under an 8-hex-char template hash,
    /// so a top-level directory whose name parses as a small integer can only
    /// be a legacy zoom level — anything else is left alone.
    /// </summary>
    private static void CleanupLegacyCacheLayout(DirectoryInfo root)
    {
        foreach (var dir in root.EnumerateDirectories())
        {
            if (int.TryParse(dir.Name, out var zoom) && zoom >= 0 && zoom <= 22)
            {
                try { dir.Delete(recursive: true); }
                catch { /* best effort — retried on the next sweep */ }
            }
        }
    }

    private static string HashTemplate(string template)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(template));
        return Convert.ToHexString(bytes, 0, 4).ToLowerInvariant();
    }

    public void Dispose()
    {
        // The shared Default instance is process-lifetime: disposing it would
        // tear down the HttpClient under every SkiaMap in the process, so
        // treat that as a no-op. Privately constructed instances dispose fully.
        if (s_default.IsValueCreated && ReferenceEquals(s_default.Value, this))
            return;

        _http.Dispose();
        _downloadGate.Dispose();
        _inflight.Clear();
        _failedUntil.Clear();

        List<SKImage> images;
        lock (_memLock)
        {
            images = _lru.Select(e => e.Image).ToList();
            _lru.Clear();
            _memory.Clear();
        }
        DisposeDeferred(images);
    }
}
