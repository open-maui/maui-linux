// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Rendering;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Base class for Skia-rendered pages.
/// </summary>
public class SkiaPage : SkiaView
{
    private SkiaView? _content;

    /// <summary>
    /// Theme-refresh walker hook: yield this page's <see cref="Content"/>. The
    /// content lives in a private field rather than the standard Children list,
    /// so without this override the walker can't reach the page's view tree
    /// (e.g. the CollectionView inside TodoApp's TodoListPage).
    /// </summary>
    public override IEnumerable<SkiaView> ExtraContentRoots
    {
        get
        {
            if (_content != null) yield return _content;
        }
    }
    private string _title = "";
    protected SKColor _titleBarColor = SkiaTheme.PrimarySK;
    protected SKColor _titleTextColor = SKColors.White;
    private Color _titleBarColorMaui = Color.FromRgb(0x21, 0x96, 0xF3); // Material Blue
    private Color _titleTextColorMaui = Colors.White;
    private bool _showNavigationBar = false;
    private float _navigationBarHeight = 56;

    // Padding
    private float _paddingLeft;
    private float _paddingTop;
    private float _paddingRight;
    private float _paddingBottom;

    /// <summary>
    /// Reference to the MAUI Page for handler access during theme refresh.
    /// </summary>
    public Microsoft.Maui.Controls.Page? MauiPage { get; set; }

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
            _contentLaidOutFor = null;
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

    public Color TitleBarColor
    {
        get => _titleBarColorMaui;
        set
        {
            _titleBarColorMaui = value;
            _titleBarColor = value.ToSKColor();
            Invalidate();
        }
    }

    public Color TitleTextColor
    {
        get => _titleTextColorMaui;
        set
        {
            _titleTextColorMaui = value;
            _titleTextColor = value.ToSKColor();
            Invalidate();
        }
    }

    public bool ShowNavigationBar
    {
        get => _showNavigationBar;
        set
        {
            _showNavigationBar = value;
            _contentLaidOutFor = null;
            Invalidate();
        }
    }

    public float NavigationBarHeight
    {
        get => _navigationBarHeight;
        set
        {
            _navigationBarHeight = value;
            _contentLaidOutFor = null;
            Invalidate();
        }
    }

    public float PaddingLeft
    {
        get => _paddingLeft;
        set { _paddingLeft = value; _contentLaidOutFor = null; Invalidate(); }
    }

    public float PaddingTop
    {
        get => _paddingTop;
        set { _paddingTop = value; _contentLaidOutFor = null; Invalidate(); }
    }

    public float PaddingRight
    {
        get => _paddingRight;
        set { _paddingRight = value; _contentLaidOutFor = null; Invalidate(); }
    }

    public float PaddingBottom
    {
        get => _paddingBottom;
        set { _paddingBottom = value; _contentLaidOutFor = null; Invalidate(); }
    }

    public bool IsBusy { get; set; }

    /// <summary>
    /// Icon image source for this page (used by navigation containers).
    /// </summary>
    public SKBitmap? IconImage { get; set; }

    /// <summary>
    /// Background image source for this page.
    /// </summary>
    public SKBitmap? BackgroundImage { get; set; }

    // Lifecycle events
    public event EventHandler? Appearing;
    public event EventHandler? Disappearing;
    public event EventHandler? NavigatedTo;
    public event EventHandler? NavigatedFrom;
    public event EventHandler? NavigatingFrom;

    private SKRect? _contentLaidOutFor;

    protected override Rect ArrangeOverride(Rect bounds)
    {
        LayoutContent(new SKRect((float)bounds.Left, (float)bounds.Top, (float)bounds.Right, (float)bounds.Bottom));
        return bounds;
    }

    /// <summary>
    /// Measures and arranges the content below the navigation bar, inside the
    /// page padding and the content's margin; a content view with
    /// Start/Center/End options keeps its desired size within the page,
    /// exactly like any other single-child container.
    /// </summary>
    private void LayoutContent(SKRect bounds)
    {
        _contentLaidOutFor = bounds;
        if (_content == null)
            return;

        var contentTop = _showNavigationBar ? bounds.Top + _navigationBarHeight : bounds.Top;
        var contentBounds = new SKRect(
            bounds.Left + _paddingLeft,
            contentTop + _paddingTop,
            bounds.Right - _paddingRight,
            bounds.Bottom - _paddingBottom);

        var margin = _content.Margin;
        var adjustedBounds = new SKRect(
            contentBounds.Left + (float)margin.Left,
            contentBounds.Top + (float)margin.Top,
            contentBounds.Right - (float)margin.Right,
            contentBounds.Bottom - (float)margin.Bottom);

        var availableSize = new Size(adjustedBounds.Width, adjustedBounds.Height);
        var desired = _content.Measure(availableSize);
        _content.Arrange(AlignContent(_content, adjustedBounds, desired));
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        // Use BackgroundColor if explicitly set (including via AppThemeBinding),
        // otherwise fall back to theme-aware default
        // Unset: the theme's page background. Set, including an explicit
        // Transparent: that colour. A see-through page is how popups presented
        // as modal pages (Mopups' PopupPage) show the page beneath them.
        SKColor bgColor = BackgroundColor != null
            ? GetEffectiveBackgroundColor()
            : SkiaTheme.CurrentPageBackgroundSK;

        if (bgColor.Alpha > 0)
        {
            using var bgPaint = new SKPaint
            {
                Color = bgColor,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(bounds, bgPaint);
        }

        // Draw background image if set
        if (BackgroundImage != null)
        {
            var destRect = new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
            canvas.DrawBitmap(BackgroundImage, destRect);
        }

        // Draw navigation bar if visible
        if (_showNavigationBar)
        {
            DrawNavigationBar(canvas, new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Top + _navigationBarHeight));
        }

        // Draw content: laid out in the layout pass (ArrangeOverride), so a
        // change a SizeChanged handler makes there settles before this frame
        // is drawn. A page drawn at bounds it was not arranged to (a
        // screenshot) lays its content out here.
        if (_content != null)
        {
            if (_contentLaidOutFor != bounds)
                LayoutContent(bounds);
            DiagnosticLog.Debug("SkiaPage", $"Drawing content: {_content.GetType().Name}, Bounds={_content.Bounds}, IsVisible={_content.IsVisible}");
            _content.Draw(canvas);
        }

        // Draw busy indicator overlay
        if (IsBusy)
        {
            DrawBusyIndicator(canvas, bounds);
        }
    }

    /// <summary>
    /// Places the page content within <paramref name="area"/> according to its
    /// HorizontalOptions/VerticalOptions: Fill (the default) takes the whole
    /// area, Start/Center/End take the desired size, clamped to the area.
    /// </summary>
    internal static Rect AlignContent(SkiaView content, SKRect area, Size desired)
    {
        var hAlign = content.MauiView is IView hv
            ? (LayoutAlignment)(int)hv.HorizontalLayoutAlignment
            : LayoutAlignmentHelper.MapFromMaui(content.HorizontalOptions);
        var vAlign = content.MauiView is IView vv
            ? (LayoutAlignment)(int)vv.VerticalLayoutAlignment
            : LayoutAlignmentHelper.MapFromMaui(content.VerticalOptions);

        static (double Start, double Length) Place(LayoutAlignment align, double start, double available, double wanted)
        {
            if (align == LayoutAlignment.Fill || wanted <= 0 || double.IsNaN(wanted) || wanted >= available)
                return (start, available);
            return align switch
            {
                LayoutAlignment.Center => (start + (available - wanted) / 2, wanted),
                LayoutAlignment.End => (start + available - wanted, wanted),
                _ => (start, wanted),
            };
        }

        var (x, w) = Place(hAlign, area.Left, area.Width, desired.Width);
        var (y, h) = Place(vAlign, area.Top, area.Height, desired.Height);
        return new Rect(x, y, w, h);
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
            using var font = SkiaFontFactory.Create(20);
            using var textPaint = new SKPaint
            {
                Color = _titleTextColor,
                IsAntialias = true
            };

            var x = bounds.Left + 16;
            var y = TextRenderingHelper.BaselineForVerticalCenter(font, bounds.MidY);
            canvas.DrawText(_title, x, y, font, textPaint);
        }

        // Draw shadow
        using var shadowPaint = new SKPaint
        {
            Color = SkiaTheme.Shadow20SK,
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
            Color = SkiaTheme.WhiteSemiTransparentSK,
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
        DiagnosticLog.Debug("SkiaPage", $"OnAppearing called for: {Title}, HasListeners={Appearing != null}");
        Appearing?.Invoke(this, EventArgs.Empty);
    }

    public void OnDisappearing()
    {
        Disappearing?.Invoke(this, EventArgs.Empty);
    }

    public void OnNavigatedTo()
    {
        NavigatedTo?.Invoke(this, EventArgs.Empty);
    }

    public void OnNavigatedFrom()
    {
        NavigatedFrom?.Invoke(this, EventArgs.Empty);
    }

    public void OnNavigatingFrom()
    {
        NavigatingFrom?.Invoke(this, EventArgs.Empty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Page takes all available space
        return availableSize;
    }

    /// <summary>
    /// Forwarding is for presses on the content; a press on the page area
    /// around a Start/Center/End-aligned content view must not reach it.
    /// </summary>
    private bool ContentContains(PointerEventArgs e)
    {
        var b = _content!.Bounds;
        return b.Width <= 0 || b.Height <= 0 || b.Contains(e.X, e.Y);
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        // Adjust coordinates for content
        var contentTop = _showNavigationBar ? _navigationBarHeight : 0;
        if (e.Y > contentTop && _content != null && ContentContains(e))
        {
            var contentE = new PointerEventArgs(e.X - _paddingLeft, e.Y - contentTop - _paddingTop, e.Button);
            _content.OnPointerPressed(contentE);
        }
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        var contentTop = _showNavigationBar ? _navigationBarHeight : 0;
        if (e.Y > contentTop && _content != null && ContentContains(e))
        {
            var contentE = new PointerEventArgs(e.X - _paddingLeft, e.Y - contentTop - _paddingTop, e.Button);
            _content.OnPointerMoved(contentE);
        }
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        var contentTop = _showNavigationBar ? _navigationBarHeight : 0;
        if (e.Y > contentTop && _content != null && ContentContains(e))
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
            Color = _titleBarColor,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(bounds, barPaint);

        // Draw title
        if (!string.IsNullOrEmpty(Title))
        {
            using var font = SkiaFontFactory.Create(20);
            using var textPaint = new SKPaint
            {
                Color = _titleTextColor,
                IsAntialias = true
            };

            var x = bounds.Left + 56; // Leave space for back button
            var y = TextRenderingHelper.BaselineForVerticalCenter(font, bounds.MidY);
            canvas.DrawText(Title, x, y, font, textPaint);
        }

        // Draw toolbar items on the right
        DrawToolbarItems(canvas, bounds);

        // Draw shadow
        using var shadowPaint = new SKPaint
        {
            Color = SkiaTheme.Shadow20SK,
            Style = SKPaintStyle.Fill,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 2)
        };
        canvas.DrawRect(new SKRect(bounds.Left, bounds.Bottom, bounds.Right, bounds.Bottom + 4), shadowPaint);
    }

    private void DrawToolbarItems(SKCanvas canvas, SKRect navBarBounds)
    {
        var primaryItems = _toolbarItems.Where(t => t.Order == SkiaToolbarItemOrder.Primary).ToList();
        DiagnosticLog.Debug("SkiaContentPage", $"DrawToolbarItems: {primaryItems.Count} primary items, navBarBounds={navBarBounds}");
        if (primaryItems.Count == 0) return;

        using var font = SkiaFontFactory.Create(14);
        using var textPaint = new SKPaint
        {
            Color = _titleTextColor,
            IsAntialias = true
        };

        float rightEdge = navBarBounds.Right - 16;
        const float iconSize = 24f;
        const float itemPadding = 12f;

        foreach (var item in primaryItems.AsEnumerable().Reverse())
        {
            float itemWidth;
            float itemLeft;

            if (item.Icon != null)
            {
                // Icon-based toolbar item
                itemWidth = iconSize + itemPadding * 2;
                itemLeft = rightEdge - itemWidth;

                // Store hit area for click handling
                item.HitBounds = new SKRect(itemLeft, navBarBounds.Top, rightEdge, navBarBounds.Bottom);

                // Draw icon centered in the hit area
                var iconX = itemLeft + itemPadding;
                var iconY = navBarBounds.MidY - iconSize / 2;
                var destRect = new SKRect(iconX, iconY, iconX + iconSize, iconY + iconSize);
                canvas.DrawBitmap(item.Icon, destRect);

                DiagnosticLog.Debug("SkiaContentPage", $"Drew toolbar icon '{item.Text}' at ({iconX}, {iconY})");
            }
            else
            {
                // Text-based toolbar item (fallback)
                font.MeasureText(item.Text, out var textBounds);

                itemWidth = textBounds.Width + 24;
                itemLeft = rightEdge - itemWidth;

                // Store hit area for click handling
                item.HitBounds = new SKRect(itemLeft, navBarBounds.Top, rightEdge, navBarBounds.Bottom);

                // Draw text
                var x = itemLeft + 12;
                var y = TextRenderingHelper.BaselineForVerticalCenter(font, navBarBounds.MidY);
                canvas.DrawText(item.Text, x, y, font, textPaint);
            }

            DiagnosticLog.Debug("SkiaContentPage", $"Toolbar item '{item.Text}' HitBounds set to {item.HitBounds}");
            rightEdge = itemLeft - 8; // Gap between items
        }
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        DiagnosticLog.Debug("SkiaContentPage", $"OnPointerPressed at ({e.X}, {e.Y}), ShowNavigationBar={ShowNavigationBar}, NavigationBarHeight={NavigationBarHeight}");
        DiagnosticLog.Debug("SkiaContentPage", $"ToolbarItems count: {_toolbarItems.Count}");

        // Check toolbar item clicks
        if (ShowNavigationBar && e.Y < NavigationBarHeight)
        {
            DiagnosticLog.Debug("SkiaContentPage", "In navigation bar area, checking toolbar items");
            foreach (var item in _toolbarItems.Where(t => t.Order == SkiaToolbarItemOrder.Primary))
            {
                var bounds = item.HitBounds;
                var contains = bounds.Contains(e.X, e.Y);
                DiagnosticLog.Debug("SkiaContentPage", $"Checking item '{item.Text}', HitBounds=({bounds.Left},{bounds.Top},{bounds.Right},{bounds.Bottom}), Click=({e.X},{e.Y}), Contains={contains}, Command={item.Command != null}");
                if (contains)
                {
                    DiagnosticLog.Debug("SkiaContentPage", $"Toolbar item clicked: {item.Text}");
                    item.Command?.Execute(null);
                    return;
                }
            }
            DiagnosticLog.Debug("SkiaContentPage", "No toolbar item hit");
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
    public SKBitmap? Icon { get; set; }
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
