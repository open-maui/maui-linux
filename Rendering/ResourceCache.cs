// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Rendering;

public class ResourceCache : IDisposable
{
    // Value carries ownership: faces handed out by the font registrar belong to
    // the registrar and must not be disposed when this cache is cleared.
    private readonly Dictionary<string, (SKTypeface Typeface, bool Owned)> _typefaces = new();
    private readonly LinuxFontRegistrar? _registrar;
    private bool _disposed;

    /// <summary>
    /// Creates a cache that consults <see cref="LinuxFontRegistrar.Instance"/>
    /// for fonts registered through <c>ConfigureFonts</c> before fontconfig.
    /// </summary>
    public ResourceCache()
        : this(LinuxFontRegistrar.Instance)
    {
    }

    /// <summary>
    /// Creates a cache backed by an explicit registrar (tests, custom hosts);
    /// pass <c>null</c> to resolve through fontconfig only.
    /// </summary>
    public ResourceCache(LinuxFontRegistrar? registrar)
    {
        _registrar = registrar;
    }

    /// <summary>
    /// Resolves a typeface for <paramref name="fontFamily"/>: a font registered
    /// under that alias or family name wins, then fontconfig's match for the
    /// family and <paramref name="style"/>, then the Skia default face.
    /// </summary>
    public SKTypeface GetTypeface(string fontFamily, SKFontStyle style)
    {
        var key = $"{fontFamily}_{style.Weight}_{style.Width}_{style.Slant}";

        if (!_typefaces.TryGetValue(key, out var entry))
        {
            var registered = _registrar?.TryGetTypeface(fontFamily, style);
            entry = registered != null
                ? (registered, false)
                : (SKTypeface.FromFamilyName(fontFamily, style) ?? SKTypeface.Default, true);
            _typefaces[key] = entry;
        }

        return entry.Typeface;
    }

    public void Clear()
    {
        foreach (var (typeface, owned) in _typefaces.Values)
        {
            if (owned && !ReferenceEquals(typeface, SKTypeface.Default))
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
