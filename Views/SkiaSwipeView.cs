// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// A view that supports swipe gestures to reveal actions.
/// </summary>
public class SkiaSwipeView : SkiaLayoutView
{
    private SkiaView? _content;
    private readonly List<SwipeItem> _leftItems = new();
    private readonly List<SwipeItem> _rightItems = new();
    private readonly List<SwipeItem> _topItems = new();
    private readonly List<SwipeItem> _bottomItems = new();

    private float _swipeOffset = 0f;
    private SwipeDirection _activeDirection = SwipeDirection.None;
    private bool _isSwiping = false;
    private float _swipeStartX;
    private float _swipeStartY;
    private float _swipeStartOffset;
    private bool _isOpen = false;

    private const float SwipeThreshold = 60f;
    private const float VelocityThreshold = 500f;
    private float _velocity;
    private DateTime _lastMoveTime;
    private float _lastMovePosition;

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
    /// Gets or sets the swipe mode.
    /// </summary>
    public SwipeMode Mode { get; set; } = SwipeMode.Reveal;

    /// <summary>
    /// Gets or sets the left swipe threshold.
    /// </summary>
    public float LeftSwipeThreshold { get; set; } = 100f;

    /// <summary>
    /// Gets or sets the right swipe threshold.
    /// </summary>
    public float RightSwipeThreshold { get; set; } = 100f;

    /// <summary>
    /// Event raised when swipe is started.
    /// </summary>
    public event EventHandler<SwipeStartedEventArgs>? SwipeStarted;

    /// <summary>
    /// Event raised when swipe ends.
    /// </summary>
    public event EventHandler<SwipeEndedEventArgs>? SwipeEnded;

    /// <summary>
    /// Opens the swipe view in the specified direction.
    /// </summary>
    public void Open(SwipeDirection direction)
    {
        _activeDirection = direction;
        _isOpen = true;

        float targetOffset = direction switch
        {
            SwipeDirection.Left => -RightSwipeThreshold,
            SwipeDirection.Right => LeftSwipeThreshold,
            _ => 0
        };

        AnimateTo(targetOffset);
    }

    /// <summary>
    /// Closes the swipe view.
    /// </summary>
    public void Close()
    {
        _isOpen = false;
        AnimateTo(0);
    }

    private void AnimateTo(float target)
    {
        // Simple animation - in production would use proper animation
        _swipeOffset = target;
        Invalidate();
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        if (_content != null)
        {
            _content.Measure(availableSize);
        }
        return availableSize;
    }

    protected override SKRect ArrangeOverride(SKRect bounds)
    {
        if (_content != null)
        {
            var contentBounds = new SKRect(
                bounds.Left + _swipeOffset,
                bounds.Top,
                bounds.Right + _swipeOffset,
                bounds.Bottom);
            _content.Arrange(contentBounds);
        }
        return bounds;
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        canvas.Save();
        canvas.ClipRect(bounds);

        // Draw swipe items behind content
        if (_swipeOffset > 0)
        {
            DrawSwipeItems(canvas, bounds, _leftItems, true);
        }
        else if (_swipeOffset < 0)
        {
            DrawSwipeItems(canvas, bounds, _rightItems, false);
        }

        // Draw content
        _content?.Draw(canvas);

        canvas.Restore();
    }

    private void DrawSwipeItems(SKCanvas canvas, SKRect bounds, List<SwipeItem> items, bool isLeft)
    {
        if (items.Count == 0) return;

        float revealWidth = Math.Abs(_swipeOffset);
        float itemWidth = revealWidth / items.Count;

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            float x = isLeft ? bounds.Left + i * itemWidth : bounds.Right - (items.Count - i) * itemWidth;

            var itemBounds = new SKRect(
                x,
                bounds.Top,
                x + itemWidth,
                bounds.Bottom);

            // Draw background
            using var bgPaint = new SKPaint
            {
                Color = item.BackgroundColor,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(itemBounds, bgPaint);

            // Draw icon or text
            if (!string.IsNullOrEmpty(item.Text))
            {
                using var textPaint = new SKPaint
                {
                    Color = item.TextColor,
                    TextSize = 14f,
                    IsAntialias = true,
                    TextAlign = SKTextAlign.Center
                };

                float textY = itemBounds.MidY + 5;
                canvas.DrawText(item.Text, itemBounds.MidX, textY, textPaint);
            }
        }
    }

    public override SkiaView? HitTest(float x, float y)
    {
        if (!IsVisible || !Bounds.Contains(x, y)) return null;

        // Check if hit is on swipe items
        if (_isOpen)
        {
            if (_swipeOffset > 0 && x < Bounds.Left + _swipeOffset)
            {
                return this; // Hit on left items
            }
            else if (_swipeOffset < 0 && x > Bounds.Right + _swipeOffset)
            {
                return this; // Hit on right items
            }
        }

        if (_content != null)
        {
            var hit = _content.HitTest(x, y);
            if (hit != null) return hit;
        }

        return this;
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        // Check for swipe item tap when open
        if (_isOpen)
        {
            SwipeItem? tappedItem = null;

            if (_swipeOffset > 0)
            {
                int index = (int)((e.X - Bounds.Left) / (_swipeOffset / _leftItems.Count));
                if (index >= 0 && index < _leftItems.Count)
                {
                    tappedItem = _leftItems[index];
                }
            }
            else if (_swipeOffset < 0)
            {
                float itemWidth = Math.Abs(_swipeOffset) / _rightItems.Count;
                int index = (int)((e.X - (Bounds.Right + _swipeOffset)) / itemWidth);
                if (index >= 0 && index < _rightItems.Count)
                {
                    tappedItem = _rightItems[index];
                }
            }

            if (tappedItem != null)
            {
                tappedItem.OnInvoked();
                Close();
                e.Handled = true;
                return;
            }
        }

        _isSwiping = true;
        _swipeStartX = e.X;
        _swipeStartY = e.Y;
        _swipeStartOffset = _swipeOffset;
        _lastMovePosition = e.X;
        _lastMoveTime = DateTime.UtcNow;
        _velocity = 0;

        base.OnPointerPressed(e);
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        if (!_isSwiping) return;

        float deltaX = e.X - _swipeStartX;
        float deltaY = e.Y - _swipeStartY;

        // Determine swipe direction
        if (_activeDirection == SwipeDirection.None)
        {
            if (Math.Abs(deltaX) > 10)
            {
                _activeDirection = deltaX > 0 ? SwipeDirection.Right : SwipeDirection.Left;
                SwipeStarted?.Invoke(this, new SwipeStartedEventArgs(_activeDirection));
            }
        }

        if (_activeDirection == SwipeDirection.Right || _activeDirection == SwipeDirection.Left)
        {
            _swipeOffset = _swipeStartOffset + deltaX;

            // Clamp offset based on available items
            float maxRight = _leftItems.Count > 0 ? LeftSwipeThreshold : 0;
            float maxLeft = _rightItems.Count > 0 ? -RightSwipeThreshold : 0;
            _swipeOffset = Math.Clamp(_swipeOffset, maxLeft, maxRight);

            // Calculate velocity
            var now = DateTime.UtcNow;
            float timeDelta = (float)(now - _lastMoveTime).TotalSeconds;
            if (timeDelta > 0)
            {
                _velocity = (e.X - _lastMovePosition) / timeDelta;
            }
            _lastMovePosition = e.X;
            _lastMoveTime = now;

            Invalidate();
            e.Handled = true;
        }

        base.OnPointerMoved(e);
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        if (!_isSwiping) return;

        _isSwiping = false;

        // Determine final state
        bool shouldOpen = false;

        if (Math.Abs(_velocity) > VelocityThreshold)
        {
            // Use velocity
            shouldOpen = (_velocity > 0 && _leftItems.Count > 0) || (_velocity < 0 && _rightItems.Count > 0);
        }
        else
        {
            // Use threshold
            shouldOpen = Math.Abs(_swipeOffset) > SwipeThreshold;
        }

        if (shouldOpen)
        {
            if (_swipeOffset > 0)
            {
                Open(SwipeDirection.Right);
            }
            else
            {
                Open(SwipeDirection.Left);
            }
        }
        else
        {
            Close();
        }

        SwipeEnded?.Invoke(this, new SwipeEndedEventArgs(_activeDirection, _isOpen));
        _activeDirection = SwipeDirection.None;

        base.OnPointerReleased(e);
    }
}

/// <summary>
/// Represents a swipe action item.
/// </summary>
public class SwipeItem
{
    /// <summary>
    /// Gets or sets the text.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the icon source.
    /// </summary>
    public string? IconSource { get; set; }

    /// <summary>
    /// Gets or sets the background color.
    /// </summary>
    public SKColor BackgroundColor { get; set; } = new SKColor(33, 150, 243);

    /// <summary>
    /// Gets or sets the text color.
    /// </summary>
    public SKColor TextColor { get; set; } = SKColors.White;

    /// <summary>
    /// Event raised when the item is invoked.
    /// </summary>
    public event EventHandler? Invoked;

    internal void OnInvoked()
    {
        Invoked?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// Swipe direction.
/// </summary>
public enum SwipeDirection
{
    None,
    Left,
    Right,
    Up,
    Down
}

/// <summary>
/// Swipe mode.
/// </summary>
public enum SwipeMode
{
    Reveal,
    Execute
}

/// <summary>
/// Event args for swipe started.
/// </summary>
public class SwipeStartedEventArgs : EventArgs
{
    public SwipeDirection Direction { get; }

    public SwipeStartedEventArgs(SwipeDirection direction)
    {
        Direction = direction;
    }
}

/// <summary>
/// Event args for swipe ended.
/// </summary>
public class SwipeEndedEventArgs : EventArgs
{
    public SwipeDirection Direction { get; }
    public bool IsOpen { get; }

    public SwipeEndedEventArgs(SwipeDirection direction, bool isOpen)
    {
        Direction = direction;
        IsOpen = isOpen;
    }
}
