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
            // Apply content's margin to the content bounds
            var margin = _content.Margin;
            var adjustedBounds = new SKRect(
                contentBounds.Left + (float)margin.Left,
                contentBounds.Top + (float)margin.Top,
                contentBounds.Right - (float)margin.Right,
                contentBounds.Bottom - (float)margin.Bottom);

            // Measure and arrange the content before drawing
            var availableSize = new SKSize(adjustedBounds.Width, adjustedBounds.Height);
            _content.Measure(availableSize);
            _content.Arrange(adjustedBounds);
            Console.WriteLine($"[SkiaPage] Drawing content: {_content.GetType().Name}, Bounds={_content.Bounds}, IsVisible={_content.IsVisible}");
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
        Console.WriteLine($"[SkiaPage] OnAppearing called for: {Title}, HasListeners={Appearing != null}");
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

    public override SkiaView? HitTest(float x, float y)
    {
        if (!IsVisible)
            return null;

        // Don't check Bounds.Contains for page - it may not be set
        // Just forward to content

        // Check content
        if (_content != null)
        {
            var hit = _content.HitTest(x, y);
            if (hit != null)
                return hit;
        }

        return this;
    }
}

/// <summary>
/// Simple content page view with toolbar items support.
/// </summary>
public class SkiaContentPage : SkiaPage
{
    private readonly List<SkiaToolbarItem> _toolbarItems = new();

    /// <summary>
    /// Gets the toolbar items for this page.
    /// </summary>
    public IList<SkiaToolbarItem> ToolbarItems => _toolbarItems;

    protected override void DrawNavigationBar(SKCanvas canvas, SKRect bounds)
    {
        // Draw navigation bar background
        using var barPaint = new SKPaint
        {
            Color = TitleBarColor,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(bounds, barPaint);

        // Draw title
        if (!string.IsNullOrEmpty(Title))
        {
            using var font = new SKFont(SKTypeface.Default, 20);
            using var textPaint = new SKPaint(font)
            {
                Color = TitleTextColor,
                IsAntialias = true
            };

            var textBounds = new SKRect();
            textPaint.MeasureText(Title, ref textBounds);

            var x = bounds.Left + 56; // Leave space for back button
            var y = bounds.MidY - textBounds.MidY;
            canvas.DrawText(Title, x, y, textPaint);
        }

        // Draw toolbar items on the right
        DrawToolbarItems(canvas, bounds);

        // Draw shadow
        using var shadowPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 30),
            Style = SKPaintStyle.Fill,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 2)
        };
        canvas.DrawRect(new SKRect(bounds.Left, bounds.Bottom, bounds.Right, bounds.Bottom + 4), shadowPaint);
    }

    private void DrawToolbarItems(SKCanvas canvas, SKRect navBarBounds)
    {
        var primaryItems = _toolbarItems.Where(t => t.Order == SkiaToolbarItemOrder.Primary).ToList();
        Console.WriteLine($"[SkiaContentPage] DrawToolbarItems: {primaryItems.Count} primary items, navBarBounds={navBarBounds}");
        if (primaryItems.Count == 0) return;

        using var font = new SKFont(SKTypeface.Default, 14);
        using var textPaint = new SKPaint(font)
        {
            Color = TitleTextColor,
            IsAntialias = true
        };

        float rightEdge = navBarBounds.Right - 16;

        foreach (var item in primaryItems.AsEnumerable().Reverse())
        {
            var textBounds = new SKRect();
            textPaint.MeasureText(item.Text, ref textBounds);

            var itemWidth = textBounds.Width + 24; // Padding
            var itemLeft = rightEdge - itemWidth;

            // Store hit area for click handling
            item.HitBounds = new SKRect(itemLeft, navBarBounds.Top, rightEdge, navBarBounds.Bottom);
            Console.WriteLine($"[SkiaContentPage] Toolbar item '{item.Text}' HitBounds set to {item.HitBounds}");

            // Draw text
            var x = itemLeft + 12;
            var y = navBarBounds.MidY - textBounds.MidY;
            canvas.DrawText(item.Text, x, y, textPaint);

            rightEdge = itemLeft - 8; // Gap between items
        }
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        Console.WriteLine($"[SkiaContentPage] OnPointerPressed at ({e.X}, {e.Y}), ShowNavigationBar={ShowNavigationBar}, NavigationBarHeight={NavigationBarHeight}");
        Console.WriteLine($"[SkiaContentPage] ToolbarItems count: {_toolbarItems.Count}");

        // Check toolbar item clicks
        if (ShowNavigationBar && e.Y < NavigationBarHeight)
        {
            Console.WriteLine($"[SkiaContentPage] In navigation bar area, checking toolbar items");
            foreach (var item in _toolbarItems.Where(t => t.Order == SkiaToolbarItemOrder.Primary))
            {
                var bounds = item.HitBounds;
                var contains = bounds.Contains(e.X, e.Y);
                Console.WriteLine($"[SkiaContentPage] Checking item '{item.Text}', HitBounds=({bounds.Left},{bounds.Top},{bounds.Right},{bounds.Bottom}), Click=({e.X},{e.Y}), Contains={contains}, Command={item.Command != null}");
                if (contains)
                {
                    Console.WriteLine($"[SkiaContentPage] Toolbar item clicked: {item.Text}");
                    item.Command?.Execute(null);
                    return;
                }
            }
            Console.WriteLine($"[SkiaContentPage] No toolbar item hit");
        }

        base.OnPointerPressed(e);
    }
}

/// <summary>
/// Represents a toolbar item in the navigation bar.
/// </summary>
public class SkiaToolbarItem
{
    public string Text { get; set; } = "";
    public SkiaToolbarItemOrder Order { get; set; } = SkiaToolbarItemOrder.Primary;
    public System.Windows.Input.ICommand? Command { get; set; }
    public SKRect HitBounds { get; set; }
}

/// <summary>
/// Order of toolbar items.
/// </summary>
public enum SkiaToolbarItemOrder
{
    Primary,
    Secondary
}
