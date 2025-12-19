// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;
using Microsoft.Maui.Graphics;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Base class for Skia-rendered pages.
/// </summary>
public class SkiaPage : SkiaView
{
    private SkiaView? _content;
    private string _title = "";
    private SKColor _titleBarColor = new SKColor(0x21, 0x96, 0xF3); // Material Blue
    private SKColor _titleTextColor = SKColors.White;
    private bool _showNavigationBar = false;
    private float _navigationBarHeight = 56;

    // Padding
    private float _paddingLeft;
    private float _paddingTop;
    private float _paddingRight;
    private float _paddingBottom;

    public SkiaView? Content
    {
        get => _content;
        set
        {
            if (_content != null)
            {
                _content.Parent = null;
            }
            _content = value;
            if (_content != null)
            {
                _content.Parent = this;
            }
            Invalidate();
        }
    }

    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            Invalidate();
        }
    }

    public SKColor TitleBarColor
    {
        get => _titleBarColor;
        set
        {
            _titleBarColor = value;
            Invalidate();
        }
    }

    public SKColor TitleTextColor
    {
        get => _titleTextColor;
        set
        {
            _titleTextColor = value;
            Invalidate();
        }
    }

    public bool ShowNavigationBar
    {
        get => _showNavigationBar;
        set
        {
            _showNavigationBar = value;
            Invalidate();
        }
    }

    public float NavigationBarHeight
    {
        get => _navigationBarHeight;
        set
        {
            _navigationBarHeight = value;
            Invalidate();
        }
    }

    public float PaddingLeft
    {
        get => _paddingLeft;
        set { _paddingLeft = value; Invalidate(); }
    }

    public float PaddingTop
    {
        get => _paddingTop;
        set { _paddingTop = value; Invalidate(); }
    }

    public float PaddingRight
    {
        get => _paddingRight;
        set { _paddingRight = value; Invalidate(); }
    }

    public float PaddingBottom
    {
        get => _paddingBottom;
        set { _paddingBottom = value; Invalidate(); }
    }

    public bool IsBusy { get; set; }

    public event EventHandler? Appearing;
    public event EventHandler? Disappearing;

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        // Draw background
        if (BackgroundColor != SKColors.Transparent)
        {
            using var bgPaint = new SKPaint
            {
                Color = BackgroundColor,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(bounds, bgPaint);
        }

        var contentTop = bounds.Top;

        // Draw navigation bar if visible
        if (_showNavigationBar)
        {
            DrawNavigationBar(canvas, new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Top + _navigationBarHeight));
            contentTop = bounds.Top + _navigationBarHeight;
        }

        // Calculate content bounds with padding
        var contentBounds = new SKRect(
            bounds.Left + _paddingLeft,
            contentTop + _paddingTop,
            bounds.Right - _paddingRight,
            bounds.Bottom - _paddingBottom);

        // Draw content
        if (_content != null)
        {
            _content.Bounds = contentBounds;
            _content.Draw(canvas);
        }

        // Draw busy indicator overlay
        if (IsBusy)
        {
            DrawBusyIndicator(canvas, bounds);
        }
    }

    protected virtual void DrawNavigationBar(SKCanvas canvas, SKRect bounds)
    {
        // Draw navigation bar background
        using var barPaint = new SKPaint
        {
            Color = _titleBarColor,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(bounds, barPaint);

        // Draw title
        if (!string.IsNullOrEmpty(_title))
        {
            using var font = new SKFont(SKTypeface.Default, 20);
            using var textPaint = new SKPaint(font)
            {
                Color = _titleTextColor,
                IsAntialias = true
            };

            var textBounds = new SKRect();
            textPaint.MeasureText(_title, ref textBounds);

            var x = bounds.Left + 16;
            var y = bounds.MidY - textBounds.MidY;
            canvas.DrawText(_title, x, y, textPaint);
        }

        // Draw shadow
        using var shadowPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 30),
            Style = SKPaintStyle.Fill,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 2)
        };
        canvas.DrawRect(new SKRect(bounds.Left, bounds.Bottom, bounds.Right, bounds.Bottom + 4), shadowPaint);
    }

    private void DrawBusyIndicator(SKCanvas canvas, SKRect bounds)
    {
        // Draw semi-transparent overlay
        using var overlayPaint = new SKPaint
        {
            Color = new SKColor(255, 255, 255, 180),
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(bounds, overlayPaint);

        // Draw spinning indicator (simplified - would animate in real impl)
        using var indicatorPaint = new SKPaint
        {
            Color = _titleBarColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 4,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        };

        var centerX = bounds.MidX;
        var centerY = bounds.MidY;
        var radius = 20f;

        using var path = new SKPath();
        path.AddArc(new SKRect(centerX - radius, centerY - radius, centerX + radius, centerY + radius), 0, 270);
        canvas.DrawPath(path, indicatorPaint);
    }

    public void OnAppearing()
    {
        Appearing?.Invoke(this, EventArgs.Empty);
    }

    public void OnDisappearing()
    {
        Disappearing?.Invoke(this, EventArgs.Empty);
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        // Page takes all available space
        return availableSize;
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        // Adjust coordinates for content
        var contentTop = _showNavigationBar ? _navigationBarHeight : 0;
        if (e.Y > contentTop && _content != null)
        {
            var contentE = new PointerEventArgs(e.X - _paddingLeft, e.Y - contentTop - _paddingTop, e.Button);
            _content.OnPointerPressed(contentE);
        }
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        var contentTop = _showNavigationBar ? _navigationBarHeight : 0;
        if (e.Y > contentTop && _content != null)
        {
            var contentE = new PointerEventArgs(e.X - _paddingLeft, e.Y - contentTop - _paddingTop, e.Button);
            _content.OnPointerMoved(contentE);
        }
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        var contentTop = _showNavigationBar ? _navigationBarHeight : 0;
        if (e.Y > contentTop && _content != null)
        {
            var contentE = new PointerEventArgs(e.X - _paddingLeft, e.Y - contentTop - _paddingTop, e.Button);
            _content.OnPointerReleased(contentE);
        }
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        _content?.OnKeyDown(e);
    }

    public override void OnKeyUp(KeyEventArgs e)
    {
        _content?.OnKeyUp(e);
    }

    public override void OnScroll(ScrollEventArgs e)
    {
        _content?.OnScroll(e);
    }
}

/// <summary>
/// Simple content page view.
/// </summary>
public class SkiaContentPage : SkiaPage
{
    // SkiaContentPage is essentially the same as SkiaPage
    // but represents a ContentPage specifically
}
