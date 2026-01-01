using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Rendering;

public class ResourceCache : IDisposable
{
	private readonly Dictionary<string, SKTypeface> _typefaces = new Dictionary<string, SKTypeface>();

	private bool _disposed;

	public SKTypeface GetTypeface(string fontFamily, SKFontStyle style)
	{
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		string key = $"{fontFamily}_{style.Weight}_{style.Width}_{style.Slant}";
		if (!_typefaces.TryGetValue(key, out SKTypeface value))
		{
			value = SKTypeface.FromFamilyName(fontFamily, style) ?? SKTypeface.Default;
			_typefaces[key] = value;
		}
		return value;
	}

	public void Clear()
	{
		foreach (SKTypeface value in _typefaces.Values)
		{
			((SKNativeObject)value).Dispose();
		}
		_typefaces.Clear();
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			Clear();
			_disposed = true;
		}
	}
}
