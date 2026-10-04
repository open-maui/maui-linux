// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Syncfusion;
using Microsoft.Maui.Platform.Linux.Window;
using SkiaSharp;
using Syncfusion.Maui.DataGrid;
using Xunit;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// SfDataGrid on Linux, driven by the mouse and keyboard as a desktop user drives it: the rows
/// draw and scroll with the wheel (raising ScrollStateChanged), a click selects, a header click
/// sorts, the arrow keys move the selection, a right-click raises CellRightTapped, and a column
/// border dragged with the mouse resizes the column under the resize cursor.
/// </summary>
[Collection("LinuxApplication.Current")]
public sealed class SyncfusionDataGridTests
{
    public sealed class Person
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public double Salary { get; set; }
    }

    private static List<Person> People() =>
        Enumerable.Range(1, 200).Select(i => new Person { Id = i, Name = "Name " + (200 - i), Salary = i * 10 }).ToList();

    // Column widths are 100 (DefaultColumnWidth); the header row is 56 high, rows 47.
    private const float HeaderY = 27;

    private static (CompatHost Host, SfDataGrid Grid) Host(Action<SfDataGrid>? configure = null)
    {
        var grid = new SfDataGrid { ItemsSource = People(), SelectionMode = DataGridSelectionMode.Single, SortingMode = DataGridSortingMode.Single };
        configure?.Invoke(grid);
        var host = new CompatHost(new ContentPage { Content = grid }, b => b.UseLinuxSyncfusion(), 600, 400);
        Pump(host);
        return (host, grid);
    }

    private static void Pump(CompatHost host, int frames = 3)
    {
        for (int i = 0; i < frames; i++)
        {
            Thread.Sleep(20);
            host.Render();
        }
    }

    private static void Click(CompatHost host, float x, float y, PointerButton button = PointerButton.Left)
    {
        host.DisplayWindow.RaisePointerMoved(x, y);
        host.DisplayWindow.RaisePointerPressed(x, y, button);
        host.DisplayWindow.RaisePointerReleased(x, y);
        host.Render();
    }

    [Fact]
    public void Rows_draw_and_the_wheel_scrolls_them()
    {
        var (host, grid) = Host();
        using (host)
        {
            var states = new List<DataGridScrollState>();
            grid.ScrollStateChanged += (_, e) => states.Add(e.ScrollState);
            // Row text is drawn below the header.
            host.CountPixelsNot(SKColors.White, new SKRectI(0, 60, 300, 400)).Should().BeGreaterThan(200);

            var scroller = Descendants(grid).OfType<ScrollView>().First(s => s.GetType().Name == "DataGridScrollView");
            scroller.Handler.Should().BeOfType<SfDataGridScrollViewBridgeHandler>();
            host.DisplayWindow.RaisePointerMoved(150, 200);
            host.DisplayWindow.RaiseScroll(150, 200, 3);
            Pump(host);

            scroller.ScrollY.Should().BeGreaterThan(0, "the wheel scrolls the rows");
            states.Should().Equal(new[] { DataGridScrollState.Fling, DataGridScrollState.Idle },
                "the Windows handler reports a wheel scroll as Fling; it settles back to Idle");
        }
    }

    [Fact]
    public void A_click_selects_a_row_and_the_arrow_keys_move_the_selection()
    {
        var (host, grid) = Host();
        using (host)
        {
            Click(host, 150, 150); // the third data row (56 + 2 * 47 = 150)
            grid.SelectedIndex.Should().Be(3);
            ((Person)grid.SelectedRow!).Id.Should().Be(3);

            host.DisplayWindow.RaiseKeyDown(Key.Down);
            host.Render();
            grid.SelectedIndex.Should().Be(4, "VisualContainer.OnKeyDown runs the selection controller's key handling, as on Windows");
            host.DisplayWindow.RaiseKeyDown(Key.Up);
            host.DisplayWindow.RaiseKeyDown(Key.Up);
            host.Render();
            grid.SelectedIndex.Should().Be(2);
        }
    }

    [Fact]
    public void A_header_click_sorts_the_column()
    {
        var (host, grid) = Host();
        using (host)
        {
            Click(host, 250, HeaderY);
            // The header cell waits 150 ms for a possible double tap before it sorts.
            for (int i = 0; i < 10 && grid.SortColumnDescriptions.Count == 0; i++)
                Pump(host, 2);
            grid.SortColumnDescriptions.Should().ContainSingle(d => d.ColumnName == nameof(Person.Salary));
        }
    }

    [Fact]
    public void A_right_click_raises_CellRightTapped_and_selects_the_row()
    {
        var (host, grid) = Host();
        using (host)
        {
            var tapped = new List<DataGridCellRightTappedEventArgs>();
            grid.CellRightTapped += (_, e) => tapped.Add(e);
            Click(host, 150, 197, PointerButton.Right);

            tapped.Should().ContainSingle();
            tapped[0].RowColumnIndex.RowIndex.Should().Be(4);
            tapped[0].RowColumnIndex.ColumnIndex.Should().Be(1);
            ((Person)tapped[0].RowData!).Id.Should().Be(4);
            grid.SelectedIndex.Should().Be(4, "AllowSelectionOnSecondaryTap is on by default");
        }
    }

    [Fact]
    public void Dragging_a_column_border_resizes_the_column_under_the_resize_cursor()
    {
        var (host, grid) = Host(g => g.AllowResizingColumns = true);
        using (host)
        {
            grid.Columns[0].ActualWidth.Should().Be(100);
            host.DisplayWindow.RaisePointerMoved(60, HeaderY);
            host.DisplayWindow.LastCursor.Should().Be(CursorType.Arrow);
            host.DisplayWindow.RaisePointerMoved(99, HeaderY);
            host.DisplayWindow.LastCursor.Should().Be(CursorType.SizeWestEast, "the Windows build shows the resize cursor over a column border");

            host.DisplayWindow.RaisePointerPressed(99, HeaderY);
            for (int i = 1; i <= 10; i++)
                host.DisplayWindow.RaisePointerMoved(99 + i * 5, HeaderY);
            host.DisplayWindow.RaisePointerReleased(149, HeaderY);
            Pump(host);

            grid.Columns[0].ActualWidth.Should().BeApproximately(150, 2, "the drag reaches the column resizing controller as pan updates");
        }
    }

    [Fact]
    public void A_header_cell_highlights_while_the_mouse_is_over_it()
    {
        var (host, grid) = Host(g => g.AllowHeaderCellHoverHighlighting = true);
        using (host)
        {
            var header = Descendants(grid).First(e => e.GetType().Name == "DataGridHeaderCell" && Text(e) == "Name");
            var dataColumn = header.GetType().GetProperty("DataColumn", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(header)!;
            var hovered = dataColumn.GetType().GetProperty("IsHeaderCellHovered", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;

            host.DisplayWindow.RaisePointerMoved(150, 200);
            host.DisplayWindow.RaisePointerMoved(150, HeaderY);
            hovered.GetValue(dataColumn).Should().Be(true);
            host.DisplayWindow.RaisePointerMoved(150, 200);
            hovered.GetValue(dataColumn).Should().Be(false);
        }
    }

    private static string? Text(Element e) =>
        Descendants(e).OfType<Label>().FirstOrDefault(l => !string.IsNullOrEmpty(l.Text))?.Text;

    private static IEnumerable<Element> Descendants(Element root)
    {
        foreach (var child in ((IVisualTreeElement)root).GetVisualChildren().OfType<Element>())
        {
            yield return child;
            foreach (var d in Descendants(child))
                yield return d;
        }
    }
}
