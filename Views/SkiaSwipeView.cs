// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
    private readonly Dictionary<OpenSwipeItem, SwipeMode> _itemsModes = new();
    private readonly Dictionary<OpenSwipeItem, SwipeBehaviorOnInvoked> _itemsBehaviors = new();

    private float _swipeOffset = 0f;
    private SwipeDirection _activeDirection = SwipeDirection.None;
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

    /// <summary>
    /// Gets or sets the swipe mode of the items whose own mode is not set
    /// (<see cref="SetItemsMode"/>).
    /// </summary>
    public SwipeMode Mode { get; set; } = SwipeMode.Reveal;

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

    /// <summary>True while a side's items are shown (opened by a swipe or <see cref="Open(SwipeDirection)"/>).</summary>
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
    public void SetItemsMode(OpenSwipeItem items, SwipeMode mode)
    {
        _itemsModes[items] = mode;
        OnItemsChanged();
    }

    /// <summary>The mode of a side's items: its own, or <see cref="Mode"/>.</summary>
    public SwipeMode GetItemsMode(OpenSwipeItem items) =>
        _itemsModes.TryGetValue(items, out var mode) ? mode : Mode;

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
    public void Open(SwipeDirection direction)
    {
        if (direction == SwipeDirection.None)
            return;

        var items = VisibleItems(direction);
        if (items.Count == 0)
            return;

        _activeDirection = direction;
        SetOffset(SignOf(direction) * OpenDistance(direction));
        SetIsOpen(true);
    }

    /// <summary>Opens the given items (MAUI's <c>SwipeView.Open(OpenSwipeItem)</c>).</summary>
    public void Open(OpenSwipeItem items) => Open(DirectionOf(items));

    /// <summary>
    /// Closes the swipe view.
    /// </summary>
    public void Close()
    {
        _activeDirection = SwipeDirection.None;
        SetOffset(0);
        SetIsOpen(false);
    }

    /// <summary>The swipe direction that shows the given items.</summary>
    public static SwipeDirection DirectionOf(OpenSwipeItem items) => items switch
    {
        OpenSwipeItem.LeftItems => SwipeDirection.Right,
        OpenSwipeItem.RightItems => SwipeDirection.Left,
        OpenSwipeItem.TopItems => SwipeDirection.Down,
        OpenSwipeItem.BottomItems => SwipeDirection.Up,
        _ => SwipeDirection.None,
    };

    private static OpenSwipeItem ItemsOf(SwipeDirection direction) => direction switch
    {
        SwipeDirection.Left => OpenSwipeItem.RightItems,
        SwipeDirection.Down => OpenSwipeItem.TopItems,
        SwipeDirection.Up => OpenSwipeItem.BottomItems,
        _ => OpenSwipeItem.LeftItems,
    };

    private static bool IsHorizontal(SwipeDirection direction) =>
        direction is SwipeDirection.Left or SwipeDirection.Right;

    private static float SignOf(SwipeDirection direction) =>
        direction is SwipeDirection.Right or SwipeDirection.Down ? 1f : -1f;

    private List<SwipeItem> ItemsFor(SwipeDirection direction) => direction switch
    {
        SwipeDirection.Right => _leftItems,
        SwipeDirection.Left => _rightItems,
        SwipeDirection.Down => _topItems,
        SwipeDirection.Up => _bottomItems,
        _ => new List<SwipeItem>(),
    };

    private List<SwipeItem> VisibleItems(SwipeDirection direction) => Visible(ItemsFor(direction));

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
        if (_isOpen && _activeDirection != SwipeDirection.None)
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
    private float ContentExtent(SwipeDirection direction)
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
    private float OpenDistance(SwipeDirection direction)
    {
        var explicitDistance = direction switch
        {
            SwipeDirection.Right => LeftSwipeThreshold,
            SwipeDirection.Left => RightSwipeThreshold,
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

        if (GetItemsMode(ItemsOf(direction)) == SwipeMode.Reveal)
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
    private float TriggerDistance(SwipeDirection direction)
    {
        var open = OpenDistance(direction);
        return Threshold > 0 ? Math.Min(Threshold, open) : OpenSwipeThresholdPercentage * open;
    }

    /// <summary>
    /// The rectangles of a side's visible items at the full open distance: from the edge they are
    /// revealed at, in item order. In Drag mode they are drawn moved with the content.
    /// </summary>
    private List<(SwipeItem Item, SKRect Rect)> ItemRects(SwipeDirection direction, SKRect bounds)
    {
        var result = new List<(SwipeItem, SKRect)>();
        var items = VisibleItems(direction);
        if (items.Count == 0)
            return result;

        float open = OpenDistance(direction);
        bool execute = GetItemsMode(ItemsOf(direction)) == SwipeMode.Execute;

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
            float x = direction == SwipeDirection.Right ? bounds.Left : bounds.Right - open;
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
            float top = direction == SwipeDirection.Down ? bounds.Top : bounds.Bottom - open;
            for (int i = 0; i < items.Count; i++)
            {
                float x = bounds.Left + i * w;
                result.Add((items[i], new SKRect(x, top, x + w, top + open)));
            }
        }
        return result;
    }

    /// <summary>Where the items of the open side are drawn: moved with the content in Drag mode.</summary>
    private SKPoint ItemsTranslation(SwipeDirection direction)
    {
        if (TransitionMode != SwipeTransitionMode.Drag || direction == SwipeDirection.None)
            return SKPoint.Empty;
        float shift = _swipeOffset - SignOf(direction) * OpenDistance(direction);
        return IsHorizontal(direction) ? new SKPoint(shift, 0) : new SKPoint(0, shift);
    }

    /// <summary>The content's translation (the swipe offset along the active axis).</summary>
    private SKPoint ContentTranslation =>
        _activeDirection is SwipeDirection.Up or SwipeDirection.Down
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
            foreach (var direction in new[] { SwipeDirection.Right, SwipeDirection.Left, SwipeDirection.Down, SwipeDirection.Up })
            {
                foreach (var (item, rect) in ItemRects(direction, sk))
                {
                    item.Content?.Arrange(new Rect(rect.Left, rect.Top, rect.Width, rect.Height));
                }
            }

            // An open view stays open at the new size.
            if (_isOpen && _activeDirection != SwipeDirection.None)
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
        if (_swipeOffset != 0 && _activeDirection != SwipeDirection.None)
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

    private void DrawSwipeItems(SKCanvas canvas, SKRect bounds, SwipeDirection direction)
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

            // Draw icon or text
            if (!string.IsNullOrEmpty(item.Text))
            {
                using var textFont = SkiaFontFactory.Create(14f);
                using var textPaint = new SKPaint
                {
                    Color = item.GetTextColorSK(),
                    IsAntialias = true
                };

                float textY = itemBounds.MidY + 5;
                canvas.DrawText(item.Text, itemBounds.MidX, textY, SKTextAlign.Center, textFont, textPaint);
            }
        }

        canvas.Restore();
    }

    /// <summary>The item at a point, when the view is open and the point is on its items.</summary>
    private SwipeItem? ItemAt(float x, float y)
    {
        if (!_isOpen || _activeDirection == SwipeDirection.None)
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
        SwipeDirection.Right => x < Bounds.Left + _swipeOffset,
        SwipeDirection.Left => x > Bounds.Right + _swipeOffset,
        SwipeDirection.Down => y < Bounds.Top + _swipeOffset,
        SwipeDirection.Up => y > Bounds.Bottom + _swipeOffset,
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
    private void InvokeItem(SwipeItem item, SwipeDirection direction)
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
    private SwipeDirection SwipeCandidate(float deltaX, float deltaY)
    {
        if (_activeDirection != SwipeDirection.None)
        {
            float along = IsHorizontal(_activeDirection) ? deltaX : deltaY;
            return Math.Abs(along) > SwipeStartDistance ? _activeDirection : SwipeDirection.None;
        }

        SwipeDirection candidate = SwipeDirection.None;
        if (Math.Abs(deltaX) > SwipeStartDistance && Math.Abs(deltaX) >= Math.Abs(deltaY))
            candidate = deltaX > 0 ? SwipeDirection.Right : SwipeDirection.Left;
        else if (Math.Abs(deltaY) > SwipeStartDistance && Math.Abs(deltaY) > Math.Abs(deltaX))
            candidate = deltaY > 0 ? SwipeDirection.Down : SwipeDirection.Up;

        return candidate != SwipeDirection.None && VisibleItems(candidate).Count > 0 ? candidate : SwipeDirection.None;
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
        return SwipeCandidate(e.X - _swipeStartX, e.Y - _swipeStartY) != SwipeDirection.None;
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        if (!_isSwiping) return;

        float deltaX = e.X - _swipeStartX;
        float deltaY = e.Y - _swipeStartY;

        // Determine swipe direction (an open view keeps its direction)
        if (_activeDirection == SwipeDirection.None)
            _activeDirection = SwipeCandidate(deltaX, deltaY);

        if (_activeDirection != SwipeDirection.None)
        {
            if (!_swipeStarted && (Math.Abs(deltaX) > SwipeStartDistance || Math.Abs(deltaY) > SwipeStartDistance))
            {
                _swipeStarted = true;
                SwipeStarted?.Invoke(this, new SwipeStartedEventArgs(_activeDirection));
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
                SwipeChanging?.Invoke(this, new SwipeOffsetChangedEventArgs(_activeDirection, offset));
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

        if (reached && GetItemsMode(itemsSide) == SwipeMode.Execute)
        {
            // Execute mode runs the first visible item (one action per swipe, as MAUI).
            if (items.Count > 0)
            {
                Open(direction);
                InvokeItem(items[0], direction);
            }
        }
        else if (reached)
        {
            Open(direction);
        }
        else
        {
            Close();
        }

        SwipeEnded?.Invoke(this, new SwipeEndedEventArgs(direction, isOpen));
        if (!_isOpen)
            _activeDirection = SwipeDirection.None;

        base.OnPointerReleased(e);
    }
}

/// <summary>A swipe's direction and how far it has moved the content (MAUI's SwipeChanging).</summary>
public class SwipeOffsetChangedEventArgs : EventArgs
{
    public SwipeOffsetChangedEventArgs(SwipeDirection direction, double offset)
    {
        Direction = direction;
        Offset = offset;
    }

    public SwipeDirection Direction { get; }

    public double Offset { get; }
}
