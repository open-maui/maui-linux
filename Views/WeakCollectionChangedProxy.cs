// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Specialized;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Subscribes to a collection's <see cref="INotifyCollectionChanged.CollectionChanged"/> without
/// the collection keeping the subscriber alive, as MAUI's items handlers do
/// (<c>WeakNotifyCollectionChangedProxy</c>): an app's view model outlives the pages that show
/// it, and a strong subscription kept every list (its handler, its item views, its page) alive
/// as long as the collection. <see cref="Dispose"/> unsubscribes; a subscriber that was
/// collected is unsubscribed on the next change.
/// </summary>
internal sealed class WeakCollectionChangedProxy<TTarget> : IDisposable where TTarget : class
{
    private readonly WeakReference<TTarget> _target;
    private readonly Action<TTarget, object?, NotifyCollectionChangedEventArgs> _callback;
    private INotifyCollectionChanged? _source;

    /// <param name="source">The collection observed.</param>
    /// <param name="target">The subscriber, held weakly.</param>
    /// <param name="callback">Called with the subscriber; must not capture it (a static lambda).</param>
    public WeakCollectionChangedProxy(INotifyCollectionChanged source, TTarget target,
        Action<TTarget, object?, NotifyCollectionChangedEventArgs> callback)
    {
        _target = new WeakReference<TTarget>(target);
        _callback = callback;
        _source = source;
        source.CollectionChanged += OnCollectionChanged;
    }

    /// <summary>The collection observed, until <see cref="Dispose"/>.</summary>
    public INotifyCollectionChanged? Source => _source;

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_target.TryGetTarget(out var target))
            _callback(target, sender, e);
        else
            Dispose();
    }

    public void Dispose()
    {
        if (_source is { } source)
        {
            _source = null;
            source.CollectionChanged -= OnCollectionChanged;
        }
    }
}
