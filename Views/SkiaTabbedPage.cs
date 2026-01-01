// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// A page that displays tabs for navigation between child pages.
/// </summary>
public class SkiaTabbedPage : SkiaLayoutView
{
    private readonly List<TabItem> _tabs = new();
    private int _selectedIndex = 0;
    private float _tabBarHeight = 48f;
    private bool _tabBarOnBottom = false;

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
    public SKColor TabBarBackgroundColor { get; set; } = new SKColor(33, 150, 243); // Material Blue

    /// <summary>
    /// Color for selected tab text/icon.
    /// </summary>
    public SKColor SelectedTabColor { get; set; } = SKColors.White;

    /// <summary>
    /// Color for unselected tab text/icon.
    /// </summary>
    public SKColor UnselectedTabColor { get; set; } = new SKColor(255, 255, 255, 180);

    /// <summary>
    /// Color of the selection indicator.
    /// </summary>
    public SKColor IndicatorColor { get; set; } = SKColors.White;

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
            IconPath = iconPath
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
        _selectedIndex = 0;
        InvalidateMeasure();
        Invalidate();
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        // Measure the content area (excluding tab bar)
        var contentHeight = availableSize.Height - TabBarHeight;
        var contentSize = new SKSize(availableSize.Width, contentHeight);

        foreach (var tab in _tabs)
        {
            tab.Content.Measure(contentSize);
        }

        return availableSize;
    }

    protected override SKRect ArrangeOverride(SKRect bounds)
    {
        // Calculate content bounds based on tab bar position
        SKRect contentBounds;
        if (TabBarOnBottom)
        {
            contentBounds = new SKRect(
                bounds.Left,
                bounds.Top,
                bounds.Right,
                bounds.Bottom - TabBarHeight);
        }
        else
        {
            contentBounds = new SKRect(
                bounds.Left,
                bounds.Top + TabBarHeight,
                bounds.Right,
                bounds.Bottom);
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
                Bounds.Left,
                Bounds.Bottom - TabBarHeight,
                Bounds.Right,
                Bounds.Bottom);
        }
        else
        {
            tabBarBounds = new SKRect(
                Bounds.Left,
                Bounds.Top,
                Bounds.Right,
                Bounds.Top + TabBarHeight);
        }

        // Draw background
        using var bgPaint = new SKPaint
        {
            Color = TabBarBackgroundColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRect(tabBarBounds, bgPaint);

        if (_tabs.Count == 0) return;

        // Calculate tab width
        float tabWidth = tabBarBounds.Width / _tabs.Count;

        // Draw tabs
        using var textPaint = new SKPaint
        {
            IsAntialias = true,
            TextSize = 14f,
            Typeface = SKTypeface.Default
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
            textPaint.Color = isSelected ? SelectedTabColor : UnselectedTabColor;
            textPaint.FakeBoldText = isSelected;

            // Draw tab title centered
            var textBounds = new SKRect();
            textPaint.MeasureText(tab.Title, ref textBounds);

            float textX = tabBounds.MidX - textBounds.MidX;
            float textY = tabBounds.MidY - textBounds.MidY;

            canvas.DrawText(tab.Title, textX, textY, textPaint);
        }

        // Draw selection indicator
        if (_selectedIndex >= 0 && _selectedIndex < _tabs.Count)
        {
            using var indicatorPaint = new SKPaint
            {
                Color = IndicatorColor,
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
                Bounds.Left,
                Bounds.Bottom - TabBarHeight,
                Bounds.Right,
                Bounds.Bottom);
        }
        else
        {
            tabBarBounds = new SKRect(
                Bounds.Left,
                Bounds.Top,
                Bounds.Right,
                Bounds.Top + TabBarHeight);
        }

        if (tabBarBounds.Contains(x, y))
        {
            return this; // Tab bar handles its own hits
        }

        // Check selected content
        if (_selectedIndex >= 0 && _selectedIndex < _tabs.Count)
        {
            var hit = _tabs[_selectedIndex].Content.HitTest(x, y);
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
                Bounds.Left,
                Bounds.Bottom - TabBarHeight,
                Bounds.Right,
                Bounds.Bottom);
        }
        else
        {
            tabBarBounds = new SKRect(
                Bounds.Left,
                Bounds.Top,
                Bounds.Right,
                Bounds.Top + TabBarHeight);
        }

        if (tabBarBounds.Contains(e.X, e.Y) && _tabs.Count > 0)
        {
            // Calculate which tab was clicked
            float tabWidth = tabBarBounds.Width / _tabs.Count;
            int clickedIndex = (int)((e.X - tabBarBounds.Left) / tabWidth);
            clickedIndex = Math.Clamp(clickedIndex, 0, _tabs.Count - 1);

            SelectedIndex = clickedIndex;
            e.Handled = true;
        }

        base.OnPointerPressed(e);
    }
}

/// <summary>
/// Represents a tab item with title, icon, and content.
/// </summary>
public class TabItem
{
    /// <summary>
    /// The title displayed in the tab.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Optional icon path for the tab.
    /// </summary>
    public string? IconPath { get; set; }

    /// <summary>
    /// The content view displayed when this tab is selected.
    /// </summary>
    public SkiaView Content { get; set; } = null!;

    /// <summary>
    /// Optional badge text to display on the tab.
    /// </summary>
    public string? Badge { get; set; }
}
