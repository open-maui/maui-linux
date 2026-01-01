// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Rendering;

public class TextRenderCache : IDisposable
{
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
            _weight = paint.Typeface?.FontWeight ?? 400;
            _hashCode = HashCode.Combine(_text, _textSize, _color, _weight);
        }

        public bool Equals(TextCacheKey other)
        {
            return _text == other._text
                && Math.Abs(_textSize - other._textSize) < 0.001f
                && _color == other._color
                && _weight == other._weight;
        }

        public override bool Equals(object? obj)
        {
            return obj is TextCacheKey other && Equals(other);
        }

        public override int GetHashCode() => _hashCode;
    }

    private readonly Dictionary<TextCacheKey, SKBitmap> _cache = new();
    private readonly object _lock = new();
    private int _maxEntries = 500;
    private bool _disposed;

    public int MaxEntries
    {
        get => _maxEntries;
        set => _maxEntries = Math.Max(10, value);
    }

    public SKBitmap GetOrCreate(string text, SKPaint paint)
    {
        var key = new TextCacheKey(text, paint);

        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var bounds = new SKRect();
            paint.MeasureText(text, ref bounds);

            var width = Math.Max(1, (int)Math.Ceiling(bounds.Width) + 2);
            var height = Math.Max(1, (int)Math.Ceiling(bounds.Height) + 2);

            var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);
            canvas.DrawText(text, -bounds.Left + 1f, -bounds.Top + 1f, paint);

            if (_cache.Count >= _maxEntries)
            {
                var first = _cache.First();
                first.Value.Dispose();
                _cache.Remove(first.Key);
            }

            _cache[key] = bitmap;
            return bitmap;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            foreach (var bitmap in _cache.Values)
            {
                bitmap.Dispose();
            }
            _cache.Clear();
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            Clear();
        }
    }
}
