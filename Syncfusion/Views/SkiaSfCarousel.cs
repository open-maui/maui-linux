// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// Platform view for <c>SfCarousel</c>, which draws nothing itself in any build:
/// its native views (a WinUI ItemsControl with plane projections, UIKit and
/// Android views) place the item views. This does the same on Skia: in the
/// Default view mode the selected item sits in the centre at full size and the
/// others fan out to each side, scaled by <c>ScaleOffset</c>, tilted by
/// <c>RotationAngle</c> and spaced by <c>Offset</c> and
/// <c>SelectedItemOffset</c>, with the positions the Windows build computes; in
/// the Linear mode the items form a strip spaced by <c>ItemSpacing</c> that
/// scrolls. A swipe moves the selection (one item, or as many as the swipe
/// covers), a click selects the item under the pointer, and changes animate
/// over <c>Duration</c>.
/// </summary>
/// <remarks>
/// Every item view is arranged in the same slot, the size of one item, and
/// drawn through its own transform: moving an item costs no layout pass. The
/// carousel takes the pointer itself, as the native carousels do, so swipes
/// start anywhere on it.
/// </remarks>
public class SkiaSfCarousel : SkiaLayoutView
{
    private const float DragThreshold = 8f;
    private const float CameraDistance = 900f;

    private readonly List<SkiaView> _items = new();
    private int _selectedIndex;

    // Default mode: the fractional index at the centre (animated towards _selectedIndex).
    private float _position;
    // Linear mode: the strip's scroll offset.
    private float _scroll;

    private float _animFrom, _animTo;
    private DateTime _animStart;
    private bool _animating;
    private bool _animatingScroll;

    private bool _pressed, _dragging;
    private float _pressX, _pressY, _pressValue;

    public CarouselViewMode Mode { get; set; } = CarouselViewMode.Default;
    public float ItemWidth { get; set; } = 200;
    public float ItemHeight { get; set; } = 300;
    public float ItemSpacing { get; set; } = 12;
    public float RotationAngle { get; set; } = 45;
    public float ItemOffset { get; set; } = 18;
    public float ScaleOffset { get; set; } = 0.7f;
    public float SelectedItemOffset { get; set; } = 40;
    public int DurationMs { get; set; } = 600;
    public bool EnableInteraction { get; set; } = true;
    public bool MultipleItemSwipe { get; set; } = true;

    /// <summary>Raised when the user asks for another item (swipe, click).</summary>
    public event EventHandler<int>? SelectionRequested;

    /// <summary>Raised when the user taps an item that is already selected.</summary>
    public event EventHandler<int>? ItemTapped;

    /// <summary>Raised when a swipe starts (true: towards the left).</summary>
    public event EventHandler<bool>? SwipeStarted;

    /// <summary>Raised when a swipe ends.</summary>
    public event EventHandler? SwipeEnded;

    public int SelectedIndex => _selectedIndex;

    public int ItemCount => _items.Count;

    /// <summary>The item views in order.</summary>
    public IReadOnlyList<SkiaView> Items => _items;

    /// <summary>Replaces the item views, keeping those that are still present.</summary>
    public void SetItems(IReadOnlyList<SkiaView> items)
    {
        foreach (var old in _items)
            old.Invalidated -= OnItemInvalidated;
        foreach (var old in _items)
            if (!items.Contains(old))
                RemoveChild(old);
        _items.Clear();
        foreach (var item in items)
        {
            if (item.Parent == null)
                AddChild(item);
            else if (!ReferenceEquals(item.Parent, this))
                continue;
            _items.Add(item);
            item.Invalidated += OnItemInvalidated;
        }
        _selectedIndex = Math.Clamp(_selectedIndex, 0, Math.Max(0, _items.Count - 1));
        _position = Math.Clamp(_position, 0, Math.Max(0, _items.Count - 1));
        InvalidateMeasure();
        Invalidate();
    }

    /// <summary>
    /// An item repaints the whole control: it is drawn away from its arranged
    /// place (and, for thumbnails, twice), so its own damage rect misses it.
    /// </summary>
    private void OnItemInvalidated(object? sender, EventArgs e) => Invalidate();

    /// <summary>Selects an item, animating there when <paramref name="animate"/>.</summary>
    public void SetSelectedIndex(int index, bool animate)
    {
        if (_items.Count == 0)
        {
            _selectedIndex = Math.Max(0, index);
            return;
        }
        index = Math.Clamp(index, 0, _items.Count - 1);
        _selectedIndex = index;
        if (Mode == CarouselViewMode.Linear)
            StartAnimation(_scroll, LinearScrollFor(index), scroll: true, animate);
        else
            StartAnimation(_position, index, scroll: false, animate);
        CheckShownRange();
    }

    private void StartAnimation(float from, float to, bool scroll, bool animate)
    {
        if (!animate || DurationMs <= 0 || Bounds.Width <= 0 || Math.Abs(from - to) < 0.001f)
        {
            if (scroll) _scroll = to; else _position = to;
            _animating = false;
            Invalidate();
            return;
        }
        _animFrom = from;
        _animTo = to;
        _animatingScroll = scroll;
        _animStart = DateTime.UtcNow;
        _animating = true;
        Invalidate();
    }

    private void StepAnimation()
    {
        if (!_animating)
            return;
        float t = Math.Clamp((float)(DateTime.UtcNow - _animStart).TotalMilliseconds / DurationMs, 0f, 1f);
        float eased = 1f - (1f - t) * (1f - t) * (1f - t);
        float value = _animFrom + (_animTo - _animFrom) * eased;
        if (_animatingScroll) _scroll = value; else _position = value;
        if (t >= 1f)
            _animating = false;
        else
            Invalidate();
    }

    #region Shown range (virtualization)

    /// <summary>
    /// Raised when the items the Default mode can show may have changed (the
    /// selection moved, a drag passed an item, the size changed): a
    /// virtualizing handler realizes the items in <see cref="ShownRange"/>.
    /// </summary>
    internal event EventHandler? ShownRangeChanged;

    private (int First, int Last) _lastShownRange = (-1, -1);
    private double _lastShownWidth = -1;

    /// <summary>Runs after the current layout pass (a range change replaces children).</summary>
    private void Dispatch(Action action)
    {
        var dispatcher = (MauiView as Microsoft.Maui.Controls.BindableObject)?.Dispatcher;
        if (dispatcher == null || !dispatcher.Dispatch(action))
            action();
    }

    /// <summary>
    /// The items the Default mode places at least partly inside the carousel,
    /// for the selected item and for the drag or animation position, as the
    /// Windows build's <c>DefaultVisualModeIndex</c> finds its first and last
    /// visible item.
    /// </summary>
    internal (int First, int Last) ShownRange(int count)
    {
        if (count <= 0)
            return (0, -1);
        int lo = Math.Clamp((int)Math.Floor(_position), 0, count - 1);
        int hi = Math.Clamp((int)Math.Ceiling(_position), 0, count - 1);
        var a = ShownRangeFor(Math.Clamp(_selectedIndex, 0, count - 1), count);
        var b = ShownRangeFor(lo, count);
        var c = ShownRangeFor(hi, count);
        return (Math.Min(a.First, Math.Min(b.First, c.First)), Math.Max(a.Last, Math.Max(b.Last, c.Last)));
    }

    private (int First, int Last) ShownRangeFor(int selected, int count)
    {
        const int Unmeasured = 3; // before the first layout: the selection and its neighbours
        if (Bounds.Width <= 0)
            return (Math.Max(0, selected - Unmeasured), Math.Min(count - 1, selected + Unmeasured));
        float width = (float)Bounds.Width;
        bool Shown(int index)
        {
            var (left, scale, _, _) = DefaultPlacement(index, selected);
            return left < width && left + SlotWidth * scale > 0;
        }
        int first = selected;
        while (first > 0 && Shown(first - 1) && selected - first < 500)
            first--;
        int last = selected;
        while (last < count - 1 && Shown(last + 1) && last - selected < 500)
            last++;
        return (first, last);
    }

    private void CheckShownRange()
    {
        if (ShownRangeChanged == null || Mode != CarouselViewMode.Default)
            return;
        var range = ShownRange(_items.Count);
        if (range == _lastShownRange)
            return;
        _lastShownRange = range;
        ShownRangeChanged.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Layout

    private float SlotWidth => ItemWidth > 0 ? ItemWidth : (float)Math.Max(1, Bounds.Width);

    private float SlotHeight => ItemHeight > 0 ? ItemHeight : (float)Math.Max(1, Bounds.Height);

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var item in _items)
            item.Measure(new Size(SlotWidth, SlotHeight));
        double width = double.IsInfinity(availableSize.Width) ? SlotWidth : availableSize.Width;
        double height = double.IsInfinity(availableSize.Height) ? SlotHeight : availableSize.Height;
        return new Size(width, height);
    }

    protected override Rect ArrangeOverride(Rect bounds)
    {
        var slot = SlotIn(bounds);
        foreach (var item in _items)
            item.Arrange(slot);
        if (!_animating)
        {
            // Keep the selection where it belongs when the size changes.
            if (Mode == CarouselViewMode.Linear)
                _scroll = LinearScrollFor(_selectedIndex, bounds);
            else
                _position = _selectedIndex;
        }
        if (Math.Abs(bounds.Width - _lastShownWidth) > 0.5)
        {
            _lastShownWidth = bounds.Width;
            _lastShownRange = (-1, -1);
            Dispatch(CheckShownRange);
        }
        return bounds;
    }

    /// <summary>
    /// The rect every item is arranged in: left-aligned, vertically centred
    /// (top-aligned when taller than the carousel), as the native panels place it.
    /// </summary>
    private Rect SlotIn(Rect bounds)
    {
        float h = SlotHeight;
        double top = h < bounds.Height ? bounds.Top + (bounds.Height - h) / 2 : bounds.Top;
        return new Rect(bounds.Left, top, SlotWidth, h);
    }

    private float LinearContentWidth => _items.Count == 0 ? 0 : _items.Count * SlotWidth + (_items.Count - 1) * ItemSpacing;

    private float LinearScrollFor(int index) => LinearScrollFor(index, Bounds);

    /// <summary>The strip offset that centres an item, clamped to the strip.</summary>
    private float LinearScrollFor(int index, Rect bounds)
    {
        float x = index * (SlotWidth + ItemSpacing);
        float scroll = x - ((float)bounds.Width - SlotWidth + ItemSpacing) / 2f;
        return ClampScroll(scroll, bounds);
    }

    private float ClampScroll(float scroll, Rect bounds)
        => Math.Clamp(scroll, 0, Math.Max(0, LinearContentWidth - (float)bounds.Width));

    /// <summary>
    /// An item's placement in the Default mode for a whole-number selection:
    /// its left edge relative to the carousel, its scale, its tilt and its
    /// stacking (higher is nearer the front). The Windows build's arithmetic.
    /// </summary>
    private (float Left, float Scale, float Angle, int Z) DefaultPlacement(int index, int selected)
    {
        float half = (float)Bounds.Width / 2f;
        float w = SlotWidth;
        int d = index - selected;
        float left = d switch
        {
            0 => half - w / 2f,
            -1 => half - w - SelectedItemOffset,
            < -1 => half - w + ItemOffset * (d + 1) - SelectedItemOffset,
            1 => half + SelectedItemOffset,
            _ => half + ItemOffset * (d - 1) + SelectedItemOffset,
        };
        return (left, d == 0 ? 1f : ScaleOffset, RotationAngle * Math.Sign(d), -Math.Abs(d));
    }

    /// <summary>The Default-mode placement for a fractional position (mid-animation or drag).</summary>
    private (float Left, float Scale, float Angle, float Z) DefaultPlacementAt(int index, float position)
    {
        int lo = (int)Math.Floor(position);
        int hi = (int)Math.Ceiling(position);
        float f = position - lo;
        var a = DefaultPlacement(index, lo);
        if (hi == lo)
            return (a.Left, a.Scale, a.Angle, a.Z);
        var b = DefaultPlacement(index, hi);
        return (a.Left + (b.Left - a.Left) * f, a.Scale + (b.Scale - a.Scale) * f,
                a.Angle + (b.Angle - a.Angle) * f, a.Z + (b.Z - a.Z) * f);
    }

    /// <summary>The transform that moves an item from its slot to where it is shown.</summary>
    private SKMatrix TransformFor(int index)
    {
        var slot = SlotIn(Bounds);
        if (Mode == CarouselViewMode.Linear)
        {
            float x = index * (SlotWidth + ItemSpacing) - _scroll;
            return SKMatrix.CreateTranslation(x, 0);
        }

        var (left, scale, angle, _) = DefaultPlacementAt(index, _position);
        float cx = (float)slot.Left + left + SlotWidth / 2f;
        float cy = (float)slot.Top + SlotHeight / 2f;
        float rad = angle * MathF.PI / 180f;

        // Rotation about the item's vertical axis seen from a camera in front:
        // x' = x cos(a) / w, y' = y / w, w = 1 + x sin(a) / d (items to the left
        // turn their outer edge away, as the native carousels' projections do).
        var perspective = new SKMatrix(
            MathF.Cos(rad) * scale, 0, 0,
            0, scale, 0,
            MathF.Sin(rad) / CameraDistance, 0, 1);
        var m = SKMatrix.CreateTranslation(cx, cy);
        m = m.PreConcat(perspective);
        m = m.PreConcat(SKMatrix.CreateTranslation(-((float)slot.Left + SlotWidth / 2f), -((float)slot.Top + SlotHeight / 2f)));
        return m;
    }

    /// <summary>Item indices back to front.</summary>
    private IEnumerable<int> DrawOrder()
    {
        var order = Enumerable.Range(0, _items.Count);
        if (Mode == CarouselViewMode.Linear)
            return order;
        return order.OrderBy(i => DefaultPlacementAt(i, _position).Z).ThenBy(i => Math.Abs(i - _position));
    }

    #endregion

    #region Drawing

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        StepAnimation();
        canvas.Save();
        canvas.ClipRect(bounds);
        foreach (int i in DrawOrder().ToArray())
        {
            var item = _items[i];
            if (!item.IsVisible)
                continue;
            var m = TransformFor(i);
            var shown = m.MapRect(item.BoundsSK);
            if (shown.Right < bounds.Left || shown.Left > bounds.Right)
                continue;
            canvas.Save();
            canvas.Concat(m);
            item.Draw(canvas);
            canvas.Restore();
        }
        canvas.Restore();
    }

    #endregion

    #region Input

    /// <summary>The front-most item shown under a window point, or -1.</summary>
    public int ItemAt(float x, float y)
    {
        foreach (int i in DrawOrder().Reverse())
        {
            var shown = TransformFor(i).MapRect(_items[i].BoundsSK);
            if (shown.Contains(x, y))
                return i;
        }
        return -1;
    }

    /// <summary>Where an item is shown, in window coordinates (its bounding box).</summary>
    public Rect ShownBoundsOf(int index)
    {
        var r = TransformFor(index).MapRect(_items[index].BoundsSK);
        return new Rect(r.Left, r.Top, r.Width, r.Height);
    }

    public override SkiaView? HitTest(float x, float y)
    {
        if (!IsVisible || !IsEnabled || !Bounds.Contains(x, y))
            return null;
        return InputTransparent ? null : this;
    }

    /// <summary>Pixels of drag per item in the Default mode: the distance between neighbouring centres.</summary>
    private float DefaultStep => Math.Max(40f, SelectedItemOffset + SlotWidth / 2f);

    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!EnableInteraction || !IsEnabled || _items.Count == 0 || e.Button == PointerButton.Right)
            return;
        _pressed = true;
        _dragging = false;
        _pressX = e.X;
        _pressY = e.Y;
        _animating = false;
        _pressValue = Mode == CarouselViewMode.Linear ? _scroll : _position;
        e.Handled = true;
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_pressed)
            return;
        float dx = e.X - _pressX;
        if (!_dragging)
        {
            if (Math.Abs(dx) < DragThreshold)
                return;
            _dragging = true;
            SwipeStarted?.Invoke(this, dx < 0);
        }
        if (Mode == CarouselViewMode.Linear)
            _scroll = ClampScroll(_pressValue - dx, Bounds);
        else
            _position = Math.Clamp(_pressValue - dx / DefaultStep, 0, Math.Max(0, _items.Count - 1));
        e.Handled = true;
        Invalidate();
        CheckShownRange();
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
            return;
        _pressed = false;
        e.Handled = true;
        if (!_dragging)
        {
            int hit = ItemAt(e.X, e.Y);
            if (hit < 0)
                return;
            if (hit == _selectedIndex)
                ItemTapped?.Invoke(this, hit);
            else
                SelectionRequested?.Invoke(this, hit);
            return;
        }

        _dragging = false;
        if (Mode == CarouselViewMode.Linear)
        {
            // A free scroll, as the native strip's scroll viewer: the selection stays.
            SwipeEnded?.Invoke(this, EventArgs.Empty);
            Invalidate();
            return;
        }

        float dx = e.X - _pressX;
        int start = (int)Math.Round(_pressValue);
        int target = MultipleItemSwipe
            ? (int)Math.Round(_position)
            : start + (dx < 0 ? 1 : -1);
        if (target == start && Math.Abs(dx) >= DragThreshold * 2)
            target = start + (dx < 0 ? 1 : -1); // a short swipe still moves one item
        target = Math.Clamp(target, 0, _items.Count - 1);
        SwipeEnded?.Invoke(this, EventArgs.Empty);
        if (target != _selectedIndex)
            SelectionRequested?.Invoke(this, target);
        if (_selectedIndex == target)
            StartAnimation(_position, target, scroll: false, animate: true);
        else
            StartAnimation(_position, _selectedIndex, scroll: false, animate: true);
    }

    #endregion
}

/// <summary>The carousel's arrangement (Syncfusion's <c>ViewMode</c>).</summary>
public enum CarouselViewMode
{
    Default,
    Linear,
}

/// <summary>
/// The place of an item a virtualizing carousel has not realized: it is
/// arranged like an item and draws nothing.
/// </summary>
internal sealed class SkiaSfCarouselPlaceholder : SkiaView
{
    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
    }
}
