// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Manages view recycling for virtualized lists and collections.
/// Implements a pool-based recycling strategy to minimize allocations.
/// </summary>
public class VirtualizationManager<T> where T : SkiaView
{
    private readonly Dictionary<int, T> _activeViews = new();
    private readonly Queue<T> _recyclePool = new();
    private readonly Func<T> _viewFactory;
    private readonly Action<T>? _viewRecycler;
    private readonly int _maxPoolSize;

    private int _firstVisibleIndex = -1;
    private int _lastVisibleIndex = -1;

    /// <summary>
    /// Number of views currently active (bound to data).
    /// </summary>
    public int ActiveViewCount => _activeViews.Count;

    /// <summary>
    /// Number of views in the recycle pool.
    /// </summary>
    public int PooledViewCount => _recyclePool.Count;

    /// <summary>
    /// Current visible range.
    /// </summary>
    public (int First, int Last) VisibleRange => (_firstVisibleIndex, _lastVisibleIndex);

    /// <summary>
    /// Creates a new virtualization manager.
    /// </summary>
    /// <param name="viewFactory">Factory function to create new views.</param>
    /// <param name="viewRecycler">Optional function to reset views before recycling.</param>
    /// <param name="maxPoolSize">Maximum number of views to keep in the recycle pool.</param>
    public VirtualizationManager(
        Func<T> viewFactory,
        Action<T>? viewRecycler = null,
        int maxPoolSize = 20)
    {
        _viewFactory = viewFactory ?? throw new ArgumentNullException(nameof(viewFactory));
        _viewRecycler = viewRecycler;
        _maxPoolSize = maxPoolSize;
    }

    /// <summary>
    /// Updates the visible range and recycles views that scrolled out of view.
    /// </summary>
    /// <param name="firstVisible">Index of first visible item.</param>
    /// <param name="lastVisible">Index of last visible item.</param>
    public void UpdateVisibleRange(int firstVisible, int lastVisible)
    {
        if (firstVisible == _firstVisibleIndex && lastVisible == _lastVisibleIndex)
            return;

        // Recycle views that scrolled out of view
        var toRecycle = new List<int>();
        foreach (var kvp in _activeViews)
        {
            if (kvp.Key < firstVisible || kvp.Key > lastVisible)
            {
                toRecycle.Add(kvp.Key);
            }
        }

        foreach (var index in toRecycle)
        {
            RecycleView(index);
        }

        _firstVisibleIndex = firstVisible;
        _lastVisibleIndex = lastVisible;
    }

    /// <summary>
    /// Gets or creates a view for the specified index.
    /// </summary>
    /// <param name="index">Item index.</param>
    /// <param name="bindData">Action to bind data to the view.</param>
    /// <returns>A view bound to the data.</returns>
    public T GetOrCreateView(int index, Action<T> bindData)
    {
        if (_activeViews.TryGetValue(index, out var existing))
        {
            return existing;
        }

        // Get from pool or create new
        T view;
        if (_recyclePool.Count > 0)
        {
            view = _recyclePool.Dequeue();
        }
        else
        {
            view = _viewFactory();
        }

        // Bind data
        bindData(view);
        _activeViews[index] = view;

        return view;
    }

    /// <summary>
    /// Gets an existing view for the index, or null if not active.
    /// </summary>
    public T? GetActiveView(int index)
    {
        return _activeViews.TryGetValue(index, out var view) ? view : default;
    }

    /// <summary>
    /// Recycles a view at the specified index.
    /// </summary>
    private void RecycleView(int index)
    {
        if (!_activeViews.TryGetValue(index, out var view))
            return;

        _activeViews.Remove(index);

        // Reset the view
        _viewRecycler?.Invoke(view);

        // Add to pool if not full
        if (_recyclePool.Count < _maxPoolSize)
        {
            _recyclePool.Enqueue(view);
        }
        else
        {
            // Pool is full, dispose the view
            view.Dispose();
        }
    }

    /// <summary>
    /// Clears all active views and the recycle pool.
    /// </summary>
    public void Clear()
    {
        foreach (var view in _activeViews.Values)
        {
            view.Dispose();
        }
        _activeViews.Clear();

        while (_recyclePool.Count > 0)
        {
            _recyclePool.Dequeue().Dispose();
        }

        _firstVisibleIndex = -1;
        _lastVisibleIndex = -1;
    }

    /// <summary>
    /// Removes a specific item and recycles its view.
    /// </summary>
    public void RemoveItem(int index)
    {
        RecycleView(index);

        // Shift indices for items after the removed one
        var toShift = _activeViews
            .Where(kvp => kvp.Key > index)
            .OrderBy(kvp => kvp.Key)
            .ToList();

        foreach (var kvp in toShift)
        {
            _activeViews.Remove(kvp.Key);
            _activeViews[kvp.Key - 1] = kvp.Value;
        }
    }

    /// <summary>
    /// Inserts an item and shifts existing indices.
    /// </summary>
    public void InsertItem(int index)
    {
        // Shift indices for items at or after the insert position
        var toShift = _activeViews
            .Where(kvp => kvp.Key >= index)
            .OrderByDescending(kvp => kvp.Key)
            .ToList();

        foreach (var kvp in toShift)
        {
            _activeViews.Remove(kvp.Key);
            _activeViews[kvp.Key + 1] = kvp.Value;
        }
    }
}
