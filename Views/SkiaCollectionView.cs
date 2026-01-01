// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered CollectionView with selection, headers, and flexible layouts.
/// </summary>
public class SkiaCollectionView : SkiaItemsView
{
    #region BindableProperties

    public static readonly BindableProperty SelectionModeProperty = BindableProperty.Create(
        nameof(SelectionMode),
        typeof(SkiaSelectionMode),
        typeof(SkiaCollectionView),
        SkiaSelectionMode.Single,
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((SkiaCollectionView)b).OnSelectionModeChanged());

    public static readonly BindableProperty SelectedItemProperty = BindableProperty.Create(
        nameof(SelectedItem),
        typeof(object),
        typeof(SkiaCollectionView),
        null,
        BindingMode.OneWay,
        propertyChanged: (b, o, n) => ((SkiaCollectionView)b).OnSelectedItemChanged(n));

    public static readonly BindableProperty OrientationProperty = BindableProperty.Create(
        nameof(Orientation),
        typeof(ItemsLayoutOrientation),
        typeof(SkiaCollectionView),
        ItemsLayoutOrientation.Vertical,
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((SkiaCollectionView)b).Invalidate());

    public static readonly BindableProperty SpanCountProperty = BindableProperty.Create(
        nameof(SpanCount),
        typeof(int),
        typeof(SkiaCollectionView),
        1,
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((SkiaCollectionView)b).Invalidate(),
        coerceValue: (b, v) => Math.Max(1, (int)v));

    public static readonly BindableProperty GridItemWidthProperty = BindableProperty.Create(
        nameof(GridItemWidth),
        typeof(float),
        typeof(SkiaCollectionView),
        100f,
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((SkiaCollectionView)b).Invalidate());

    public static readonly BindableProperty HeaderProperty = BindableProperty.Create(
        nameof(Header),
        typeof(object),
        typeof(SkiaCollectionView),
        null,
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((SkiaCollectionView)b).OnHeaderChanged(n));

    public static readonly BindableProperty FooterProperty = BindableProperty.Create(
        nameof(Footer),
        typeof(object),
        typeof(SkiaCollectionView),
        null,
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((SkiaCollectionView)b).OnFooterChanged(n));

    public static readonly BindableProperty HeaderHeightProperty = BindableProperty.Create(
        nameof(HeaderHeight),
        typeof(float),
        typeof(SkiaCollectionView),
        0f,
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((SkiaCollectionView)b).Invalidate());

    public static readonly BindableProperty FooterHeightProperty = BindableProperty.Create(
        nameof(FooterHeight),
        typeof(float),
        typeof(SkiaCollectionView),
        0f,
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((SkiaCollectionView)b).Invalidate());

    public static readonly BindableProperty SelectionColorProperty = BindableProperty.Create(
        nameof(SelectionColor),
        typeof(SKColor),
        typeof(SkiaCollectionView),
        new SKColor(33, 150, 243, 89),
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((SkiaCollectionView)b).Invalidate());

    public static readonly BindableProperty HeaderBackgroundColorProperty = BindableProperty.Create(
        nameof(HeaderBackgroundColor),
        typeof(SKColor),
        typeof(SkiaCollectionView),
        new SKColor(245, 245, 245),
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((SkiaCollectionView)b).Invalidate());

    public static readonly BindableProperty FooterBackgroundColorProperty = BindableProperty.Create(
        nameof(FooterBackgroundColor),
        typeof(SKColor),
        typeof(SkiaCollectionView),
        new SKColor(245, 245, 245),
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((SkiaCollectionView)b).Invalidate());

    #endregion

    private List<object> _selectedItems = new List<object>();
    private int _selectedIndex = -1;
    private bool _isSelectingItem;
    private bool _heightsChangedDuringDraw;

    public SkiaSelectionMode SelectionMode
    {
        get => (SkiaSelectionMode)GetValue(SelectionModeProperty);
        set => SetValue(SelectionModeProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public IList<object> SelectedItems => _selectedItems.AsReadOnly();

    public override int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (SelectionMode != SkiaSelectionMode.None)
            {
                var item = GetItemAt(value);
                if (item != null)
                {
                    SelectedItem = item;
                }
            }
        }
    }

    public ItemsLayoutOrientation Orientation
    {
        get => (ItemsLayoutOrientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public int SpanCount
    {
        get => (int)GetValue(SpanCountProperty);
        set => SetValue(SpanCountProperty, value);
    }

    public float GridItemWidth
    {
        get => (float)GetValue(GridItemWidthProperty);
        set => SetValue(GridItemWidthProperty, value);
    }

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public object? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    public float HeaderHeight
    {
        get => (float)GetValue(HeaderHeightProperty);
        set => SetValue(HeaderHeightProperty, value);
    }

    public float FooterHeight
    {
        get => (float)GetValue(FooterHeightProperty);
        set => SetValue(FooterHeightProperty, value);
    }

    public SKColor SelectionColor
    {
        get => (SKColor)GetValue(SelectionColorProperty);
        set => SetValue(SelectionColorProperty, value);
    }

    public SKColor HeaderBackgroundColor
    {
        get => (SKColor)GetValue(HeaderBackgroundColorProperty);
        set => SetValue(HeaderBackgroundColorProperty, value);
    }

    public SKColor FooterBackgroundColor
    {
        get => (SKColor)GetValue(FooterBackgroundColorProperty);
        set => SetValue(FooterBackgroundColorProperty, value);
    }

    public event EventHandler<CollectionSelectionChangedEventArgs>? SelectionChanged;

    protected override void RefreshItems()
    {
        _selectedItems.Clear();
        SetValue(SelectedItemProperty, null);
        _selectedIndex = -1;
        base.RefreshItems();
    }

    private void OnSelectionModeChanged()
    {
        switch (SelectionMode)
        {
            case SkiaSelectionMode.None:
                ClearSelection();
                break;
            case SkiaSelectionMode.Single:
                if (_selectedItems.Count > 1)
                {
                    var first = _selectedItems.FirstOrDefault();
                    ClearSelection();
                    if (first != null)
                    {
                        SelectItem(first);
                    }
                }
                break;
        }
        Invalidate();
    }

    private void OnSelectedItemChanged(object? newValue)
    {
        if (SelectionMode != SkiaSelectionMode.None && !_isSelectingItem)
        {
            ClearSelection();
            if (newValue != null)
            {
                SelectItem(newValue);
            }
        }
    }

    private void OnHeaderChanged(object? newValue)
    {
        HeaderHeight = newValue != null ? 44 : 0;
        Invalidate();
    }

    private void OnFooterChanged(object? newValue)
    {
        FooterHeight = newValue != null ? 44 : 0;
        Invalidate();
    }

    private void SelectItem(object item)
    {
        if (SelectionMode == SkiaSelectionMode.None)
        {
            return;
        }

        var previousSelection = _selectedItems.ToList();

        if (SelectionMode == SkiaSelectionMode.Single)
        {
            _selectedItems.Clear();
            _selectedItems.Add(item);
            SetValue(SelectedItemProperty, item);

            for (int i = 0; i < ItemCount; i++)
            {
                if (GetItemAt(i) == item)
                {
                    _selectedIndex = i;
                    break;
                }
            }
        }
        else
        {
            if (_selectedItems.Contains(item))
            {
                _selectedItems.Remove(item);
                if (SelectedItem == item)
                {
                    SetValue(SelectedItemProperty, _selectedItems.FirstOrDefault());
                }
            }
            else
            {
                _selectedItems.Add(item);
                SetValue(SelectedItemProperty, item);
            }
            _selectedIndex = SelectedItem != null ? GetIndexOf(SelectedItem) : -1;
        }

        SelectionChanged?.Invoke(this, new CollectionSelectionChangedEventArgs(previousSelection, _selectedItems.ToList()));
        Invalidate();
    }

    private int GetIndexOf(object item)
    {
        for (int i = 0; i < ItemCount; i++)
        {
            if (GetItemAt(i) == item)
            {
                return i;
            }
        }
        return -1;
    }

    private void ClearSelection()
    {
        var previousItems = _selectedItems.ToList();
        _selectedItems.Clear();
        SetValue(SelectedItemProperty, null);
        _selectedIndex = -1;

        if (previousItems.Count > 0)
        {
            SelectionChanged?.Invoke(this, new CollectionSelectionChangedEventArgs(previousItems, new List<object>()));
        }
    }

    protected override void OnItemTapped(int index, object item)
    {
        if (_isSelectingItem)
        {
            return;
        }

        _isSelectingItem = true;
        try
        {
            if (SelectionMode != SkiaSelectionMode.None)
            {
                SelectItem(item);
            }
            base.OnItemTapped(index, item);
        }
        finally
        {
            _isSelectingItem = false;
        }
    }

    protected override void DrawItem(SKCanvas canvas, object item, int index, SKRect bounds, SKPaint paint)
    {
        bool isSelected = _selectedItems.Contains(item);

        if (Orientation == ItemsLayoutOrientation.Vertical && SpanCount == 1)
        {
            paint.Color = new SKColor(224, 224, 224);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 1f;
            canvas.DrawLine(bounds.Left, bounds.Bottom, bounds.Right, bounds.Bottom, paint);
        }

        if (ItemViewCreator != null)
        {
            if (!_itemViewCache.TryGetValue(index, out var itemView) || itemView == null)
            {
                itemView = ItemViewCreator(item);
                if (itemView != null)
                {
                    itemView.Parent = this;
                    _itemViewCache[index] = itemView;
                }
            }

            if (itemView != null)
            {
                try
                {
                    var availableSize = new SKSize(bounds.Width, float.MaxValue);
                    var measuredSize = itemView.Measure(availableSize);

                    var rawHeight = measuredSize.Height;
                    if (float.IsNaN(rawHeight) || float.IsInfinity(rawHeight) || rawHeight > 10000f)
                    {
                        rawHeight = ItemHeight;
                    }

                    var measuredHeight = Math.Max(rawHeight, ItemHeight);
                    if (!_itemHeights.TryGetValue(index, out var cachedHeight) || Math.Abs(cachedHeight - measuredHeight) > 1f)
                    {
                        _itemHeights[index] = measuredHeight;
                        _heightsChangedDuringDraw = true;
                    }

                    var actualBounds = new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Top + measuredHeight);
                    itemView.Arrange(actualBounds);
                    itemView.Draw(canvas);

                    if (isSelected)
                    {
                        paint.Color = SelectionColor;
                        paint.Style = SKPaintStyle.Fill;
                        canvas.DrawRoundRect(actualBounds, 12f, 12f, paint);
                    }

                    if (isSelected && SelectionMode == SkiaSelectionMode.Multiple)
                    {
                        DrawCheckmark(canvas, new SKRect(actualBounds.Right - 32f, actualBounds.MidY - 8f, actualBounds.Right - 16f, actualBounds.MidY + 8f));
                    }
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[SkiaCollectionView.DrawItem] EXCEPTION: " + ex.Message + "\n" + ex.StackTrace);
                    return;
                }
            }
        }

        if (ItemRenderer != null && ItemRenderer(item, index, bounds, canvas, paint))
        {
            return;
        }

        paint.Color = SKColors.Black;
        paint.Style = SKPaintStyle.Fill;

        using var font = new SKFont(SKTypeface.Default, 14f, 1f, 0f);
        using var textPaint = new SKPaint(font)
        {
            Color = SKColors.Black,
            IsAntialias = true
        };

        var text = item?.ToString() ?? "";
        var textBounds = new SKRect();
        textPaint.MeasureText(text, ref textBounds);

        var x = bounds.Left + 16f;
        var y = bounds.MidY - textBounds.MidY;
        canvas.DrawText(text, x, y, textPaint);

        if (isSelected && SelectionMode == SkiaSelectionMode.Multiple)
        {
            DrawCheckmark(canvas, new SKRect(bounds.Right - 32f, bounds.MidY - 8f, bounds.Right - 16f, bounds.MidY + 8f));
        }
    }

    private void DrawCheckmark(SKCanvas canvas, SKRect bounds)
    {
        using var paint = new SKPaint
        {
            Color = new SKColor(33, 150, 243),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        };

        using var path = new SKPath();
        path.MoveTo(bounds.Left, bounds.MidY);
        path.LineTo(bounds.MidX - 2f, bounds.Bottom - 2f);
        path.LineTo(bounds.Right, bounds.Top + 2f);

        canvas.DrawPath(path, paint);
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        _heightsChangedDuringDraw = false;

        if (BackgroundColor != SKColors.Transparent)
        {
            using var bgPaint = new SKPaint
            {
                Color = BackgroundColor,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(bounds, bgPaint);
        }

        if (Header != null && HeaderHeight > 0f)
        {
            var headerRect = new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Top + HeaderHeight);
            DrawHeader(canvas, headerRect);
        }

        if (Footer != null && FooterHeight > 0f)
        {
            var footerRect = new SKRect(bounds.Left, bounds.Bottom - FooterHeight, bounds.Right, bounds.Bottom);
            DrawFooter(canvas, footerRect);
        }

        var contentBounds = new SKRect(bounds.Left, bounds.Top + HeaderHeight, bounds.Right, bounds.Bottom - FooterHeight);

        if (ItemCount == 0)
        {
            DrawEmptyView(canvas, contentBounds);
            return;
        }

        if (SpanCount > 1)
        {
            DrawGridItems(canvas, contentBounds);
        }
        else
        {
            DrawListItems(canvas, contentBounds);
        }

        if (_heightsChangedDuringDraw)
        {
            _heightsChangedDuringDraw = false;
            Invalidate();
        }
    }

    private void DrawListItems(SKCanvas canvas, SKRect bounds)
    {
        canvas.Save();
        canvas.ClipRect(bounds, SKClipOperation.Intersect, false);

        using var paint = new SKPaint
        {
            IsAntialias = true
        };

        var scrollOffset = GetScrollOffset();

        int firstVisible = 0;
        float cumulativeOffset = 0f;
        for (int i = 0; i < ItemCount; i++)
        {
            var itemH = GetItemHeight(i);
            if (cumulativeOffset + itemH > scrollOffset)
            {
                firstVisible = i;
                break;
            }
            cumulativeOffset += itemH + ItemSpacing;
        }

        float currentY = bounds.Top + GetItemOffset(firstVisible) - scrollOffset;
        for (int i = firstVisible; i < ItemCount; i++)
        {
            var itemH = GetItemHeight(i);
            var itemRect = new SKRect(bounds.Left, currentY, bounds.Right - 8f, currentY + itemH);

            if (itemRect.Top > bounds.Bottom)
            {
                break;
            }

            if (itemRect.Bottom >= bounds.Top)
            {
                var item = GetItemAt(i);
                if (item != null)
                {
                    DrawItem(canvas, item, i, itemRect, paint);
                }
            }

            currentY += itemH + ItemSpacing;
        }

        canvas.Restore();

        var totalHeight = TotalContentHeight;
        if (totalHeight > bounds.Height)
        {
            DrawScrollBarInternal(canvas, bounds, scrollOffset, totalHeight);
        }
    }

    private void DrawGridItems(SKCanvas canvas, SKRect bounds)
    {
        canvas.Save();
        canvas.ClipRect(bounds, SKClipOperation.Intersect, false);

        using var paint = new SKPaint
        {
            IsAntialias = true
        };

        var cellWidth = (bounds.Width - 8f) / SpanCount;
        var cellHeight = ItemHeight;
        var rowCount = (int)Math.Ceiling((double)ItemCount / SpanCount);
        var totalHeight = rowCount * (cellHeight + ItemSpacing) - ItemSpacing;

        var scrollOffset = GetScrollOffset();
        var firstVisibleRow = Math.Max(0, (int)(scrollOffset / (cellHeight + ItemSpacing)));
        var lastVisibleRow = Math.Min(rowCount - 1, (int)((scrollOffset + bounds.Height) / (cellHeight + ItemSpacing)) + 1);

        for (int row = firstVisibleRow; row <= lastVisibleRow; row++)
        {
            var rowY = bounds.Top + row * (cellHeight + ItemSpacing) - scrollOffset;

            for (int col = 0; col < SpanCount; col++)
            {
                var index = row * SpanCount + col;
                if (index >= ItemCount)
                {
                    break;
                }

                var cellX = bounds.Left + col * cellWidth;
                var cellRect = new SKRect(cellX + 2f, rowY, cellX + cellWidth - 2f, rowY + cellHeight);

                if (cellRect.Bottom < bounds.Top || cellRect.Top > bounds.Bottom)
                {
                    continue;
                }

                var item = GetItemAt(index);
                if (item != null)
                {
                    using var cellBgPaint = new SKPaint
                    {
                        Color = _selectedItems.Contains(item) ? SelectionColor : new SKColor(250, 250, 250),
                        Style = SKPaintStyle.Fill
                    };
                    canvas.DrawRoundRect(new SKRoundRect(cellRect, 4f), cellBgPaint);
                    DrawItem(canvas, item, index, cellRect, paint);
                }
            }
        }

        canvas.Restore();

        if (totalHeight > bounds.Height)
        {
            DrawScrollBarInternal(canvas, bounds, scrollOffset, totalHeight);
        }
    }

    private void DrawScrollBarInternal(SKCanvas canvas, SKRect bounds, float scrollOffset, float totalHeight)
    {
        var scrollBarWidth = 6f;
        var scrollBarMargin = 2f;

        var trackRect = new SKRect(
            bounds.Right - scrollBarWidth - scrollBarMargin,
            bounds.Top + scrollBarMargin,
            bounds.Right - scrollBarMargin,
            bounds.Bottom - scrollBarMargin);

        using var trackPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 20),
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRoundRect(new SKRoundRect(trackRect, 3f), trackPaint);

        var maxOffset = Math.Max(0f, totalHeight - bounds.Height);
        var viewportRatio = bounds.Height / totalHeight;
        var availableTrackHeight = trackRect.Height;
        var thumbHeight = Math.Max(30f, availableTrackHeight * viewportRatio);
        var scrollRatio = maxOffset > 0f ? scrollOffset / maxOffset : 0f;
        var thumbY = trackRect.Top + (availableTrackHeight - thumbHeight) * scrollRatio;

        var thumbRect = new SKRect(trackRect.Left, thumbY, trackRect.Right, thumbY + thumbHeight);

        using var thumbPaint = new SKPaint
        {
            Color = new SKColor(100, 100, 100, 180),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        canvas.DrawRoundRect(new SKRoundRect(thumbRect, 3f), thumbPaint);
    }

    private float GetScrollOffset()
    {
        return _scrollOffset;
    }

    private void DrawHeader(SKCanvas canvas, SKRect bounds)
    {
        using var bgPaint = new SKPaint
        {
            Color = HeaderBackgroundColor,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(bounds, bgPaint);

        var text = Header?.ToString() ?? "";
        if (!string.IsNullOrEmpty(text))
        {
            using var font = new SKFont(SKTypeface.Default, 16f, 1f, 0f);
            using var textPaint = new SKPaint(font)
            {
                Color = SKColors.Black,
                IsAntialias = true
            };

            var textBounds = new SKRect();
            textPaint.MeasureText(text, ref textBounds);

            var x = bounds.Left + 16f;
            var y = bounds.MidY - textBounds.MidY;
            canvas.DrawText(text, x, y, textPaint);
        }

        using var sepPaint = new SKPaint
        {
            Color = new SKColor(224, 224, 224),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f
        };
        canvas.DrawLine(bounds.Left, bounds.Bottom, bounds.Right, bounds.Bottom, sepPaint);
    }

    private void DrawFooter(SKCanvas canvas, SKRect bounds)
    {
        using var bgPaint = new SKPaint
        {
            Color = FooterBackgroundColor,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(bounds, bgPaint);

        using var sepPaint = new SKPaint
        {
            Color = new SKColor(224, 224, 224),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f
        };
        canvas.DrawLine(bounds.Left, bounds.Top, bounds.Right, bounds.Top, sepPaint);

        var text = Footer?.ToString() ?? "";
        if (!string.IsNullOrEmpty(text))
        {
            using var font = new SKFont(SKTypeface.Default, 14f, 1f, 0f);
            using var textPaint = new SKPaint(font)
            {
                Color = new SKColor(128, 128, 128),
                IsAntialias = true
            };

            var textBounds = new SKRect();
            textPaint.MeasureText(text, ref textBounds);

            var x = bounds.MidX - textBounds.MidX;
            var y = bounds.MidY - textBounds.MidY;
            canvas.DrawText(text, x, y, textPaint);
        }
    }
}

/// <summary>
/// Event args for collection selection changed events.
/// </summary>
public class CollectionSelectionChangedEventArgs : EventArgs
{
    public IReadOnlyList<object> PreviousSelection { get; }
    public IReadOnlyList<object> CurrentSelection { get; }

    public CollectionSelectionChangedEventArgs(IList<object> previousSelection, IList<object> currentSelection)
    {
        PreviousSelection = previousSelection.ToList().AsReadOnly();
        CurrentSelection = currentSelection.ToList().AsReadOnly();
    }
}
