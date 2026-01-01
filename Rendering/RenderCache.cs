// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Rendering;

/// <summary>
/// Caches rendered content for views that don't change frequently.
/// Improves performance by avoiding redundant rendering.
/// </summary>
public class RenderCache : IDisposable
{
    private readonly Dictionary<string, CacheEntry> _cache = new();
    private readonly object _lock = new();
    private long _maxCacheSize = 50 * 1024 * 1024; // 50 MB default
    private long _currentCacheSize;
    private bool _disposed;

    /// <summary>
    /// Gets or sets the maximum cache size in bytes.
    /// </summary>
    public long MaxCacheSize
    {
        get => _maxCacheSize;
        set
        {
            _maxCacheSize = Math.Max(1024 * 1024, value); // Minimum 1 MB
            TrimCache();
        }
    }

    /// <summary>
    /// Gets the current cache size in bytes.
    /// </summary>
    public long CurrentCacheSize => _currentCacheSize;

    /// <summary>
    /// Gets the number of cached items.
    /// </summary>
    public int CachedItemCount
    {
        get
        {
            lock (_lock)
            {
                return _cache.Count;
            }
        }
    }

    /// <summary>
    /// Tries to get a cached bitmap for the given key.
    /// </summary>
    public bool TryGet(string key, out SKBitmap? bitmap)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                entry.LastAccessed = DateTime.UtcNow;
                entry.AccessCount++;
                bitmap = entry.Bitmap;
                return true;
            }
        }

        bitmap = null;
        return false;
    }

    /// <summary>
    /// Caches a bitmap with the given key.
    /// </summary>
    public void Set(string key, SKBitmap bitmap)
    {
        if (bitmap == null) return;

        long bitmapSize = bitmap.ByteCount;

        // Don't cache if bitmap is larger than max size
        if (bitmapSize > _maxCacheSize)
        {
            return;
        }

        lock (_lock)
        {
            // Remove existing entry if present
            if (_cache.TryGetValue(key, out var existing))
            {
                _currentCacheSize -= existing.Size;
                existing.Bitmap?.Dispose();
            }

            // Create copy of bitmap for cache
            var cachedBitmap = bitmap.Copy();
            if (cachedBitmap == null) return;

            var entry = new CacheEntry
            {
                Key = key,
                Bitmap = cachedBitmap,
                Size = bitmapSize,
                Created = DateTime.UtcNow,
                LastAccessed = DateTime.UtcNow,
                AccessCount = 1
            };

            _cache[key] = entry;
            _currentCacheSize += bitmapSize;

            // Trim cache if needed
            TrimCache();
        }
    }

    /// <summary>
    /// Invalidates a cached entry.
    /// </summary>
    public void Invalidate(string key)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                _currentCacheSize -= entry.Size;
                entry.Bitmap?.Dispose();
                _cache.Remove(key);
            }
        }
    }

    /// <summary>
    /// Invalidates all entries matching a prefix.
    /// </summary>
    public void InvalidatePrefix(string prefix)
    {
        lock (_lock)
        {
            var keysToRemove = _cache.Keys
                .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
                .ToList();

            foreach (var key in keysToRemove)
            {
                if (_cache.TryGetValue(key, out var entry))
                {
                    _currentCacheSize -= entry.Size;
                    entry.Bitmap?.Dispose();
                    _cache.Remove(key);
                }
            }
        }
    }

    /// <summary>
    /// Clears all cached content.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            foreach (var entry in _cache.Values)
            {
                entry.Bitmap?.Dispose();
            }
            _cache.Clear();
            _currentCacheSize = 0;
        }
    }

    /// <summary>
    /// Renders content with caching.
    /// </summary>
    public SKBitmap GetOrCreate(string key, int width, int height, Action<SKCanvas> render)
    {
        // Check cache first
        if (TryGet(key, out var cached) && cached != null &&
            cached.Width == width && cached.Height == height)
        {
            return cached;
        }

        // Create new bitmap
        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            render(canvas);
        }

        // Cache it
        Set(key, bitmap);

        return bitmap;
    }

    private void TrimCache()
    {
        if (_currentCacheSize <= _maxCacheSize) return;

        // Remove least recently used entries until under limit
        var entries = _cache.Values
            .OrderBy(e => e.LastAccessed)
            .ThenBy(e => e.AccessCount)
            .ToList();

        foreach (var entry in entries)
        {
            if (_currentCacheSize <= _maxCacheSize * 0.8) // Target 80% usage
            {
                break;
            }

            _currentCacheSize -= entry.Size;
            entry.Bitmap?.Dispose();
            _cache.Remove(entry.Key);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Clear();
    }

    private class CacheEntry
    {
        public string Key { get; set; } = string.Empty;
        public SKBitmap? Bitmap { get; set; }
        public long Size { get; set; }
        public DateTime Created { get; set; }
        public DateTime LastAccessed { get; set; }
        public int AccessCount { get; set; }
    }
}
