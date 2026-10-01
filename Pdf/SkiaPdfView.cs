// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Pdf;

/// <summary>The page in view of a <see cref="SkiaPdfView"/> changed.</summary>
public sealed class PdfPageChangedEventArgs : EventArgs
{
    public PdfPageChangedEventArgs(int pageIndex, int pageCount)
    {
        PageIndex = pageIndex;
        PageCount = pageCount;
    }

    /// <summary>The page most in view, from 0; -1 when there is none.</summary>
    public int PageIndex { get; }

    public int PageCount { get; }
}

/// <summary>
/// A PDF's pages in a scrolling strip, rendered by PDFium: at their natural size (96 DPI) times
/// <see cref="Zoom"/>, centred across the strip, with a margin, an optional shadow and an optional
/// crop. Only the pages in view are rendered, in the background, at the screen's pixel density.
/// Wheel scrolls; Ctrl+wheel and Ctrl +, -, 0 zoom (1 to <see cref="MaxZoom"/>); the scrollbar
/// drags; arrows, Page Up/Down, Home and End move when it has focus. A platform view a library's
/// PDF control (MarketAlly.ViewEngine's PdfView) maps onto.
/// </summary>
public class SkiaPdfView : SkiaView
{
    private const float PixelsPerPoint = 96f / 72f;
    private const float WheelStep = 40f;
    private const float ScrollBarWidth = 8f;
    private static readonly SKColor ScrollBarColor = new(0x80, 0x80, 0x80, 0x80);

    private PdfiumDocument? _document;
    private bool _ownsDocument;
    private SKSize[] _pageSizes = Array.Empty<SKSize>(); // in points, cropped
    private float _zoom = 1f;
    private float _maxZoom = 4f;
    private bool _isHorizontal;
    private Thickness _pageMargin = new(16, 8);
    private Thickness _crop;
    private bool _shadowEnabled = true;
    private float _scrollX, _scrollY;
    private int _pageIndex = -1;
    private bool _settingPageIndex;

    private readonly Dictionary<int, SKBitmap> _bitmaps = new();
    private readonly HashSet<int> _rendering = new();
    private readonly Lock _cacheLock = new();
    private int _generation; // bumped when the document or crop changes: stale renders are dropped

    private bool _draggingScrollBar;
    private float _dragStart, _dragStartScroll;

    public SkiaPdfView()
    {
        IsFocusable = true;
    }

    /// <summary>The page most in view changed (also when a document loads).</summary>
    public event EventHandler<PdfPageChangedEventArgs>? PageChanged;

    /// <summary>The document shown, or null.</summary>
    public PdfiumDocument? Document => _document;

    /// <summary>The number of pages shown.</summary>
    public int PageCount => _pageSizes.Length;

    /// <summary>Shows a document (null clears). With <paramref name="takeOwnership"/>, the view disposes it when replaced.</summary>
    public void Load(PdfiumDocument? document, bool takeOwnership = true)
    {
        lock (_cacheLock)
        {
            foreach (var bitmap in _bitmaps.Values)
                bitmap.Dispose();
            _bitmaps.Clear();
            _rendering.Clear();
            _generation++;
        }
        if (_ownsDocument && _document != null && !ReferenceEquals(_document, document))
            _document.Dispose();
        _document = document;
        _ownsDocument = takeOwnership;
        _zoom = 1f;
        _scrollX = _scrollY = 0;
        _pageIndex = -1;
        ReadPageSizes();
        InvalidateMeasure();
        Invalidate();
        UpdatePageIndex();
    }

    /// <summary>Opens and shows the PDF at <paramref name="path"/>.</summary>
    /// <exception cref="PdfiumException">Not a PDF, damaged, or the password is missing or wrong.</exception>
    public void LoadFile(string path, string? password = null) =>
        Load(PdfiumDocument.Open(File.ReadAllBytes(path), password));

    /// <summary>Lays the pages out vertically (the default) or horizontally.</summary>
    public bool IsHorizontal
    {
        get => _isHorizontal;
        set
        {
            if (_isHorizontal == value) return;
            _isHorizontal = value;
            _scrollX = _scrollY = 0;
            Invalidate();
            UpdatePageIndex();
        }
    }

    /// <summary>The largest zoom, at least 1 (the default is 4).</summary>
    public float MaxZoom
    {
        get => _maxZoom;
        set
        {
            if (value < 1f)
                throw new ArgumentOutOfRangeException(nameof(value), "MaxZoom cannot be less than 1.");
            _maxZoom = value;
            if (_zoom > value)
                Zoom = value;
        }
    }

    /// <summary>The zoom, from 1 (natural size) to <see cref="MaxZoom"/>.</summary>
    public float Zoom
    {
        get => _zoom;
        set => ZoomAt(value, (float)Bounds.Center.X, (float)Bounds.Center.Y);
    }

    /// <summary>Space around each page (the default is 16, 8).</summary>
    public Thickness PageMargin
    {
        get => _pageMargin;
        set { _pageMargin = value; Invalidate(); UpdatePageIndex(); }
    }

    /// <summary>A shadow under each page (the default is on).</summary>
    public bool ShadowEnabled
    {
        get => _shadowEnabled;
        set { _shadowEnabled = value; Invalidate(); }
    }

    /// <summary>Points cut from each page's edges.</summary>
    public Thickness Crop
    {
        get => _crop;
        set
        {
            _crop = value;
            lock (_cacheLock)
            {
                foreach (var bitmap in _bitmaps.Values)
                    bitmap.Dispose();
                _bitmaps.Clear();
                _rendering.Clear();
                _generation++;
            }
            ReadPageSizes();
            Invalidate();
            UpdatePageIndex();
        }
    }

    /// <summary>The page most in view, from 0 (-1 with no document). Setting it scrolls to that page.</summary>
    public int PageIndex
    {
        get => _pageIndex;
        set
        {
            if (value < 0 || value >= _pageSizes.Length)
                return;
            var rect = PageRect(value, ViewportOrigin());
            _settingPageIndex = true;
            try
            {
                if (_isHorizontal)
                    SetScroll(_scrollX + (float)(rect.Left - _pageMargin.Left - Bounds.Left), _scrollY);
                else
                    SetScroll(_scrollX, _scrollY + (float)(rect.Top - _pageMargin.Top - Bounds.Top));
            }
            finally
            {
                _settingPageIndex = false;
            }
            SetPageIndex(value);
        }
    }

    private void ReadPageSizes()
    {
        var document = _document;
        if (document == null)
        {
            _pageSizes = Array.Empty<SKSize>();
            return;
        }
        var sizes = new SKSize[document.PageCount];
        for (int i = 0; i < sizes.Length; i++)
        {
            var size = document.GetPageSize(i);
            sizes[i] = new SKSize(
                Math.Max(1f, size.Width - (float)(_crop.Left + _crop.Right)),
                Math.Max(1f, size.Height - (float)(_crop.Top + _crop.Bottom)));
        }
        _pageSizes = sizes;
    }

    // ---- Layout -------------------------------------------------------------------------------

    private float PageWidth(int i) => _pageSizes[i].Width * PixelsPerPoint * _zoom;
    private float PageHeight(int i) => _pageSizes[i].Height * PixelsPerPoint * _zoom;

    /// <summary>The strip's size: pages and margins along the axis, the widest page across it.</summary>
    private SKSize ContentSize()
    {
        float along = 0, across = 0;
        for (int i = 0; i < _pageSizes.Length; i++)
        {
            if (_isHorizontal)
            {
                along += PageWidth(i) + (float)_pageMargin.HorizontalThickness;
                across = Math.Max(across, PageHeight(i) + (float)_pageMargin.VerticalThickness);
            }
            else
            {
                along += PageHeight(i) + (float)_pageMargin.VerticalThickness;
                across = Math.Max(across, PageWidth(i) + (float)_pageMargin.HorizontalThickness);
            }
        }
        return _isHorizontal ? new SKSize(along, across) : new SKSize(across, along);
    }

    /// <summary>Where the content's top left is drawn, scroll applied.</summary>
    private SKPoint ViewportOrigin() => new((float)Bounds.Left - _scrollX, (float)Bounds.Top - _scrollY);

    /// <summary>Page <paramref name="index"/>'s rectangle, in view coordinates.</summary>
    private SKRect PageRect(int index, SKPoint origin)
    {
        var content = ContentSize();
        float viewWidth = (float)Bounds.Width, viewHeight = (float)Bounds.Height;
        float offset = 0;
        for (int i = 0; i < index; i++)
            offset += _isHorizontal
                ? PageWidth(i) + (float)_pageMargin.HorizontalThickness
                : PageHeight(i) + (float)_pageMargin.VerticalThickness;
        float w = PageWidth(index), h = PageHeight(index);
        if (_isHorizontal)
        {
            float left = origin.X + offset + (float)_pageMargin.Left;
            float band = Math.Max(viewHeight, content.Height);
            float top = origin.Y + (band - h) / 2f;
            return new SKRect(left, top, left + w, top + h);
        }
        else
        {
            float top = origin.Y + offset + (float)_pageMargin.Top;
            float band = Math.Max(viewWidth, content.Width);
            float left = origin.X + (band - w) / 2f;
            return new SKRect(left, top, left + w, top + h);
        }
    }

    private float MaxScrollX() => Math.Max(0, ContentSize().Width - (float)Bounds.Width);
    private float MaxScrollY() => Math.Max(0, ContentSize().Height - (float)Bounds.Height);

    private void SetScroll(float x, float y)
    {
        float nx = Math.Clamp(x, 0, MaxScrollX()), ny = Math.Clamp(y, 0, MaxScrollY());
        if (nx == _scrollX && ny == _scrollY)
            return;
        _scrollX = nx;
        _scrollY = ny;
        Invalidate();
        UpdatePageIndex();
    }

    /// <summary>Zooms to <paramref name="zoom"/> keeping the page point under (x, y) in place.</summary>
    public void ZoomAt(float zoom, float x, float y)
    {
        zoom = Math.Clamp(zoom, 1f, _maxZoom);
        if (Math.Abs(zoom - _zoom) < 0.0001f || _pageSizes.Length == 0)
        {
            _zoom = zoom;
            return;
        }

        // The page under (x, y), or the nearest along the strip, and where in it (u, v from 0 to 1).
        var origin = ViewportOrigin();
        int anchor = 0;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < _pageSizes.Length; i++)
        {
            var r = PageRect(i, origin);
            float distance = _isHorizontal
                ? (x < r.Left ? r.Left - x : x > r.Right ? x - r.Right : 0)
                : (y < r.Top ? r.Top - y : y > r.Bottom ? y - r.Bottom : 0);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                anchor = i;
            }
        }
        var before = PageRect(anchor, origin);
        float u = (x - before.Left) / before.Width;
        float v = (y - before.Top) / before.Height;

        // At the new zoom, the scroll that puts that page point back under (x, y). Pages are
        // centred across the strip, so solve from the page's position with no scroll.
        _zoom = zoom;
        var unscrolled = PageRect(anchor, new SKPoint((float)Bounds.Left, (float)Bounds.Top));
        float scrollX = unscrolled.Left + u * unscrolled.Width - x;
        float scrollY = unscrolled.Top + v * unscrolled.Height - y;
        _scrollX = Math.Clamp(scrollX, 0, MaxScrollX());
        _scrollY = Math.Clamp(scrollY, 0, MaxScrollY());
        Invalidate();
        UpdatePageIndex();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Fills what it is given; a PDF has no natural size to ask for.
        double w = double.IsInfinity(availableSize.Width) ? 400 : availableSize.Width;
        double h = double.IsInfinity(availableSize.Height) ? 400 : availableSize.Height;
        return new Size(w, h);
    }

    protected override Rect ArrangeOverride(Rect bounds)
    {
        var result = base.ArrangeOverride(bounds);
        SetScroll(_scrollX, _scrollY); // keep within the new size
        UpdatePageIndex();
        return result;
    }

    // ---- Page tracking ------------------------------------------------------------------------

    private void UpdatePageIndex()
    {
        if (_settingPageIndex)
            return;
        if (_pageSizes.Length == 0 || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            SetPageIndex(_pageSizes.Length == 0 ? -1 : 0);
            return;
        }
        var origin = ViewportOrigin();
        var view = new SKRect((float)Bounds.Left, (float)Bounds.Top, (float)Bounds.Right, (float)Bounds.Bottom);
        int best = 0;
        float bestVisible = -1;
        for (int i = 0; i < _pageSizes.Length; i++)
        {
            var r = PageRect(i, origin);
            float visible = _isHorizontal
                ? Math.Min(r.Right, view.Right) - Math.Max(r.Left, view.Left)
                : Math.Min(r.Bottom, view.Bottom) - Math.Max(r.Top, view.Top);
            if (visible > bestVisible)
            {
                bestVisible = visible;
                best = i;
            }
        }
        SetPageIndex(best);
    }

    private void SetPageIndex(int index)
    {
        if (index == _pageIndex)
            return;
        _pageIndex = index;
        try
        {
            PageChanged?.Invoke(this, new PdfPageChangedEventArgs(index, _pageSizes.Length));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaPdfView", "A PageChanged handler failed", ex);
        }
    }

    // ---- Drawing ------------------------------------------------------------------------------

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        var document = _document;
        if (document == null || _pageSizes.Length == 0)
            return;

        canvas.Save();
        canvas.ClipRect(bounds);
        float deviceScale = Math.Max(1f, canvas.TotalMatrix.ScaleX);
        var origin = ViewportOrigin();
        var visible = new HashSet<int>();

        using var white = new SKPaint { Color = SKColors.White, IsAntialias = true };
        using var shadow = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 0x50),
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6f),
        };
        using var imagePaint = new SKPaint { IsAntialias = true };

        for (int i = 0; i < _pageSizes.Length; i++)
        {
            var r = PageRect(i, origin);
            if (r.Bottom < bounds.Top - 1 || r.Top > bounds.Bottom + 1 || r.Right < bounds.Left - 1 || r.Left > bounds.Right + 1)
                continue;
            visible.Add(i);

            if (_shadowEnabled)
                canvas.DrawRoundRect(new SKRect(r.Left + 1, r.Top + 3, r.Right + 1, r.Bottom + 4), 4, 4, shadow);
            canvas.DrawRoundRect(r, 4, 4, white);

            int wantWidth = Math.Max(1, (int)Math.Round(r.Width * deviceScale));
            int wantHeight = Math.Max(1, (int)Math.Round(r.Height * deviceScale));
            SKBitmap? bitmap;
            lock (_cacheLock)
                _bitmaps.TryGetValue(i, out bitmap);
            if (bitmap != null)
            {
                using var image = SKImage.FromBitmap(bitmap);
                canvas.Save();
                canvas.ClipRoundRect(new SKRoundRect(r, 4, 4), antialias: true);
                canvas.DrawImage(image, r, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), imagePaint);
                canvas.Restore();
            }
            // Missing, or rendered for another size (zoom or density changed): render again.
            if (bitmap == null || Math.Abs(bitmap.Width - wantWidth) > 1)
                RequestRender(document, i, wantWidth, wantHeight);
        }

        EvictFarPages(visible);
        DrawScrollBar(canvas, bounds);
        canvas.Restore();
    }

    private void DrawScrollBar(SKCanvas canvas, SKRect bounds)
    {
        if (!TryGetThumb(out var thumb))
            return;
        using var paint = new SKPaint { Color = ScrollBarColor, IsAntialias = true };
        canvas.DrawRoundRect(thumb, ScrollBarWidth / 2, ScrollBarWidth / 2, paint);
    }

    /// <summary>The scrollbar thumb along the strip's axis, when the strip scrolls that way.</summary>
    private bool TryGetThumb(out SKRect thumb)
    {
        thumb = default;
        var content = ContentSize();
        if (_isHorizontal)
        {
            float max = MaxScrollX();
            if (max <= 0) return false;
            float track = (float)Bounds.Width;
            float length = Math.Max(20f, track * (float)Bounds.Width / content.Width);
            float left = (float)Bounds.Left + (track - length) * (_scrollX / max);
            thumb = new SKRect(left, (float)Bounds.Bottom - ScrollBarWidth - 2, left + length, (float)Bounds.Bottom - 2);
        }
        else
        {
            float max = MaxScrollY();
            if (max <= 0) return false;
            float track = (float)Bounds.Height;
            float length = Math.Max(20f, track * (float)Bounds.Height / content.Height);
            float top = (float)Bounds.Top + (track - length) * (_scrollY / max);
            thumb = new SKRect((float)Bounds.Right - ScrollBarWidth - 2, top, (float)Bounds.Right - 2, top + length);
        }
        return true;
    }

    private void RequestRender(PdfiumDocument document, int page, int width, int height)
    {
        int generation;
        lock (_cacheLock)
        {
            if (!_rendering.Add(page))
                return;
            generation = _generation;
        }
        var crop = _crop;
        var full = document.GetPageSize(page);
        var source = new SKRect((float)crop.Left, (float)crop.Top,
            full.Width - (float)crop.Right, full.Height - (float)crop.Bottom);
        Task.Run(() =>
        {
            SKBitmap? rendered = null;
            try
            {
                rendered = document.RenderRegion(page, source, width, height);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("SkiaPdfView", $"Rendering page {page} failed", ex);
            }
            lock (_cacheLock)
            {
                _rendering.Remove(page);
                if (rendered == null)
                    return;
                if (generation != _generation)
                {
                    rendered.Dispose(); // the document or crop changed while it rendered
                    return;
                }
                if (_bitmaps.TryGetValue(page, out var old))
                    old.Dispose();
                _bitmaps[page] = rendered;
            }
            Invalidate();
        });
    }

    /// <summary>Frees the bitmaps of pages more than two pages from the ones in view.</summary>
    private void EvictFarPages(HashSet<int> visible)
    {
        if (visible.Count == 0)
            return;
        int first = visible.Min() - 2, last = visible.Max() + 2;
        lock (_cacheLock)
        {
            foreach (var page in _bitmaps.Keys.Where(p => p < first || p > last).ToList())
            {
                _bitmaps[page].Dispose();
                _bitmaps.Remove(page);
            }
        }
    }

    // ---- Input --------------------------------------------------------------------------------

    public override void OnScroll(ScrollEventArgs e)
    {
        if (_pageSizes.Length == 0)
            return;
        if (e.IsControlPressed)
        {
            ZoomAt(_zoom * MathF.Pow(1.1f, -e.DeltaY), e.X, e.Y);
            e.Handled = true;
            return;
        }
        float oldX = _scrollX, oldY = _scrollY;
        if (_isHorizontal)
            SetScroll(_scrollX + (e.DeltaX + e.DeltaY) * WheelStep, _scrollY);
        else
            SetScroll(_scrollX + e.DeltaX * WheelStep, _scrollY + e.DeltaY * WheelStep);
        if (oldX != _scrollX || oldY != _scrollY)
            e.Handled = true;
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (TryGetThumb(out var thumb) && thumb.Contains(e.X, e.Y))
        {
            _draggingScrollBar = true;
            _dragStart = _isHorizontal ? e.X : e.Y;
            _dragStartScroll = _isHorizontal ? _scrollX : _scrollY;
            e.Handled = true;
            return;
        }
        base.OnPointerPressed(e);
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        if (_draggingScrollBar && TryGetThumb(out var thumb))
        {
            float track = (float)(_isHorizontal ? Bounds.Width : Bounds.Height);
            float length = _isHorizontal ? thumb.Width : thumb.Height;
            float max = _isHorizontal ? MaxScrollX() : MaxScrollY();
            float delta = (_isHorizontal ? e.X : e.Y) - _dragStart;
            float scroll = _dragStartScroll + (track - length > 0 ? delta / (track - length) * max : 0);
            if (_isHorizontal) SetScroll(scroll, _scrollY);
            else SetScroll(_scrollX, scroll);
            e.Handled = true;
            return;
        }
        base.OnPointerMoved(e);
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        if (_draggingScrollBar)
        {
            _draggingScrollBar = false;
            e.Handled = true;
            return;
        }
        base.OnPointerReleased(e);
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (_pageSizes.Length == 0)
            return;
        float page = (float)(_isHorizontal ? Bounds.Width : Bounds.Height) * 0.9f;
        bool control = (e.Modifiers & KeyModifiers.Control) != 0;
        float dx = 0, dy = 0;
        switch (e.Key)
        {
            case Key.Up: dy = -WheelStep; break;
            case Key.Down: dy = WheelStep; break;
            case Key.Left: dx = -WheelStep; break;
            case Key.Right: dx = WheelStep; break;
            case Key.PageUp: if (_isHorizontal) dx = -page; else dy = -page; break;
            case Key.PageDown:
            case Key.Space: if (_isHorizontal) dx = page; else dy = page; break;
            case Key.Home: PageIndex = 0; e.Handled = true; return;
            case Key.End: PageIndex = _pageSizes.Length - 1; e.Handled = true; return;
            case Key.Equals when control:
            case Key.NumPadAdd when control: Zoom = _zoom * 1.25f; e.Handled = true; return;
            case Key.Minus when control:
            case Key.NumPadSubtract when control: Zoom = _zoom / 1.25f; e.Handled = true; return;
            case Key.D0 when control: Zoom = 1f; e.Handled = true; return;
            default: return;
        }
        SetScroll(_scrollX + dx, _scrollY + dy);
        e.Handled = true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (_cacheLock)
            {
                foreach (var bitmap in _bitmaps.Values)
                    bitmap.Dispose();
                _bitmaps.Clear();
                _generation++;
            }
            if (_ownsDocument)
                _document?.Dispose();
            _document = null;
        }
        base.Dispose(disposing);
    }
}
