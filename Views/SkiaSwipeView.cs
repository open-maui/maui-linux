// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// SwipeItem is the element type of the public item lists (LeftItems, ...) until OpenMaui 11.0,
// when SkiaSwipeItem replaces it; the swipe events carry the legacy event args until then.
#pragma warning disable CS0618

using SkiaSharp;
using Microsoft.Maui.Platform.Linux.Rendering;

namespace Microsoft.Maui.Platform;

/// <summary>
/// A view that supports swipe gestures to reveal actions. Swiping right reveals the left items,
/// left the right items, down the top items and up the bottom items, as MAUI's SwipeView does.
/// The content slides by <see cref="SwipeOffset"/> (drawn translated, as MAUI's platforms
/// translate the content view, so its frame does not change).
/// </summary>
public class SkiaSwipeView : SkiaLayoutView, IPointerDragInterceptor
{
    /// <summary>MAUI's width of a swipe item (SwipeViewExtensions.SwipeItemWidth).</summary>
    private const float SwipeItemWidth = 100f;

    /// <summary>MAUI's trigger threshold when none is set: 60% of the open distance.</summary>
    private const float OpenSwipeThresholdPercentage = 0.6f;

    /// <summary>The distance a press must move before it becomes a swipe.</summary>
    private const float SwipeStartDistance = 10f;

    private SkiaView? _content;
    private readonly List<SwipeItem> _leftItems = new();
    private readonly List<SwipeItem> _rightItems = new();
    private readonly List<SwipeItem> _topItems = new();
    private readonly List<SwipeItem> _bottomItems = new();
    private readonly Dictionary<OpenSwipeItem, Microsoft.Maui.SwipeMode> _itemsModes = new();
    private readonly Dictionary<OpenSwipeItem, SwipeBehaviorOnInvoked> _itemsBehaviors = new();

    private float _swipeOffset = 0f;
    private Microsoft.Maui.SwipeDirection? _activeDirection = null;
    private bool _isSwiping = false;
    private bool _swipeStarted = false;
    private float _swipeStartX;
    private float _swipeStartY;
    private float _swipeStartOffset;
    private bool _isOpen = false;
    private Rect? _arranging;


    /// <summary>
    /// Gets or sets the content view.
    /// </summary>
    public SkiaView? Content
    {
        get => _content;
        set
        {
            if (_content != value)
            {
                if (_content != null)
                {
                    RemoveChild(_content);
                }

                _content = value;

                if (_content != null)
                {
                    AddChild(_content);
                }

                InvalidateMeasure();
                Invalidate();
            }
        }
    }

    /// <summary>
    /// Gets the left swipe items.
    /// </summary>
    public IList<SwipeItem> LeftItems => _leftItems;

    /// <summary>
    /// Gets the right swipe items.
    /// </summary>
    public IList<SwipeItem> RightItems => _rightItems;

    /// <summary>
    /// Gets the top swipe items.
    /// </summary>
    public IList<SwipeItem> TopItems => _topItems;

    /// <summary>
    /// Gets the bottom swipe items.
    /// </summary>
    public IList<SwipeItem> BottomItems => _bottomItems;

    private Microsoft.Maui.SwipeMode _mode = Microsoft.Maui.SwipeMode.Reveal;

    /// <summary>
    /// Gets or sets the swipe mode of the items whose own mode is not set.
    /// </summary>
    [Obsolete("Use MauiMode, which takes Microsoft.Maui.SwipeMode; this property is removed in OpenMaui 11.0.")]
    public SwipeMode Mode
    {
        get => _mode.ToLegacy();
        set => _mode = value.ToMaui();
    }

    /// <summary>
    /// Gets or sets the swipe mode of the items whose own mode is not set
    /// (<see cref="SetItemsMode(OpenSwipeItem, Microsoft.Maui.SwipeMode)"/>).
    /// </summary>
    public Microsoft.Maui.SwipeMode MauiMode
    {
        get => _mode;
        set => _mode = value;
    }

    /// <summary>
    /// The distance the left items open to. 0 (the default) computes it as MAUI does: the
    /// items' widths in Reveal mode, 80% of the content in Execute mode.
    /// </summary>
    public float LeftSwipeThreshold { get; set; }

    /// <summary>
    /// The distance the right items open to. 0 (the default) computes it as MAUI does: the
    /// items' widths in Reveal mode, 80% of the content in Execute mode.
    /// </summary>
    public float RightSwipeThreshold { get; set; }

    /// <summary>
    /// MAUI's <c>SwipeView.Threshold</c>: how far a swipe must go for the items to open (or, in
    /// Execute mode, to run). 0 means 60% of the open distance; a larger value than the open
    /// distance is capped to it.
    /// </summary>
    public float Threshold { get; set; }

    /// <summary>
    /// How the items appear: Reveal keeps them in place under the content as it slides away,
    /// Drag moves them in with the content.
    /// </summary>
    public SwipeTransitionMode TransitionMode { get; set; } = SwipeTransitionMode.Reveal;

    /// <summary>True while a side's items are shown (opened by a swipe or <see cref="Open(Microsoft.Maui.SwipeDirection)"/>).</summary>
    public bool IsOpen => _isOpen;

    /// <summary>
    /// How far the content is moved: positive to the right (left items) or down (top items),
    /// negative to the left (right items) or up (bottom items).
    /// </summary>
    public float SwipeOffset => _swipeOffset;

    /// <summary>
    /// Event raised when swipe is started.
    /// </summary>
    public event EventHandler<SwipeStartedEventArgs>? SwipeStarted;

    /// <summary>Raised as a swipe moves the content (MAUI's SwipeChanging).</summary>
    public event EventHandler<SwipeOffsetChangedEventArgs>? SwipeChanging;

    /// <summary>
    /// Event raised when swipe ends.
    /// </summary>
    public event EventHandler<SwipeEndedEventArgs>? SwipeEnded;

    /// <summary>Raised when <see cref="IsOpen"/> changes.</summary>
    public event EventHandler? IsOpenChanged;

    /// <summary>The mode of a side's items (MAUI's SwipeItems.Mode).</summary>
    [Obsolete("Use the overload that takes Microsoft.Maui.SwipeMode; this overload is removed in OpenMaui 11.0.")]
    public void SetItemsMode(OpenSwipeItem items, SwipeMode mode) => SetItemsMode(items, mode.ToMaui());

    /// <summary>The mode of a side's items: its own, or <see cref="Mode"/>.</summary>
    [Obsolete("Use GetMauiItemsMode, which returns Microsoft.Maui.SwipeMode; this method is removed in OpenMaui 11.0.")]
    public SwipeMode GetItemsMode(OpenSwipeItem items) => GetMauiItemsMode(items).ToLegacy();

    /// <summary>The mode of a side's items (MAUI's SwipeItems.Mode).</summary>
    public void SetItemsMode(OpenSwipeItem items, Microsoft.Maui.SwipeMode mode)
    {
        _itemsModes[items] = mode;
        OnItemsChanged();
    }

    /// <summary>The mode of a side's items: its own, or <see cref="MauiMode"/>.</summary>
    public Microsoft.Maui.SwipeMode GetMauiItemsMode(OpenSwipeItem items) =>
        _itemsModes.TryGetValue(items, out var mode) ? mode : _mode;

    /// <summary>What happens after a side's item is invoked (MAUI's SwipeItems.SwipeBehaviorOnInvoked).</summary>
    public void SetItemsBehaviorOnInvoked(OpenSwipeItem items, SwipeBehaviorOnInvoked behavior) =>
        _itemsBehaviors[items] = behavior;

    /// <summary>What happens after a side's item is invoked; Auto (the default) closes the view.</summary>
    public SwipeBehaviorOnInvoked GetItemsBehaviorOnInvoked(OpenSwipeItem items) =>
        _itemsBehaviors.TryGetValue(items, out var behavior) ? behavior : SwipeBehaviorOnInvoked.Auto;

    /// <summary>
    /// Opens the swipe view in the specified direction (Right shows the left items, Left the right
    /// items, Down the top items, Up the bottom items). A side without visible items stays closed.
    /// </summary>
    public void Open(Microsoft.Maui.SwipeDirection direction) => OpenTo(direction);

    /// <summary>
    /// Opens the swipe view in the specified direction (Right shows the left items, Left the right
    /// items, Down the top items, Up the bottom items). A side without visible items stays closed.
    /// </summary>
    [Obsolete("Use the overload that takes Microsoft.Maui.SwipeDirection; this overload is removed in OpenMaui 11.0.")]
    public void Open(SwipeDirection direction) => OpenTo(direction.ToMaui());

    private void OpenTo(Microsoft.Maui.SwipeDirection? direction)
    {
        if (direction == null)
            return;

        var items = VisibleItems(direction);
        if (items.Count == 0)
            return;

        _activeDirection = direction;
        SetOffset(SignOf(direction) * OpenDistance(direction));
        SetIsOpen(true);
    }

    /// <summary>Opens the given items (MAUI's <c>SwipeView.Open(OpenSwipeItem)</c>).</summary>
    public void Open(OpenSwipeItem items) => OpenTo(MauiDirectionOf(items));

    /// <summary>
    /// Closes the swipe view.
    /// </summary>
    public void Close()
    {
        _activeDirection = null;
        SetOffset(0);
        SetIsOpen(false);
    }

    /// <summary>The swipe direction that shows the given items.</summary>
    [Obsolete("Use MauiDirectionOf, which returns Microsoft.Maui.SwipeDirection; this method is removed in OpenMaui 11.0.")]
    public static SwipeDirection DirectionOf(OpenSwipeItem items) => MauiDirectionOf(items).ToLegacy();

    /// <summary>The swipe direction that shows the given items (null for a value that is not an <see cref="OpenSwipeItem"/> member).</summary>
    public static Microsoft.Maui.SwipeDirection? MauiDirectionOf(OpenSwipeItem items) => items switch
    {
        OpenSwipeItem.LeftItems => Microsoft.Maui.SwipeDirection.Right,
        OpenSwipeItem.RightItems => Microsoft.Maui.SwipeDirection.Left,
        OpenSwipeItem.TopItems => Microsoft.Maui.SwipeDirection.Down,
        OpenSwipeItem.BottomItems => Microsoft.Maui.SwipeDirection.Up,
        _ => null,
    };

    private static OpenSwipeItem ItemsOf(Microsoft.Maui.SwipeDirection? direction) => direction switch
    {
        Microsoft.Maui.SwipeDirection.Left => OpenSwipeItem.RightItems,
        Microsoft.Maui.SwipeDirection.Down => OpenSwipeItem.TopItems,
        Microsoft.Maui.SwipeDirection.Up => OpenSwipeItem.BottomItems,
        _ => OpenSwipeItem.LeftItems,
    };

    private static bool IsHorizontal(Microsoft.Maui.SwipeDirection? direction) =>
        direction is Microsoft.Maui.SwipeDirection.Left or Microsoft.Maui.SwipeDirection.Right;

    private static float SignOf(Microsoft.Maui.SwipeDirection? direction) =>
        direction is Microsoft.Maui.SwipeDirection.Right or Microsoft.Maui.SwipeDirection.Down ? 1f : -1f;

    private List<SwipeItem> ItemsFor(Microsoft.Maui.SwipeDirection? direction) => direction switch
    {
        Microsoft.Maui.SwipeDirection.Right => _leftItems,
        Microsoft.Maui.SwipeDirection.Left => _rightItems,
        Microsoft.Maui.SwipeDirection.Down => _topItems,
        Microsoft.Maui.SwipeDirection.Up => _bottomItems,
        _ => new List<SwipeItem>(),
    };

    private List<SwipeItem> VisibleItems(Microsoft.Maui.SwipeDirection? direction) => Visible(ItemsFor(direction));

    private void SetIsOpen(bool isOpen)
    {
        if (_isOpen == isOpen)
            return;
        _isOpen = isOpen;
        IsOpenChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetOffset(float offset)
    {
        if (_swipeOffset == offset)
            return;
        _swipeOffset = offset;
        Invalidate();
    }

    /// <summary>
    /// Call after changing the items (or an item's visibility): an open view opens to the new
    /// items' size, or closes when the side has none left.
    /// </summary>
    internal void OnItemsChanged()
    {
        if (_isOpen && _activeDirection != null)
        {
            // The open distance follows the items (an item hidden or added while open).
            var items = VisibleItems(_activeDirection);
            if (items.Count == 0)
                Close();
            else
                SetOffset(SignOf(_activeDirection) * OpenDistance(_activeDirection));
        }
        Invalidate();
    }

    /// <summary>The content's size along a swipe direction's axis.</summary>
    private float ContentExtent(Microsoft.Maui.SwipeDirection? direction)
    {
        var bounds = _arranging ?? Bounds;
        return (float)(IsHorizontal(direction) ? bounds.Width : bounds.Height);
    }

    /// <summary>
    /// How far a side opens, as MAUI's SwipeView computes it: in Reveal mode the items' widths
    /// (a menu item is 100 wide, an item view its measured width) or, vertically, the content's
    /// height (an item view's measured height); in Execute mode 80% of the content (the items'
    /// size with item views). Never more than the content.
    /// </summary>
    private float OpenDistance(Microsoft.Maui.SwipeDirection? direction)
    {
        var explicitDistance = direction switch
        {
            Microsoft.Maui.SwipeDirection.Right => LeftSwipeThreshold,
            Microsoft.Maui.SwipeDirection.Left => RightSwipeThreshold,
            _ => 0f,
        };
        if (explicitDistance > 0)
            return explicitDistance;

        var items = VisibleItems(direction);
        if (items.Count == 0)
            return 0;

        bool horizontal = IsHorizontal(direction);
        float extent = ContentExtent(direction);
        float distance;

        if (GetMauiItemsMode(ItemsOf(direction)) == Microsoft.Maui.SwipeMode.Reveal)
        {
            if (horizontal)
            {
                distance = 0;
                foreach (var item in items)
                    distance += ItemWidth(item);
            }
            else
            {
                distance = ItemViewsHeight(items) ?? extent;
            }
        }
        else if (items.Exists(static i => i.Content != null))
        {
            distance = 0;
            foreach (var item in items)
                distance = horizontal ? distance + ItemWidth(item) : Math.Max(distance, ItemHeight(item, extent));
        }
        else
        {
            distance = extent * 0.8f;
        }

        // Before the first layout the content has no size: do not cap to it.
        return extent > 0 ? Math.Min(distance, extent) : distance;
    }

    private static float ItemWidth(SwipeItem item)
    {
        if (item.Content is { } view)
        {
            var size = view.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            if (size.Width > 0)
                return (float)size.Width;
        }
        return SwipeItemWidth;
    }

    private static float ItemHeight(SwipeItem item, float fallback)
    {
        if (item.Content is { } view)
        {
            var size = view.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            if (size.Height > 0)
                return (float)size.Height;
        }
        return fallback;
    }

    private static float? ItemViewsHeight(List<SwipeItem> items)
    {
        float? height = null;
        foreach (var item in items)
        {
            if (item.Content is { } view)
            {
                var size = view.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                height = Math.Max(height ?? 0, (float)size.Height);
            }
        }
        return height;
    }

    /// <summary>The trigger distance: <see cref="Threshold"/> (capped to the open distance) or 60% of it.</summary>
    private float TriggerDistance(Microsoft.Maui.SwipeDirection? direction)
    {
        var open = OpenDistance(direction);
        return Threshold > 0 ? Math.Min(Threshold, open) : OpenSwipeThresholdPercentage * open;
    }

    /// <summary>
    /// The rectangles of a side's visible items at the full open distance: from the edge they are
    /// revealed at, in item order. In Drag mode they are drawn moved with the content.
    /// </summary>
    private List<(SwipeItem Item, SKRect Rect)> ItemRects(Microsoft.Maui.SwipeDirection? direction, SKRect bounds)
    {
        var result = new List<(SwipeItem, SKRect)>();
        var items = VisibleItems(direction);
        if (items.Count == 0)
            return result;

        float open = OpenDistance(direction);
        bool execute = GetMauiItemsMode(ItemsOf(direction)) == Microsoft.Maui.SwipeMode.Execute;

        if (IsHorizontal(direction))
        {
            var widths = new float[items.Count];
            float total = 0;
            for (int i = 0; i < items.Count; i++)
            {
                widths[i] = execute && items[i].Content == null ? open / items.Count : ItemWidth(items[i]);
                total += widths[i];
            }
            float scale = total > 0 ? open / total : 1f;
            float x = direction == Microsoft.Maui.SwipeDirection.Right ? bounds.Left : bounds.Right - open;
            for (int i = 0; i < items.Count; i++)
            {
                float w = widths[i] * scale;
                result.Add((items[i], new SKRect(x, bounds.Top, x + w, bounds.Bottom)));
                x += w;
            }
        }
        else
        {
            float w = bounds.Width / items.Count;
            float top = direction == Microsoft.Maui.SwipeDirection.Down ? bounds.Top : bounds.Bottom - open;
            for (int i = 0; i < items.Count; i++)
            {
                float x = bounds.Left + i * w;
                result.Add((items[i], new SKRect(x, top, x + w, top + open)));
            }
        }
        return result;
    }

    /// <summary>Where the items of the open side are drawn: moved with the content in Drag mode.</summary>
    private SKPoint ItemsTranslation(Microsoft.Maui.SwipeDirection? direction)
    {
        if (TransitionMode != SwipeTransitionMode.Drag || direction == null)
            return SKPoint.Empty;
        float shift = _swipeOffset - SignOf(direction) * OpenDistance(direction);
        return IsHorizontal(direction) ? new SKPoint(shift, 0) : new SKPoint(0, shift);
    }

    /// <summary>The content's translation (the swipe offset along the active axis).</summary>
    private SKPoint ContentTranslation =>
        _activeDirection is Microsoft.Maui.SwipeDirection.Up or Microsoft.Maui.SwipeDirection.Down
            ? new SKPoint(0, _swipeOffset)
            : new SKPoint(_swipeOffset, 0);

    /// <summary>
    /// As large as its content (and the content's margin), as MAUI's SwipeView measures on every
    /// platform: in a CollectionView row, which measures at an unbounded height, it asked for
    /// all of it, and the row fell back to the default 44 px, cutting the content off.
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        SyncItemViews();

        if (_content == null)
            return Size.Zero;

        var margin = _content.Margin;
        var desired = _content.Measure(new Size(
            Math.Max(0, availableSize.Width - margin.HorizontalThickness),
            Math.Max(0, availableSize.Height - margin.VerticalThickness)));
        return new Size(desired.Width + margin.HorizontalThickness, desired.Height + margin.VerticalThickness);
    }

    protected override Rect ArrangeOverride(Rect bounds)
    {
        if (_content != null)
        {
            // The content keeps its place: the swipe offset is a translation when drawing and
            // hit-testing, as MAUI's platforms translate the content view.
            var margin = _content.Margin;
            var contentBounds = new Rect(
                bounds.Left + margin.Left,
                bounds.Top + margin.Top,
                Math.Max(0, bounds.Width - margin.HorizontalThickness),
                Math.Max(0, bounds.Height - margin.VerticalThickness));
            _content.Arrange(contentBounds);
        }

        // Bounds is set to the result after this returns: the items are laid out in the new rectangle.
        _arranging = bounds;
        try
        {
            SyncItemViews();
            var sk = new SKRect((float)bounds.Left, (float)bounds.Top, (float)bounds.Right, (float)bounds.Bottom);
            foreach (var direction in new[] { Microsoft.Maui.SwipeDirection.Right, Microsoft.Maui.SwipeDirection.Left, Microsoft.Maui.SwipeDirection.Down, Microsoft.Maui.SwipeDirection.Up })
            {
                foreach (var (item, rect) in ItemRects(direction, sk))
                {
                    item.Content?.Arrange(new Rect(rect.Left, rect.Top, rect.Width, rect.Height));
                }
            }

            // An open view stays open at the new size.
            if (_isOpen && _activeDirection != null)
                _swipeOffset = SignOf(_activeDirection) * OpenDistance(_activeDirection);
        }
        finally
        {
            _arranging = null;
        }

        return bounds;
    }

    /// <summary>
    /// The item views (SwipeItem.Content) are children of the swipe view (for layout, binding
    /// context and the visual tree); the content stays the first child.
    /// </summary>
    private void SyncItemViews()
    {
        var wanted = new List<SkiaView>();
        foreach (var list in new[] { _leftItems, _rightItems, _topItems, _bottomItems })
            foreach (var item in list)
                if (item.Content is { } view && !wanted.Contains(view))
                    wanted.Add(view);

        foreach (var child in Children.ToArray())
            if (!ReferenceEquals(child, _content) && !wanted.Contains(child))
                RemoveChild(child);

        foreach (var view in wanted)
        {
            if (ReferenceEquals(view.Parent, this))
                continue;
            if (view.Parent is SkiaLayoutView oldParent)
                oldParent.RemoveChild(view);
            else if (view.Parent != null)
                continue;
            AddChild(view);
        }
    }

    /// <summary>The items of a side that are shown (SwipeItem.IsVisible).</summary>
    private static List<SwipeItem> Visible(List<SwipeItem> items) =>
        items.TrueForAll(IsShown) ? items : items.FindAll(IsShown);

    /// <summary>An item is shown when it and its view (an item view's) are visible.</summary>
    private static bool IsShown(SwipeItem item) => item.IsVisible && (item.Content?.IsVisible ?? true);

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        canvas.Save();
        canvas.ClipRect(bounds);

        // The swipe items behind the content
        if (_swipeOffset != 0 && _activeDirection != null)
        {
            DrawSwipeItems(canvas, bounds, _activeDirection);
        }

        // The content, moved by the swipe
        if (_content != null)
        {
            var t = ContentTranslation;
            canvas.Save();
            canvas.Translate(t.X, t.Y);
            _content.Draw(canvas);
            canvas.Restore();
        }

        canvas.Restore();
    }

    private void DrawSwipeItems(SKCanvas canvas, SKRect bounds, Microsoft.Maui.SwipeDirection? direction)
    {
        var rects = ItemRects(direction, bounds);
        if (rects.Count == 0) return;

        var t = ItemsTranslation(direction);
        canvas.Save();
        canvas.Translate(t.X, t.Y);

        foreach (var (item, itemBounds) in rects)
        {
            if (item.Content is { } view)
            {
                canvas.Save();
                canvas.ClipRect(itemBounds);
                view.Draw(canvas);
                canvas.Restore();
                continue;
            }

            // Draw background
            using var bgPaint = new SKPaint
            {
                Color = item.GetBackgroundColorSK(),
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(itemBounds, bgPaint);

            DrawItemContent(canvas, item, itemBounds);
        }

        canvas.Restore();
    }

    /// <summary>The height of a menu item's icon (WinUI's AppBarButton content height).</summary>
    internal const float ItemIconSize = 16f;

    /// <summary>The space between a menu item's icon and its text.</summary>
    internal const float ItemIconSpacing = 4f;

    /// <summary>The size of a menu item's text (WinUI's AppBarButton label).</summary>
    internal const float ItemFontSize = 12f;

    /// <summary>
    /// Where a menu item's icon and text go, as Windows' SwipeItem (an AppBarButton) lays them
    /// out: the icon, 16 high, above the 12-point text, the two centred together in the item.
    /// </summary>
    internal static (SKRect? Icon, float? Baseline) ItemContentLayout(SwipeItem item, SKRect bounds, SKFontMetrics metrics)
    {
        var icon = item.Icon;
        bool hasIcon = icon is { Width: > 0, Height: > 0 };
        bool hasText = !string.IsNullOrEmpty(item.Text);
        float lineHeight = metrics.Descent - metrics.Ascent;

        float height = (hasIcon ? ItemIconSize : 0) + (hasIcon && hasText ? ItemIconSpacing : 0) + (hasText ? lineHeight : 0);
        float top = bounds.MidY - height / 2;

        SKRect? iconRect = null;
        if (hasIcon)
        {
            // As WinUI's Viewbox of fixed height: the icon keeps its aspect, within the item's width.
            float w = ItemIconSize * icon!.Width / icon.Height;
            float h = ItemIconSize;
            if (w > bounds.Width)
            {
                h *= bounds.Width / w;
                w = bounds.Width;
            }
            iconRect = SKRect.Create(bounds.MidX - w / 2, top + (ItemIconSize - h) / 2, w, h);
            top += ItemIconSize + (hasText ? ItemIconSpacing : 0);
        }

        float? baseline = hasText ? top - metrics.Ascent : null;
        return (iconRect, baseline);
    }

    private static void DrawItemContent(SKCanvas canvas, SwipeItem item, SKRect bounds)
    {
        using var font = SkiaFontFactory.Create(ItemFontSize);
        var (iconRect, baseline) = ItemContentLayout(item, bounds, font.Metrics);

        canvas.Save();
        canvas.ClipRect(bounds);

        if (iconRect is { } dest && item.Icon is { } icon)
        {
            using var image = SKImage.FromBitmap(icon);
            using var filter = item.IconTintColor is { } tint ? SKColorFilter.CreateBlendMode(tint.ToSKColor(), SKBlendMode.SrcIn) : null;
            using var iconPaint = new SKPaint { IsAntialias = true, ColorFilter = filter };
            canvas.DrawImage(image, dest, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), iconPaint);
        }

        if (baseline is { } y)
        {
            using var textPaint = new SKPaint { Color = item.GetTextColorSK(), IsAntialias = true };
            canvas.DrawText(item.Text, bounds.MidX, y, SKTextAlign.Center, font, textPaint);
        }

        canvas.Restore();
    }

    /// <summary>The item at a point, when the view is open and the point is on its items.</summary>
    private SwipeItem? ItemAt(float x, float y)
    {
        if (!_isOpen || _activeDirection == null)
            return null;

        var bounds = new SKRect((float)Bounds.Left, (float)Bounds.Top, (float)Bounds.Right, (float)Bounds.Bottom);
        var t = ItemsTranslation(_activeDirection);
        foreach (var (item, rect) in ItemRects(_activeDirection, bounds))
        {
            var r = rect;
            r.Offset(t.X, t.Y);
            if (r.Contains(x, y) && OnUncoveredSide(x, y))
                return item;
        }
        return null;
    }

    /// <summary>True when a point is outside the moved content (on the revealed area).</summary>
    private bool OnUncoveredSide(float x, float y) => _activeDirection switch
    {
        Microsoft.Maui.SwipeDirection.Right => x < Bounds.Left + _swipeOffset,
        Microsoft.Maui.SwipeDirection.Left => x > Bounds.Right + _swipeOffset,
        Microsoft.Maui.SwipeDirection.Down => y < Bounds.Top + _swipeOffset,
        Microsoft.Maui.SwipeDirection.Up => y > Bounds.Bottom + _swipeOffset,
        _ => false,
    };

    public override SkiaView? HitTest(float x, float y)
    {
        if (!IsVisible || !Bounds.Contains(x, y)) return null;

        // An open swipe view takes the pointer: a tap on an item invokes it, a tap on the
        // content closes the view (as MAUI's SwipeView does).
        if (_isOpen)
            return this;

        if (_content != null)
        {
            var t = ContentTranslation;
            var hit = _content.HitTestAt(x - t.X, y - t.Y);
            if (hit != null) return hit;
        }

        return this;
    }

    /// <summary>
    /// Invokes an item as MAUI's SwipeView does (when enabled), then closes the view unless the
    /// items' SwipeBehaviorOnInvoked is RemainOpen.
    /// </summary>
    private void InvokeItem(SwipeItem item, Microsoft.Maui.SwipeDirection? direction)
    {
        if (item.IsEnabled)
            item.OnInvoked();

        if (GetItemsBehaviorOnInvoked(ItemsOf(direction)) != SwipeBehaviorOnInvoked.RemainOpen)
            Close();
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        // Check for swipe item tap when open
        if (_isOpen)
        {
            var tappedItem = ItemAt(e.X, e.Y);
            if (tappedItem != null)
            {
                InvokeItem(tappedItem, _activeDirection);
                e.Handled = true;
                return;
            }
        }

        BeginTracking(e);

        base.OnPointerPressed(e);
    }

    private void BeginTracking(PointerEventArgs e)
    {
        _isSwiping = true;
        _swipeStarted = false;
        _swipeStartX = e.X;
        _swipeStartY = e.Y;
        _swipeStartOffset = _swipeOffset;
    }

    /// <summary>
    /// The direction a move from the press starts a swipe in: past the start distance, along its
    /// main axis, towards a side that has items (an open view keeps its direction).
    /// </summary>
    private Microsoft.Maui.SwipeDirection? SwipeCandidate(float deltaX, float deltaY)
    {
        if (_activeDirection != null)
        {
            float along = IsHorizontal(_activeDirection) ? deltaX : deltaY;
            return Math.Abs(along) > SwipeStartDistance ? _activeDirection : null;
        }

        Microsoft.Maui.SwipeDirection? candidate = null;
        if (Math.Abs(deltaX) > SwipeStartDistance && Math.Abs(deltaX) >= Math.Abs(deltaY))
            candidate = deltaX > 0 ? Microsoft.Maui.SwipeDirection.Right : Microsoft.Maui.SwipeDirection.Left;
        else if (Math.Abs(deltaY) > SwipeStartDistance && Math.Abs(deltaY) > Math.Abs(deltaX))
            candidate = deltaY > 0 ? Microsoft.Maui.SwipeDirection.Down : Microsoft.Maui.SwipeDirection.Up;

        return candidate != null && VisibleItems(candidate).Count > 0 ? candidate : null;
    }

    void IPointerDragInterceptor.OnDescendantPointerPressed(PointerEventArgs e)
    {
        if (IsEnabled)
            BeginTracking(e);
    }

    /// <summary>A press on the content becomes a swipe once it moves towards a side with items.</summary>
    bool IPointerDragInterceptor.ShouldInterceptDrag(PointerEventArgs e)
    {
        if (!_isSwiping || !IsEnabled)
            return false;
        return SwipeCandidate(e.X - _swipeStartX, e.Y - _swipeStartY) != null;
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        if (!_isSwiping) return;

        float deltaX = e.X - _swipeStartX;
        float deltaY = e.Y - _swipeStartY;

        // Determine swipe direction (an open view keeps its direction)
        if (_activeDirection == null)
            _activeDirection = SwipeCandidate(deltaX, deltaY);

        if (_activeDirection != null)
        {
            if (!_swipeStarted && (Math.Abs(deltaX) > SwipeStartDistance || Math.Abs(deltaY) > SwipeStartDistance))
            {
                _swipeStarted = true;
                SwipeStarted?.Invoke(this, new SwipeStartedEventArgs(_activeDirection.ToLegacy()));
            }

            bool horizontal = IsHorizontal(_activeDirection);
            float delta = horizontal ? deltaX : deltaY;

            // Clamp to the open side: between closed and fully open
            float open = SignOf(_activeDirection) * OpenDistance(_activeDirection);
            float offset = _swipeStartOffset + delta;
            offset = open > 0 ? Math.Clamp(offset, 0, open) : Math.Clamp(offset, open, 0);

            if (offset != _swipeOffset)
            {
                SetOffset(offset);
                SwipeChanging?.Invoke(this, new SwipeOffsetChangedEventArgs(_activeDirection ?? default, offset));
            }
            e.Handled = true;
        }

        base.OnPointerMoved(e);
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        if (!_isSwiping) return;

        _isSwiping = false;

        if (!_swipeStarted)
        {
            // A tap: on the content of an open view it closes the view.
            if (_isOpen)
            {
                Close();
                e.Handled = true;
            }
            base.OnPointerReleased(e);
            return;
        }

        var direction = _activeDirection;
        float trigger = TriggerDistance(direction);
        // As MAUI's SwipeView: the distance decides, not the speed.
        bool reached = trigger > 0 && Math.Abs(_swipeOffset) >= trigger;

        // MAUI reports the swipe as open when it went past the trigger distance.
        bool isOpen = reached;
        var items = VisibleItems(direction);
        var itemsSide = ItemsOf(direction);

        if (reached && GetMauiItemsMode(itemsSide) == Microsoft.Maui.SwipeMode.Execute)
        {
            // Execute mode runs the first visible item (one action per swipe, as MAUI).
            if (items.Count > 0)
            {
                OpenTo(direction);
                InvokeItem(items[0], direction);
            }
        }
        else if (reached)
        {
            OpenTo(direction);
        }
        else
        {
            Close();
        }

        SwipeEnded?.Invoke(this, new SwipeEndedEventArgs(direction.ToLegacy(), isOpen));
        if (!_isOpen)
            _activeDirection = null;

        base.OnPointerReleased(e);
    }
}

/// <summary>A swipe's direction and how far it has moved the content (MAUI's SwipeChanging).</summary>
public class SwipeOffsetChangedEventArgs : EventArgs
{
    [Obsolete("Use the constructor that takes Microsoft.Maui.SwipeDirection; this constructor is removed in OpenMaui 11.0.")]
    public SwipeOffsetChangedEventArgs(SwipeDirection direction, double offset)
    {
        Direction = direction;
        SwipeDirection = direction.ToMaui() ?? default;
        Offset = offset;
    }

    public SwipeOffsetChangedEventArgs(Microsoft.Maui.SwipeDirection swipeDirection, double offset)
    {
        SwipeDirection = swipeDirection;
        Direction = ((Microsoft.Maui.SwipeDirection?)swipeDirection).ToLegacy();
        Offset = offset;
    }

    [Obsolete("Use SwipeDirection (Microsoft.Maui.SwipeDirection); this property is removed in OpenMaui 11.0.")]
    public SwipeDirection Direction { get; }

    /// <summary>The direction of the swipe.</summary>
    public Microsoft.Maui.SwipeDirection SwipeDirection { get; }

    public double Offset { get; }
}
