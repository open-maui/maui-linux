// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Rendering;

public class ResourceCache : IDisposable
{
    private readonly Dictionary<string, SKTypeface> _typefaces = new();
    private bool _disposed;

    public SKTypeface GetTypeface(string fontFamily, SKFontStyle style)
    {
        var key = $"{fontFamily}_{style.Weight}_{style.Width}_{style.Slant}";

        if (!_typefaces.TryGetValue(key, out var typeface))
        {
            typeface = SKTypeface.FromFamilyName(fontFamily, style) ?? SKTypeface.Default;
            _typefaces[key] = typeface;
        }

        return typeface;
    }

    public void Clear()
    {
        foreach (var typeface in _typefaces.Values)
        {
            typeface.Dispose();
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
