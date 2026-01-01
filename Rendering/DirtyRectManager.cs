// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Rendering;

/// <summary>
/// Manages dirty rectangles for optimized rendering.
/// Only redraws areas that have been invalidated.
/// </summary>
public class DirtyRectManager
{
    private readonly List<SKRect> _dirtyRects = new();
    private readonly object _lock = new();
    private bool _fullRedrawNeeded = true;
    private SKRect _bounds;
    private int _maxDirtyRects = 10;

    /// <summary>
    /// Gets or sets the maximum number of dirty rectangles to track before
    /// falling back to a full redraw.
    /// </summary>
    public int MaxDirtyRects
    {
        get => _maxDirtyRects;
        set => _maxDirtyRects = Math.Max(1, value);
    }

    /// <summary>
    /// Gets whether a full redraw is needed.
    /// </summary>
    public bool NeedsFullRedraw => _fullRedrawNeeded;

    /// <summary>
    /// Gets the current dirty rectangles.
    /// </summary>
    public IReadOnlyList<SKRect> DirtyRects
    {
        get
        {
            lock (_lock)
            {
                return _dirtyRects.ToList();
            }
        }
    }

    /// <summary>
    /// Gets whether there are any dirty regions.
    /// </summary>
    public bool HasDirtyRegions
    {
        get
        {
            lock (_lock)
            {
                return _fullRedrawNeeded || _dirtyRects.Count > 0;
            }
        }
    }

    /// <summary>
    /// Sets the rendering bounds.
    /// </summary>
    public void SetBounds(SKRect bounds)
    {
        if (_bounds != bounds)
        {
            _bounds = bounds;
            InvalidateAll();
        }
    }

    /// <summary>
    /// Invalidates a specific region.
    /// </summary>
    public void Invalidate(SKRect rect)
    {
        if (rect.IsEmpty) return;

        lock (_lock)
        {
            if (_fullRedrawNeeded) return;

            // Clamp to bounds
            rect = SKRect.Intersect(rect, _bounds);
            if (rect.IsEmpty) return;

            // Try to merge with existing dirty rects
            for (int i = 0; i < _dirtyRects.Count; i++)
            {
                if (_dirtyRects[i].Contains(rect))
                {
                    // Already covered
                    return;
                }

                if (rect.Contains(_dirtyRects[i]))
                {
                    // New rect covers existing
                    _dirtyRects[i] = rect;
                    MergeDirtyRects();
                    return;
                }

                // Check if they overlap significantly (50% overlap)
                var intersection = SKRect.Intersect(_dirtyRects[i], rect);
                if (!intersection.IsEmpty)
                {
                    float intersectArea = intersection.Width * intersection.Height;
                    float smallerArea = Math.Min(
                        _dirtyRects[i].Width * _dirtyRects[i].Height,
                        rect.Width * rect.Height);

                    if (intersectArea > smallerArea * 0.5f)
                    {
                        // Merge the rectangles
                        _dirtyRects[i] = SKRect.Union(_dirtyRects[i], rect);
                        MergeDirtyRects();
                        return;
                    }
                }
            }

            // Add as new dirty rect
            _dirtyRects.Add(rect);

            // Check if we have too many dirty rects
            if (_dirtyRects.Count > _maxDirtyRects)
            {
                // Fall back to full redraw
                _fullRedrawNeeded = true;
                _dirtyRects.Clear();
            }
        }
    }

    /// <summary>
    /// Invalidates the entire rendering area.
    /// </summary>
    public void InvalidateAll()
    {
        lock (_lock)
        {
            _fullRedrawNeeded = true;
            _dirtyRects.Clear();
        }
    }

    /// <summary>
    /// Clears all dirty regions after rendering.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _fullRedrawNeeded = false;
            _dirtyRects.Clear();
        }
    }

    /// <summary>
    /// Gets the combined dirty region as a single rectangle.
    /// </summary>
    public SKRect GetCombinedDirtyRect()
    {
        lock (_lock)
        {
            if (_fullRedrawNeeded || _dirtyRects.Count == 0)
            {
                return _bounds;
            }

            var combined = _dirtyRects[0];
            for (int i = 1; i < _dirtyRects.Count; i++)
            {
                combined = SKRect.Union(combined, _dirtyRects[i]);
            }
            return combined;
        }
    }

    /// <summary>
    /// Applies dirty region clipping to a canvas.
    /// </summary>
    public void ApplyClipping(SKCanvas canvas)
    {
        lock (_lock)
        {
            if (_fullRedrawNeeded || _dirtyRects.Count == 0)
            {
                // No clipping needed for full redraw
                return;
            }

            // Create a path from all dirty rects
            using var path = new SKPath();
            foreach (var rect in _dirtyRects)
            {
                path.AddRect(rect);
            }

            canvas.ClipPath(path);
        }
    }

    private void MergeDirtyRects()
    {
        // Simple merge pass - could be optimized
        bool merged;
        do
        {
            merged = false;
            for (int i = 0; i < _dirtyRects.Count - 1; i++)
            {
                for (int j = i + 1; j < _dirtyRects.Count; j++)
                {
                    var intersection = SKRect.Intersect(_dirtyRects[i], _dirtyRects[j]);
                    if (!intersection.IsEmpty)
                    {
                        _dirtyRects[i] = SKRect.Union(_dirtyRects[i], _dirtyRects[j]);
                        _dirtyRects.RemoveAt(j);
                        merged = true;
                        break;
                    }
                }
                if (merged) break;
            }
        } while (merged);
    }
}
