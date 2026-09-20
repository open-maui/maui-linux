// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Reflection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Builds a <see cref="SkiaCellView"/> for a MAUI <see cref="Cell"/>
/// (TextCell, EntryCell, SwitchCell, ImageCell, ViewCell) and keeps it in
/// sync: cell property changes update the row, and the row's editor /
/// switch write back to the cell. Shared by the TableView and ListView
/// handlers so both render cells identically.
/// </summary>
internal static class CellViewFactory
{
    // Cell.OnTapped is protected internal; the platform equivalent of what
    // MAUI's own renderers call when a row is tapped (it raises Cell.Tapped
    // and runs TextCell.Command).
    private static readonly MethodInfo? s_onTapped = typeof(Cell).GetMethod(
        "OnTapped", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    /// <summary>
    /// Raises the cell's Tapped (and TextCell.Command) as a row tap would.
    /// </summary>
    public static void SendTapped(Cell cell)
    {
        if (!cell.IsEnabled) return;
        try
        {
            s_onTapped?.Invoke(cell, null);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            DiagnosticLog.Error("CellViewFactory", "Cell.Tapped handler threw", ex.InnerException);
        }
    }

    /// <summary>
    /// Creates the row for <paramref name="cell"/>. The row's
    /// <see cref="SkiaCellView.Detach"/> unsubscribes the cell's events.
    /// </summary>
    public static SkiaCellView Create(Cell cell, IMauiContext? mauiContext)
    {
        var view = new SkiaCellView { Cell = cell, IsEnabled = cell.IsEnabled };
        view.RowHeight = cell.Height > 0 ? (float)cell.Height : 0f;

        switch (cell)
        {
            case ImageCell imageCell:
                view.Kind = SkiaCellKind.Image;
                ApplyText(view, imageCell);
                view.Image = LoadImage(imageCell.ImageSource);
                break;

            case TextCell textCell:
                view.Kind = SkiaCellKind.Text;
                ApplyText(view, textCell);
                break;

            case EntryCell entryCell:
                view.Kind = SkiaCellKind.Entry;
                view.Text = entryCell.Label ?? string.Empty;
                view.TextColor = entryCell.LabelColor;
                var entry = new SkiaEntry
                {
                    Text = entryCell.Text ?? string.Empty,
                    Placeholder = entryCell.Placeholder ?? string.Empty,
                    HorizontalTextAlignment = entryCell.HorizontalTextAlignment,
                };
                entry.TextChanged += (s, e) =>
                {
                    if (entryCell.Text != e.NewTextValue)
                        entryCell.Text = e.NewTextValue;
                };
                entry.Completed += (s, e) => ((IEntryCellController)entryCell).SendCompleted();
                view.Editor = entry;
                break;

            case SwitchCell switchCell:
                view.Kind = SkiaCellKind.Switch;
                view.Text = switchCell.Text ?? string.Empty;
                var sw = new SkiaSwitch { IsOn = switchCell.On };
                if (switchCell.OnColor != null)
                    sw.OnTrackColor = switchCell.OnColor;
                sw.Toggled += (s, e) =>
                {
                    if (switchCell.On != sw.IsOn)
                        switchCell.On = sw.IsOn;
                };
                view.Accessory = sw;
                break;

            case ViewCell viewCell:
                view.Kind = SkiaCellKind.View;
                view.Content = RenderContent(viewCell.View, mauiContext);
                break;

            default:
                view.Kind = SkiaCellKind.Text;
                view.Text = cell.ToString() ?? string.Empty;
                break;
        }

        PropertyChangedEventHandler onChanged = (s, e) => OnCellPropertyChanged(view, cell, e.PropertyName, mauiContext);
        cell.PropertyChanged += onChanged;
        view.Detach = () =>
        {
            cell.PropertyChanged -= onChanged;
            // Release hosted views so a rebuilt row can re-host the same
            // platform view (a ViewCell's content keeps its handler).
            view.Content = null;
            view.Editor = null;
            view.Accessory = null;
        };
        return view;
    }

    /// <summary>A header row for a ListView group.</summary>
    public static SkiaCellView CreateGroupHeader(Cell headerCell, IMauiContext? mauiContext)
    {
        if (headerCell is ViewCell)
        {
            var view = Create(headerCell, mauiContext);
            view.Kind = SkiaCellKind.GroupHeader;
            return view;
        }

        var header = Create(headerCell, mauiContext);
        header.Kind = SkiaCellKind.GroupHeader;
        header.BackgroundColor = SkiaTheme.Gray100;
        return header;
    }

    private static void ApplyText(SkiaCellView view, TextCell textCell)
    {
        view.Text = textCell.Text ?? string.Empty;
        view.Detail = textCell.Detail;
        view.TextColor = textCell.TextColor;
        view.DetailColor = textCell.DetailColor;
    }

    private static void OnCellPropertyChanged(SkiaCellView view, Cell cell, string? propertyName, IMauiContext? mauiContext)
    {
        switch (cell)
        {
            case TextCell textCell when propertyName is nameof(TextCell.Text) or nameof(TextCell.Detail)
                or nameof(TextCell.TextColor) or nameof(TextCell.DetailColor):
                ApplyText(view, textCell);
                break;
            case ImageCell imageCell when propertyName == nameof(ImageCell.ImageSource):
                view.Image = LoadImage(imageCell.ImageSource);
                break;
            case EntryCell entryCell:
                if (propertyName == nameof(EntryCell.Label)) view.Text = entryCell.Label ?? string.Empty;
                else if (propertyName == nameof(EntryCell.LabelColor)) view.TextColor = entryCell.LabelColor;
                else if (view.Editor is SkiaEntry entry)
                {
                    if (propertyName == nameof(EntryCell.Text) && entry.Text != (entryCell.Text ?? string.Empty))
                        entry.Text = entryCell.Text ?? string.Empty;
                    else if (propertyName == nameof(EntryCell.Placeholder))
                        entry.Placeholder = entryCell.Placeholder ?? string.Empty;
                }
                break;
            case SwitchCell switchCell:
                if (propertyName == nameof(SwitchCell.Text)) view.Text = switchCell.Text ?? string.Empty;
                else if (propertyName == nameof(SwitchCell.On) && view.Accessory is SkiaSwitch sw && sw.IsOn != switchCell.On)
                    sw.IsOn = switchCell.On;
                else if (propertyName == nameof(SwitchCell.OnColor) && view.Accessory is SkiaSwitch sw2 && switchCell.OnColor != null)
                    sw2.OnTrackColor = switchCell.OnColor;
                break;
            case ViewCell viewCell when propertyName == nameof(ViewCell.View):
                view.Content = RenderContent(viewCell.View, mauiContext);
                break;
        }

        if (propertyName == nameof(Cell.IsEnabled))
            view.IsEnabled = cell.IsEnabled;
        else if (propertyName == nameof(Cell.Height))
            view.RowHeight = cell.Height > 0 ? (float)cell.Height : 0f;

        view.Invalidate();
    }

    private static SkiaView? RenderContent(View? content, IMauiContext? mauiContext)
    {
        if (content == null || mauiContext == null) return null;
        try
        {
            if (content.Handler?.PlatformView is SkiaView existing)
                return existing;
            content.Handler = content.ToViewHandler(mauiContext);
            return content.Handler?.PlatformView as SkiaView;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("CellViewFactory", $"Rendering cell content {content.GetType().Name} failed", ex);
            return null;
        }
    }

    private static SKBitmap? LoadImage(ImageSource? source)
    {
        if (source is not FileImageSource fileSource || string.IsNullOrEmpty(fileSource.File))
            return null;

        try
        {
            string path = System.IO.Path.IsPathRooted(fileSource.File)
                ? fileSource.File
                : System.IO.Path.Combine(AppContext.BaseDirectory, fileSource.File);
            if (!System.IO.File.Exists(path)) return null;
            using var stream = System.IO.File.OpenRead(path);
            return SKBitmap.Decode(stream);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("CellViewFactory", $"Image load failed: {fileSource.File}", ex);
            return null;
        }
    }
}
