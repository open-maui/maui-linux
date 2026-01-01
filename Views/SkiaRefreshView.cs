// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// A pull-to-refresh container view.
/// </summary>
public class SkiaRefreshView : SkiaLayoutView
{
    private SkiaView? _content;
    private bool _isRefreshing = false;
    private float _pullDistance = 0f;
    private float _refreshThreshold = 80f;
    private bool _isPulling = false;
    private float _pullStartY;
    private float _spinnerRotation = 0f;
    private DateTime _lastSpinnerUpdate;

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
    /// Gets or sets whether the view is currently refreshing.
    /// </summary>
    public bool IsRefreshing
    {
        get => _isRefreshing;
        set
        {
            if (_isRefreshing != value)
            {
                _isRefreshing = value;
                if (!value)
                {
                    _pullDistance = 0;
                }
                Invalidate();
            }
        }
    }

    /// <summary>
    /// Gets or sets the pull distance required to trigger refresh.
    /// </summary>
    public float RefreshThreshold
    {
        get => _refreshThreshold;
        set => _refreshThreshold = Math.Max(40, value);
    }

    /// <summary>
    /// Gets or sets the refresh indicator color.
    /// </summary>
    public SKColor RefreshColor { get; set; } = new SKColor(33, 150, 243);

    /// <summary>
    /// Gets or sets the background color of the refresh indicator.
    /// </summary>
    public SKColor RefreshBackgroundColor { get; set; } = SKColors.White;

    /// <summary>
    /// Event raised when refresh is triggered.
    /// </summary>
    public event EventHandler? Refreshing;

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
            float offset = _isRefreshing ? _refreshThreshold : _pullDistance;
            var contentBounds = new SKRect(
                bounds.Left,
                bounds.Top + offset,
                bounds.Right,
                bounds.Bottom + offset);
            _content.Arrange(contentBounds);
        }
        return bounds;
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        canvas.Save();
        canvas.ClipRect(bounds);

        // Draw refresh indicator
        float indicatorY = bounds.Top + (_isRefreshing ? _refreshThreshold : _pullDistance) / 2;

        if (_pullDistance > 0 || _isRefreshing)
        {
            DrawRefreshIndicator(canvas, bounds.MidX, indicatorY);
        }

        // Draw content
        _content?.Draw(canvas);

        canvas.Restore();
    }

    private void DrawRefreshIndicator(SKCanvas canvas, float x, float y)
    {
        float size = 36f;
        float progress = Math.Clamp(_pullDistance / _refreshThreshold, 0f, 1f);

        // Draw background circle
        using var bgPaint = new SKPaint
        {
            Color = RefreshBackgroundColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        // Add shadow
        bgPaint.ImageFilter = SKImageFilter.CreateDropShadow(0, 2, 4, 4, new SKColor(0, 0, 0, 40));
        canvas.DrawCircle(x, y, size / 2, bgPaint);

        // Draw spinner
        using var spinnerPaint = new SKPaint
        {
            Color = RefreshColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        };

        if (_isRefreshing)
        {
            // Animate spinner
            var now = DateTime.UtcNow;
            float elapsed = (float)(now - _lastSpinnerUpdate).TotalMilliseconds;
            _spinnerRotation += elapsed * 0.36f; // 360 degrees per second
            _lastSpinnerUpdate = now;

            canvas.Save();
            canvas.Translate(x, y);
            canvas.RotateDegrees(_spinnerRotation);

            // Draw spinning arc
            using var path = new SKPath();
            var rect = new SKRect(-size / 3, -size / 3, size / 3, size / 3);
            path.AddArc(rect, 0, 270);
            canvas.DrawPath(path, spinnerPaint);

            canvas.Restore();

            Invalidate(); // Continue animation
        }
        else
        {
            // Draw progress arc
            canvas.Save();
            canvas.Translate(x, y);

            using var path = new SKPath();
            var rect = new SKRect(-size / 3, -size / 3, size / 3, size / 3);
            float sweepAngle = 270 * progress;
            path.AddArc(rect, -90, sweepAngle);
            canvas.DrawPath(path, spinnerPaint);

            canvas.Restore();
        }
    }

    public override SkiaView? HitTest(float x, float y)
    {
        if (!IsVisible || !Bounds.Contains(x, y)) return null;

        if (_content != null)
        {
            var hit = _content.HitTest(x, y);
            if (hit != null) return hit;
        }

        return this;
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled || _isRefreshing) return;

        // Check if content is at top (can pull to refresh)
        bool canPull = true;
        if (_content is SkiaScrollView scrollView)
        {
            canPull = scrollView.ScrollY <= 0;
        }

        if (canPull)
        {
            _isPulling = true;
            _pullStartY = e.Y;
            _pullDistance = 0;
        }

        base.OnPointerPressed(e);
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        if (!_isPulling) return;

        float delta = e.Y - _pullStartY;
        if (delta > 0)
        {
            // Apply resistance
            _pullDistance = delta * 0.5f;
            _pullDistance = Math.Min(_pullDistance, _refreshThreshold * 1.5f);
            Invalidate();
            e.Handled = true;
        }
        else
        {
            _pullDistance = 0;
        }

        base.OnPointerMoved(e);
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        if (!_isPulling) return;

        _isPulling = false;

        if (_pullDistance >= _refreshThreshold)
        {
            _isRefreshing = true;
            _pullDistance = _refreshThreshold;
            _lastSpinnerUpdate = DateTime.UtcNow;
            Refreshing?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            _pullDistance = 0;
        }

        Invalidate();
        base.OnPointerReleased(e);
    }
}
