// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using Microsoft.Maui.Platform.Linux.Rendering;

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
        propertyChanged: (b, o, n) => ((SkiaCollectionView)b).OnLayoutChanged());

    public static readonly BindableProperty SpanCountProperty = BindableProperty.Create(
        nameof(SpanCount),
        typeof(int),
        typeof(SkiaCollectionView),
        1,
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((SkiaCollectionView)b).OnLayoutChanged(),
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
        typeof(Color),
        typeof(SkiaCollectionView),
        Color.FromRgba(33, 150, 243, 89),
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((SkiaCollectionView)b).OnSelectionColorChanged((Color?)n));

    public static readonly BindableProperty HeaderBackgroundColorProperty = BindableProperty.Create(
        nameof(HeaderBackgroundColor),
        typeof(Color),
        typeof(SkiaCollectionView),
        Color.FromRgb(245, 245, 245),
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((SkiaCollectionView)b).OnHeaderBackgroundColorChanged((Color?)n));

    public static readonly BindableProperty FooterBackgroundColorProperty = BindableProperty.Create(
        nameof(FooterBackgroundColor),
        typeof(Color),
        typeof(SkiaCollectionView),
        Color.FromRgb(245, 245, 245),
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((SkiaCollectionView)b).OnFooterBackgroundColorChanged((Color?)n));

    #endregion

    private List<object> _selectedItems = new List<object>();
    private int _selectedIndex = -1;
    private bool _isSelectingItem;
    private bool _heightsChangedDuringDraw;

    // SKColor fields for rendering
    private SKColor _selectionColorSK = SkiaTheme.PrimarySelectionSK;
    private SKColor _headerBackgroundColorSK = SkiaTheme.Gray100SK;
    private SKColor _footerBackgroundColorSK = SkiaTheme.Gray100SK;

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
                if (item != null && item is not INonSelectableItem)
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

    private SkiaView? _headerView;
    private SkiaView? _footerView;
    private float _contentWidth;

    /// <summary>
    /// The header as a view (a View header, a HeaderTemplate's content, or a label for any other
    /// header), laid out at its content's height and scrolled with the items, as on every MAUI
    /// platform. Set by the handler; a string or object <see cref="Header"/> gets a label.
    /// </summary>
    public SkiaView? HeaderView
    {
        get => _headerView;
        set => _headerView = SwapSlotView(_headerView, value);
    }

    /// <summary>The footer as a view, after the last item; see <see cref="HeaderView"/>.</summary>
    public SkiaView? FooterView
    {
        get => _footerView;
        set => _footerView = SwapSlotView(_footerView, value);
    }

    private SkiaView? SwapSlotView(SkiaView? old, SkiaView? value)
    {
        if (ReferenceEquals(old, value))
            return old;
        if (old != null && ReferenceEquals(old.Parent, this))
            old.Parent = null;
        if (value != null)
            value.Parent = this;
        InvalidateMeasure();
        Invalidate();
        return value;
    }

    /// <inheritdoc />
    /// <remarks>A linear horizontal ItemsLayout: the items in a row, scrolled horizontally.</remarks>
    protected override bool IsHorizontal => Orientation == ItemsLayoutOrientation.Horizontal && SpanCount == 1;

    /// <summary>The orientation or span changed: rows are measured again along the new axis.</summary>
    private void OnLayoutChanged()
    {
        _itemHeights.Clear();
        _scrollOffset = 0;
        InvalidateMeasure();
        Invalidate();
    }

    /// <summary>
    /// The length a header or footer view takes along the scroll axis: its height across the
    /// list's content width, or its width across a horizontal list's height.
    /// </summary>
    private float SlotHeight(SkiaView? view)
    {
        if (view is not { IsVisible: true })
            return 0f;
        if (IsHorizontal)
        {
            var height = Bounds.Height > 0 ? (float)Bounds.Height : float.PositiveInfinity;
            var desired = view.Measure(new Size(double.PositiveInfinity, Math.Max(0, height - view.Margin.VerticalThickness)));
            return (float)(desired.Width + view.Margin.HorizontalThickness);
        }
        var width = _contentWidth > 0 ? _contentWidth : Math.Max(0f, (float)Bounds.Width - 8f);
        var size = view.Measure(new Size(width, double.PositiveInfinity));
        return (float)(size.Height + view.Margin.VerticalThickness);
    }

    /// <inheritdoc />
    protected override float NaturalCrossExtent(float height)
    {
        var cross = base.NaturalCrossExtent(height);
        foreach (var slot in new[] { _headerView, _footerView })
        {
            if (slot is not { IsVisible: true })
                continue;
            var size = slot.Measure(new Size(double.PositiveInfinity, height));
            cross = Math.Max(cross, (float)(size.Height + slot.Margin.VerticalThickness));
        }
        return cross;
    }

    /// <summary>Lays a header or footer out down a horizontal list's height at <paramref name="left"/> and draws it.</summary>
    private void DrawSlotHorizontal(SKCanvas canvas, SkiaView? view, float left, float top, float height)
    {
        if (view is not { IsVisible: true })
            return;
        var margin = view.Margin;
        var width = SlotHeight(view);
        view.Arrange(new Rect(left + margin.Left, top + margin.Top,
            Math.Max(0, width - margin.HorizontalThickness), Math.Max(0, height - margin.VerticalThickness)));
        view.Draw(canvas);
    }

    /// <inheritdoc />
    protected override float LeadingContentHeight => SlotHeight(_headerView);

    /// <inheritdoc />
    protected override float TrailingContentHeight => SlotHeight(_footerView);

    /// <summary>Lays a header or footer out across the content width at <paramref name="top"/> and draws it.</summary>
    private void DrawSlot(SKCanvas canvas, SkiaView? view, float left, float top, float width)
    {
        if (view is not { IsVisible: true })
            return;
        var margin = view.Margin;
        var height = SlotHeight(view);
        view.Arrange(new Rect(left + margin.Left, top + margin.Top,
            Math.Max(0, width - margin.HorizontalThickness), Math.Max(0, height - margin.VerticalThickness)));
        view.Draw(canvas);
    }

    /// <inheritdoc />
    public override SkiaView? HitTest(float x, float y)
    {
        // A button in the header or footer takes the pointer; taps elsewhere there select nothing.
        if (IsVisible && Bounds.Contains(x, y))
        {
            foreach (var slot in new[] { _headerView, _footerView })
            {
                if (slot is { IsVisible: true } && slot.Bounds.Contains(x, y) && slot.HitTestAt(x, y) is { } hit)
                    return hit;
            }
        }
        return base.HitTest(x, y);
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

    public Color SelectionColor
    {
        get => (Color)GetValue(SelectionColorProperty);
        set => SetValue(SelectionColorProperty, value);
    }

    /// <summary>Gets the SKColor for rendering selection highlight.</summary>
    internal SKColor SelectionColorSK => _selectionColorSK;

    private bool _showSeparators;
    private Color? _separatorColor;

    /// <summary>
    /// Row separators. A ListView draws them (SeparatorVisibility); a MAUI
    /// CollectionView has none on any platform, so they are off by default.
    /// </summary>
    public bool ShowSeparators
    {
        get => _showSeparators;
        set { if (_showSeparators != value) { _showSeparators = value; Invalidate(); } }
    }

    /// <summary>Separator colour (ListView.SeparatorColor); a theme hairline when null.</summary>
    public Color? SeparatorColor
    {
        get => _separatorColor;
        set { _separatorColor = value; Invalidate(); }
    }

    internal SKColor SeparatorColorSK => _separatorColor?.ToSKColor()
        ?? (SkiaTheme.IsDarkMode ? SkiaTheme.Gray700SK : SkiaTheme.Gray300SK);

    public Color HeaderBackgroundColor
    {
        get => (Color)GetValue(HeaderBackgroundColorProperty);
        set => SetValue(HeaderBackgroundColorProperty, value);
    }

    /// <summary>Gets the SKColor for rendering header background.</summary>
    internal SKColor HeaderBackgroundColorSK => _headerBackgroundColorSK;

    public Color FooterBackgroundColor
    {
        get => (Color)GetValue(FooterBackgroundColorProperty);
        set => SetValue(FooterBackgroundColorProperty, value);
    }

    /// <summary>Gets the SKColor for rendering footer background.</summary>
    internal SKColor FooterBackgroundColorSK => _footerBackgroundColorSK;

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
        if (SelectionMode == SkiaSelectionMode.None || _isSelectingItem)
            return;

        // Programmatic SelectedItem set: mirror it into the selection state
        // WITHOUT writing the property back. BindableObject queues re-entrant
        // sets of the same property until the outer set returns, so a
        // SetValue here would fire after the guard is released and ping-pong
        // with this callback forever.
        var previousSelection = _selectedItems.ToList();
        _selectedItems.Clear();
        if (newValue != null)
            _selectedItems.Add(newValue);
        _selectedIndex = newValue != null ? GetIndexOf(newValue) : -1;

        // SelectItem/ClearSelection update the list before writing the property,
        // so when they are the writer the state already matches: no second event.
        if (!previousSelection.SequenceEqual(_selectedItems))
            SelectionChanged?.Invoke(this, new CollectionSelectionChangedEventArgs(previousSelection, _selectedItems.ToList()));
        Invalidate();
    }

    private void OnHeaderChanged(object? newValue) => HeaderView = SlotViewFor(newValue);

    private void OnFooterChanged(object? newValue) => FooterView = SlotViewFor(newValue);

    /// <summary>A Skia view as it is; any other header or footer shows as text, as MAUI shows it.</summary>
    private static SkiaView? SlotViewFor(object? value) => value switch
    {
        null => null,
        SkiaView view => view,
        IView => null, // a MAUI view: the handler supplies its platform view
        _ => new SkiaLabel { Text = value.ToString() ?? string.Empty, Padding = new Thickness(16, 8) },
    };

    private void OnSelectionColorChanged(Color? newValue)
    {
        _selectionColorSK = newValue?.ToSKColor() ?? SkiaTheme.PrimarySelectionSK;
        Invalidate();
    }

    private void OnHeaderBackgroundColorChanged(Color? newValue)
    {
        _headerBackgroundColorSK = newValue?.ToSKColor() ?? SkiaTheme.Gray100SK;
        Invalidate();
    }

    private void OnFooterBackgroundColorChanged(Color? newValue)
    {
        _footerBackgroundColorSK = newValue?.ToSKColor() ?? SkiaTheme.Gray100SK;
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

        // A group header or footer row is tapped, not selected.
        if (item is INonSelectableItem)
        {
            RaiseItemTapped(index, item);
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

        if (ShowSeparators && Orientation == ItemsLayoutOrientation.Vertical && SpanCount == 1)
        {
            paint.Color = SeparatorColorSK;
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 1f;
            canvas.DrawLine(bounds.Left, bounds.Bottom, bounds.Right, bounds.Bottom, paint);
        }

        if (ItemViewCreator != null)
        {
            NoteItemDrawn(index);
            var itemView = GetOrCreateItemView(index);

            if (itemView != null)
            {
                try
                {
                    // The row is the item's cell: its margin is inside the row, as MAUI's cells
                    // hold it, and the item is placed across the row by its layout options.
                    var horizontal = IsHorizontal;
                    var margin = itemView.Margin;
                    var extent = MeasureItemExtent(itemView, horizontal ? bounds.Height : bounds.Width, out var desired);

                    // Store the actual measured length for row sizing
                    var cellHeight = Math.Max(extent, MinimumItemHeight);
                    if (!_itemHeights.TryGetValue(index, out var cachedHeight) || Math.Abs(cachedHeight - cellHeight) > 1f)
                    {
                        _itemHeights[index] = cellHeight;
                        _heightsChangedDuringDraw = true;
                    }

                    var area = new SKRect(bounds.Left + (float)margin.Left, bounds.Top + (float)margin.Top,
                        Math.Max(bounds.Left + (float)margin.Left, bounds.Right - (float)margin.Right),
                        Math.Max(bounds.Top + (float)margin.Top, bounds.Bottom - (float)margin.Bottom));
                    var across = SkiaPage.AlignContent(itemView, area, desired);
                    SKRect actualBounds;
                    if (horizontal)
                    {
                        var contentWidth = Math.Min((float)desired.Width, area.Width);
                        var offset = Math.Max(0, (area.Width - contentWidth) / 2);
                        actualBounds = new SKRect(area.Left + offset, (float)across.Y, area.Left + offset + contentWidth, (float)across.Bottom);
                    }
                    else
                    {
                        // Center the content along the row when the row is taller than it
                        // (a ListView's row height); a CollectionView's row is the item's height.
                        var contentHeight = Math.Min((float)desired.Height, area.Height);
                        var verticalOffset = Math.Max(0, (area.Height - contentHeight) / 2);
                        actualBounds = new SKRect((float)across.X, area.Top + verticalOffset, (float)across.Right, area.Top + verticalOffset + contentHeight);
                    }
                    SetRowOrigin(itemView, bounds.Left, bounds.Top);
                    itemView.Arrange(new Rect(actualBounds.Left, actualBounds.Top, actualBounds.Width, actualBounds.Height));
                    itemView.Draw(canvas);

                    if (isSelected)
                    {
                        paint.Color = SelectionColorSK;
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
                    DiagnosticLog.Error("SkiaCollectionView", "DrawItem EXCEPTION: " + ex.Message + "\n" + ex.StackTrace, ex);
                    return;
                }
            }
        }

        if (ItemRenderer != null && ItemRenderer(item, index, bounds, canvas, paint))
        {
            return;
        }

        paint.Color = SkiaTheme.TextPrimarySK;
        paint.Style = SKPaintStyle.Fill;

        using var font = SkiaFontFactory.Create(14f);
        using var textPaint = new SKPaint
        {
            Color = SkiaTheme.TextPrimarySK,
            IsAntialias = true
        };

        var text = item?.ToString() ?? "";

        var x = bounds.Left + 16f;
        var y = TextRenderingHelper.BaselineForVerticalCenter(font, bounds.MidY);
        canvas.DrawText(text, x, y, font, textPaint);

        if (isSelected && SelectionMode == SkiaSelectionMode.Multiple)
        {
            DrawCheckmark(canvas, new SKRect(bounds.Right - 32f, bounds.MidY - 8f, bounds.Right - 16f, bounds.MidY + 8f));
        }
    }

    private void DrawCheckmark(SKCanvas canvas, SKRect bounds)
    {
        using var paint = new SKPaint
        {
            Color = SkiaTheme.PrimarySK,
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

        if (BackgroundColor != null && BackgroundColor != Colors.Transparent)
        {
            using var bgPaint = new SKPaint
            {
                Color = GetEffectiveBackgroundColor(),
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(bounds, bgPaint);
        }

        _contentWidth = Math.Max(0f, bounds.Width - 8f);
        HeaderHeight = LeadingContentHeight;
        FooterHeight = TrailingContentHeight;

        if (ItemCount == 0 && IsHorizontal)
        {
            // The header at the start, the footer at the end, and the empty view between them.
            float headerWidth = HeaderHeight, footerWidth = FooterHeight;
            DrawSlotHorizontal(canvas, _headerView, bounds.Left, bounds.Top, bounds.Height);
            DrawSlotHorizontal(canvas, _footerView, bounds.Right - footerWidth, bounds.Top, bounds.Height);
            DrawEmptyView(canvas, new SKRect(bounds.Left + headerWidth, bounds.Top, bounds.Right - footerWidth, bounds.Bottom));
            return;
        }

        if (ItemCount == 0)
        {
            // The header at the top, the footer under it, and the empty view in the room left.
            float headerHeight = HeaderHeight, footerHeight = FooterHeight;
            DrawSlot(canvas, _headerView, bounds.Left, bounds.Top, _contentWidth);
            DrawSlot(canvas, _footerView, bounds.Left, bounds.Bottom - footerHeight, _contentWidth);
            DrawEmptyView(canvas, new SKRect(bounds.Left, bounds.Top + headerHeight, bounds.Right, bounds.Bottom - footerHeight));
            return;
        }

        var contentBounds = bounds;

        if (SpanCount > 1)
        {
            DrawGridItems(canvas, contentBounds);
        }
        else if (IsHorizontal)
        {
            DrawHorizontalListItems(canvas, contentBounds);
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

        // Row content width (matches the itemRect below). Measure each item before
        // using its height so the FIRST frame after an ItemsSource refresh positions
        // rows with real heights instead of the default ItemHeight - otherwise the
        // frame right after a refresh (e.g. OnAppearing after a navigation pop)
        // paints squashed rows and only corrects itself a frame later (visible flash).
        var contentWidth = bounds.Width - 8f;

        int firstVisible = 0;
        float cumulativeOffset = LeadingContentHeight;
        for (int i = 0; i < ItemCount; i++)
        {
            EnsureItemMeasured(i, contentWidth);
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
            EnsureItemMeasured(i, contentWidth);
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

        var total = TotalContentHeight;
        DrawSlot(canvas, _headerView, bounds.Left, bounds.Top - scrollOffset, contentWidth);
        DrawSlot(canvas, _footerView, bounds.Left, bounds.Top + total - TrailingContentHeight - scrollOffset, contentWidth);

        canvas.Restore();

        var totalHeight = total;
        if (totalHeight > bounds.Height)
        {
            DrawScrollBarInternal(canvas, bounds, scrollOffset, totalHeight);
        }
    }

    /// <summary>
    /// A horizontal linear list: items left to right across the list's height, the header
    /// before the first and the footer after the last, scrolled horizontally.
    /// </summary>
    private void DrawHorizontalListItems(SKCanvas canvas, SKRect bounds)
    {
        canvas.Save();
        canvas.ClipRect(bounds, SKClipOperation.Intersect, false);

        using var paint = new SKPaint
        {
            IsAntialias = true
        };

        var scrollOffset = GetScrollOffset();
        var height = bounds.Height;

        int firstVisible = 0;
        float cumulativeOffset = LeadingContentHeight;
        for (int i = 0; i < ItemCount; i++)
        {
            EnsureItemMeasured(i, height);
            var itemW = GetItemHeight(i);
            if (cumulativeOffset + itemW > scrollOffset)
            {
                firstVisible = i;
                break;
            }
            cumulativeOffset += itemW + ItemSpacing;
        }

        float currentX = bounds.Left + GetItemOffset(firstVisible) - scrollOffset;
        for (int i = firstVisible; i < ItemCount; i++)
        {
            EnsureItemMeasured(i, height);
            var itemW = GetItemHeight(i);
            var itemRect = new SKRect(currentX, bounds.Top, currentX + itemW, bounds.Bottom);

            if (itemRect.Left > bounds.Right)
            {
                break;
            }

            if (itemRect.Right >= bounds.Left)
            {
                var item = GetItemAt(i);
                if (item != null)
                {
                    DrawItem(canvas, item, i, itemRect, paint);
                }
            }

            currentX += itemW + ItemSpacing;
        }

        var total = TotalContentHeight;
        DrawSlotHorizontal(canvas, _headerView, bounds.Left - scrollOffset, bounds.Top, height);
        DrawSlotHorizontal(canvas, _footerView, bounds.Left + total - TrailingContentHeight - scrollOffset, bounds.Top, height);

        canvas.Restore();

        if (total > bounds.Width)
        {
            DrawHorizontalScrollBar(canvas, bounds, scrollOffset, total);
        }
    }

    private void DrawHorizontalScrollBar(SKCanvas canvas, SKRect bounds, float scrollOffset, float totalWidth)
    {
        const float scrollBarHeight = 6f, scrollBarMargin = 2f;
        var trackRect = new SKRect(bounds.Left + scrollBarMargin, bounds.Bottom - scrollBarHeight - scrollBarMargin,
            bounds.Right - scrollBarMargin, bounds.Bottom - scrollBarMargin);

        using var trackPaint = new SKPaint { Color = SkiaTheme.Shadow10SK, Style = SKPaintStyle.Fill };
        canvas.DrawRoundRect(new SKRoundRect(trackRect, 3f), trackPaint);

        var maxOffset = Math.Max(0f, totalWidth - bounds.Width);
        var thumbWidth = Math.Max(30f, trackRect.Width * (bounds.Width / totalWidth));
        var scrollRatio = maxOffset > 0f ? scrollOffset / maxOffset : 0f;
        var thumbX = trackRect.Left + (trackRect.Width - thumbWidth) * scrollRatio;

        using var thumbPaint = new SKPaint { Color = SkiaTheme.ScrollbarThumbSK, Style = SKPaintStyle.Fill, IsAntialias = true };
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(thumbX, trackRect.Top, thumbX + thumbWidth, trackRect.Bottom), 3f), thumbPaint);
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
        var leading = LeadingContentHeight;
        var firstVisibleRow = Math.Max(0, (int)((scrollOffset - leading) / (cellHeight + ItemSpacing)));
        var lastVisibleRow = Math.Min(rowCount - 1, (int)((scrollOffset - leading + bounds.Height) / (cellHeight + ItemSpacing)) + 1);
        DrawSlot(canvas, _headerView, bounds.Left, bounds.Top - scrollOffset, bounds.Width - 8f);
        DrawSlot(canvas, _footerView, bounds.Left, bounds.Top + leading + totalHeight - scrollOffset, bounds.Width - 8f);
        totalHeight += leading + TrailingContentHeight;

        for (int row = firstVisibleRow; row <= lastVisibleRow; row++)
        {
            var rowY = bounds.Top + leading + row * (cellHeight + ItemSpacing) - scrollOffset;

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
                        Color = _selectedItems.Contains(item) ? SelectionColorSK : SkiaTheme.Gray50SK,
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
            Color = SkiaTheme.Shadow10SK,
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
            Color = SkiaTheme.ScrollbarThumbSK,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        canvas.DrawRoundRect(new SKRoundRect(thumbRect, 3f), thumbPaint);
    }

    private float GetScrollOffset()
    {
        return _scrollOffset;
    }

}

/// <summary>
/// A row of an items view that is not an item (a group header or footer): it is drawn and can be
/// tapped, but is never selected.
/// </summary>
internal interface INonSelectableItem
{
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
