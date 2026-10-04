// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Rendering;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// The bottom tab bar of a MAUI Shell: the current ShellItem's sections (the Tabs of a TabBar
/// or FlyoutItem, and the implicit section MAUI wraps around each bare ShellContent) as tabs,
/// shown when MAUI's ShellItem says so (more than one section, and Shell.TabBarIsVisible not
/// false for the presented page). Each tab shows its section's icon and title in the colours of
/// the Shell's appearance for the presented page (Shell.TabBarForegroundColor, TabBarTitleColor,
/// TabBarUnselectedColor, TabBarDisabledColor, TabBarBackgroundColor and their fallbacks), as
/// MAUI's platforms show them; a click selects the section through MAUI.
/// </summary>
public partial class SkiaShell
{
    private TabIconCache? _tabIconCache;
    private ShellAppearance? _appearance;
    private AppearanceObserver? _appearanceObserver;
    private readonly List<(Microsoft.Maui.Controls.ShellSection Section, SKRect Bounds, SKRect Icon, SKRect Title)> _tabHits = new();

    private TabIconCache TabIcons => _tabIconCache ??= new TabIconCache(this);

    /// <summary>Receives the Shell's appearance for the presented page (MAUI's IAppearanceObserver).</summary>
    private sealed class AppearanceObserver : IAppearanceObserver
    {
        private readonly SkiaShell _owner;

        public AppearanceObserver(SkiaShell owner) => _owner = owner;

        public void OnAppearanceChanged(ShellAppearance appearance)
        {
            _owner._appearance = appearance;
            // The bar's visibility follows the page (Shell.TabBarIsVisible) as well as its colours.
            _owner.InvalidateMeasure();
            _owner.Invalidate();
        }
    }

    private void ObserveAppearance(Shell shell)
    {
        if (shell is not IShellController controller)
            return;
        _appearanceObserver = new AppearanceObserver(this);
        try
        {
            controller.AddAppearanceObserver(_appearanceObserver, shell);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaShell", "Observing the Shell's appearance failed", ex);
        }
    }

    private void StopObservingAppearance(Shell shell)
    {
        if (_appearanceObserver != null && shell is IShellController controller)
            controller.RemoveAppearanceObserver(_appearanceObserver);
        _appearanceObserver = null;
        _appearance = null;
    }

    /// <summary>
    /// The sections the bottom tab bar shows (the current ShellItem's visible sections), empty
    /// when it shows none.
    /// </summary>
    public IReadOnlyList<Microsoft.Maui.Controls.ShellSection> TabBarSections =>
        ShowsMauiTabBar && _mauiShell?.CurrentItem is IShellItemController item
            ? item.GetItems()
            : Array.Empty<Microsoft.Maui.Controls.ShellSection>();

    /// <summary>MAUI's ShellItem shows its tabs (more than one section, the page does not hide them).</summary>
    private bool ShowsMauiTabBar
    {
        get
        {
            if (_mauiShell?.CurrentItem is not IShellItemController item)
                return false;
            try
            {
                return item.ShowTabs;
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("SkiaShell", "Reading ShellItem.ShowTabs failed", ex);
                return false;
            }
        }
    }

    /// <summary>
    /// The bottom bar is shown: MAUI's tabs with a MAUI Shell attached, else the platform's
    /// own bar of the section's contents when <see cref="TabBarIsVisible"/> is set.
    /// </summary>
    private bool IsTabBarShown => _mauiShell != null ? ShowsMauiTabBar : TabBarIsVisible;

    /// <summary>Where the bottom tab bar is, in the coordinates of <see cref="SkiaView.Bounds"/>; empty when none is shown.</summary>
    public Rect TabBarBounds => IsTabBarShown
        ? new Rect(Bounds.Left, Bounds.Bottom - TabBarHeight, Bounds.Width, TabBarHeight)
        : Rect.Zero;

    /// <summary>Where the last frame drew the tab of <paramref name="section"/>: its icon and title (tests).</summary>
    internal (SKRect Icon, SKRect Title) TabBarItemBounds(Microsoft.Maui.Controls.ShellSection section)
    {
        foreach (var hit in _tabHits)
            if (ReferenceEquals(hit.Section, section))
                return (hit.Icon, hit.Title);
        return (SKRect.Empty, SKRect.Empty);
    }

    private const float ShellTabTitleHeight = 18f;

    private void DrawMauiTabBar(SKCanvas canvas, SKRect bounds)
    {
        _tabHits.Clear();
        var sections = TabBarSections;
        var bar = new SKRect(bounds.Left, bounds.Bottom - TabBarHeight, bounds.Right, bounds.Bottom);
        var appearance = _appearance as IShellAppearanceElement;

        using (var bgPaint = new SKPaint
        {
            Color = appearance?.EffectiveTabBarBackgroundColor?.ToSKColor() ?? SkiaTheme.CurrentSurfaceSK,
            Style = SKPaintStyle.Fill,
        })
            canvas.DrawRect(bar, bgPaint);
        using (var borderPaint = new SKPaint { Color = SkiaTheme.Gray300SK, Style = SKPaintStyle.Stroke, StrokeWidth = 1 })
            canvas.DrawLine(bar.Left, bar.Top, bar.Right, bar.Top, borderPaint);

        if (sections.Count == 0)
            return;
        TabIcons.Retain(sections.Select(s => (IImageSource?)s.Icon));

        // MAUI's colours: the selected tab's icon takes the foreground colour (else the title
        // colour), its title the title colour (else the foreground); the others take the
        // unselected colour, a disabled section the disabled colour.
        var foreground = appearance?.EffectiveTabBarForegroundColor?.ToSKColor();
        var title = appearance?.EffectiveTabBarTitleColor?.ToSKColor();
        var unselected = appearance?.EffectiveTabBarUnselectedColor?.ToSKColor() ?? SkiaTheme.TextTertiarySK;
        var disabled = appearance?.EffectiveTabBarDisabledColor?.ToSKColor() ?? SkiaTheme.TextTertiarySK.WithAlpha(97);
        var current = _mauiShell?.CurrentItem?.CurrentItem;

        float tabWidth = bar.Width / sections.Count;
        using var font = SkiaFontFactory.Create(12f);
        using var textPaint = new SKPaint { IsAntialias = true };
        for (int i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            var tab = new SKRect(bar.Left + i * tabWidth, bar.Top, bar.Left + (i + 1) * tabWidth, bar.Bottom);
            bool selected = ReferenceEquals(section, current);
            SKColor iconColor, textColor;
            if (!section.IsEnabled)
                iconColor = textColor = disabled;
            else if (selected)
            {
                iconColor = foreground ?? title ?? SkiaTheme.PrimarySK;
                textColor = title ?? foreground ?? SkiaTheme.PrimarySK;
            }
            else
                iconColor = textColor = unselected;

            var text = section.Title ?? string.Empty;
            var icon = TabIcons.Get(section.Icon);
            SKRect iconRect = SKRect.Empty;
            float titleCenter = tab.MidY;
            if (icon != null)
            {
                float size = Math.Min(TabIconCache.IconSize, Math.Max(0f, tab.Height - ShellTabTitleHeight - 4f));
                float top = tab.Top + Math.Max(2f, (tab.Height - ShellTabTitleHeight - size) / 2f);
                iconRect = SKRect.Create(tab.MidX - size / 2f, top, size, size);
                TabIconCache.DrawTinted(canvas, icon, iconRect, iconColor);
                titleCenter = tab.Bottom - ShellTabTitleHeight / 2f;
            }

            textPaint.Color = textColor;
            float textWidth = font.MeasureText(text, out var textBounds);
            float baseline = TextRenderingHelper.BaselineForVerticalCenter(font, titleCenter);
            canvas.DrawText(text, tab.MidX - textBounds.MidX, baseline, SKTextAlign.Left, font, textPaint);
            var titleRect = new SKRect(tab.MidX - textWidth / 2f, baseline + textBounds.Top, tab.MidX + textWidth / 2f, baseline + textBounds.Bottom);
            _tabHits.Add((section, tab, iconRect, titleRect));
        }
    }

    /// <summary>A press on a MAUI tab selects its section through MAUI; true when the press was on one.</summary>
    private bool TryPressMauiTab(float x, float y)
    {
        foreach (var (section, bounds, _, _) in _tabHits)
        {
            if (!bounds.Contains(x, y))
                continue;
            if (section.IsEnabled && _mauiShell?.CurrentItem is IShellItemController item
                && !ReferenceEquals(_mauiShell.CurrentItem.CurrentItem, section))
            {
                try
                {
                    item.ProposeSection(section);
                }
                catch (Exception ex)
                {
                    DiagnosticLog.Error("SkiaShell", $"Selecting the tab '{section.Title}' failed", ex);
                }
            }
            return true;
        }
        return false;
    }
}
