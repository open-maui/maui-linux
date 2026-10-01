// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Rendering;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// The presented page's ToolbarItems in the navigation bar, as MAUI's Shell shows them on the
/// other platforms: primary items at the right end (the icon, or the text when there is none),
/// secondary items behind a "more" button, a disabled item dimmed. A click activates the item
/// (its Clicked event and Command). Items, icons (an AppThemeBinding) and IsEnabled are followed
/// as they change.
/// </summary>
public partial class SkiaShell
{
    private const float ToolbarIconSize = 24f;
    private const float ToolbarItemPadding = 10f;

    private Microsoft.Maui.Controls.Page? _toolbarPage;
    private readonly List<ToolbarItem> _toolbarItems = new();
    private readonly List<(ToolbarItem? Item, SKRect Bounds)> _toolbarHits = new();
    private readonly Dictionary<ToolbarItem, (ImageSource? Source, SKBitmap? Bitmap)> _toolbarIcons = new();
    private SKRect _toolbarMoreHit = SKRect.Empty;

    /// <summary>The presented page's toolbar items, in order (for tests and the inspector).</summary>
    internal IReadOnlyList<ToolbarItem> PresentedToolbarItems => _toolbarItems;

    /// <summary>Where the last frame drew each item (tests).</summary>
    internal IReadOnlyList<(ToolbarItem? Item, SKRect Bounds)> ToolbarHitAreas => _toolbarHits;

    /// <summary>Follows <paramref name="page"/>'s toolbar items in the bar.</summary>
    private void TrackToolbarItems(Microsoft.Maui.Controls.Page? page)
    {
        if (ReferenceEquals(_toolbarPage, page))
            return;
        if (_toolbarPage?.ToolbarItems is INotifyCollectionChanged oldItems)
            oldItems.CollectionChanged -= OnToolbarItemsChanged;
        _toolbarPage = page;
        if (page?.ToolbarItems is INotifyCollectionChanged newItems)
            newItems.CollectionChanged += OnToolbarItemsChanged;
        SyncToolbarItems();
    }

    private void OnToolbarItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => SyncToolbarItems();

    private void SyncToolbarItems()
    {
        foreach (var item in _toolbarItems)
            item.PropertyChanged -= OnToolbarItemPropertyChanged;
        _toolbarItems.Clear();
        if (_toolbarPage != null)
            _toolbarItems.AddRange(_toolbarPage.ToolbarItems);
        foreach (var item in _toolbarItems)
            item.PropertyChanged += OnToolbarItemPropertyChanged;

        // Icons of items no longer shown are dropped.
        foreach (var gone in _toolbarIcons.Keys.Where(k => !_toolbarItems.Contains(k)).ToList())
        {
            _toolbarIcons[gone].Bitmap?.Dispose();
            _toolbarIcons.Remove(gone);
        }
        Invalidate();
    }

    private void OnToolbarItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ToolbarItem.IconImageSource) or nameof(ToolbarItem.Text)
            or nameof(ToolbarItem.IsEnabled) or nameof(ToolbarItem.Order))
            Invalidate();
    }

    /// <summary>The item's icon at the bar's size and the screen's density; cached per source.</summary>
    private SKBitmap? ToolbarIcon(ToolbarItem item)
    {
        var source = item.IconImageSource;
        if (_toolbarIcons.TryGetValue(item, out var cached) && ReferenceEquals(cached.Source, source))
            return cached.Bitmap;
        cached.Bitmap?.Dispose();

        int pixels = (int)Math.Ceiling(ToolbarIconSize * Math.Max(1f, DeviceScale));
        SKBitmap? bitmap = null;
        try
        {
            bitmap = source switch
            {
                FileImageSource file => ImageFileResolver.LoadBitmap(file.File, pixels),
                FontImageSource font => ImageHandler.ImageSourceServiceResultManager.RenderFontImageSource(font, pixels, pixels),
                _ => null,
            };
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaShell", $"Loading the toolbar icon of '{item.Text}' failed", ex);
        }
        if (bitmap == null && source != null)
            DiagnosticLog.Warn("SkiaShell", $"Toolbar icon not found for '{item.Text}': {source}");
        _toolbarIcons[item] = (source, bitmap);
        return bitmap;
    }

    /// <summary>
    /// Draws the items at the right end of <paramref name="bar"/>; returns the left edge of the
    /// first one, where the title has to stop.
    /// </summary>
    private float DrawToolbarItems(SKCanvas canvas, SKRect bar)
    {
        _toolbarHits.Clear();
        _toolbarMoreHit = SKRect.Empty;
        float right = bar.Right - 8;
        if (_toolbarItems.Count == 0)
            return right;

        using var font = SkiaFontFactory.Create(14f);
        using var textPaint = new SKPaint { Color = _navBarTextColorSK, IsAntialias = true };
        using var iconPaint = new SKPaint { IsAntialias = true };
        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);

        bool hasSecondary = _toolbarItems.Any(i => i.Order == ToolbarItemOrder.Secondary);
        if (hasSecondary)
        {
            // "More": three dots, the overflow every platform puts secondary items behind.
            float w = ToolbarIconSize + ToolbarItemPadding * 2;
            var hit = new SKRect(right - w, bar.Top, right, bar.Bottom);
            using var dot = new SKPaint { Color = _navBarTextColorSK, IsAntialias = true };
            for (int d = -1; d <= 1; d++)
                canvas.DrawCircle(hit.MidX, hit.MidY + d * 6, 2f, dot);
            _toolbarMoreHit = hit;
            right = hit.Left;
        }

        // Primary items, the first one leftmost.
        var primary = _toolbarItems.Where(i => i.Order != ToolbarItemOrder.Secondary).ToList();
        for (int index = primary.Count - 1; index >= 0; index--)
        {
            var item = primary[index];
            byte alpha = item.IsEnabled ? (byte)255 : (byte)100;
            var icon = ToolbarIcon(item);
            float width;
            if (icon != null)
            {
                width = ToolbarIconSize + ToolbarItemPadding * 2;
                var hit = new SKRect(right - width, bar.Top, right, bar.Bottom);
                float scale = ToolbarIconSize / Math.Max(icon.Width, icon.Height);
                float w = icon.Width * scale, h = icon.Height * scale;
                var dest = SKRect.Create(hit.MidX - w / 2, hit.MidY - h / 2, w, h);
                iconPaint.Color = new SKColor(255, 255, 255, alpha);
                using var image = SKImage.FromBitmap(icon);
                canvas.DrawImage(image, dest, sampling, iconPaint);
                _toolbarHits.Add((item, hit));
            }
            else
            {
                var text = item.Text ?? string.Empty;
                width = font.MeasureText(text) + ToolbarItemPadding * 2;
                var hit = new SKRect(right - width, bar.Top, right, bar.Bottom);
                textPaint.Color = _navBarTextColorSK.WithAlpha(alpha);
                canvas.DrawText(text, hit.Left + ToolbarItemPadding,
                    TextRenderingHelper.BaselineForVerticalCenter(font, hit.MidY), SKTextAlign.Left, font, textPaint);
                _toolbarHits.Add((item, hit));
            }
            right -= width;
        }
        return right;
    }

    /// <summary>A press on a toolbar item activates it; true when the press was on one.</summary>
    private bool TryPressToolbarItem(float x, float y)
    {
        if (_toolbarMoreHit.Contains(x, y))
        {
            ShowToolbarOverflow(_toolbarMoreHit);
            return true;
        }
        foreach (var (item, bounds) in _toolbarHits)
        {
            if (item == null || !bounds.Contains(x, y))
                continue;
            if (item.IsEnabled)
                ActivateToolbarItem(item);
            return true;
        }
        return false;
    }

    private static void ActivateToolbarItem(ToolbarItem item)
    {
        try
        {
            ((IMenuItemController)item).Activate();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaShell", $"Toolbar item '{item.Text}' threw", ex);
        }
    }

    private void ShowToolbarOverflow(SKRect anchor)
    {
        var items = _toolbarItems
            .Where(i => i.Order == ToolbarItemOrder.Secondary)
            .Select(i => new ContextMenuItem(i.Text ?? string.Empty, () => ActivateToolbarItem(i), i.IsEnabled))
            .ToList();
        if (items.Count == 0)
            return;
        bool dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        LinuxDialogService.ShowContextMenu(new SkiaContextMenu(anchor.Left, anchor.Bottom, items, dark));
    }
}
