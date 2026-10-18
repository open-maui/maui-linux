// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;

namespace Microsoft.Maui.Platform;

/// <summary>
/// A live <see cref="IList{T}"/> view of another list whose elements are converted on the way
/// in and out. Backs the obsolete collection properties typed with OpenMaui's legacy
/// duplicates of MAUI types (for example <c>SkiaGrid.RowDefinitions</c>), over the list of
/// MAUI values the view now keeps.
/// </summary>
internal sealed class ConvertingList<TOuter, TInner> : IList<TOuter>
{
    private readonly IList<TInner> _inner;
    private readonly Func<TInner, TOuter> _toOuter;
    private readonly Func<TOuter, TInner> _toInner;

    public ConvertingList(IList<TInner> inner, Func<TInner, TOuter> toOuter, Func<TOuter, TInner> toInner)
    {
        _inner = inner;
        _toOuter = toOuter;
        _toInner = toInner;
    }

    public TOuter this[int index]
    {
        get => _toOuter(_inner[index]);
        set => _inner[index] = _toInner(value);
    }

    public int Count => _inner.Count;

    public bool IsReadOnly => _inner.IsReadOnly;

    public void Add(TOuter item) => _inner.Add(_toInner(item));

    public void Clear() => _inner.Clear();

    public bool Contains(TOuter item) => _inner.Contains(_toInner(item));

    public void CopyTo(TOuter[] array, int arrayIndex)
    {
        for (int i = 0; i < _inner.Count; i++)
            array[arrayIndex + i] = _toOuter(_inner[i]);
    }

    public IEnumerator<TOuter> GetEnumerator()
    {
        foreach (var item in _inner)
            yield return _toOuter(item);
    }

    public int IndexOf(TOuter item) => _inner.IndexOf(_toInner(item));

    public void Insert(int index, TOuter item) => _inner.Insert(index, _toInner(item));

    public bool Remove(TOuter item) => _inner.Remove(_toInner(item));

    public void RemoveAt(int index) => _inner.RemoveAt(index);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
