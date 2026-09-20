// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Rendering;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Styling intent of a <see cref="SkiaTableView"/>; mirrors MAUI's TableIntent.
/// </summary>
public enum SkiaTableIntent
{
    Menu,
    Settings,
    Form,
    Data
}

/// <summary>
/// A titled group of rows in a <see cref="SkiaTableView"/>.
/// </summary>
public class SkiaTableSection
{
    private readonly List<SkiaCellView> _cells = new();

    public SkiaTableSection() { }

    public SkiaTableSection(string? title) { Title = title; }

    /// <summary>Section title; an empty title draws no header band.</summary>
    public string? Title { get; set; }

    /// <summary>Title colour override.</summary>
    public Color? TitleColor { get; set; }

    /// <summary>The MAUI section this mirrors, when created from one.</summary>
    public object? Tag { get; set; }

    public IReadOnlyList<SkiaCellView> Cells => _cells;

    internal List<SkiaCellView> CellList => _cells;
}

/// <summary>
/// A sectioned, scrollable list of cells: section header bands, rows with
/// hairline separators and intent-based styling — the platform's TableView.
/// Rows are <see cref="SkiaCellView"/> children (so their editors, switches
/// and hosted content receive input); the table itself owns row taps.
/// </summary>
public class SkiaTableView : SkiaLayoutView
{
    public const float SectionHeaderHeight = 36f;
    private const float ScrollBarWidth = 6f;

    private readonly List<SkiaTableSection> _sections = new();
    private readonly List<SKRect> _headerRects = new();
    private float _rowHeight = SkiaCellView.DefaultHeight;
    private bool _hasUnevenRows;
    private SkiaTableIntent _intent = SkiaTableIntent.Data;
    private float _scrollOffset;
    private float _contentHeight;
    private SkiaCellView? _pressedCell;
    private Color? _separatorColor;

    public SkiaTableView()
    {
        IsFocusable = true;
    }

    /// <summary>Sections in display order.</summary>
    public IReadOnlyList<SkiaTableSection> Sections => _sections;

    /// <summary>
    /// Height of every row when <see cref="HasUnevenRows"/> is false; the
    /// minimum row height otherwise.
    /// </summary>
    public float RowHeight
    {
        get => _rowHeight;
        set
        {
            _rowHeight = value > 0 ? value : SkiaCellView.DefaultHeight;
            InvalidateMeasure();
        }
    }

    /// <summary>Rows size to their content instead of <see cref="RowHeight"/>.</summary>
    public bool HasUnevenRows
    {
        get => _hasUnevenRows;
        set { _hasUnevenRows = value; InvalidateMeasure(); }
    }

    /// <summary>
    /// Styling intent: Settings and Menu draw grouped header bands with
    /// small-caps titles (Menu rows bold); Form and Data draw plain headers.
    /// </summary>
    public SkiaTableIntent Intent
    {
        get => _intent;
        set { _intent = value; Invalidate(); }
    }

    /// <summary>Row separator colour; theme grey when null.</summary>
    public Color? SeparatorColor
    {
        get => _separatorColor;
        set { _separatorColor = value; Invalidate(); }
    }

    /// <summary>Current vertical scroll offset in logical pixels.</summary>
    public float ScrollOffset => _scrollOffset;

    /// <summary>Total height of all sections and rows.</summary>
    public float ContentHeight => _contentHeight;

    /// <summary>Raised after a row tap has been delivered to the cell.</summary>
    public event EventHandler<SkiaCellView>? CellTapped;

    #region Sections

    public void AddSection(SkiaTableSection section)
    {
        _sections.Add(section);
        foreach (var cell in section.Cells)
            AddChild(cell);
        InvalidateMeasure();
    }

    public void RemoveSection(SkiaTableSection section)
    {
        if (!_sections.Remove(section)) return;
        foreach (var cell in section.Cells)
            RemoveChild(cell);
        InvalidateMeasure();
    }

    public void ClearSections()
    {
        foreach (var section in _sections)
            foreach (var cell in section.Cells)
                RemoveChild(cell);
        _sections.Clear();
        _scrollOffset = 0;
        InvalidateMeasure();
    }

    /// <summary>Appends a row to a section already in the table (or not yet added).</summary>
    public void AddCell(SkiaTableSection section, SkiaCellView cell)
    {
        section.CellList.Add(cell);
        if (_sections.Contains(section))
            AddChild(cell);
        InvalidateMeasure();
    }

    public void RemoveCell(SkiaTableSection section, SkiaCellView cell)
    {
        if (!section.CellList.Remove(cell)) return;
        if (_sections.Contains(section))
            RemoveChild(cell);
        InvalidateMeasure();
    }

    /// <summary>Every row in display order.</summary>
    public IEnumerable<SkiaCellView> Cells => _sections.SelectMany(s => s.Cells);

    #endregion

    #region Layout

    private bool HasHeader(SkiaTableSection section) => !string.IsNullOrEmpty(section.Title);

    private float RowHeightFor(SkiaCellView cell, double width)
    {
        if (!_hasUnevenRows)
        {
            cell.RowHeight = _rowHeight;
            cell.Measure(new Size(width, _rowHeight));
            return _rowHeight;
        }

        cell.RowHeight = 0;
        var desired = cell.Measure(new Size(width, double.PositiveInfinity));
        float h = (float)desired.Height;
        if (float.IsNaN(h) || float.IsInfinity(h) || h <= 0) h = _rowHeight;
        return Math.Max(h, _rowHeight);
    }

    private float MeasureContent(double width)
    {
        float total = 0;
        foreach (var section in _sections)
        {
            if (HasHeader(section)) total += SectionHeaderHeight;
            foreach (var cell in section.Cells)
                total += RowHeightFor(cell, width);
        }
        return total;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsInfinity(availableSize.Width) || availableSize.Width >= double.MaxValue
            ? 300
            : availableSize.Width;
        _contentHeight = MeasureContent(width);
        double height = double.IsInfinity(availableSize.Height) || availableSize.Height >= double.MaxValue
            ? _contentHeight
            : availableSize.Height;
        return new Size(width, height);
    }

    protected override Rect ArrangeOverride(Rect bounds)
    {
        _headerRects.Clear();
        double width = bounds.Width;
        _contentHeight = MeasureContent(width);
        ClampScroll((float)bounds.Height);

        float y = (float)bounds.Top - _scrollOffset;
        foreach (var section in _sections)
        {
            if (HasHeader(section))
            {
                _headerRects.Add(new SKRect((float)bounds.Left, y, (float)bounds.Right, y + SectionHeaderHeight));
                y += SectionHeaderHeight;
            }
            else
            {
                _headerRects.Add(SKRect.Empty);
            }

            foreach (var cell in section.Cells)
            {
                float h = _hasUnevenRows ? Math.Max((float)cell.DesiredSize.Height, _rowHeight) : _rowHeight;
                cell.Arrange(new Rect(bounds.Left, y, width, h));
                y += h;
            }
        }

        return bounds;
    }

    private float MaxScrollOffset(float viewportHeight) => Math.Max(0, _contentHeight - viewportHeight);

    private void ClampScroll(float viewportHeight)
    {
        _scrollOffset = Math.Clamp(_scrollOffset, 0, MaxScrollOffset(viewportHeight));
    }

    #endregion

    #region Drawing

    private bool GroupedStyle => _intent is SkiaTableIntent.Settings or SkiaTableIntent.Menu;

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        if (BackgroundColor != null && BackgroundColor != Colors.Transparent)
        {
            using var bgPaint = new SKPaint { Color = GetEffectiveBackgroundColor(), Style = SKPaintStyle.Fill };
            canvas.DrawRect(bounds, bgPaint);
        }

        canvas.Save();
        canvas.ClipRect(bounds);

        using var separatorPaint = new SKPaint
        {
            Color = _separatorColor?.ToSKColor() ?? SkiaTheme.Gray300SK,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f
        };

        for (int s = 0; s < _sections.Count; s++)
        {
            var section = _sections[s];
            if (HasHeader(section) && s < _headerRects.Count)
            {
                var rect = _headerRects[s];
                if (rect.Bottom >= bounds.Top && rect.Top <= bounds.Bottom)
                    DrawSectionHeader(canvas, section, rect);
            }

            for (int i = 0; i < section.Cells.Count; i++)
            {
                var cell = section.Cells[i];
                var cellBounds = cell.BoundsSK;
                if (cellBounds.Bottom < bounds.Top || cellBounds.Top > bounds.Bottom) continue;

                if (_intent == SkiaTableIntent.Menu)
                    cell.FontAttributes = FontAttributes.Bold;

                cell.Draw(canvas);

                // Separator under every row but the last of a section.
                if (i < section.Cells.Count - 1)
                    canvas.DrawLine(cellBounds.Left + 16f, cellBounds.Bottom - 0.5f, cellBounds.Right, cellBounds.Bottom - 0.5f, separatorPaint);
                else
                    canvas.DrawLine(cellBounds.Left, cellBounds.Bottom - 0.5f, cellBounds.Right, cellBounds.Bottom - 0.5f, separatorPaint);
            }
        }

        canvas.Restore();

        if (_contentHeight > bounds.Height)
            DrawScrollBar(canvas, bounds);
    }

    private void DrawSectionHeader(SKCanvas canvas, SkiaTableSection section, SKRect rect)
    {
        bool grouped = GroupedStyle;
        if (grouped)
        {
            using var bandPaint = new SKPaint
            {
                Color = SkiaTheme.IsDarkMode ? SkiaTheme.DarkSurfaceSK : SkiaTheme.Gray100SK,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(rect, bandPaint);
        }

        using var font = SkiaFontFactory.Create(grouped ? 13f : 15f);
        font.Embolden = !grouped;
        using var paint = new SKPaint
        {
            Color = section.TitleColor?.ToSKColor() ?? (grouped ? SkiaTheme.TextSecondarySK : SkiaTheme.CurrentTextSK),
            IsAntialias = true
        };

        string title = grouped ? section.Title!.ToUpperInvariant() : section.Title!;
        float baseline = TextRenderingHelper.BaselineForVerticalCenter(font, rect.MidY + (grouped ? 4f : 0f));
        canvas.DrawText(title, rect.Left + 16f, baseline, font, paint);
    }

    private void DrawScrollBar(SKCanvas canvas, SKRect bounds)
    {
        float maxOffset = MaxScrollOffset(bounds.Height);
        float trackHeight = bounds.Height - 4f;
        float thumbHeight = Math.Max(30f, trackHeight * (bounds.Height / _contentHeight));
        float thumbY = bounds.Top + 2f + (trackHeight - thumbHeight) * (maxOffset > 0 ? _scrollOffset / maxOffset : 0f);
        var thumb = new SKRect(bounds.Right - ScrollBarWidth - 2f, thumbY, bounds.Right - 2f, thumbY + thumbHeight);

        using var thumbPaint = new SKPaint { Color = SkiaTheme.ScrollbarThumbSK, Style = SKPaintStyle.Fill, IsAntialias = true };
        canvas.DrawRoundRect(new SKRoundRect(thumb, 3f), thumbPaint);
    }

    #endregion

    #region Input

    /// <summary>The row under a point, or null.</summary>
    public SkiaCellView? CellAt(float x, float y)
    {
        if (!Bounds.Contains(x, y)) return null;
        foreach (var cell in Cells)
        {
            if (cell.IsVisible && cell.Bounds.Contains(x, y))
                return cell;
        }
        return null;
    }

    public override SkiaView? HitTest(float x, float y)
    {
        if (!IsVisible || !IsEnabled || !Bounds.Contains(x, y)) return null;

        var cell = CellAt(x, y);
        if (cell != null)
        {
            // A row's editor / switch / hosted content takes the input; the
            // row's own surface is the table's (row taps).
            var hit = cell.HitTest(x, y);
            if (hit != null && !ReferenceEquals(hit, cell))
                return hit;
        }
        return this;
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled) return;
        _pressedCell = CellAt(e.X, e.Y);
        e.Handled = _pressedCell != null;
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        var pressed = _pressedCell;
        _pressedCell = null;
        if (pressed == null) return;

        if (ReferenceEquals(CellAt(e.X, e.Y), pressed) && pressed.IsEnabled)
        {
            TapCell(pressed);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Delivers a tap to a row: a switch row toggles its switch, then the
    /// row's Tapped is raised, then <see cref="CellTapped"/>.
    /// </summary>
    public void TapCell(SkiaCellView cell)
    {
        if (cell.Kind == SkiaCellKind.Switch && cell.Accessory is SkiaSwitch sw && sw.IsEnabled)
            sw.IsOn = !sw.IsOn;

        cell.RaiseTapped();
        CellTapped?.Invoke(this, cell);
        Invalidate();
    }

    public override void OnScroll(ScrollEventArgs e)
    {
        float viewport = (float)Bounds.Height;
        if (_contentHeight <= viewport) return;

        _scrollOffset += e.DeltaY * 20f;
        ClampScroll(viewport);
        Arrange(Bounds);
        Invalidate();
        e.Handled = true;
    }

    /// <summary>Scrolls so the row is visible.</summary>
    public void ScrollTo(SkiaCellView cell)
    {
        float viewport = (float)Bounds.Height;
        float top = (float)(cell.Bounds.Top - Bounds.Top) + _scrollOffset;
        float bottom = top + (float)cell.Bounds.Height;
        if (top < _scrollOffset) _scrollOffset = top;
        else if (bottom > _scrollOffset + viewport) _scrollOffset = bottom - viewport;
        ClampScroll(viewport);
        Arrange(Bounds);
        Invalidate();
    }

    #endregion
}
