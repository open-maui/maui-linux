// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for TableView on Linux: TableRoot / TableSection / cells become
/// a <see cref="SkiaTableView"/> of <see cref="SkiaCellView"/> rows. Row
/// taps raise Cell.Tapped (and TextCell.Command); switch and entry cells
/// write back to their cells through <see cref="CellViewFactory"/>.
/// </summary>
public partial class TableViewHandler : ViewHandler<TableView, SkiaTableView>
{
    public static IPropertyMapper<TableView, TableViewHandler> Mapper =
        new PropertyMapper<TableView, TableViewHandler>(ViewHandler.ViewMapper)
        {
            [nameof(TableView.Root)] = MapRoot,
            [nameof(TableView.RowHeight)] = MapRowHeight,
            [nameof(TableView.HasUnevenRows)] = MapHasUnevenRows,
            [nameof(TableView.Intent)] = MapIntent,
            [nameof(IView.Background)] = MapBackground,
            [nameof(TableView.BackgroundColor)] = MapBackgroundColor,
        };

    public static CommandMapper<TableView, TableViewHandler> CommandMapper =
        new(ViewHandler.ViewCommandMapper)
        {
        };

    public TableViewHandler() : base(Mapper, CommandMapper)
    {
    }

    public TableViewHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaTableView CreatePlatformView()
    {
        return new SkiaTableView();
    }

    protected override void ConnectHandler(SkiaTableView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.CellTapped += OnCellTapped;
        VirtualView.ModelChanged += OnModelChanged;
        RebuildSections();
    }

    protected override void DisconnectHandler(SkiaTableView platformView)
    {
        platformView.CellTapped -= OnCellTapped;
        if (VirtualView != null)
            VirtualView.ModelChanged -= OnModelChanged;
        ReleaseSections(platformView);
        base.DisconnectHandler(platformView);
    }

    private void OnModelChanged(object? sender, EventArgs e) => RebuildSections();

    private void OnCellTapped(object? sender, SkiaCellView row)
    {
        if (row.Cell is Cell cell)
            CellViewFactory.SendTapped(cell);
    }

    private static void ReleaseSections(SkiaTableView platformView)
    {
        foreach (var row in platformView.Cells)
            row.Detach?.Invoke();
        platformView.ClearSections();
    }

    /// <summary>
    /// Rebuilds the platform sections from <see cref="TableView.Root"/>.
    /// MAUI raises ModelChanged for every structural change (root, sections,
    /// cells), so a full rebuild keeps the mirror simple and exact.
    /// </summary>
    private void RebuildSections()
    {
        if (PlatformView is null || VirtualView is null) return;

        ReleaseSections(PlatformView);

        var root = VirtualView.Root;
        if (root == null) return;

        foreach (var section in root)
        {
            var platformSection = new SkiaTableSection(section.Title)
            {
                TitleColor = section.TextColor,
                Tag = section,
            };
            foreach (var cell in section)
            {
                platformSection.CellList.Add(CellViewFactory.Create(cell, MauiContext));
            }
            PlatformView.AddSection(platformSection);
        }

        PlatformView.InvalidateMeasure();
    }

    public static void MapRoot(TableViewHandler handler, TableView tableView)
    {
        handler.RebuildSections();
    }

    public static void MapRowHeight(TableViewHandler handler, TableView tableView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.RowHeight = tableView.RowHeight > 0 ? (float)tableView.RowHeight : SkiaCellView.DefaultHeight;
    }

    public static void MapHasUnevenRows(TableViewHandler handler, TableView tableView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.HasUnevenRows = tableView.HasUnevenRows;
    }

    public static void MapIntent(TableViewHandler handler, TableView tableView)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Intent = tableView.Intent switch
        {
            TableIntent.Menu => SkiaTableIntent.Menu,
            TableIntent.Settings => SkiaTableIntent.Settings,
            TableIntent.Form => SkiaTableIntent.Form,
            _ => SkiaTableIntent.Data
        };
    }

    public static void MapBackground(TableViewHandler handler, TableView tableView)
    {
        if (handler.PlatformView is null) return;
        if (tableView.BackgroundColor is not null) return;
        if (tableView.Background is SolidColorBrush { Color: Color color })
            handler.PlatformView.BackgroundColor = color;
    }

    public static void MapBackgroundColor(TableViewHandler handler, TableView tableView)
    {
        if (handler.PlatformView is null) return;
        if (tableView.BackgroundColor is not null)
            handler.PlatformView.BackgroundColor = tableView.BackgroundColor;
    }
}
