// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Shell provides a common navigation experience for MAUI applications.
/// Supports flyout menu, tabs, and URI-based navigation.
/// </summary>
public class SkiaShell : SkiaLayoutView
{
    private readonly List<ShellSection> _sections = new();
    private SkiaView? _currentContent;
    private bool _flyoutIsPresented = false;
    private float _flyoutWidth = 280f;
    private float _flyoutAnimationProgress = 0f;
    private int _selectedSectionIndex = 0;
    private int _selectedItemIndex = 0;

    /// <summary>
    /// Gets or sets whether the flyout is presented.
    /// </summary>
    public bool FlyoutIsPresented
    {
        get => _flyoutIsPresented;
        set
        {
            if (_flyoutIsPresented != value)
            {
                _flyoutIsPresented = value;
                _flyoutAnimationProgress = value ? 1f : 0f;
                FlyoutIsPresentedChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
        }
    }

    /// <summary>
    /// Gets or sets the flyout behavior.
    /// </summary>
    public ShellFlyoutBehavior FlyoutBehavior { get; set; } = ShellFlyoutBehavior.Flyout;

    /// <summary>
    /// Gets or sets the flyout width.
    /// </summary>
    public float FlyoutWidth
    {
        get => _flyoutWidth;
        set
        {
            if (_flyoutWidth != value)
            {
                _flyoutWidth = Math.Max(100, value);
                Invalidate();
            }
        }
    }

    /// <summary>
    /// Background color of the flyout.
    /// </summary>
    public SKColor FlyoutBackgroundColor { get; set; } = SKColors.White;

    /// <summary>
    /// Background color of the navigation bar.
    /// </summary>
    public SKColor NavBarBackgroundColor { get; set; } = new SKColor(33, 150, 243);

    /// <summary>
    /// Text color of the navigation bar title.
    /// </summary>
    public SKColor NavBarTextColor { get; set; } = SKColors.White;

    /// <summary>
    /// Height of the navigation bar.
    /// </summary>
    public float NavBarHeight { get; set; } = 56f;

    /// <summary>
    /// Height of the tab bar (when using bottom tabs).
    /// </summary>
    public float TabBarHeight { get; set; } = 56f;

    /// <summary>
    /// Gets or sets whether the navigation bar is visible.
    /// </summary>
    public bool NavBarIsVisible { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the tab bar is visible.
    /// </summary>
    public bool TabBarIsVisible { get; set; } = false;

    /// <summary>
    /// Current title displayed in the navigation bar.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The sections in this shell.
    /// </summary>
    public IReadOnlyList<ShellSection> Sections => _sections;

    /// <summary>
    /// Event raised when FlyoutIsPresented changes.
    /// </summary>
    public event EventHandler? FlyoutIsPresentedChanged;

    /// <summary>
    /// Event raised when navigation occurs.
    /// </summary>
    public event EventHandler<ShellNavigationEventArgs>? Navigated;

    /// <summary>
    /// Adds a section to the shell.
    /// </summary>
    public void AddSection(ShellSection section)
    {
        _sections.Add(section);

        if (_sections.Count == 1)
        {
            NavigateToSection(0, 0);
        }

        Invalidate();
    }

    /// <summary>
    /// Removes a section from the shell.
    /// </summary>
    public void RemoveSection(ShellSection section)
    {
        _sections.Remove(section);
        Invalidate();
    }

    /// <summary>
    /// Navigates to a specific section and item.
    /// </summary>
    public void NavigateToSection(int sectionIndex, int itemIndex = 0)
    {
        if (sectionIndex < 0 || sectionIndex >= _sections.Count) return;

        var section = _sections[sectionIndex];
        if (itemIndex < 0 || itemIndex >= section.Items.Count) return;

        _selectedSectionIndex = sectionIndex;
        _selectedItemIndex = itemIndex;

        var item = section.Items[itemIndex];
        SetCurrentContent(item.Content);
        Title = item.Title;

        Navigated?.Invoke(this, new ShellNavigationEventArgs(section, item));
        Invalidate();
    }

    /// <summary>
    /// Navigates using a URI route.
    /// </summary>
    public void GoToAsync(string route)
    {
        // Simple route parsing - format: "//section/item"
        if (string.IsNullOrEmpty(route)) return;

        var parts = route.TrimStart('/').Split('/');
        if (parts.Length == 0) return;

        // Find matching section
        for (int i = 0; i < _sections.Count; i++)
        {
            var section = _sections[i];
            if (section.Route.Equals(parts[0], StringComparison.OrdinalIgnoreCase))
            {
                if (parts.Length > 1)
                {
                    // Find matching item
                    for (int j = 0; j < section.Items.Count; j++)
                    {
                        if (section.Items[j].Route.Equals(parts[1], StringComparison.OrdinalIgnoreCase))
                        {
                            NavigateToSection(i, j);
                            return;
                        }
                    }
                }
                NavigateToSection(i, 0);
                return;
            }
        }
    }

    private void SetCurrentContent(SkiaView? content)
    {
        if (_currentContent != null)
        {
            RemoveChild(_currentContent);
        }

        _currentContent = content;

        if (_currentContent != null)
        {
            AddChild(_currentContent);
        }
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        // Measure current content
        if (_currentContent != null)
        {
            float contentTop = NavBarIsVisible ? NavBarHeight : 0;
            float contentBottom = TabBarIsVisible ? TabBarHeight : 0;
            var contentSize = new SKSize(
                availableSize.Width,
                availableSize.Height - contentTop - contentBottom);
            _currentContent.Measure(contentSize);
        }

        return availableSize;
    }

    protected override SKRect ArrangeOverride(SKRect bounds)
    {
        // Arrange current content
        if (_currentContent != null)
        {
            float contentTop = bounds.Top + (NavBarIsVisible ? NavBarHeight : 0);
            float contentBottom = bounds.Bottom - (TabBarIsVisible ? TabBarHeight : 0);
            var contentBounds = new SKRect(
                bounds.Left,
                contentTop,
                bounds.Right,
                contentBottom);
            _currentContent.Arrange(contentBounds);
        }

        return bounds;
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        canvas.Save();
        canvas.ClipRect(bounds);

        // Draw content
        _currentContent?.Draw(canvas);

        // Draw navigation bar
        if (NavBarIsVisible)
        {
            DrawNavBar(canvas, bounds);
        }

        // Draw tab bar
        if (TabBarIsVisible)
        {
            DrawTabBar(canvas, bounds);
        }

        // Draw flyout overlay and panel
        if (_flyoutAnimationProgress > 0)
        {
            DrawFlyout(canvas, bounds);
        }

        canvas.Restore();
    }

    private void DrawNavBar(SKCanvas canvas, SKRect bounds)
    {
        var navBarBounds = new SKRect(
            bounds.Left,
            bounds.Top,
            bounds.Right,
            bounds.Top + NavBarHeight);

        // Draw background
        using var bgPaint = new SKPaint
        {
            Color = NavBarBackgroundColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRect(navBarBounds, bgPaint);

        // Draw hamburger menu icon
        if (FlyoutBehavior == ShellFlyoutBehavior.Flyout)
        {
            using var iconPaint = new SKPaint
            {
                Color = NavBarTextColor,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2,
                IsAntialias = true
            };

            float iconLeft = navBarBounds.Left + 16;
            float iconCenter = navBarBounds.MidY;

            canvas.DrawLine(iconLeft, iconCenter - 8, iconLeft + 18, iconCenter - 8, iconPaint);
            canvas.DrawLine(iconLeft, iconCenter, iconLeft + 18, iconCenter, iconPaint);
            canvas.DrawLine(iconLeft, iconCenter + 8, iconLeft + 18, iconCenter + 8, iconPaint);
        }

        // Draw title
        using var titlePaint = new SKPaint
        {
            Color = NavBarTextColor,
            TextSize = 20f,
            IsAntialias = true,
            FakeBoldText = true
        };

        float titleX = FlyoutBehavior == ShellFlyoutBehavior.Flyout ? navBarBounds.Left + 56 : navBarBounds.Left + 16;
        float titleY = navBarBounds.MidY + 6;
        canvas.DrawText(Title, titleX, titleY, titlePaint);
    }

    private void DrawTabBar(SKCanvas canvas, SKRect bounds)
    {
        if (_selectedSectionIndex < 0 || _selectedSectionIndex >= _sections.Count) return;

        var section = _sections[_selectedSectionIndex];
        if (section.Items.Count <= 1) return;

        var tabBarBounds = new SKRect(
            bounds.Left,
            bounds.Bottom - TabBarHeight,
            bounds.Right,
            bounds.Bottom);

        // Draw background
        using var bgPaint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRect(tabBarBounds, bgPaint);

        // Draw top border
        using var borderPaint = new SKPaint
        {
            Color = new SKColor(224, 224, 224),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1
        };
        canvas.DrawLine(tabBarBounds.Left, tabBarBounds.Top, tabBarBounds.Right, tabBarBounds.Top, borderPaint);

        // Draw tabs
        float tabWidth = tabBarBounds.Width / section.Items.Count;

        using var textPaint = new SKPaint
        {
            TextSize = 12f,
            IsAntialias = true
        };

        for (int i = 0; i < section.Items.Count; i++)
        {
            var item = section.Items[i];
            bool isSelected = i == _selectedItemIndex;

            textPaint.Color = isSelected ? NavBarBackgroundColor : new SKColor(117, 117, 117);

            var textBounds = new SKRect();
            textPaint.MeasureText(item.Title, ref textBounds);

            float textX = tabBarBounds.Left + i * tabWidth + tabWidth / 2 - textBounds.MidX;
            float textY = tabBarBounds.MidY - textBounds.MidY;

            canvas.DrawText(item.Title, textX, textY, textPaint);
        }
    }

    private void DrawFlyout(SKCanvas canvas, SKRect bounds)
    {
        // Draw scrim
        using var scrimPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, (byte)(100 * _flyoutAnimationProgress)),
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(bounds, scrimPaint);

        // Draw flyout panel
        float flyoutX = bounds.Left - FlyoutWidth + (FlyoutWidth * _flyoutAnimationProgress);
        var flyoutBounds = new SKRect(
            flyoutX,
            bounds.Top,
            flyoutX + FlyoutWidth,
            bounds.Bottom);

        using var flyoutPaint = new SKPaint
        {
            Color = FlyoutBackgroundColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRect(flyoutBounds, flyoutPaint);

        // Draw flyout items
        float itemY = flyoutBounds.Top + 80;
        float itemHeight = 48f;

        using var itemTextPaint = new SKPaint
        {
            TextSize = 14f,
            IsAntialias = true
        };

        for (int i = 0; i < _sections.Count; i++)
        {
            var section = _sections[i];
            bool isSelected = i == _selectedSectionIndex;

            // Draw selection background
            if (isSelected)
            {
                using var selectionPaint = new SKPaint
                {
                    Color = new SKColor(33, 150, 243, 30),
                    Style = SKPaintStyle.Fill
                };
                canvas.DrawRect(flyoutBounds.Left, itemY, flyoutBounds.Right, itemY + itemHeight, selectionPaint);
            }

            itemTextPaint.Color = isSelected ? NavBarBackgroundColor : new SKColor(33, 33, 33);
            canvas.DrawText(section.Title, flyoutBounds.Left + 16, itemY + 30, itemTextPaint);

            itemY += itemHeight;
        }
    }

    public override SkiaView? HitTest(float x, float y)
    {
        if (!IsVisible || !Bounds.Contains(x, y)) return null;

        // Check flyout area
        if (_flyoutAnimationProgress > 0)
        {
            float flyoutX = Bounds.Left - FlyoutWidth + (FlyoutWidth * _flyoutAnimationProgress);
            var flyoutBounds = new SKRect(flyoutX, Bounds.Top, flyoutX + FlyoutWidth, Bounds.Bottom);

            if (flyoutBounds.Contains(x, y))
            {
                return this; // Flyout handles its own hits
            }

            // Tap on scrim closes flyout
            if (_flyoutIsPresented)
            {
                return this;
            }
        }

        // Check nav bar
        if (NavBarIsVisible && y < Bounds.Top + NavBarHeight)
        {
            return this;
        }

        // Check tab bar
        if (TabBarIsVisible && y > Bounds.Bottom - TabBarHeight)
        {
            return this;
        }

        // Check content
        if (_currentContent != null)
        {
            var hit = _currentContent.HitTest(x, y);
            if (hit != null) return hit;
        }

        return this;
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        // Check flyout tap
        if (_flyoutAnimationProgress > 0)
        {
            float flyoutX = Bounds.Left - FlyoutWidth + (FlyoutWidth * _flyoutAnimationProgress);
            var flyoutBounds = new SKRect(flyoutX, Bounds.Top, flyoutX + FlyoutWidth, Bounds.Bottom);

            if (flyoutBounds.Contains(e.X, e.Y))
            {
                // Check which section was tapped
                float itemY = flyoutBounds.Top + 80;
                float itemHeight = 48f;

                for (int i = 0; i < _sections.Count; i++)
                {
                    if (e.Y >= itemY && e.Y < itemY + itemHeight)
                    {
                        NavigateToSection(i, 0);
                        FlyoutIsPresented = false;
                        e.Handled = true;
                        return;
                    }
                    itemY += itemHeight;
                }
            }
            else if (_flyoutIsPresented)
            {
                // Tap on scrim
                FlyoutIsPresented = false;
                e.Handled = true;
                return;
            }
        }

        // Check nav bar hamburger tap
        if (NavBarIsVisible && e.Y < Bounds.Top + NavBarHeight && e.X < 56 && FlyoutBehavior == ShellFlyoutBehavior.Flyout)
        {
            FlyoutIsPresented = !FlyoutIsPresented;
            e.Handled = true;
            return;
        }

        // Check tab bar tap
        if (TabBarIsVisible && e.Y > Bounds.Bottom - TabBarHeight)
        {
            if (_selectedSectionIndex >= 0 && _selectedSectionIndex < _sections.Count)
            {
                var section = _sections[_selectedSectionIndex];
                float tabWidth = Bounds.Width / section.Items.Count;
                int tappedIndex = (int)((e.X - Bounds.Left) / tabWidth);
                tappedIndex = Math.Clamp(tappedIndex, 0, section.Items.Count - 1);

                if (tappedIndex != _selectedItemIndex)
                {
                    NavigateToSection(_selectedSectionIndex, tappedIndex);
                }
                e.Handled = true;
                return;
            }
        }

        base.OnPointerPressed(e);
    }
}

/// <summary>
/// Shell flyout behavior options.
/// </summary>
public enum ShellFlyoutBehavior
{
    /// <summary>
    /// No flyout menu.
    /// </summary>
    Disabled,

    /// <summary>
    /// Flyout slides over content.
    /// </summary>
    Flyout,

    /// <summary>
    /// Flyout is always visible (side-by-side layout).
    /// </summary>
    Locked
}

/// <summary>
/// Represents a section in the shell (typically shown in flyout).
/// </summary>
public class ShellSection
{
    /// <summary>
    /// The route identifier for this section.
    /// </summary>
    public string Route { get; set; } = string.Empty;

    /// <summary>
    /// The display title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Optional icon path.
    /// </summary>
    public string? IconPath { get; set; }

    /// <summary>
    /// Items in this section.
    /// </summary>
    public List<ShellContent> Items { get; } = new();
}

/// <summary>
/// Represents content within a shell section.
/// </summary>
public class ShellContent
{
    /// <summary>
    /// The route identifier for this content.
    /// </summary>
    public string Route { get; set; } = string.Empty;

    /// <summary>
    /// The display title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Optional icon path.
    /// </summary>
    public string? IconPath { get; set; }

    /// <summary>
    /// The content view.
    /// </summary>
    public SkiaView? Content { get; set; }
}

/// <summary>
/// Event args for shell navigation events.
/// </summary>
public class ShellNavigationEventArgs : EventArgs
{
    public ShellSection Section { get; }
    public ShellContent Content { get; }

    public ShellNavigationEventArgs(ShellSection section, ShellContent content)
    {
        Section = section;
        Content = content;
    }
}
