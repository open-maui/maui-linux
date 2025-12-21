// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;
using System.Collections;
using Microsoft.Maui.Graphics;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Selection mode for collection views.
/// </summary>
public enum SkiaSelectionMode
{
    None,
    Single,
    Multiple
}

/// <summary>
/// Layout orientation for items.
/// </summary>
public enum ItemsLayoutOrientation
{
    Vertical,
    Horizontal
}

/// <summary>
/// Skia-rendered CollectionView with selection, headers, and flexible layouts.
/// </summary>
public class SkiaCollectionView : SkiaItemsView
{
    private SkiaSelectionMode _selectionMode = SkiaSelectionMode.Single;
    private object? _selectedItem;
    private List<object> _selectedItems = new();
    private int _selectedIndex = -1;

    // Layout
    private ItemsLayoutOrientation _orientation = ItemsLayoutOrientation.Vertical;
    private int _spanCount = 1; // For grid layout
    private float _itemWidth = 100;

    // Header/Footer
    private object? _header;
    private object? _footer;
    private float _headerHeight = 0;
    private float _footerHeight = 0;

    // Track if heights changed during draw (requires redraw for correct positioning)
    private bool _heightsChangedDuringDraw;

    // Uses parent's _itemViewCache for virtualization

    protected override void RefreshItems()
    {
        // Clear selection when items change to avoid stale references
        _selectedItems.Clear();
        _selectedItem = null;
        _selectedIndex = -1;

        base.RefreshItems();
    }

    public SkiaSelectionMode SelectionMode
    {
        get => _selectionMode;
        set
        {
            _selectionMode = value;
            if (value == SkiaSelectionMode.None)
            {
                ClearSelection();
            }
            else if (value == SkiaSelectionMode.Single && _selectedItems.Count > 1)
            {
                // Keep only first selected
                var first = _selectedItems.FirstOrDefault();
                ClearSelection();
                if (first != null)
                {
                    SelectItem(first);
                }
            }
            Invalidate();
        }
    }

    public object? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (_selectionMode == SkiaSelectionMode.None) return;

            ClearSelection();
            if (value != null)
            {
                SelectItem(value);
            }
        }
    }

    public IList<object> SelectedItems => _selectedItems.AsReadOnly();

    public override int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (_selectionMode == SkiaSelectionMode.None) return;

            var item = GetItemAt(value);
            if (item != null)
            {
                SelectedItem = item;
            }
        }
    }

    public ItemsLayoutOrientation Orientation
    {
        get => _orientation;
        set
        {
            _orientation = value;
            Invalidate();
        }
    }

    public int SpanCount
    {
        get => _spanCount;
        set
        {
            _spanCount = Math.Max(1, value);
            Invalidate();
        }
    }

    public float GridItemWidth
    {
        get => _itemWidth;
        set
        {
            _itemWidth = value;
            Invalidate();
        }
    }

    public object? Header
    {
        get => _header;
        set
        {
            _header = value;
            _headerHeight = value != null ? 44 : 0;
            Invalidate();
        }
    }

    public object? Footer
    {
        get => _footer;
        set
        {
            _footer = value;
            _footerHeight = value != null ? 44 : 0;
            Invalidate();
        }
    }

    public float HeaderHeight
    {
        get => _headerHeight;
        set
        {
            _headerHeight = value;
            Invalidate();
        }
    }

    public float FooterHeight
    {
        get => _footerHeight;
        set
        {
            _footerHeight = value;
            Invalidate();
        }
    }

    public SKColor SelectionColor { get; set; } = new SKColor(0x21, 0x96, 0xF3, 0x59); // 35% opacity
    public SKColor HeaderBackgroundColor { get; set; } = new SKColor(0xF5, 0xF5, 0xF5);
    public SKColor FooterBackgroundColor { get; set; } = new SKColor(0xF5, 0xF5, 0xF5);

    public event EventHandler<CollectionSelectionChangedEventArgs>? SelectionChanged;

    private void SelectItem(object item)
    {
        if (_selectionMode == SkiaSelectionMode.None) return;

        var oldSelectedItems = _selectedItems.ToList();

        if (_selectionMode == SkiaSelectionMode.Single)
        {
            _selectedItems.Clear();
            _selectedItems.Add(item);
            _selectedItem = item;

            // Find index
            for (int i = 0; i < ItemCount; i++)
            {
                if (GetItemAt(i) == item)
                {
                    _selectedIndex = i;
                    break;
                }
            }
        }
        else // Multiple
        {
            if (_selectedItems.Contains(item))
            {
                _selectedItems.Remove(item);
                if (_selectedItem == item)
                {
                    _selectedItem = _selectedItems.FirstOrDefault();
                }
            }
            else
            {
                _selectedItems.Add(item);
                _selectedItem = item;
            }

            _selectedIndex = _selectedItem != null ? GetIndexOf(_selectedItem) : -1;
        }

        SelectionChanged?.Invoke(this, new CollectionSelectionChangedEventArgs(oldSelectedItems, _selectedItems.ToList()));
        Invalidate();
    }

    private int GetIndexOf(object item)
    {
        for (int i = 0; i < ItemCount; i++)
        {
            if (GetItemAt(i) == item)
                return i;
        }
        return -1;
    }

    private void ClearSelection()
    {
        var oldItems = _selectedItems.ToList();
        _selectedItems.Clear();
        _selectedItem = null;
        _selectedIndex = -1;

        if (oldItems.Count > 0)
        {
            SelectionChanged?.Invoke(this, new CollectionSelectionChangedEventArgs(oldItems, new List<object>()));
        }
    }

    protected override void OnItemTapped(int index, object item)
    {
        if (_selectionMode != SkiaSelectionMode.None)
        {
            SelectItem(item);
        }

        base.OnItemTapped(index, item);
    }

    protected override void DrawItem(SKCanvas canvas, object item, int index, SKRect bounds, SKPaint paint)
    {
        bool isSelected = _selectedItems.Contains(item);

        // Draw separator (only for vertical list layout)
        if (_orientation == ItemsLayoutOrientation.Vertical && _spanCount == 1)
        {
            paint.Color = new SKColor(0xE0, 0xE0, 0xE0);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 1;
            canvas.DrawLine(bounds.Left, bounds.Bottom, bounds.Right, bounds.Bottom, paint);
        }

        // Try to use ItemViewCreator for templated rendering (from DataTemplate)
        if (ItemViewCreator != null)
        {
            // Get or create cached view for this index
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
                    // Measure with large height to get natural size
                    var availableSize = new SKSize(bounds.Width, float.MaxValue);
                    var measuredSize = itemView.Measure(availableSize);

                    // Cap measured height - if item returns infinity/MaxValue, use ItemHeight as default
                    // This happens with Star-sized Grids that have no natural height preference
                    var rawHeight = measuredSize.Height;
                    if (float.IsNaN(rawHeight) || float.IsInfinity(rawHeight) || rawHeight > 10000)
                    {
                        rawHeight = ItemHeight;
                    }
                    // Ensure minimum height
                    var measuredHeight = Math.Max(rawHeight, ItemHeight);
                    if (!_itemHeights.TryGetValue(index, out var cachedHeight) || Math.Abs(cachedHeight - measuredHeight) > 1)
                    {
                        _itemHeights[index] = measuredHeight;
                        _heightsChangedDuringDraw = true; // Flag for redraw with correct positions
                    }

                    // Arrange with the actual measured height
                    var actualBounds = new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Top + measuredHeight);
                    itemView.Arrange(actualBounds);
                    itemView.Draw(canvas);

                    // Draw selection highlight ON TOP of the item content (semi-transparent overlay)
                    if (isSelected)
                    {
                        paint.Color = SelectionColor;
                        paint.Style = SKPaintStyle.Fill;
                        canvas.DrawRoundRect(actualBounds, 12, 12, paint);
                    }

                    // Draw checkmark for selected items in multiple selection mode
                    if (isSelected && _selectionMode == SkiaSelectionMode.Multiple)
                    {
                        DrawCheckmark(canvas, new SKRect(actualBounds.Right - 32, actualBounds.MidY - 8, actualBounds.Right - 16, actualBounds.MidY + 8));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SkiaCollectionView.DrawItem] EXCEPTION: {ex.Message}\n{ex.StackTrace}");
                }
                return;
            }
        }

        // Use custom renderer if provided
        if (ItemRenderer != null)
        {
            if (ItemRenderer(item, index, bounds, canvas, paint))
                return;
        }

        // Default rendering - fall back to ToString
        paint.Color = SKColors.Black;
        paint.Style = SKPaintStyle.Fill;

        using var font = new SKFont(SKTypeface.Default, 14);
        using var textPaint = new SKPaint(font)
        {
            Color = SKColors.Black,
            IsAntialias = true
        };

        var text = item?.ToString() ?? "";
        var textBounds = new SKRect();
        textPaint.MeasureText(text, ref textBounds);

        var x = bounds.Left + 16;
        var y = bounds.MidY - textBounds.MidY;
        canvas.DrawText(text, x, y, textPaint);

        // Draw checkmark for selected items in multiple selection mode
        if (isSelected && _selectionMode == SkiaSelectionMode.Multiple)
        {
            DrawCheckmark(canvas, new SKRect(bounds.Right - 32, bounds.MidY - 8, bounds.Right - 16, bounds.MidY + 8));
        }
    }

    private void DrawCheckmark(SKCanvas canvas, SKRect bounds)
    {
        using var paint = new SKPaint
        {
            Color = new SKColor(0x21, 0x96, 0xF3),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        };

        using var path = new SKPath();
        path.MoveTo(bounds.Left, bounds.MidY);
        path.LineTo(bounds.MidX - 2, bounds.Bottom - 2);
        path.LineTo(bounds.Right, bounds.Top + 2);

        canvas.DrawPath(path, paint);
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        // Reset the heights-changed flag at the start of each draw
        _heightsChangedDuringDraw = false;

        // Draw background if set
        if (BackgroundColor != SKColors.Transparent)
        {
            using var bgPaint = new SKPaint
            {
                Color = BackgroundColor,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(bounds, bgPaint);
        }

        // Draw header if present
        if (_header != null && _headerHeight > 0)
        {
            var headerRect = new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Top + _headerHeight);
            DrawHeader(canvas, headerRect);
        }

        // Draw footer if present
        if (_footer != null && _footerHeight > 0)
        {
            var footerRect = new SKRect(bounds.Left, bounds.Bottom - _footerHeight, bounds.Right, bounds.Bottom);
            DrawFooter(canvas, footerRect);
        }

        // Adjust content bounds for header/footer
        var contentBounds = new SKRect(
            bounds.Left,
            bounds.Top + _headerHeight,
            bounds.Right,
            bounds.Bottom - _footerHeight);

        // Draw items
        if (ItemCount == 0)
        {
            DrawEmptyView(canvas, contentBounds);
            return;
        }

        // Use grid layout if spanCount > 1
        if (_spanCount > 1)
        {
            DrawGridItems(canvas, contentBounds);
        }
        else
        {
            DrawListItems(canvas, contentBounds);
        }

        // If heights changed during this draw, schedule a redraw with correct positions
        // This will queue another frame to be drawn with the correct cached heights
        if (_heightsChangedDuringDraw)
        {
            _heightsChangedDuringDraw = false;
            Invalidate();
        }
    }

    private void DrawListItems(SKCanvas canvas, SKRect bounds)
    {
        // Standard list drawing with variable item heights
        canvas.Save();
        canvas.ClipRect(bounds);

        using var paint = new SKPaint { IsAntialias = true };

        var scrollOffset = GetScrollOffset();

        // Find first visible item by walking through items
        int firstVisible = 0;
        float cumulativeOffset = 0;
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

        // Draw visible items using variable heights
        float currentY = bounds.Top + GetItemOffset(firstVisible) - scrollOffset;
        for (int i = firstVisible; i < ItemCount; i++)
        {
            var itemH = GetItemHeight(i);
            var itemRect = new SKRect(bounds.Left, currentY, bounds.Right - 8, currentY + itemH);

            // Stop if we've passed the visible area
            if (itemRect.Top > bounds.Bottom)
                break;

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

        // Draw scrollbar
        var totalHeight = TotalContentHeight;
        if (totalHeight > bounds.Height)
        {
            DrawScrollBarInternal(canvas, bounds, scrollOffset, totalHeight);
        }
    }

    private void DrawGridItems(SKCanvas canvas, SKRect bounds)
    {
        canvas.Save();
        canvas.ClipRect(bounds);

        using var paint = new SKPaint { IsAntialias = true };

        var cellWidth = (bounds.Width - 8) / _spanCount; // -8 for scrollbar
        var cellHeight = ItemHeight;
        var rowCount = (int)Math.Ceiling((double)ItemCount / _spanCount);
        var totalHeight = rowCount * (cellHeight + ItemSpacing) - ItemSpacing;

        var scrollOffset = GetScrollOffset();
        var firstVisibleRow = Math.Max(0, (int)(scrollOffset / (cellHeight + ItemSpacing)));
        var lastVisibleRow = Math.Min(rowCount - 1,
            (int)((scrollOffset + bounds.Height) / (cellHeight + ItemSpacing)) + 1);

        for (int row = firstVisibleRow; row <= lastVisibleRow; row++)
        {
            var rowY = bounds.Top + (row * (cellHeight + ItemSpacing)) - scrollOffset;

            for (int col = 0; col < _spanCount; col++)
            {
                var index = row * _spanCount + col;
                if (index >= ItemCount) break;

                var cellX = bounds.Left + col * cellWidth;
                var cellRect = new SKRect(cellX + 2, rowY, cellX + cellWidth - 2, rowY + cellHeight);

                if (cellRect.Bottom < bounds.Top || cellRect.Top > bounds.Bottom)
                    continue;

                var item = GetItemAt(index);
                if (item != null)
                {
                    // Draw cell background
                    using var cellBgPaint = new SKPaint
                    {
                        Color = _selectedItems.Contains(item) ? SelectionColor : new SKColor(0xFA, 0xFA, 0xFA),
                        Style = SKPaintStyle.Fill
                    };
                    canvas.DrawRoundRect(new SKRoundRect(cellRect, 4), cellBgPaint);

                    DrawItem(canvas, item, index, cellRect, paint);
                }
            }
        }

        canvas.Restore();

        // Draw scrollbar
        if (totalHeight > bounds.Height)
        {
            DrawScrollBarInternal(canvas, bounds, scrollOffset, totalHeight);
        }
    }

    private void DrawScrollBarInternal(SKCanvas canvas, SKRect bounds, float scrollOffset, float totalHeight)
    {
        var scrollBarWidth = 6f;
        var scrollBarMargin = 2f;

        // Draw scrollbar track (subtle)
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
        canvas.DrawRoundRect(new SKRoundRect(trackRect, 3), trackPaint);

        // Calculate thumb position and size
        var maxOffset = Math.Max(0, totalHeight - bounds.Height);
        var viewportRatio = bounds.Height / totalHeight;
        var availableTrackHeight = trackRect.Height;
        var thumbHeight = Math.Max(30, availableTrackHeight * viewportRatio);
        var scrollRatio = maxOffset > 0 ? scrollOffset / maxOffset : 0;
        var thumbY = trackRect.Top + (availableTrackHeight - thumbHeight) * scrollRatio;

        var thumbRect = new SKRect(
            trackRect.Left,
            thumbY,
            trackRect.Right,
            thumbY + thumbHeight);

        // Draw thumb with more visible color
        using var thumbPaint = new SKPaint
        {
            Color = new SKColor(100, 100, 100, 180),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        canvas.DrawRoundRect(new SKRoundRect(thumbRect, 3), thumbPaint);
    }

    private float GetScrollOffset()
    {
        // Access base class scroll offset through reflection or expose it
        // For now, use the field directly through internal access
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

        // Draw header text
        var text = _header?.ToString() ?? "";
        if (!string.IsNullOrEmpty(text))
        {
            using var font = new SKFont(SKTypeface.Default, 16);
            using var textPaint = new SKPaint(font)
            {
                Color = SKColors.Black,
                IsAntialias = true
            };

            var textBounds = new SKRect();
            textPaint.MeasureText(text, ref textBounds);

            var x = bounds.Left + 16;
            var y = bounds.MidY - textBounds.MidY;
            canvas.DrawText(text, x, y, textPaint);
        }

        // Draw separator
        using var sepPaint = new SKPaint
        {
            Color = new SKColor(0xE0, 0xE0, 0xE0),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1
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

        // Draw separator
        using var sepPaint = new SKPaint
        {
            Color = new SKColor(0xE0, 0xE0, 0xE0),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1
        };
        canvas.DrawLine(bounds.Left, bounds.Top, bounds.Right, bounds.Top, sepPaint);

        // Draw footer text
        var text = _footer?.ToString() ?? "";
        if (!string.IsNullOrEmpty(text))
        {
            using var font = new SKFont(SKTypeface.Default, 14);
            using var textPaint = new SKPaint(font)
            {
                Color = new SKColor(0x80, 0x80, 0x80),
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
