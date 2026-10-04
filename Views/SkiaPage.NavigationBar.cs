// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Rendering;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// What a navigation container (a NavigationPage) shows in this page's navigation bar besides
/// its title, as MAUI's Windows toolbar lays it out: the title icon
/// (NavigationPage.TitleIconImageSource), then the title, then the TitleView
/// (NavigationPage.TitleView), which takes the rest of the bar up to the toolbar items (MAUI
/// clears the title while a TitleView is set). The bar is painted with its brush
/// (NavigationPage.BarBackground, gradients included), and the back arrow takes the icon colour
/// (NavigationPage.IconColor).
/// </summary>
public partial class SkiaPage
{
    private SkiaView? _titleView;
    private Rect _titleViewBounds;
    private Thickness _titleViewMargin;

    /// <summary>
    /// The view shown in the navigation bar in the title's place (the platform view of the
    /// page's NavigationPage.TitleView), null for none.
    /// </summary>
    public SkiaView? TitleView
    {
        get => _titleView;
        set
        {
            if (ReferenceEquals(_titleView, value))
                return;
            if (_titleView != null && ReferenceEquals(_titleView.Parent, this))
                _titleView.Parent = null;
            _titleView = value;
            _titleViewBounds = Rect.Zero;
            if (_titleView != null)
                _titleView.Parent = this; // its invalidations reach the window
            Invalidate();
        }
    }

    /// <summary>The TitleView's margin in the bar (the MAUI view's Margin, as Windows applies it).</summary>
    public Thickness TitleViewMargin
    {
        get => _titleViewMargin;
        set
        {
            _titleViewMargin = value;
            Invalidate();
        }
    }

    /// <summary>Where the navigation bar placed the TitleView in the last frame.</summary>
    internal Rect TitleViewBounds => _titleViewBounds;

    private SKBitmap? _titleIcon;

    /// <summary>The image left of the title (NavigationPage.TitleIconImageSource), null for none.</summary>
    public SKBitmap? TitleIcon
    {
        get => _titleIcon;
        set
        {
            _titleIcon = value;
            Invalidate();
        }
    }

    /// <summary>Where the navigation bar drew the title icon in the last frame (empty when none).</summary>
    internal SKRect TitleIconBounds { get; private set; }

    private Color? _iconColor;

    /// <summary>
    /// The colour of the bar's navigation icons, the back arrow (NavigationPage.IconColor); null
    /// draws them in the bar's text colour.
    /// </summary>
    public Color? IconColor
    {
        get => _iconColor;
        set
        {
            _iconColor = value;
            Invalidate();
        }
    }

    /// <summary>
    /// The title of the back button to this page's predecessor (NavigationPage.BackButtonTitle of
    /// the page beneath), as MAUI's toolbar carries it. Windows' navigation bar shows no text on
    /// its back button (its BackButtonTitle mapping only refreshes the button), and neither does
    /// this one.
    /// </summary>
    public string? BackButtonTitle { get; set; }

    private Microsoft.Maui.Controls.Brush? _titleBarBrush;

    /// <summary>
    /// The bar's brush (NavigationPage.BarBackground: solid, linear or radial); null paints
    /// it with <see cref="TitleBarColor"/>.
    /// </summary>
    public Microsoft.Maui.Controls.Brush? TitleBarBrush
    {
        get => _titleBarBrush;
        set
        {
            _titleBarBrush = value;
            Invalidate();
        }
    }

    /// <summary>The bar's background: its brush, else its colour.</summary>
    protected void DrawNavigationBarBackground(SKCanvas canvas, SKRect bounds)
    {
        using var brushPaint = BrushPaint.Create(_titleBarBrush, bounds);
        if (brushPaint != null)
        {
            canvas.DrawRect(bounds, brushPaint);
            return;
        }
        using var barPaint = new SKPaint
        {
            Color = _titleBarColor,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(bounds, barPaint);
    }

    /// <summary>
    /// The bar's title area from <paramref name="left"/> to <paramref name="right"/>: the title
    /// icon, then the title text, then the TitleView filling what is left (Windows' toolbar
    /// columns). The title is not drawn while a TitleView is shown (MAUI clears it).
    /// </summary>
    protected void DrawTitleArea(SKCanvas canvas, SKRect bounds, float left, float right)
    {
        float x = left;
        TitleIconBounds = SKRect.Empty;
        if (_titleIcon is { Width: > 0, Height: > 0 } icon)
        {
            // At most 24 logical pixels tall (the bar's icon size), the aspect kept.
            float scale = Math.Max(1f, DeviceScale);
            float height = Math.Min(24f, Math.Min(bounds.Height - 8f, icon.Height / scale));
            float width = Math.Min(height * icon.Width / icon.Height, Math.Max(0, right - x));
            var dest = new SKRect(x, bounds.MidY - height / 2, x + width, bounds.MidY + height / 2);
            using var image = SKImage.FromBitmap(icon);
            using var paint = new SKPaint { IsAntialias = true };
            canvas.DrawImage(image, dest, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), paint);
            TitleIconBounds = dest;
            x = dest.Right + 10; // Windows' title margin
        }

        if (_titleView != null)
        {
            var slot = new SKRect(
                x + (float)_titleViewMargin.Left,
                bounds.Top + (float)_titleViewMargin.Top,
                Math.Max(x, right - (float)_titleViewMargin.Right),
                Math.Max(bounds.Top, bounds.Bottom - (float)_titleViewMargin.Bottom));
            var rect = new Rect(slot.Left, slot.Top, Math.Max(0, slot.Width), Math.Max(0, slot.Height));
            _titleViewBounds = rect;
            _titleView.Measure(new Size(rect.Width, rect.Height));
            _titleView.Arrange(rect);
            canvas.Save();
            canvas.ClipRect(slot);
            _titleView.Draw(canvas);
            canvas.Restore();
            return;
        }
        _titleViewBounds = Rect.Zero;

        if (string.IsNullOrEmpty(Title) || right <= x)
            return;
        using var font = SkiaFontFactory.Create(20);
        using var textPaint = new SKPaint
        {
            Color = _titleTextColor,
            IsAntialias = true
        };
        var y = TextRenderingHelper.BaselineForVerticalCenter(font, bounds.MidY);
        canvas.Save();
        canvas.ClipRect(new SKRect(x, bounds.Top, right, bounds.Bottom));
        canvas.DrawText(Title, x, y, font, textPaint);
        canvas.Restore();
    }

    /// <summary>The TitleView's view under a point of the navigation bar, null when none is there.</summary>
    private SkiaView? HitTestTitleView(float x, float y)
    {
        if (_titleView == null || !ShowNavigationBar || _titleViewBounds.Width <= 0 || !_titleViewBounds.Contains(x, y))
            return null;
        return _titleView.HitTestAt(x, y);
    }
}
