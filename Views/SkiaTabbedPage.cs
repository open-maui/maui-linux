// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using SkiaSharp;
using Microsoft.Maui.Platform.Linux.Rendering;

namespace Microsoft.Maui.Platform;

/// <summary>
/// A page that displays tabs for navigation between child pages.
/// </summary>
public class SkiaTabbedPage : SkiaLayoutView
{
    private readonly List<TabItem> _tabs = new();
    private readonly TabIconCache _icons;
    private Microsoft.Maui.Controls.Brush? _tabBarBackground;
    private int _selectedIndex = 0;
    private float _tabBarHeight = 48f;
    private bool _tabBarOnBottom = false;

    // SKColor fields for rendering
    private SKColor _tabBarBackgroundColorSK = SkiaTheme.PrimarySK;
    private SKColor _selectedTabColorSK = SKColors.White;
    private SKColor _unselectedTabColorSK = SkiaTheme.WhiteSemiTransparentSK;
    private SKColor _indicatorColorSK = SKColors.White;

    // MAUI Color backing fields
    private Color _tabBarBackgroundColor = Color.FromRgb(33, 150, 243);
    private Color _selectedTabColor = Colors.White;
    private Color _unselectedTabColor = Color.FromRgba(255, 255, 255, 180);
    private Color _indicatorColor = Colors.White;

    public SkiaTabbedPage()
    {
        _icons = new TabIconCache(this);
    }

    /// <summary>
    /// The tab bar's background brush (TabbedPage.BarBackground); when set it paints the bar
    /// instead of <see cref="TabBarBackgroundColor"/>, gradients included.
    /// </summary>
    public Microsoft.Maui.Controls.Brush? TabBarBackground
    {
        get => _tabBarBackground;
        set
        {
            _tabBarBackground = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Gets or sets the height of the tab bar.
    /// </summary>
    public float TabBarHeight
    {
        get => _tabBarHeight;
        set
        {
            if (_tabBarHeight != value)
            {
                _tabBarHeight = value;
                InvalidateMeasure();
                Invalidate();
            }
        }
    }

    /// <summary>
    /// Gets or sets whether the tab bar is positioned at the bottom.
    /// </summary>
    public bool TabBarOnBottom
    {
        get => _tabBarOnBottom;
        set
        {
            if (_tabBarOnBottom != value)
            {
                _tabBarOnBottom = value;
                // Content bounds move with the bar, so a re-arrange is needed.
                InvalidateMeasure();
                Invalidate();
            }
        }
    }

    /// <summary>
    /// Gets or sets the selected tab index.
    /// </summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (value >= 0 && value < _tabs.Count && _selectedIndex != value)
            {
                _selectedIndex = value;
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
        }
    }

    /// <summary>
    /// Gets the currently selected tab.
    /// </summary>
    public TabItem? SelectedTab => _selectedIndex >= 0 && _selectedIndex < _tabs.Count
        ? _tabs[_selectedIndex]
        : null;

    /// <summary>
    /// Gets the tabs in this page.
    /// </summary>
    public IReadOnlyList<TabItem> Tabs => _tabs;

    /// <summary>
    /// Background color for the tab bar.
    /// </summary>
    public Color TabBarBackgroundColor
    {
        get => _tabBarBackgroundColor;
        set
        {
            _tabBarBackgroundColor = value;
            _tabBarBackgroundColorSK = value.ToSKColor();
            Invalidate();
        }
    }

    /// <summary>
    /// Color for selected tab text/icon.
    /// </summary>
    public Color SelectedTabColor
    {
        get => _selectedTabColor;
        set
        {
            _selectedTabColor = value;
            _selectedTabColorSK = value.ToSKColor();
            Invalidate();
        }
    }

    /// <summary>
    /// Color for unselected tab text/icon.
    /// </summary>
    public Color UnselectedTabColor
    {
        get => _unselectedTabColor;
        set
        {
            _unselectedTabColor = value;
            _unselectedTabColorSK = value.ToSKColor();
            Invalidate();
        }
    }

    /// <summary>
    /// Color of the selection indicator.
    /// </summary>
    public Color IndicatorColor
    {
        get => _indicatorColor;
        set
        {
            _indicatorColor = value;
            _indicatorColorSK = value.ToSKColor();
            Invalidate();
        }
    }

    /// <summary>
    /// Height of the selection indicator.
    /// </summary>
    public float IndicatorHeight { get; set; } = 3f;

    /// <summary>
    /// Event raised when the selected index changes.
    /// </summary>
    public event EventHandler? SelectedIndexChanged;

    /// <summary>
    /// Adds a tab with the specified title and content.
    /// </summary>
    public void AddTab(string title, SkiaView content, string? iconPath = null)
    {
        var tab = new TabItem
        {
            Title = title,
            Content = content,
            IconPath = iconPath,
            IconSource = string.IsNullOrEmpty(iconPath) ? null : Microsoft.Maui.Controls.ImageSource.FromFile(iconPath),
        };

        _tabs.Add(tab);
        AddChild(content);

        if (_tabs.Count == 1)
        {
            _selectedIndex = 0;
        }

        InvalidateMeasure();
        Invalidate();
    }

    /// <summary>
    /// Removes a tab at the specified index.
    /// </summary>
    public void RemoveTab(int index)
    {
        if (index >= 0 && index < _tabs.Count)
        {
            var tab = _tabs[index];
            _tabs.RemoveAt(index);
            RemoveChild(tab.Content);

            if (_selectedIndex >= _tabs.Count)
            {
                _selectedIndex = Math.Max(0, _tabs.Count - 1);
            }

            InvalidateMeasure();
            Invalidate();
        }
    }

    /// <summary>
    /// Clears all tabs.
    /// </summary>
    public void ClearTabs()
    {
        foreach (var tab in _tabs)
        {
            RemoveChild(tab.Content);
        }
        _tabs.Clear();
        _icons.Clear();
        _selectedIndex = 0;
        InvalidateMeasure();
        Invalidate();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Measure the content area (excluding tab bar)
        var contentHeight = availableSize.Height - TabBarHeight;
        var contentSize = new Size(availableSize.Width, contentHeight);

        foreach (var tab in _tabs)
        {
            tab.Content.Measure(contentSize);
        }

        return availableSize;
    }

    protected override Rect ArrangeOverride(Rect bounds)
    {
        // Calculate content bounds based on tab bar position
        Rect contentBounds;
        if (TabBarOnBottom)
        {
            contentBounds = new Rect(
                bounds.Left,
                bounds.Top,
                bounds.Width,
                bounds.Height - TabBarHeight);
        }
        else
        {
            contentBounds = new Rect(
                bounds.Left,
                bounds.Top + TabBarHeight,
                bounds.Width,
                bounds.Height - TabBarHeight);
        }

        // Arrange each tab's content to fill the content area
        foreach (var tab in _tabs)
        {
            tab.Content.Arrange(contentBounds);
        }

        return bounds;
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        canvas.Save();
        canvas.ClipRect(bounds);

        // Draw tab bar background
        DrawTabBar(canvas);

        // Draw selected content
        if (_selectedIndex >= 0 && _selectedIndex < _tabs.Count)
        {
            _tabs[_selectedIndex].Content.Draw(canvas);
        }

        canvas.Restore();
    }

    private void DrawTabBar(SKCanvas canvas)
    {
        // Calculate tab bar bounds
        SKRect tabBarBounds;
        if (TabBarOnBottom)
        {
            tabBarBounds = new SKRect(
                (float)Bounds.Left,
                (float)Bounds.Bottom - TabBarHeight,
                (float)Bounds.Right,
                (float)Bounds.Bottom);
        }
        else
        {
            tabBarBounds = new SKRect(
                (float)Bounds.Left,
                (float)Bounds.Top,
                (float)Bounds.Right,
                (float)Bounds.Top + TabBarHeight);
        }

        // Draw background: BarBackground (a brush, gradients included), else BarBackgroundColor.
        using var bgPaint = BrushPaint.Create(_tabBarBackground, tabBarBounds) ?? new SKPaint
        {
            Color = _tabBarBackgroundColorSK,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRect(tabBarBounds, bgPaint);

        if (_tabs.Count == 0) return;
        _icons.Retain(_tabs.Select(t => t.IconSource));

        // Calculate tab width
        float tabWidth = tabBarBounds.Width / _tabs.Count;

        // Draw tabs
        using var textFont = SkiaFontFactory.Create(14f);
        using var textPaint = new SKPaint
        {
            IsAntialias = true
        };

        for (int i = 0; i < _tabs.Count; i++)
        {
            var tab = _tabs[i];
            var tabBounds = new SKRect(
                tabBarBounds.Left + i * tabWidth,
                tabBarBounds.Top,
                tabBarBounds.Left + (i + 1) * tabWidth,
                tabBarBounds.Bottom);

            bool isSelected = i == _selectedIndex;
            var color = isSelected ? _selectedTabColorSK : _unselectedTabColorSK;
            textPaint.Color = color;
            textFont.Embolden = isSelected;

            // The icon (tinted with the tab's colour, as every platform tints it) above the
            // title; the title alone is centred.
            var icon = _icons.Get(tab.IconSource);
            float titleCenterY = tabBounds.MidY;
            if (icon != null)
            {
                TabIconCache.DrawTinted(canvas, icon, IconBounds(tabBounds), color);
                titleCenterY = tabBounds.Bottom - TabTitleHeightWithIcon / 2;
                textFont.Size = 12f;
            }
            else
            {
                textFont.Size = 14f;
            }

            // Draw tab title centered
            textFont.MeasureText(tab.Title, out var textBounds);

            float textX = tabBounds.MidX - textBounds.MidX;
            float textY = TextRenderingHelper.BaselineForVerticalCenter(textFont, titleCenterY);

            canvas.DrawText(tab.Title, textX, textY, SKTextAlign.Left, textFont, textPaint);
        }

        // Draw selection indicator
        if (_selectedIndex >= 0 && _selectedIndex < _tabs.Count)
        {
            using var indicatorPaint = new SKPaint
            {
                Color = _indicatorColorSK,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };

            float indicatorLeft = tabBarBounds.Left + _selectedIndex * tabWidth;
            float indicatorTop = TabBarOnBottom
                ? tabBarBounds.Top
                : tabBarBounds.Bottom - IndicatorHeight;

            var indicatorRect = new SKRect(
                indicatorLeft,
                indicatorTop,
                indicatorLeft + tabWidth,
                indicatorTop + IndicatorHeight);

            canvas.DrawRect(indicatorRect, indicatorPaint);
        }
    }

    public override SkiaView? HitTest(float x, float y)
    {
        if (!IsVisible || !Bounds.Contains(x, y)) return null;

        // Check if hit is in tab bar
        SKRect tabBarBounds;
        if (TabBarOnBottom)
        {
            tabBarBounds = new SKRect(
                (float)Bounds.Left,
                (float)(Bounds.Top + Bounds.Height) - TabBarHeight,
                (float)(Bounds.Left + Bounds.Width),
                (float)(Bounds.Top + Bounds.Height));
        }
        else
        {
            tabBarBounds = new SKRect(
                (float)Bounds.Left,
                (float)Bounds.Top,
                (float)(Bounds.Left + Bounds.Width),
                (float)Bounds.Top + TabBarHeight);
        }

        if (tabBarBounds.Contains(x, y))
        {
            return this; // Tab bar handles its own hits
        }

        // Check selected content
        if (_selectedIndex >= 0 && _selectedIndex < _tabs.Count)
        {
            var hit = _tabs[_selectedIndex].Content.HitTestAt(x, y);
            if (hit != null) return hit;
        }

        return this;
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        // Check if click is in tab bar
        SKRect tabBarBounds;
        if (TabBarOnBottom)
        {
            tabBarBounds = new SKRect(
                (float)Bounds.Left,
                (float)(Bounds.Top + Bounds.Height) - TabBarHeight,
                (float)(Bounds.Left + Bounds.Width),
                (float)(Bounds.Top + Bounds.Height));
        }
        else
        {
            tabBarBounds = new SKRect(
                (float)Bounds.Left,
                (float)Bounds.Top,
                (float)(Bounds.Left + Bounds.Width),
                (float)Bounds.Top + TabBarHeight);
        }

        if (tabBarBounds.Contains(e.X, e.Y) && _tabs.Count > 0)
        {
            // Calculate which tab was clicked
            float tabWidth = tabBarBounds.Width / _tabs.Count;
            int clickedIndex = (int)((e.X - tabBarBounds.Left) / tabWidth);
            clickedIndex = Math.Clamp(clickedIndex, 0, _tabs.Count - 1);

            SelectedIndex = clickedIndex;
            e.Handled = true;
            return;
        }

        base.OnPointerPressed(e);
    }

    // Below an icon the title takes the bottom of the tab.
    private const float TabTitleHeightWithIcon = 18f;

    /// <summary>Where a tab with an icon draws it: centred, at the top of the tab.</summary>
    private static SKRect IconBounds(SKRect tab)
    {
        float size = Math.Min(TabIconCache.IconSize, Math.Max(0f, tab.Height - TabTitleHeightWithIcon - 4f));
        float top = tab.Top + Math.Max(2f, (tab.Height - TabTitleHeightWithIcon - size) / 2f);
        return SKRect.Create(tab.MidX - size / 2f, top, size, size);
    }

    /// <summary>Where tab <paramref name="index"/> draws its icon, empty when it has none (tests).</summary>
    internal SKRect TabIconBounds(int index)
    {
        if (index < 0 || index >= _tabs.Count || _icons.Get(_tabs[index].IconSource) == null)
            return SKRect.Empty;
        var bar = TabBarBounds;
        float width = bar.Width / _tabs.Count;
        return IconBounds(new SKRect(bar.Left + index * width, bar.Top, bar.Left + (index + 1) * width, bar.Bottom));
    }

    /// <summary>
    /// The tab bar's bounds in the same coordinate space as <see cref="SkiaView.Bounds"/>.
    /// </summary>
    public SKRect TabBarBounds => TabBarOnBottom
        ? new SKRect((float)Bounds.Left, (float)Bounds.Bottom - TabBarHeight, (float)Bounds.Right, (float)Bounds.Bottom)
        : new SKRect((float)Bounds.Left, (float)Bounds.Top, (float)Bounds.Right, (float)Bounds.Top + TabBarHeight);
}
