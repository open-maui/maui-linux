// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Rendering;

public class LayeredRenderer : IDisposable
{
    private readonly Dictionary<int, RenderLayer> _layers = new();
    private readonly object _lock = new();
    private bool _disposed;

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
