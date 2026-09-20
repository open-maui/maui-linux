// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Handlers;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

using PointerEventArgs = Microsoft.Maui.Platform.PointerEventArgs;
using PointerButton = Microsoft.Maui.Platform.PointerButton;
using ScrollEventArgs = Microsoft.Maui.Platform.ScrollEventArgs;

/// <summary>
/// SkiaTableView on its own (sections, rows, measure/arrange, drawing, taps,
/// scrolling) and through the TableViewHandler with a MAUI TableView
/// (TableRoot / TableSection / TextCell, EntryCell, SwitchCell, ImageCell,
/// ViewCell; Cell.Tapped; two-way switch and entry state; model changes).
/// </summary>
[Collection(HeadlessMaui.Collection)]
public class SkiaTableViewTests
{
    private static SKBitmap Render(SkiaView view, int width, int height)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        view.Draw(canvas);
        return bitmap;
    }

    private static int CountDarkPixels(SKBitmap bitmap, SKRectI area)
    {
        int count = 0;
        for (int y = Math.Max(0, area.Top); y < Math.Min(bitmap.Height, area.Bottom); y++)
            for (int x = Math.Max(0, area.Left); x < Math.Min(bitmap.Width, area.Right); x++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Red < 120 && p.Green < 120 && p.Blue < 120) count++;
            }
        return count;
    }

    private static SkiaTableView CreateTable(out SkiaCellView first, out SkiaCellView second, out SkiaCellView third)
    {
        var table = new SkiaTableView();
        var section = new SkiaTableSection("Account");
        first = new SkiaCellView { Text = "Name", Detail = "Ada Lovelace" };
        second = new SkiaCellView { Text = "Email" };
        table.AddCell(section, first);
        table.AddCell(section, second);
        table.AddSection(section);

        var other = new SkiaTableSection("Options");
        third = new SkiaCellView { Text = "Notifications", Kind = SkiaCellKind.Switch, Accessory = new SkiaSwitch() };
        table.AddCell(other, third);
        table.AddSection(other);

        table.Measure(new Size(400, 600));
        table.Arrange(new Rect(0, 0, 400, 600));
        return table;
    }

    #region Platform view

    [Fact]
    public void Sections_and_cells_are_registered_as_children()
    {
        var table = CreateTable(out var first, out var second, out var third);

        table.Sections.Should().HaveCount(2);
        table.Sections[0].Title.Should().Be("Account");
        table.Sections[0].Cells.Should().Equal(first, second);
        table.Sections[1].Cells.Should().Equal(third);
        table.Children.Should().Contain(new[] { first, second, third });
    }

    [Fact]
    public void Rows_are_arranged_under_their_section_headers_at_RowHeight()
    {
        var table = CreateTable(out var first, out var second, out var third);
        float h = SkiaTableView.SectionHeaderHeight;

        first.Bounds.Top.Should().Be(h);
        first.Bounds.Height.Should().Be(table.RowHeight);
        second.Bounds.Top.Should().Be(h + table.RowHeight);
        third.Bounds.Top.Should().Be(h + 2 * table.RowHeight + h);
        table.ContentHeight.Should().Be(2 * h + 3 * table.RowHeight);
    }

    [Fact]
    public void Uneven_rows_size_to_their_content()
    {
        var table = CreateTable(out var first, out var second, out _);

        table.HasUnevenRows = true;
        table.Measure(new Size(400, 600));
        table.Arrange(new Rect(0, 0, 400, 600));

        first.Bounds.Height.Should().Be(SkiaCellView.DetailHeight, "a text cell with a detail line is taller");
        second.Bounds.Height.Should().Be(SkiaCellView.DefaultHeight);
    }

    [Fact]
    public void Measure_with_unbounded_height_returns_the_content_height()
    {
        var table = CreateTable(out _, out _, out _);

        var size = table.Measure(new Size(400, double.PositiveInfinity));

        size.Height.Should().Be(table.ContentHeight);
        size.Width.Should().Be(400);
    }

    [Fact]
    public void Draw_paints_section_titles_and_cell_text()
    {
        var table = CreateTable(out var first, out _, out _);

        using var bitmap = Render(table, 400, 600);

        CountDarkPixels(bitmap, new SKRectI(0, 0, 200, (int)SkiaTableView.SectionHeaderHeight))
            .Should().BeGreaterThan(20, "the section title is drawn");
        CountDarkPixels(bitmap, new SKRectI(0, (int)first.Bounds.Top, 200, (int)first.Bounds.Bottom))
            .Should().BeGreaterThan(20, "the cell text is drawn");
    }

    [Fact]
    public void TextCell_row_shows_text_and_detail()
    {
        var row = new SkiaCellView { Text = "Title" };
        row.Measure(new Size(300, double.PositiveInfinity));
        row.Arrange(new Rect(0, 0, 300, row.DesiredSize.Height));
        using var single = Render(row, 300, 60);
        int singleDark = CountDarkPixels(single, new SKRectI(0, 0, 300, 60));

        row.Detail = "Subtitle text";
        row.Measure(new Size(300, double.PositiveInfinity));
        row.Arrange(new Rect(0, 0, 300, row.DesiredSize.Height));
        using var two = Render(row, 300, 60);

        row.DesiredSize.Height.Should().Be(SkiaCellView.DetailHeight);
        CountDarkPixels(two, new SKRectI(0, 0, 300, 60)).Should().BeGreaterThan(singleDark, "the detail line adds glyphs");
        CountDarkPixels(two, new SKRectI(0, 35, 300, 60)).Should().BeGreaterThan(0, "the detail is drawn below the text");
    }

    [Fact]
    public void Tap_on_a_row_raises_the_cells_Tapped_and_the_tables_CellTapped()
    {
        var table = CreateTable(out _, out var second, out _);
        SkiaCellView? tapped = null;
        int cellTapped = 0;
        table.CellTapped += (s, cell) => tapped = cell;
        second.Tapped += (s, e) => cellTapped++;

        float y = (float)second.Bounds.Top + 10;
        table.OnPointerPressed(new PointerEventArgs(50, y, PointerButton.Left));
        table.OnPointerReleased(new PointerEventArgs(55, y + 2, PointerButton.Left));

        tapped.Should().BeSameAs(second);
        cellTapped.Should().Be(1);
    }

    [Fact]
    public void Release_on_a_different_row_does_not_tap()
    {
        var table = CreateTable(out var first, out var second, out _);
        int tapped = 0;
        first.Tapped += (s, e) => tapped++;
        second.Tapped += (s, e) => tapped++;

        table.OnPointerPressed(new PointerEventArgs(50, (float)first.Bounds.Top + 5, PointerButton.Left));
        table.OnPointerReleased(new PointerEventArgs(50, (float)second.Bounds.Top + 5, PointerButton.Left));

        tapped.Should().Be(0);
    }

    [Fact]
    public void Tap_on_a_switch_row_toggles_its_switch()
    {
        var table = CreateTable(out _, out _, out var third);
        var sw = (SkiaSwitch)third.Accessory!;
        sw.IsOn.Should().BeFalse();

        table.TapCell(third);
        sw.IsOn.Should().BeTrue();

        table.TapCell(third);
        sw.IsOn.Should().BeFalse();
    }

    [Fact]
    public void HitTest_routes_to_the_switch_but_keeps_the_row_surface_for_the_table()
    {
        var table = CreateTable(out _, out var second, out var third);
        var sw = third.Accessory!;

        table.HitTest((float)sw.Bounds.Left + 4, (float)sw.Bounds.Top + 4).Should().BeSameAs(sw);
        table.HitTest(30, (float)second.Bounds.Top + 5).Should().BeSameAs(table);
    }

    [Fact]
    public void ViewCell_row_hosts_arbitrary_content_filling_the_row()
    {
        var content = new SkiaLabel { Text = "custom", HeightRequest = 80 };
        var row = new SkiaCellView { Kind = SkiaCellKind.View, Content = content };
        var table = new SkiaTableView { HasUnevenRows = true };
        var section = new SkiaTableSection();
        table.AddCell(section, row);
        table.AddSection(section);

        table.Measure(new Size(400, 600));
        table.Arrange(new Rect(0, 0, 400, 600));

        row.Children.Should().Contain(content);
        row.Bounds.Height.Should().Be(80);
        content.Bounds.Should().Be(row.Bounds);
        table.HitTest(50, 40).Should().BeSameAs(content);
    }

    [Fact]
    public void Scrolling_moves_rows_and_clamps_to_content()
    {
        var table = new SkiaTableView();
        var section = new SkiaTableSection("Long");
        for (int i = 0; i < 30; i++)
            table.AddCell(section, new SkiaCellView { Text = $"Row {i}" });
        table.AddSection(section);
        table.Measure(new Size(300, 200));
        table.Arrange(new Rect(0, 0, 300, 200));
        var firstTop = section.Cells[0].Bounds.Top;

        table.OnScroll(new ScrollEventArgs(10, 10, 0, 5));
        table.ScrollOffset.Should().Be(100);
        section.Cells[0].Bounds.Top.Should().Be(firstTop - 100);

        table.OnScroll(new ScrollEventArgs(10, 10, 0, 100000));
        table.ScrollOffset.Should().Be(table.ContentHeight - 200);

        table.OnScroll(new ScrollEventArgs(10, 10, 0, -100000));
        table.ScrollOffset.Should().Be(0);
    }

    [Fact]
    public void Intent_changes_header_styling_without_throwing()
    {
        var table = CreateTable(out _, out _, out _);

        foreach (var intent in new[] { SkiaTableIntent.Settings, SkiaTableIntent.Menu, SkiaTableIntent.Form, SkiaTableIntent.Data })
        {
            table.Intent = intent;
            var exception = Record.Exception(() => { using var _ = Render(table, 400, 600); });
            exception.Should().BeNull();
        }
    }

    #endregion

    #region Handler

    private static (TableView table, SkiaTableView platform) CreateMauiTable(TableRoot root, Action<TableView>? configure = null)
    {
        var ctx = HeadlessMaui.CreateContext();
        var table = new TableView { Root = root };
        configure?.Invoke(table);
        var handler = HeadlessMaui.AttachHandler<TableViewHandler>(table, ctx);
        var platform = (SkiaTableView)handler.PlatformView!;
        platform.Measure(new Size(400, 600));
        platform.Arrange(new Rect(0, 0, 400, 600));
        return (table, platform);
    }

    [Fact]
    public void Handler_mirrors_sections_and_cell_kinds()
    {
        var root = new TableRoot
        {
            new TableSection("Profile")
            {
                new TextCell { Text = "Name", Detail = "Ada" },
                new EntryCell { Label = "Email", Placeholder = "you@example.com" },
                new ImageCell { Text = "Avatar" },
            },
            new TableSection("Settings")
            {
                new SwitchCell { Text = "Dark mode", On = true },
                new ViewCell { View = new Label { Text = "Custom" } },
            },
        };

        var (_, platform) = CreateMauiTable(root, t => t.Intent = TableIntent.Settings);

        platform.Sections.Select(s => s.Title).Should().Equal("Profile", "Settings");
        platform.Intent.Should().Be(SkiaTableIntent.Settings);
        var rows = platform.Cells.ToList();
        rows.Select(r => r.Kind).Should().Equal(
            SkiaCellKind.Text, SkiaCellKind.Entry, SkiaCellKind.Image, SkiaCellKind.Switch, SkiaCellKind.View);
        rows[0].Text.Should().Be("Name");
        rows[0].Detail.Should().Be("Ada");
        rows[1].Text.Should().Be("Email");
        rows[1].Editor.Should().BeOfType<SkiaEntry>().Which.Placeholder.Should().Be("you@example.com");
        rows[3].Accessory.Should().BeOfType<SkiaSwitch>().Which.IsOn.Should().BeTrue();
        rows[4].Content.Should().BeOfType<SkiaLabel>().Which.Text.Should().Be("Custom");
    }

    [Fact]
    public void Handler_row_tap_raises_Cell_Tapped_and_runs_TextCell_Command()
    {
        var textCell = new TextCell { Text = "Go" };
        int tapped = 0;
        object? commandArg = null;
        textCell.Tapped += (s, e) => tapped++;
        textCell.Command = new Command<object>(p => commandArg = p);
        textCell.CommandParameter = "param";
        var (_, platform) = CreateMauiTable(new TableRoot { new TableSection("S") { textCell } });
        var row = platform.Cells.Single();

        float y = (float)row.Bounds.Top + 10;
        platform.OnPointerPressed(new PointerEventArgs(50, y, PointerButton.Left));
        platform.OnPointerReleased(new PointerEventArgs(50, y, PointerButton.Left));

        tapped.Should().Be(1);
        commandArg.Should().Be("param");
    }

    [Fact]
    public void Handler_SwitchCell_toggles_both_ways()
    {
        var switchCell = new SwitchCell { Text = "Wifi" };
        int changed = 0;
        switchCell.OnChanged += (s, e) => changed++;
        var (_, platform) = CreateMauiTable(new TableRoot { new TableSection("S") { switchCell } });
        var row = platform.Cells.Single();
        var sw = (SkiaSwitch)row.Accessory!;

        platform.TapCell(row);
        switchCell.On.Should().BeTrue();
        changed.Should().Be(1);

        switchCell.On = false;
        sw.IsOn.Should().BeFalse();
        changed.Should().Be(2);
    }

    [Fact]
    public void Handler_EntryCell_edits_flow_both_ways()
    {
        var entryCell = new EntryCell { Label = "Name", Text = "Ada" };
        var (_, platform) = CreateMauiTable(new TableRoot { new TableSection("S") { entryCell } });
        var entry = (SkiaEntry)platform.Cells.Single().Editor!;
        entry.Text.Should().Be("Ada");

        entry.Text = "Grace";
        entryCell.Text.Should().Be("Grace");

        entryCell.Text = "Linus";
        entry.Text.Should().Be("Linus");
    }

    [Fact]
    public void Handler_TextCell_property_changes_update_the_row()
    {
        var textCell = new TextCell { Text = "Old" };
        var (_, platform) = CreateMauiTable(new TableRoot { new TableSection("S") { textCell } });
        var row = platform.Cells.Single();

        textCell.Text = "New";
        textCell.Detail = "More";
        textCell.TextColor = Colors.Red;

        row.Text.Should().Be("New");
        row.Detail.Should().Be("More");
        row.TextColor.Should().Be(Colors.Red);
    }

    [Fact]
    public void Handler_model_changes_rebuild_the_sections()
    {
        var section = new TableSection("S") { new TextCell { Text = "A" } };
        var (table, platform) = CreateMauiTable(new TableRoot { section });
        platform.Cells.Should().HaveCount(1);

        section.Add(new TextCell { Text = "B" });
        platform.Cells.Select(c => c.Text).Should().Equal("A", "B");

        table.Root.Add(new TableSection("T") { new TextCell { Text = "C" } });
        platform.Sections.Select(s => s.Title).Should().Equal("S", "T");
        platform.Cells.Select(c => c.Text).Should().Equal("A", "B", "C");

        table.Root = new TableRoot { new TableSection("Only") { new TextCell { Text = "Z" } } };
        platform.Sections.Select(s => s.Title).Should().Equal("Only");
        platform.Cells.Select(c => c.Text).Should().Equal("Z");
    }

    [Fact]
    public void Handler_maps_RowHeight_HasUnevenRows_and_Intent()
    {
        var (table, platform) = CreateMauiTable(
            new TableRoot { new TableSection("S") { new TextCell { Text = "A" } } },
            t => { t.RowHeight = 60; t.HasUnevenRows = false; t.Intent = TableIntent.Menu; });

        platform.RowHeight.Should().Be(60);
        platform.HasUnevenRows.Should().BeFalse();
        platform.Intent.Should().Be(SkiaTableIntent.Menu);
        platform.Cells.Single().Bounds.Height.Should().Be(60);

        table.HasUnevenRows = true;
        table.Intent = TableIntent.Form;
        platform.HasUnevenRows.Should().BeTrue();
        platform.Intent.Should().Be(SkiaTableIntent.Form);
    }

    [Fact]
    public void Handler_disabled_cell_ignores_taps()
    {
        var textCell = new TextCell { Text = "Off", IsEnabled = false };
        int tapped = 0;
        textCell.Tapped += (s, e) => tapped++;
        var (_, platform) = CreateMauiTable(new TableRoot { new TableSection("S") { textCell } });
        var row = platform.Cells.Single();

        float y = (float)row.Bounds.Top + 10;
        platform.OnPointerPressed(new PointerEventArgs(50, y, PointerButton.Left));
        platform.OnPointerReleased(new PointerEventArgs(50, y, PointerButton.Left));

        row.IsEnabled.Should().BeFalse();
        tapped.Should().Be(0);
    }

    #endregion
}
