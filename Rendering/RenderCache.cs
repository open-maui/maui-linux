using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Rendering;

public class RenderCache : IDisposable
{
	private class CacheEntry
	{
		public string Key { get; set; } = string.Empty;

		public SKBitmap? Bitmap { get; set; }

		public long Size { get; set; }

		public DateTime Created { get; set; }

		public DateTime LastAccessed { get; set; }

		public int AccessCount { get; set; }
	}

	private readonly Dictionary<string, CacheEntry> _cache = new Dictionary<string, CacheEntry>();

	private readonly object _lock = new object();

	private long _maxCacheSize = 52428800L;

	private long _currentCacheSize;

	private bool _disposed;

	public long MaxCacheSize
	{
		get
		{
			return _maxCacheSize;
		}
		set
		{
			_maxCacheSize = Math.Max(1048576L, value);
			TrimCache();
		}
	}

	public long CurrentCacheSize => _currentCacheSize;

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

	public bool TryGet(string key, out SKBitmap? bitmap)
	{
		lock (_lock)
		{
			if (_cache.TryGetValue(key, out CacheEntry value))
			{
				value.LastAccessed = DateTime.UtcNow;
				value.AccessCount++;
				bitmap = value.Bitmap;
				return true;
			}
		}
		bitmap = null;
		return false;
	}

	public void Set(string key, SKBitmap bitmap)
	{
		if (bitmap == null)
		{
			return;
		}
		long num = bitmap.ByteCount;
		if (num > _maxCacheSize)
		{
			return;
		}
		lock (_lock)
		{
			if (_cache.TryGetValue(key, out CacheEntry value))
			{
				_currentCacheSize -= value.Size;
				SKBitmap? bitmap2 = value.Bitmap;
				if (bitmap2 != null)
				{
					((SKNativeObject)bitmap2).Dispose();
				}
			}
			SKBitmap val = bitmap.Copy();
			if (val != null)
			{
				CacheEntry value2 = new CacheEntry
				{
					Key = key,
					Bitmap = val,
					Size = num,
					Created = DateTime.UtcNow,
					LastAccessed = DateTime.UtcNow,
					AccessCount = 1
				};
				_cache[key] = value2;
				_currentCacheSize += num;
				TrimCache();
			}
		}
	}

	public void Invalidate(string key)
	{
		lock (_lock)
		{
			if (_cache.TryGetValue(key, out CacheEntry value))
			{
				_currentCacheSize -= value.Size;
				SKBitmap? bitmap = value.Bitmap;
				if (bitmap != null)
				{
					((SKNativeObject)bitmap).Dispose();
				}
				_cache.Remove(key);
			}
		}
	}

	public void InvalidatePrefix(string prefix)
	{
		lock (_lock)
		{
			foreach (string item in _cache.Keys.Where((string k) => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
			{
				if (_cache.TryGetValue(item, out CacheEntry value))
				{
					_currentCacheSize -= value.Size;
					SKBitmap? bitmap = value.Bitmap;
					if (bitmap != null)
					{
						((SKNativeObject)bitmap).Dispose();
					}
					_cache.Remove(item);
				}
			}
		}
	}

	public void Clear()
	{
		lock (_lock)
		{
			foreach (CacheEntry value in _cache.Values)
			{
				SKBitmap? bitmap = value.Bitmap;
				if (bitmap != null)
				{
					((SKNativeObject)bitmap).Dispose();
				}
			}
			_cache.Clear();
			_currentCacheSize = 0L;
		}
	}

	public SKBitmap GetOrCreate(string key, int width, int height, Action<SKCanvas> render)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Expected O, but got Unknown
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Expected O, but got Unknown
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		if (TryGet(key, out SKBitmap bitmap) && bitmap != null && bitmap.Width == width && bitmap.Height == height)
		{
			return bitmap;
		}
		SKBitmap val = new SKBitmap(width, height, (SKColorType)4, (SKAlphaType)2);
		SKCanvas val2 = new SKCanvas(val);
		try
		{
			val2.Clear(SKColors.Transparent);
			render(val2);
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
		Set(key, val);
		return val;
	}

	private void TrimCache()
	{
		if (_currentCacheSize <= _maxCacheSize)
		{
			return;
		}
		foreach (CacheEntry item in (from e in _cache.Values
			orderby e.LastAccessed, e.AccessCount
			select e).ToList())
		{
			if ((double)_currentCacheSize <= (double)_maxCacheSize * 0.8)
			{
				break;
			}
			_currentCacheSize -= item.Size;
			SKBitmap? bitmap = item.Bitmap;
			if (bitmap != null)
			{
				((SKNativeObject)bitmap).Dispose();
			}
			_cache.Remove(item.Key);
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
