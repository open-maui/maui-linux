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

/// <summary>
/// Provides layered rendering for separating static and dynamic content.
/// </summary>
public class LayeredRenderer : IDisposable
{
    private readonly Dictionary<int, RenderLayer> _layers = new();
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Gets or creates a render layer.
    /// </summary>
    public RenderLayer GetLayer(int zIndex)
    {
        lock (_lock)
        {
            if (!_layers.TryGetValue(zIndex, out var layer))
            {
                layer = new RenderLayer(zIndex);
                _layers[zIndex] = layer;
            }
            return layer;
        }
    }

    /// <summary>
    /// Removes a render layer.
    /// </summary>
    public void RemoveLayer(int zIndex)
    {
        lock (_lock)
        {
            if (_layers.TryGetValue(zIndex, out var layer))
            {
                layer.Dispose();
                _layers.Remove(zIndex);
            }
        }
    }

    /// <summary>
    /// Composites all layers onto the target canvas.
    /// </summary>
    public void Composite(SKCanvas canvas, SKRect bounds)
    {
        lock (_lock)
        {
            foreach (var layer in _layers.Values.OrderBy(l => l.ZIndex))
            {
                layer.DrawTo(canvas, bounds);
            }
        }
    }

    /// <summary>
    /// Invalidates all layers.
    /// </summary>
    public void InvalidateAll()
    {
        lock (_lock)
        {
            foreach (var layer in _layers.Values)
            {
                layer.Invalidate();
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lock)
        {
            foreach (var layer in _layers.Values)
            {
                layer.Dispose();
            }
            _layers.Clear();
        }
    }
}

/// <summary>
/// Represents a single render layer with its own bitmap buffer.
/// </summary>
public class RenderLayer : IDisposable
{
    private SKBitmap? _bitmap;
    private SKCanvas? _canvas;
    private bool _isDirty = true;
    private SKRect _bounds;
    private bool _disposed;

    /// <summary>
    /// Gets the Z-index of this layer.
    /// </summary>
    public int ZIndex { get; }

    /// <summary>
    /// Gets whether this layer needs to be redrawn.
    /// </summary>
    public bool IsDirty => _isDirty;

    /// <summary>
    /// Gets or sets whether this layer is visible.
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Gets or sets the layer opacity (0-1).
    /// </summary>
    public float Opacity { get; set; } = 1f;

    public RenderLayer(int zIndex)
    {
        ZIndex = zIndex;
    }

    /// <summary>
    /// Prepares the layer for rendering.
    /// </summary>
    public SKCanvas BeginDraw(SKRect bounds)
    {
        if (_bitmap == null || _bounds != bounds)
        {
            _bitmap?.Dispose();
            _canvas?.Dispose();

            int width = Math.Max(1, (int)bounds.Width);
            int height = Math.Max(1, (int)bounds.Height);

            _bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            _canvas = new SKCanvas(_bitmap);
            _bounds = bounds;
        }

        _canvas!.Clear(SKColors.Transparent);
        _isDirty = false;
        return _canvas;
    }

    /// <summary>
    /// Marks the layer as needing redraw.
    /// </summary>
    public void Invalidate()
    {
        _isDirty = true;
    }

    /// <summary>
    /// Draws this layer to the target canvas.
    /// </summary>
    public void DrawTo(SKCanvas canvas, SKRect bounds)
    {
        if (!IsVisible || _bitmap == null) return;

        using var paint = new SKPaint
        {
            Color = SKColors.White.WithAlpha((byte)(Opacity * 255))
        };

        canvas.DrawBitmap(_bitmap, bounds.Left, bounds.Top, paint);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _canvas?.Dispose();
        _bitmap?.Dispose();
    }
}

/// <summary>
/// Provides text rendering optimization with glyph caching.
/// </summary>
public class TextRenderCache : IDisposable
{
    private readonly Dictionary<TextCacheKey, SKBitmap> _cache = new();
    private readonly object _lock = new();
    private int _maxEntries = 500;
    private bool _disposed;

    /// <summary>
    /// Gets or sets the maximum number of cached text entries.
    /// </summary>
    public int MaxEntries
    {
        get => _maxEntries;
        set => _maxEntries = Math.Max(10, value);
    }

    /// <summary>
    /// Gets a cached text bitmap or creates one.
    /// </summary>
    public SKBitmap GetOrCreate(string text, SKPaint paint)
    {
        var key = new TextCacheKey(text, paint);

        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            // Create text bitmap
            var bounds = new SKRect();
            paint.MeasureText(text, ref bounds);

            int width = Math.Max(1, (int)Math.Ceiling(bounds.Width) + 2);
            int height = Math.Max(1, (int)Math.Ceiling(bounds.Height) + 2);

            var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.DrawText(text, -bounds.Left + 1, -bounds.Top + 1, paint);
            }

            // Trim cache if needed
            if (_cache.Count >= _maxEntries)
            {
                var oldest = _cache.First();
                oldest.Value.Dispose();
                _cache.Remove(oldest.Key);
            }

            _cache[key] = bitmap;
            return bitmap;
        }
    }

    /// <summary>
    /// Clears all cached text.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            foreach (var entry in _cache.Values)
            {
                entry.Dispose();
            }
            _cache.Clear();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Clear();
    }

    private readonly struct TextCacheKey : IEquatable<TextCacheKey>
    {
        private readonly string _text;
        private readonly float _textSize;
        private readonly SKColor _color;
        private readonly int _weight;
        private readonly int _hashCode;

        public TextCacheKey(string text, SKPaint paint)
        {
            _text = text;
            _textSize = paint.TextSize;
            _color = paint.Color;
            _weight = paint.Typeface?.FontWeight ?? (int)SKFontStyleWeight.Normal;
            _hashCode = HashCode.Combine(_text, _textSize, _color, _weight);
        }

        public bool Equals(TextCacheKey other)
        {
            return _text == other._text &&
                   Math.Abs(_textSize - other._textSize) < 0.001f &&
                   _color == other._color &&
                   _weight == other._weight;
        }

        public override bool Equals(object? obj) => obj is TextCacheKey other && Equals(other);
        public override int GetHashCode() => _hashCode;
    }
}
