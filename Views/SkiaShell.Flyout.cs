// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Rendering;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// The flyout's item list and its header's scrolling. With a MAUI Shell attached the list is
/// MAUI's own (IShellController.GenerateFlyoutGrouping, as Windows' ShellView lists it): the
/// visible items, a TabBar's sections left out, FlyoutDisplayOptions.AsMultipleItems listing a
/// FlyoutItem's sections or a section's contents, Shell.FlyoutItemIsVisible and IsVisible
/// respected, the current content's MenuItems, and a separator between groups. Without one the
/// platform sections are listed. The header follows Shell.FlyoutHeaderBehavior: Default and Fixed
/// stay put, Scroll scrolls away with the items, CollapseOnScroll shrinks to its minimum height
/// (MinimumHeightRequest, else 56) as the items scroll, then the items scroll under it.
/// </summary>
public partial class SkiaShell
{
    /// <summary>One row of the flyout list.</summary>
    internal sealed class FlyoutRow
    {
        /// <summary>The MAUI element the row stands for (a ShellItem, ShellSection, ShellContent or MenuItem).</summary>
        public Element? Element { get; init; }

        /// <summary>The platform section the row selects when no MAUI Shell is attached; -1 otherwise.</summary>
        public int SectionIndex { get; init; } = -1;

        /// <summary>A line between two groups of items.</summary>
        public bool IsSeparator { get; init; }

        public string Title { get; init; } = string.Empty;
        public Microsoft.Maui.Controls.ImageSource? IconSource { get; init; }
        public string? IconPath { get; init; }
        public SkiaView? TemplateView { get; init; }
        public bool IsEnabled { get; init; } = true;
    }

    private const float FlyoutSeparatorHeight = 9f;

    /// <summary>
    /// Realises the flyout row of a MAUI flyout element from Shell.ItemTemplate or
    /// Shell.MenuItemTemplate (null for the built-in row). Set by the host.
    /// </summary>
    internal Func<Element, SkiaView?>? FlyoutItemViewRenderer { get; set; }

    private SkiaView? _flyoutHeaderView;

    /// <summary>
    /// Optional header view in the flyout.
    /// </summary>
    public SkiaView? FlyoutHeaderView
    {
        get => _flyoutHeaderView;
        set
        {
            if (ReferenceEquals(_flyoutHeaderView, value))
                return;
            if (_flyoutHeaderView != null && ReferenceEquals(_flyoutHeaderView.Parent, this))
                _flyoutHeaderView.Parent = null;
            _flyoutHeaderView = value;
            // The wheel over the header reaches the shell, which scrolls a Scroll or CollapseOnScroll header.
            if (value != null && value.Parent == null)
                value.Parent = this;
            Invalidate();
        }
    }

    /// <summary>
    /// The header's MinimumHeightRequest: the height a CollapseOnScroll header shrinks to.
    /// Negative (the default) when unset; the header then keeps 56, as on every MAUI platform.
    /// </summary>
    public float FlyoutHeaderMinimumHeight { get; set; } = -1f;

    /// <summary>The height a CollapseOnScroll header shrinks to.</summary>
    private float CollapsedHeaderHeight => FlyoutHeaderMinimumHeight >= 0 ? FlyoutHeaderMinimumHeight : CollapsedFlyoutHeaderHeight;

    /// <summary>How far the flyout's items (and a Scroll or CollapseOnScroll header) are scrolled.</summary>
    internal float FlyoutScrollOffset => _flyoutScrollOffset;

    /// <summary>The flyout's rows in order (with a MAUI Shell attached, MAUI's flyout items).</summary>
    internal List<FlyoutRow> FlyoutRows()
    {
        var rows = new List<FlyoutRow>();
        if (_mauiShell is IShellController controller)
        {
            List<List<Element>> groups;
            try
            {
                groups = controller.GenerateFlyoutGrouping();
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("SkiaShell", "Reading the Shell's flyout items failed", ex);
                return rows;
            }
            bool first = true;
            foreach (var group in groups)
            {
                if (group.Count == 0)
                    continue;
                // Windows' ShellView puts a separator before every group but the first.
                if (!first)
                    rows.Add(new FlyoutRow { IsSeparator = true });
                first = false;
                foreach (var element in group)
                    rows.Add(RowFor(element));
            }
            return rows;
        }

        for (int i = 0; i < _sections.Count; i++)
        {
            var section = _sections[i];
            if (!section.IsVisibleInFlyout)
                continue;
            rows.Add(new FlyoutRow
            {
                SectionIndex = i,
                Title = section.Title,
                IconSource = section.IconSource,
                IconPath = section.IconPath,
                TemplateView = section.TemplateView,
            });
        }
        return rows;
    }

    private FlyoutRow RowFor(Element element)
    {
        SkiaView? template = null;
        try
        {
            template = FlyoutItemViewRenderer?.Invoke(element);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaShell", "Realising a flyout item template failed", ex);
        }
        return element switch
        {
            BaseShellItem item => new FlyoutRow
            {
                Element = element,
                Title = item.Title ?? string.Empty,
                IconSource = item.FlyoutIcon,
                IsEnabled = item.IsEnabled,
                TemplateView = template,
            },
            Microsoft.Maui.Controls.MenuItem menuItem => new FlyoutRow
            {
                Element = element,
                Title = menuItem.Text ?? string.Empty,
                IconSource = menuItem.IconImageSource,
                IsEnabled = menuItem.IsEnabled,
                TemplateView = template,
            },
            _ => new FlyoutRow { Element = element, TemplateView = template },
        };
    }

    /// <summary>The titles of the flyout's rows, separators as "-" (tests).</summary>
    internal IReadOnlyList<string> FlyoutItemTitles() =>
        FlyoutRows().Select(r => r.IsSeparator ? "-" : r.Title).ToList();

    /// <summary>The MAUI elements the flyout lists, in order, separators left out.</summary>
    internal IReadOnlyList<Element> FlyoutElements() =>
        FlyoutRows().Where(r => r.Element != null).Select(r => r.Element!).ToList();

    // MAUI marks the shell parts it creates around a bare section or content with an "IMPL_"
    // route (Routing.IsImplicit is internal).
    internal static bool IsImplicitShellElement(Element? element) =>
        element != null && Routing.GetRoute(element)?.StartsWith("IMPL_", StringComparison.Ordinal) == true;

    /// <summary>
    /// The row the flyout shows selected: the element standing for the current item, as Windows'
    /// ShellView.SwitchShellItem picks it (an implicit item's section or content, else the item,
    /// its section or its content, whichever is listed).
    /// </summary>
    private bool IsFlyoutRowSelected(FlyoutRow row, List<FlyoutRow> rows)
    {
        if (row.IsSeparator)
            return false;
        if (row.Element == null)
            return row.SectionIndex == _selectedSectionIndex;
        var item = _mauiShell?.CurrentItem;
        if (item == null)
            return false;
        Element? selected;
        if (IsImplicitShellElement(item))
            selected = IsImplicitShellElement(item.CurrentItem) ? item.CurrentItem?.CurrentItem : item.CurrentItem;
        else if (rows.Any(r => ReferenceEquals(r.Element, item)))
            selected = item;
        else if (rows.Any(r => ReferenceEquals(r.Element, item.CurrentItem)))
            selected = item.CurrentItem;
        else
            selected = item.CurrentItem?.CurrentItem;
        return selected != null && ReferenceEquals(row.Element, selected);
    }

    /// <summary>A flyout row's height: a separator's, a templated row's measured height, else the built-in 48.</summary>
    private static float FlyoutRowHeight(FlyoutRow row, float width)
    {
        if (row.IsSeparator)
            return FlyoutSeparatorHeight;
        if (row.TemplateView is not { } view)
            return DefaultFlyoutRowHeight;
        var desired = view.Measure(new Size(width, double.PositiveInfinity));
        return desired.Height > 0 && !double.IsInfinity(desired.Height) ? (float)desired.Height : DefaultFlyoutRowHeight;
    }

    private static float FlyoutItemsHeight(List<FlyoutRow> rows, float width)
    {
        float total = 0;
        foreach (var row in rows)
            total += FlyoutRowHeight(row, width);
        return total;
    }

    /// <summary>
    /// Where the flyout's parts are for the current scroll offset. <c>HeaderTop</c> and
    /// <c>HeaderHeight</c> place the header; rows start at <c>ItemsOrigin</c> and show between
    /// <c>ItemsClipTop</c> and <c>ItemsBottom</c> (the footer's top).
    /// </summary>
    private readonly record struct FlyoutGeometry(
        SKRect Panel,
        float HeaderTop,
        float HeaderHeight,
        float ItemsClipTop,
        float ItemsOrigin,
        float ItemsBottom,
        float FooterHeight,
        float MaxScroll,
        float Offset);

    /// <summary>The flyout panel's rectangle now (off to the left while it slides in).</summary>
    private SKRect FlyoutPanelRect()
    {
        var b = FlyoutBounds;
        return new SKRect((float)b.Left, (float)b.Top, (float)b.Right, (float)b.Bottom);
    }

    private FlyoutGeometry ComputeFlyoutGeometry(SKRect panel, List<FlyoutRow>? rows)
    {
        float full = ResolveFlyoutHeaderHeight(panel.Width, panel.Height);
        float footer = ResolveFlyoutFooterHeight(panel.Width);
        float bottom = panel.Bottom - footer;
        float viewport = Math.Max(0f, bottom - panel.Top);

        // FlyoutContent scrolls itself (it is the app's own view); the header stays put above it.
        if (FlyoutContentView != null)
            return new FlyoutGeometry(panel, panel.Top, full, panel.Top + full, panel.Top + full, bottom, footer, 0f, 0f);

        float items = FlyoutItemsHeight(rows ?? FlyoutRows(), panel.Width);
        float max, offset;
        switch (FlyoutHeaderBehavior)
        {
            case Microsoft.Maui.Controls.FlyoutHeaderBehavior.Scroll:
                // The header is the list's first part: it scrolls away with the items.
                max = Math.Max(0f, full + items - viewport);
                offset = Math.Clamp(_flyoutScrollOffset, 0f, max);
                return new FlyoutGeometry(panel, panel.Top - offset, full, panel.Top, panel.Top + full - offset, bottom, footer, max, offset);

            case Microsoft.Maui.Controls.FlyoutHeaderBehavior.CollapseOnScroll:
                // The header shrinks to its minimum as the items move up under it, then the
                // items scroll under the collapsed header.
                max = Math.Max(0f, full + items - viewport);
                offset = Math.Clamp(_flyoutScrollOffset, 0f, max);
                float min = Math.Min(full, CollapsedHeaderHeight);
                float height = Math.Max(min, full - offset);
                return new FlyoutGeometry(panel, panel.Top, height, panel.Top + height, panel.Top + full - offset, bottom, footer, max, offset);

            default:
                // Default and Fixed: the header stays, the items scroll below it.
                max = Math.Max(0f, items - (viewport - full));
                offset = Math.Clamp(_flyoutScrollOffset, 0f, max);
                return new FlyoutGeometry(panel, panel.Top, full, panel.Top + full, panel.Top + full - offset, bottom, footer, max, offset);
        }
    }

    private void DrawFlyout(SKCanvas canvas, SKRect bounds)
    {
        bool isLocked = FlyoutBehavior == ShellFlyoutBehavior.Locked;

        // Draw scrim only for non-locked flyout (overlay mode)
        if (!isLocked)
        {
            using var scrimPaint = new SKPaint
            {
                Color = SkiaTheme.Shadow40SK.WithAlpha((byte)(100 * _flyoutAnimationProgress)),
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(bounds, scrimPaint);
        }

        // Draw flyout panel — locked mode uses fixed position, overlay mode uses animation
        float flyoutX = isLocked ? bounds.Left : bounds.Left - FlyoutWidth + (FlyoutWidth * _flyoutAnimationProgress);
        var flyoutBounds = new SKRect(flyoutX, bounds.Top, flyoutX + FlyoutWidth, bounds.Bottom);

        using (var flyoutPaint = new SKPaint { Color = _flyoutBackgroundColorSK, Style = SKPaintStyle.Fill, IsAntialias = true })
            canvas.DrawRect(flyoutBounds, flyoutPaint);

        var rows = FlyoutContentView == null ? FlyoutRows() : null;
        var g = ComputeFlyoutGeometry(flyoutBounds, rows);

        // The header, clipped to the scrolling area (a Scroll header slides up out of it).
        if (FlyoutHeaderView != null && g.HeaderHeight > 0)
        {
            var headerBounds = new SKRect(flyoutBounds.Left, g.HeaderTop, flyoutBounds.Right, g.HeaderTop + g.HeaderHeight);
            FlyoutHeaderView.Measure(new Size(headerBounds.Width, headerBounds.Height));
            FlyoutHeaderView.Arrange(new Rect(headerBounds.Left, headerBounds.Top, headerBounds.Width, headerBounds.Height));
            canvas.Save();
            canvas.ClipRect(new SKRect(flyoutBounds.Left, flyoutBounds.Top, flyoutBounds.Right, g.ItemsBottom));

            // If the header view has a BackgroundColor, draw it over the flyout background
            // to ensure it covers the default flyout color (e.g. white) in the header region
            if (FlyoutHeaderView.BackgroundColor != null && FlyoutHeaderView.BackgroundColor != Colors.Transparent)
            {
                using var headerBgPaint = new SKPaint { Color = FlyoutHeaderView.BackgroundColor.ToSKColor(), Style = SKPaintStyle.Fill };
                canvas.DrawRect(headerBounds, headerBgPaint);
            }

            FlyoutHeaderView.Draw(canvas);
            canvas.Restore();
        }

        // Flyout content replaces the item list.
        if (FlyoutContentView is { } flyoutContent)
        {
            var contentRect = new Rect(flyoutBounds.Left, g.ItemsClipTop, flyoutBounds.Width, Math.Max(0, g.ItemsBottom - g.ItemsClipTop));
            flyoutContent.Measure(new Size(contentRect.Width, contentRect.Height));
            flyoutContent.Arrange(contentRect);
            canvas.Save();
            canvas.ClipRect(new SKRect(flyoutBounds.Left, g.ItemsClipTop, flyoutBounds.Right, g.ItemsBottom));
            flyoutContent.Draw(canvas);
            canvas.Restore();
            DrawFlyoutFooter(canvas, flyoutBounds, g.FooterHeight);
            return;
        }

        // Clip to the items area (below the header, above the footer).
        canvas.Save();
        canvas.ClipRect(new SKRect(flyoutBounds.Left, g.ItemsClipTop, flyoutBounds.Right, g.ItemsBottom));

        using var itemTextFont = SkiaFontFactory.Create(14f);
        using var itemTextPaint = new SKPaint { IsAntialias = true };

        float itemY = g.ItemsOrigin;
        foreach (var row in rows!)
        {
            float itemHeight = FlyoutRowHeight(row, flyoutBounds.Width);
            var rowRect = new SKRect(flyoutBounds.Left, itemY, flyoutBounds.Right, itemY + itemHeight);
            itemY += itemHeight;

            // A templated row keeps its arrangement even when scrolled out, so its bounds are current.
            if (row.TemplateView is { } rowView)
                rowView.Arrange(new Rect(rowRect.Left, rowRect.Top, rowRect.Width, rowRect.Height));

            if (rowRect.Bottom < g.ItemsClipTop || rowRect.Top > g.ItemsBottom)
                continue;

            if (row.IsSeparator)
            {
                using var separatorPaint = new SKPaint { Color = SkiaTheme.Gray300SK, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
                canvas.DrawLine(rowRect.Left + 16, rowRect.MidY, rowRect.Right - 16, rowRect.MidY, separatorPaint);
                continue;
            }

            // A templated row draws itself, selection included (it binds IsChecked).
            if (row.TemplateView is { } templated)
            {
                templated.Draw(canvas);
                continue;
            }

            bool isSelected = IsFlyoutRowSelected(row, rows);
            if (isSelected)
            {
                using var selectionPaint = new SKPaint { Color = SkiaTheme.PrimarySelectionSK, Style = SKPaintStyle.Fill };
                canvas.DrawRect(rowRect, selectionPaint);
            }

            itemTextPaint.Color = isSelected ? SKColors.White : row.IsEnabled ? _flyoutTextColorSK : _flyoutTextColorSK.WithAlpha(97);

            float textStartX = flyoutBounds.Left + 16;
            var icon = row.IconSource != null ? GetFlyoutIcon(row.IconSource) : GetFlyoutIcon(row.IconPath);
            if (icon != null)
            {
                float iconSize = 24f;
                float iconX = flyoutBounds.Left + 16;
                float iconY = rowRect.Top + (itemHeight - iconSize) / 2;
                canvas.DrawBitmap(icon, new SKRect(iconX, iconY, iconX + iconSize, iconY + iconSize));
                textStartX = iconX + iconSize + 12; // gap between icon and text
            }

            canvas.DrawText(row.Title, textStartX, rowRect.Top + 30, itemTextFont, itemTextPaint);
        }

        canvas.Restore();

        DrawFlyoutFooter(canvas, flyoutBounds, g.FooterHeight);
    }

    /// <summary>Where the flyout row of <paramref name="element"/> is now (tests); empty when it is not listed.</summary>
    internal SKRect FlyoutRowBounds(Element element)
    {
        var panel = FlyoutPanelRect();
        var rows = FlyoutRows();
        var g = ComputeFlyoutGeometry(panel, rows);
        float y = g.ItemsOrigin;
        foreach (var row in rows)
        {
            float h = FlyoutRowHeight(row, panel.Width);
            if (ReferenceEquals(row.Element, element))
                return new SKRect(panel.Left, y, panel.Right, y + h);
            y += h;
        }
        return SKRect.Empty;
    }

    /// <summary>A press in the flyout panel: the footer, the header, or a row. True when it was taken.</summary>
    private bool TryPressFlyout(PointerEventArgs e, SKRect flyoutBounds, bool isLocked)
    {
        var rows = FlyoutContentView == null ? FlyoutRows() : null;
        var g = ComputeFlyoutGeometry(flyoutBounds, rows);

        // Footer — dispatch to the footer view for button clicks
        if (e.Y >= g.ItemsBottom && FlyoutFooterView != null)
        {
            var footerHit = FlyoutFooterView.HitTestAt(e.X, e.Y);
            if (footerHit != null)
            {
                _pressedTarget = footerHit;
                footerHit.OnPointerPressed(e);
                return true;
            }
        }

        // Header — dispatch to the header view where it is now
        if (FlyoutHeaderView != null && e.Y >= g.HeaderTop && e.Y < g.HeaderTop + g.HeaderHeight && e.Y < g.ItemsBottom)
        {
            var headerHit = FlyoutHeaderView.HitTestAt(e.X, e.Y);
            if (headerHit != null)
            {
                _pressedTarget = headerHit;
                headerHit.OnPointerPressed(e);
                return true;
            }
        }

        if (rows == null || e.Y < g.ItemsClipTop || e.Y >= g.ItemsBottom)
            return false;

        float itemY = g.ItemsOrigin;
        foreach (var row in rows)
        {
            float itemHeight = FlyoutRowHeight(row, flyoutBounds.Width);
            if (e.Y >= itemY && e.Y < itemY + itemHeight)
            {
                if (!row.IsSeparator)
                    ActivateFlyoutRow(row, isLocked);
                e.Handled = true;
                return true;
            }
            itemY += itemHeight;
        }
        return false;
    }

    /// <summary>
    /// Selects a flyout row: a MAUI element through IShellController.OnFlyoutItemSelectedAsync (a
    /// MenuItem's command runs, an item, section or content is navigated to), a platform section
    /// directly. An unlocked flyout closes.
    /// </summary>
    private void ActivateFlyoutRow(FlyoutRow row, bool isLocked)
    {
        if (row.Element != null)
        {
            if (!row.IsEnabled || _mauiShell is not IShellController controller)
                return;
            try
            {
                controller.OnFlyoutItemSelectedAsync(row.Element).ContinueWith(
                    t => DiagnosticLog.Error("SkiaShell", $"Selecting the flyout item '{row.Title}' failed", t.Exception!),
                    TaskContinuationOptions.OnlyOnFaulted);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("SkiaShell", $"Selecting the flyout item '{row.Title}' failed", ex);
            }
        }
        else if (row.SectionIndex >= 0)
        {
            SelectSection(row.SectionIndex, 0);
        }

        if (!isLocked)
        {
            FlyoutIsPresented = false;
            _flyoutScrollOffset = 0; // Reset scroll when closing
        }
    }

    /// <summary>The wheel over an open or locked flyout scrolls its items (and a Scroll or CollapseOnScroll header).</summary>
    private bool TryScrollFlyout(ScrollEventArgs e)
    {
        bool locked = FlyoutBehavior == ShellFlyoutBehavior.Locked;
        if (!locked && !(FlyoutIsPresented && _flyoutAnimationProgress > 0))
            return false;
        var panel = FlyoutPanelRect();
        if (!panel.Contains(e.X, e.Y))
            return false;
        // The wheel over the flyout never reaches the page under it; FlyoutContent scrolls itself.
        if (FlyoutContentView != null)
            return true;
        var g = ComputeFlyoutGeometry(panel, null);
        if (e.Y >= g.ItemsBottom)
            return true;
        float offset = Math.Clamp(g.Offset + e.DeltaY * 30f, 0f, g.MaxScroll);
        if (offset != _flyoutScrollOffset)
        {
            _flyoutScrollOffset = offset;
            Invalidate();
        }
        return true;
    }

    private FlyoutBehaviorObserver? _flyoutBehaviorObserver;

    /// <summary>Receives MAUI's effective flyout behaviour (IFlyoutBehaviorObserver).</summary>
    private sealed class FlyoutBehaviorObserver : IFlyoutBehaviorObserver
    {
        private readonly SkiaShell _owner;

        public FlyoutBehaviorObserver(SkiaShell owner) => _owner = owner;

        public void OnFlyoutBehaviorChanged(Microsoft.Maui.FlyoutBehavior behavior) => _owner.SetEffectiveFlyoutBehavior(behavior);
    }

    private void ObserveFlyoutBehavior(Shell shell)
    {
        if (shell is not IShellController controller)
            return;
        _flyoutBehaviorObserver = new FlyoutBehaviorObserver(this);
        try
        {
            controller.AddFlyoutBehaviorObserver(_flyoutBehaviorObserver);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaShell", "Observing the Shell's flyout behaviour failed", ex);
        }
        ApplyMauiFlyoutBehavior();
    }

    private void StopObservingFlyoutBehavior(Shell shell)
    {
        if (_flyoutBehaviorObserver != null && shell is IShellController controller)
            controller.RemoveFlyoutBehaviorObserver(_flyoutBehaviorObserver);
        _flyoutBehaviorObserver = null;
    }

    /// <summary>
    /// Applies MAUI's effective flyout behaviour (IFlyoutView.FlyoutBehavior, Shell's
    /// GetEffectiveFlyoutBehavior): Shell.FlyoutBehavior set on the presented page, its
    /// ShellContent, section, item or the Shell wins; otherwise a FlyoutItem root has a flyout, a
    /// TabBar root has none, and a lone implicit item (a Shell of one bare page) has none.
    /// </summary>
    internal void ApplyMauiFlyoutBehavior()
    {
        if (_mauiShell is not IFlyoutView view)
            return;
        try
        {
            SetEffectiveFlyoutBehavior(view.FlyoutBehavior);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaShell", "Reading the Shell's flyout behaviour failed", ex);
        }
    }

    private void SetEffectiveFlyoutBehavior(Microsoft.Maui.FlyoutBehavior behavior)
    {
        var mapped = behavior switch
        {
            Microsoft.Maui.FlyoutBehavior.Disabled => ShellFlyoutBehavior.Disabled,
            Microsoft.Maui.FlyoutBehavior.Locked => ShellFlyoutBehavior.Locked,
            _ => ShellFlyoutBehavior.Flyout,
        };
        if (FlyoutBehavior == mapped)
            return;
        FlyoutBehavior = mapped;
        // No flyout: a drawer left open closes with it.
        if (mapped == ShellFlyoutBehavior.Disabled && FlyoutIsPresented)
            FlyoutIsPresented = false;
        InvalidateMeasure();
        Invalidate();
    }
}
