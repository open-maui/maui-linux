// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Rendering;
using SkiaSharp;
using Syncfusion.Maui.Core.Rotator;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// Platform view for <c>SfRotator</c>, whose native views (a WinUI slider with
/// a tab strip or thumbnail list, and their iOS and Android counterparts)
/// hold everything the control shows. One item fills the content area; the
/// next slides in along <c>NavigationDirection</c> on a swipe, a wheel step, a
/// click on a dot, thumbnail or navigation button, or autoplay. The navigation
/// strip is a row of dots (over the item or beside it, as
/// <c>DotPlacement</c> asks) or of thumbnails, at <c>NavigationStripPosition</c>.
/// </summary>
/// <remarks>
/// All item views are arranged in the content area and drawn translated while
/// they slide; a thumbnail is the item view itself drawn scaled into its tile,
/// so it always matches the item. The rotator takes the pointer itself, as the
/// native rotators do: a click on the item raises <c>ItemTapped</c>.
/// </remarks>
public class SkiaSfRotator : SkiaLayoutView
{
    private const float DragThreshold = 8f;
    private const float AnimationMs = 300f;
    private const float DotSize = 10f;
    private const float DotGap = 10f;
    private const float DotsStripThickness = 30f;
    private const float ThumbSize = 60f;
    private const float ThumbGap = 8f;
    private const float ThumbStripThickness = ThumbSize + 20f;
    private const float NavButtonSize = 40f;
    private const float TextBandHeight = 32f;

    private readonly List<SkiaView> _items = new();
    private readonly List<string?> _texts = new();
    private int _selected;

    // Sliding state, in items: 0 shows _selected; +1 shows the incoming item
    // after it along the navigation direction, -1 the one before it.
    private float _shift;
    private int _incoming = -1;
    private float _animFrom, _animTo;
    private DateTime _animStart;
    private bool _animating;

    private bool _pressed, _dragging;
    private float _pressX, _pressY, _pressShift;

    public NavigationStripMode StripMode { get; set; } = NavigationStripMode.Dots;
    public DotsPlacement DotPlacement { get; set; } = DotsPlacement.Default;
    public NavigationStripPosition StripPosition { get; set; } = NavigationStripPosition.Bottom;
    public NavigationDirection Direction { get; set; } = NavigationDirection.Horizontal;
    public bool EnableLooping { get; set; } = true;
    public bool EnableSwiping { get; set; } = true;
    public bool ShowText { get; set; }
    public bool ShowNavigationButton { get; set; }
    public Color SelectedDotColor { get; set; } = Color.FromRgb(73, 69, 79);
    public Color UnselectedDotColor { get; set; } = Color.FromRgb(202, 196, 208);
    public Color DotsStroke { get; set; } = Colors.Transparent;
    public Color SelectedThumbnailStroke { get; set; } = Color.FromRgb(103, 80, 164);
    public Color UnselectedThumbnailStroke { get; set; } = Color.FromRgb(202, 196, 208);
    public Color NavigationButtonBackgroundColor { get; set; } = Color.FromRgb(247, 242, 251);
    public Color NavigationButtonIconColor { get; set; } = Color.FromRgb(73, 69, 79);

    /// <summary>Raised when the user asks for another item.</summary>
    public event EventHandler<int>? SelectionRequested;

    /// <summary>Raised when the user clicks the shown item.</summary>
    public event EventHandler? ItemTapped;

    public int SelectedIndex => _selected;

    public int ItemCount => _items.Count;

    public IReadOnlyList<SkiaView> Items => _items;

    /// <summary>Replaces the item views and their captions, keeping the views still present.</summary>
    public void SetItems(IReadOnlyList<SkiaView> items, IReadOnlyList<string?> texts)
    {
        foreach (var old in _items)
            old.Invalidated -= OnItemInvalidated;
        foreach (var old in _items)
            if (!items.Contains(old))
                RemoveChild(old);
        _items.Clear();
        _texts.Clear();
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item.Parent == null)
                AddChild(item);
            else if (!ReferenceEquals(item.Parent, this))
                continue;
            _items.Add(item);
            item.Invalidated += OnItemInvalidated;
            _texts.Add(i < texts.Count ? texts[i] : null);
        }
        _selected = Math.Clamp(_selected, 0, Math.Max(0, _items.Count - 1));
        _shift = 0;
        _animating = false;
        InvalidateMeasure();
        Invalidate();
    }

    /// <summary>
    /// An item repaints the whole control: it is drawn away from its arranged
    /// place (and, for thumbnails, twice), so its own damage rect misses it.
    /// </summary>
    private void OnItemInvalidated(object? sender, EventArgs e) => Invalidate();

    #region Navigation

    public int NextOf(int index) => index + 1 < _items.Count ? index + 1 : EnableLooping && _items.Count > 1 ? 0 : -1;

    public int PreviousOf(int index) => index > 0 ? index - 1 : EnableLooping && _items.Count > 1 ? _items.Count - 1 : -1;

    /// <summary>Asks for the next item (navigation button, autoplay, wheel).</summary>
    public void RequestNext()
    {
        int next = NextOf(_selected);
        if (next >= 0)
            SelectionRequested?.Invoke(this, next);
    }

    /// <summary>Asks for the previous item.</summary>
    public void RequestPrevious()
    {
        int previous = PreviousOf(_selected);
        if (previous >= 0)
            SelectionRequested?.Invoke(this, previous);
    }

    /// <summary>
    /// Shows an item, sliding it in from the side it lies on (a neighbour, or
    /// the direction of its index for a jump), continuing a partial swipe.
    /// </summary>
    public void SetSelectedIndex(int index, bool animate)
    {
        if (_items.Count == 0 || index < 0 || index >= _items.Count)
        {
            _selected = Math.Max(0, index);
            return;
        }
        if (index == _selected && !_animating)
        {
            if (_shift != 0)
                Animate(_shift, 0, -1);
            return;
        }
        if (_animating && _incoming >= 0)
            Commit(); // finish the slide in progress first

        if (index == _selected)
            return;

        int dir = index == NextOf(_selected) ? 1
                : index == PreviousOf(_selected) ? -1
                : Math.Sign(index - _selected);
        if (!animate || Bounds.Width <= 0)
        {
            _selected = index;
            _shift = 0;
            _incoming = -1;
            _animating = false;
            Invalidate();
            return;
        }
        float from = Math.Sign(_shift) == dir ? _shift : 0;
        Animate(from, dir, index);
    }

    private void Animate(float from, float to, int incoming)
    {
        _animFrom = from;
        _animTo = to;
        _incoming = incoming;
        _shift = from;
        _animStart = DateTime.UtcNow;
        _animating = true;
        Invalidate();
    }

    private void Commit()
    {
        if (_incoming >= 0 && _animTo != 0)
            _selected = _incoming;
        _shift = 0;
        _incoming = -1;
        _animating = false;
    }

    private void StepAnimation()
    {
        if (!_animating)
            return;
        float t = Math.Clamp((float)(DateTime.UtcNow - _animStart).TotalMilliseconds / AnimationMs, 0f, 1f);
        float eased = 1f - (1f - t) * (1f - t) * (1f - t);
        _shift = _animFrom + (_animTo - _animFrom) * eased;
        if (t >= 1f)
            Commit();
        else
            Invalidate();
    }

    /// <summary>The item shown beside the selected one while sliding, or -1.</summary>
    private int IncomingFor(float shift)
    {
        if (_incoming >= 0 && Math.Sign(shift) == Math.Sign(_animTo))
            return _incoming;
        return shift > 0 ? NextOf(_selected) : shift < 0 ? PreviousOf(_selected) : -1;
    }

    #endregion

    #region Geometry

    private bool Vertical => Direction is NavigationDirection.Vertical or NavigationDirection.TopToBottom or NavigationDirection.BottomToTop;

    /// <summary>+1 when the next item lies to the right (or below), -1 when to the left (or above).</summary>
    private int NextSide => Direction is NavigationDirection.LeftToRight or NavigationDirection.TopToBottom ? -1 : 1;

    private bool StripReserved => StripMode == NavigationStripMode.Thumbnail || DotPlacement == DotsPlacement.OutSide;

    private bool StripShown => StripMode == NavigationStripMode.Thumbnail || DotPlacement != DotsPlacement.None;

    private float StripThickness => StripMode == NavigationStripMode.Thumbnail ? ThumbStripThickness : DotsStripThickness;

    private bool StripHorizontal => StripPosition is NavigationStripPosition.Top or NavigationStripPosition.Bottom;

    /// <summary>The area the items show in.</summary>
    private SKRect ContentRect(SKRect bounds)
    {
        if (!StripReserved || _items.Count == 0)
            return bounds;
        float t = Math.Min(StripThickness, StripHorizontal ? bounds.Height / 2 : bounds.Width / 2);
        return StripPosition switch
        {
            NavigationStripPosition.Top => new SKRect(bounds.Left, bounds.Top + t, bounds.Right, bounds.Bottom),
            NavigationStripPosition.Left => new SKRect(bounds.Left + t, bounds.Top, bounds.Right, bounds.Bottom),
            NavigationStripPosition.Right => new SKRect(bounds.Left, bounds.Top, bounds.Right - t, bounds.Bottom),
            _ => new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom - t),
        };
    }

    /// <summary>The navigation strip's area (inside the content for overlaid dots).</summary>
    private SKRect StripRect(SKRect bounds)
    {
        var content = ContentRect(bounds);
        float t = StripThickness;
        if (!StripReserved)
        {
            return StripPosition switch
            {
                NavigationStripPosition.Top => new SKRect(content.Left, content.Top, content.Right, content.Top + t),
                NavigationStripPosition.Left => new SKRect(content.Left, content.Top, content.Left + t, content.Bottom),
                NavigationStripPosition.Right => new SKRect(content.Right - t, content.Top, content.Right, content.Bottom),
                _ => new SKRect(content.Left, content.Bottom - t, content.Right, content.Bottom),
            };
        }
        return StripPosition switch
        {
            NavigationStripPosition.Top => new SKRect(bounds.Left, bounds.Top, bounds.Right, content.Top),
            NavigationStripPosition.Left => new SKRect(bounds.Left, bounds.Top, content.Left, bounds.Bottom),
            NavigationStripPosition.Right => new SKRect(content.Right, bounds.Top, bounds.Right, bounds.Bottom),
            _ => new SKRect(bounds.Left, content.Bottom, bounds.Right, bounds.Bottom),
        };
    }

    /// <summary>A dot's centre, for index <paramref name="i"/>.</summary>
    public SKPoint DotCenter(int i) => DotCenter(i, BoundsSK);

    private SKPoint DotCenter(int i, SKRect bounds)
    {
        var strip = StripRect(bounds);
        float length = _items.Count * DotSize + (_items.Count - 1) * DotGap;
        float along = -length / 2 + DotSize / 2 + i * (DotSize + DotGap);
        return StripHorizontal
            ? new SKPoint(strip.MidX + along, strip.MidY)
            : new SKPoint(strip.MidX, strip.MidY + along);
    }

    /// <summary>The strip's navigation buttons (previous, next), when shown.</summary>
    private (SKRect Previous, SKRect Next)? NavButtons(SKRect bounds)
    {
        if (StripMode != NavigationStripMode.Thumbnail || !ShowNavigationButton)
            return null;
        var strip = StripRect(bounds);
        float s = NavButtonSize;
        if (StripHorizontal)
            return (SKRect.Create(strip.Left + 4, strip.MidY - s / 2, s, s), SKRect.Create(strip.Right - 4 - s, strip.MidY - s / 2, s, s));
        return (SKRect.Create(strip.MidX - s / 2, strip.Top + 4, s, s), SKRect.Create(strip.MidX - s / 2, strip.Bottom - 4 - s, s, s));
    }

    /// <summary>A thumbnail's tile, scrolled so the selected one stays in view.</summary>
    public SKRect ThumbRect(int i) => ThumbRect(i, BoundsSK);

    private SKRect ThumbRect(int i, SKRect bounds)
    {
        var strip = StripRect(bounds);
        if (NavButtons(bounds) is { } buttons)
            strip = StripHorizontal
                ? new SKRect(buttons.Previous.Right + ThumbGap, strip.Top, buttons.Next.Left - ThumbGap, strip.Bottom)
                : new SKRect(strip.Left, buttons.Previous.Bottom + ThumbGap, strip.Right, buttons.Next.Top - ThumbGap);
        float available = StripHorizontal ? strip.Width : strip.Height;
        float length = _items.Count * ThumbSize + (_items.Count - 1) * ThumbGap;
        float start = length <= available
            ? (available - length) / 2
            : -Math.Clamp(_selected * (ThumbSize + ThumbGap) + ThumbSize / 2 - available / 2, 0, length - available);
        float along = start + i * (ThumbSize + ThumbGap);
        return StripHorizontal
            ? SKRect.Create(strip.Left + along, strip.MidY - ThumbSize / 2, ThumbSize, ThumbSize)
            : SKRect.Create(strip.MidX - ThumbSize / 2, strip.Top + along, ThumbSize, ThumbSize);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsInfinity(availableSize.Width) ? 250 : availableSize.Width;
        double height = double.IsInfinity(availableSize.Height) ? 350 : availableSize.Height;
        var content = ContentRect(new SKRect(0, 0, (float)width, (float)height));
        foreach (var item in _items)
            item.Measure(new Size(content.Width, content.Height));
        return new Size(width, height);
    }

    protected override Rect ArrangeOverride(Rect bounds)
    {
        var content = ContentRect(new SKRect((float)bounds.Left, (float)bounds.Top, (float)bounds.Right, (float)bounds.Bottom));
        var rect = new Rect(content.Left, content.Top, content.Width, content.Height);
        foreach (var item in _items)
            item.Arrange(rect);
        return bounds;
    }

    #endregion

    #region Drawing

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        StepAnimation();
        if (_items.Count == 0)
            return;

        var content = ContentRect(bounds);
        float extent = Vertical ? content.Height : content.Width;
        canvas.Save();
        canvas.ClipRect(content);
        DrawItem(canvas, _selected, -_shift * extent * NextSide);
        int incoming = IncomingFor(_shift);
        if (incoming >= 0 && incoming != _selected)
            DrawItem(canvas, incoming, (Math.Sign(_shift) - _shift) * extent * NextSide);
        canvas.Restore();

        if (StripShown)
        {
            if (StripMode == NavigationStripMode.Thumbnail)
                DrawThumbnails(canvas, bounds, content);
            else
                DrawDots(canvas, bounds);
        }
    }

    private void DrawItem(SKCanvas canvas, int index, float offset)
    {
        var item = _items[index];
        if (!item.IsVisible)
            return;
        canvas.Save();
        canvas.Translate(Vertical ? 0 : offset, Vertical ? offset : 0);
        item.Draw(canvas);
        if (ShowText && _texts[index] is { Length: > 0 } text)
            DrawCaption(canvas, item.BoundsSK, text);
        canvas.Restore();
    }

    private static void DrawCaption(SKCanvas canvas, SKRect rect, string text)
    {
        var band = new SKRect(rect.Left, rect.Bottom - TextBandHeight, rect.Right, rect.Bottom);
        using var back = new SKPaint { Color = new SKColor(0, 0, 0, 120), Style = SKPaintStyle.Fill };
        canvas.DrawRect(band, back);
        using var font = SkiaFontFactory.Create(14);
        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        font.MeasureText(text, out var ink);
        canvas.DrawText(text, band.MidX, band.MidY - ink.MidY, SKTextAlign.Center, font, paint);
    }

    private void DrawDots(SKCanvas canvas, SKRect bounds)
    {
        using var fill = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
        using var stroke = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true, Color = DotsStroke.ToSKColor() };
        int shown = ShownIndex();
        for (int i = 0; i < _items.Count; i++)
        {
            var c = DotCenter(i, bounds);
            fill.Color = (i == shown ? SelectedDotColor : UnselectedDotColor).ToSKColor();
            canvas.DrawCircle(c, DotSize / 2, fill);
            if (DotsStroke.Alpha > 0)
                canvas.DrawCircle(c, DotSize / 2, stroke);
        }
    }

    private void DrawThumbnails(SKCanvas canvas, SKRect bounds, SKRect content)
    {
        var strip = StripRect(bounds);
        canvas.Save();
        canvas.ClipRect(strip);
        using var border = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true };
        int shown = ShownIndex();
        for (int i = 0; i < _items.Count; i++)
        {
            var tile = ThumbRect(i, bounds);
            if (!tile.IntersectsWith(strip))
                continue;
            // The item itself, scaled to cover the tile.
            float scale = Math.Max(tile.Width / content.Width, tile.Height / content.Height);
            canvas.Save();
            canvas.ClipRect(tile);
            canvas.Translate(tile.MidX, tile.MidY);
            canvas.Scale(scale);
            canvas.Translate(-content.MidX, -content.MidY);
            if (_items[i].IsVisible)
                _items[i].Draw(canvas);
            canvas.Restore();
            border.Color = (i == shown ? SelectedThumbnailStroke : UnselectedThumbnailStroke).ToSKColor();
            var edge = tile;
            edge.Inflate(-1, -1);
            canvas.DrawRect(edge, border);
        }
        canvas.Restore();

        if (NavButtons(bounds) is { } buttons)
        {
            DrawNavButton(canvas, buttons.Previous, previous: true);
            DrawNavButton(canvas, buttons.Next, previous: false);
        }
    }

    private void DrawNavButton(SKCanvas canvas, SKRect rect, bool previous)
    {
        using var back = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true, Color = NavigationButtonBackgroundColor.ToSKColor() };
        canvas.DrawRoundRect(rect, 4, 4, back);
        using var icon = new SKPaint
        {
            Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true, StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round, Color = NavigationButtonIconColor.ToSKColor(),
        };
        using var path = new SKPathBuilder();
        float cx = rect.MidX, cy = rect.MidY;
        if (StripHorizontal)
        {
            float dx = previous ? -3 : 3;
            path.MoveTo(cx - dx, cy - 6);
            path.LineTo(cx + dx, cy);
            path.LineTo(cx - dx, cy + 6);
        }
        else
        {
            float dy = previous ? -3 : 3;
            path.MoveTo(cx - 6, cy - dy);
            path.LineTo(cx, cy + dy);
            path.LineTo(cx + 6, cy - dy);
        }
        using var chevron = path.Detach();
        canvas.DrawPath(chevron, icon);
    }

    /// <summary>The item the strip marks: the incoming one once it is more than half in.</summary>
    private int ShownIndex()
    {
        int incoming = IncomingFor(_shift);
        return Math.Abs(_shift) > 0.5f && incoming >= 0 ? incoming : _selected;
    }

    #endregion

    #region Input

    public override SkiaView? HitTest(float x, float y)
    {
        if (!IsVisible || !IsEnabled || !Bounds.Contains(x, y))
            return null;
        return InputTransparent ? null : this;
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsEnabled || _items.Count == 0 || e.Button == PointerButton.Right)
            return;
        if (_animating)
            Commit();
        _pressed = true;
        _dragging = false;
        _pressX = e.X;
        _pressY = e.Y;
        _pressShift = _shift;
        e.Handled = true;
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_pressed || !EnableSwiping)
            return;
        var content = ContentRect(BoundsSK);
        if (!_dragging && !content.Contains(_pressX, _pressY))
            return;
        float delta = Vertical ? e.Y - _pressY : e.X - _pressX;
        if (!_dragging)
        {
            if (Math.Abs(delta) < DragThreshold)
                return;
            _dragging = true;
        }
        float extent = Vertical ? content.Height : content.Width;
        float shift = _pressShift - delta / Math.Max(1, extent) * NextSide;
        if (NextOf(_selected) < 0) shift = Math.Min(shift, 0);
        if (PreviousOf(_selected) < 0) shift = Math.Max(shift, 0);
        _shift = Math.Clamp(shift, -1, 1);
        _incoming = -1;
        e.Handled = true;
        Invalidate();
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
            return;
        _pressed = false;
        e.Handled = true;
        if (_dragging)
        {
            _dragging = false;
            int target = IncomingFor(_shift);
            if (Math.Abs(_shift) >= 0.2f && target >= 0)
            {
                // The selection moves now; the slide continues from here.
                _animTo = Math.Sign(_shift);
                SelectionRequested?.Invoke(this, target);
                if (_selected != target && !_animating)
                    Animate(_shift, Math.Sign(_shift), target);
            }
            else
            {
                Animate(_shift, 0, -1);
            }
            return;
        }
        Click(e.X, e.Y);
    }

    private void Click(float x, float y)
    {
        var bounds = BoundsSK;
        if (StripShown && StripRect(bounds).Contains(x, y))
        {
            if (NavButtons(bounds) is { } buttons)
            {
                if (buttons.Previous.Contains(x, y)) { RequestPrevious(); return; }
                if (buttons.Next.Contains(x, y)) { RequestNext(); return; }
            }
            for (int i = 0; i < _items.Count; i++)
            {
                bool hit = StripMode == NavigationStripMode.Thumbnail
                    ? ThumbRect(i, bounds).Contains(x, y)
                    : SKPoint.Distance(DotCenter(i, bounds), new SKPoint(x, y)) <= DotSize / 2 + DotGap / 2;
                if (hit)
                {
                    if (i != _selected)
                        SelectionRequested?.Invoke(this, i);
                    return;
                }
            }
            if (StripReserved)
                return;
        }
        if (ContentRect(bounds).Contains(x, y))
            ItemTapped?.Invoke(this, EventArgs.Empty);
    }

    public override void OnScroll(ScrollEventArgs e)
    {
        if (!EnableSwiping || _items.Count == 0)
            return;
        float delta = Vertical ? e.DeltaY : e.DeltaX;
        if (delta == 0)
            return;
        if (delta * NextSide > 0) RequestNext(); else RequestPrevious();
        e.Handled = true;
    }

    #endregion
}
