using System;
using System.Collections.Generic;
using System.Linq;
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
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0049: Unknown result type (might be due to invalid IL or missing references)
			_text = text;
			_textSize = paint.TextSize;
			_color = paint.Color;
			SKTypeface typeface = paint.Typeface;
			_weight = ((typeface != null) ? typeface.FontWeight : 400);
			_hashCode = HashCode.Combine<string, float, SKColor, int>(_text, _textSize, _color, _weight);
		}

		public bool Equals(TextCacheKey other)
		{
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			if (_text == other._text && Math.Abs(_textSize - other._textSize) < 0.001f && _color == other._color)
			{
				return _weight == other._weight;
			}
			return false;
		}

		public override bool Equals(object? obj)
		{
			if (obj is TextCacheKey other)
			{
				return Equals(other);
			}
			return false;
		}

		public override int GetHashCode()
		{
			return _hashCode;
		}
	}

	private readonly Dictionary<TextCacheKey, SKBitmap> _cache = new Dictionary<TextCacheKey, SKBitmap>();

	private readonly object _lock = new object();

	private int _maxEntries = 500;

	private bool _disposed;

	public int MaxEntries
	{
		get
		{
			return _maxEntries;
		}
		set
		{
			_maxEntries = Math.Max(10, value);
		}
	}

	public SKBitmap GetOrCreate(string text, SKPaint paint)
	{
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Expected O, but got Unknown
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Expected O, but got Unknown
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		TextCacheKey key = new TextCacheKey(text, paint);
		lock (_lock)
		{
			if (_cache.TryGetValue(key, out SKBitmap value))
			{
				return value;
			}
			SKRect val = default(SKRect);
			paint.MeasureText(text, ref val);
			int num = Math.Max(1, (int)Math.Ceiling(((SKRect)(ref val)).Width) + 2);
			int num2 = Math.Max(1, (int)Math.Ceiling(((SKRect)(ref val)).Height) + 2);
			SKBitmap val2 = new SKBitmap(num, num2, (SKColorType)4, (SKAlphaType)2);
			SKCanvas val3 = new SKCanvas(val2);
			try
			{
				val3.Clear(SKColors.Transparent);
				val3.DrawText(text, 0f - ((SKRect)(ref val)).Left + 1f, 0f - ((SKRect)(ref val)).Top + 1f, paint);
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
			if (_cache.Count >= _maxEntries)
			{
				KeyValuePair<TextCacheKey, SKBitmap> keyValuePair = _cache.First();
				((SKNativeObject)keyValuePair.Value).Dispose();
				_cache.Remove(keyValuePair.Key);
			}
			_cache[key] = val2;
			return val2;
		}
	}

	public void Clear()
	{
		lock (_lock)
		{
			foreach (SKBitmap value in _cache.Values)
			{
				((SKNativeObject)value).Dispose();
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
