// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

#pragma warning disable CS0618 // ListView is deprecated but still supported on Linux

/// <summary>
/// MAUI ListView through the Linux ListViewHandler: rows from ItemsSource
/// (default cells, TextCell and ViewCell templates), grouping with header
/// rows, Header/Footer, and ItemTapped / ItemSelected raised with the item.
/// </summary>
[Collection(HeadlessMaui.Collection)]
public class ListViewTests
{
    private sealed class Person
    {
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public override string ToString() => Name;
    }

    private sealed class Team : List<Person>
    {
        public Team(string name, params Person[] members) : base(members) { Name = name; }
        public string Name { get; }
    }

    private static readonly Person[] s_people =
    {
        new() { Name = "Ada", Role = "Math" },
        new() { Name = "Grace", Role = "Navy" },
        new() { Name = "Linus", Role = "Kernel" },
    };

    private static (ListView list, SkiaCollectionView platform, ListViewHandler handler) CreateList(Action<ListView>? configure = null)
    {
        var ctx = HeadlessMaui.CreateContext();
        var list = new ListView();
        configure?.Invoke(list);
        var handler = HeadlessMaui.AttachHandler<ListViewHandler>(list, ctx);
        var platform = (SkiaCollectionView)handler.PlatformView!;
        platform.Measure(new Size(400, 600));
        platform.Arrange(new Rect(0, 0, 400, 600));
        Draw(platform);
        return (list, platform, handler);
    }

    /// <summary>Draws once so the platform creates and measures the row views.</summary>
    private static void Draw(SkiaCollectionView platform)
    {
        using var surface = SKSurface.Create(new SKImageInfo(400, 600));
        platform.Draw(surface.Canvas);
    }

    private static SkiaCellView RowView(SkiaCollectionView platform, int index)
        => platform.GetItemView(index).Should().BeOfType<SkiaCellView>().Subject;

    /// <summary>Taps the row at <paramref name="index"/> the way the pointer path does.</summary>
    private static void TapRow(SkiaCollectionView platform, int index)
    {
        float y = (float)platform.Bounds.Top + index * platform.ItemHeight + platform.ItemHeight / 2;
        platform.OnPointerPressed(new PointerEventArgs(50, y, PointerButton.Left));
        platform.OnPointerReleased(new PointerEventArgs(50, y, PointerButton.Left));
    }

    [Fact]
    public void ItemsSource_renders_a_default_text_row_per_item()
    {
        var (_, platform, handler) = CreateList(l => l.ItemsSource = s_people);

        handler.Rows.Should().HaveCount(3);
        handler.Rows.Select(r => r.Item).Should().Equal(s_people);
        for (int i = 0; i < 3; i++)
        {
            var row = RowView(platform, i);
            row.Kind.Should().Be(SkiaCellKind.Text);
            row.Text.Should().Be(s_people[i].Name, "the default cell shows the item's string form");
        }
    }

    [Fact]
    public void ItemTemplate_with_TextCell_binds_text_and_detail()
    {
        var (_, platform, _) = CreateList(l =>
        {
            l.ItemsSource = s_people;
            l.ItemTemplate = new DataTemplate(() =>
            {
                var cell = new TextCell();
                cell.SetBinding(TextCell.TextProperty, nameof(Person.Name));
                cell.SetBinding(TextCell.DetailProperty, nameof(Person.Role));
                return cell;
            });
        });

        var row = RowView(platform, 1);
        row.Text.Should().Be("Grace");
        row.Detail.Should().Be("Navy");
    }

    [Fact]
    public void ItemTemplate_with_ViewCell_hosts_the_cells_view()
    {
        var (_, platform, _) = CreateList(l =>
        {
            l.ItemsSource = s_people;
            l.ItemTemplate = new DataTemplate(() =>
            {
                var label = new Label();
                label.SetBinding(Label.TextProperty, nameof(Person.Role));
                return new ViewCell { View = label };
            });
        });

        var row = RowView(platform, 2);
        row.Kind.Should().Be(SkiaCellKind.View);
        row.Content.Should().BeOfType<SkiaLabel>().Which.Text.Should().Be("Kernel");
    }

    [Fact]
    public void Grouping_renders_a_header_row_per_group()
    {
        var teams = new List<Team>
        {
            new("Pioneers", s_people[0], s_people[1]),
            new("Kernel", s_people[2]),
        };
        var (_, platform, handler) = CreateList(l =>
        {
            l.IsGroupingEnabled = true;
            l.GroupDisplayBinding = new Binding(nameof(Team.Name));
            l.ItemsSource = teams;
        });

        handler.Rows.Select(r => r.IsGroupHeader).Should().Equal(true, false, false, true, false);
        handler.Rows.Where(r => !r.IsGroupHeader).Select(r => r.Item).Should().Equal(s_people[0], s_people[1], s_people[2]);

        var header = RowView(platform, 0);
        header.Kind.Should().Be(SkiaCellKind.GroupHeader);
        header.Text.Should().Be("Pioneers");
        RowView(platform, 3).Text.Should().Be("Kernel");
        RowView(platform, 1).Text.Should().Be("Ada");
    }

    [Fact]
    public void Header_and_Footer_reach_the_platform_list()
    {
        var (list, platform, _) = CreateList(l =>
        {
            l.ItemsSource = s_people;
            l.Header = "Top";
            l.Footer = new Label { Text = "Bottom" };
        });

        platform.Header.Should().Be("Top");
        platform.Footer.Should().Be("Bottom");
        platform.HeaderHeight.Should().BeGreaterThan(0);

        list.Header = null;
        platform.Header.Should().BeNull();
    }

    [Fact]
    public void Tapping_a_row_raises_ItemSelected_then_ItemTapped_with_the_item()
    {
        var events = new List<string>();
        object? tapped = null;
        object? selected = null;
        var (list, platform, _) = CreateList(l => l.ItemsSource = s_people);
        list.ItemSelected += (s, e) => { events.Add("selected"); selected = e.SelectedItem; };
        list.ItemTapped += (s, e) => { events.Add("tapped"); tapped = e.Item; };

        TapRow(platform, 1);

        events.Should().Equal("selected", "tapped");
        tapped.Should().BeSameAs(s_people[1]);
        selected.Should().BeSameAs(s_people[1]);
        list.SelectedItem.Should().BeSameAs(s_people[1]);
        platform.SelectedItem.Should().BeOfType<ListViewHandler.Row>().Which.Item.Should().BeSameAs(s_people[1]);
    }

    [Fact]
    public void Tapping_a_grouped_row_reports_the_group()
    {
        var teams = new List<Team> { new("Pioneers", s_people[0], s_people[1]), new("Kernel", s_people[2]) };
        ItemTappedEventArgs? args = null;
        var (list, platform, _) = CreateList(l =>
        {
            l.IsGroupingEnabled = true;
            l.GroupDisplayBinding = new Binding(nameof(Team.Name));
            l.ItemsSource = teams;
        });
        list.ItemTapped += (s, e) => args = e;

        TapRow(platform, 4); // header, Ada, Grace, header, Linus

        args.Should().NotBeNull();
        args!.Item.Should().BeSameAs(s_people[2]);
        args.Group.Should().BeSameAs(teams[1]);
        // MAUI reports the global row index for grouped lists (headers count).
        args.ItemIndex.Should().Be(4);
    }

    [Fact]
    public void Tapping_a_group_header_selects_nothing()
    {
        var teams = new List<Team> { new("Pioneers", s_people[0], s_people[1]) };
        int tapped = 0;
        var (list, platform, _) = CreateList(l =>
        {
            l.IsGroupingEnabled = true;
            l.GroupDisplayBinding = new Binding(nameof(Team.Name));
            l.ItemsSource = teams;
        });
        list.ItemTapped += (s, e) => tapped++;

        TapRow(platform, 0);

        tapped.Should().Be(0);
        list.SelectedItem.Should().BeNull();
        platform.SelectedItem.Should().BeNull();
    }

    [Fact]
    public void SelectionMode_None_still_raises_ItemTapped_but_not_ItemSelected()
    {
        int selected = 0, tapped = 0;
        var (list, platform, _) = CreateList(l => { l.ItemsSource = s_people; l.SelectionMode = ListViewSelectionMode.None; });
        list.ItemSelected += (s, e) => selected++;
        list.ItemTapped += (s, e) => tapped++;

        TapRow(platform, 0);

        tapped.Should().Be(1);
        selected.Should().Be(0);
        list.SelectedItem.Should().BeNull();
    }

    [Fact]
    public void Setting_SelectedItem_highlights_the_matching_row()
    {
        var (list, platform, _) = CreateList(l => l.ItemsSource = s_people);

        list.SelectedItem = s_people[2];

        platform.SelectedItem.Should().BeOfType<ListViewHandler.Row>().Which.Item.Should().BeSameAs(s_people[2]);
    }

    [Fact]
    public void Observable_ItemsSource_changes_update_the_rows()
    {
        var items = new ObservableCollection<string> { "one", "two" };
        var (_, platform, handler) = CreateList(l => l.ItemsSource = items);
        handler.Rows.Should().HaveCount(2);

        items.Add("three");
        Draw(platform);

        handler.Rows.Select(r => r.Item).Should().Equal("one", "two", "three");
        RowView(platform, 2).Text.Should().Be("three");
    }

    [Fact]
    public void RowHeight_sets_the_platform_row_height()
    {
        var (_, platform, _) = CreateList(l => { l.ItemsSource = s_people; l.RowHeight = 72; });

        platform.ItemHeight.Should().Be(72);
        RowView(platform, 0).Bounds.Height.Should().Be(72);
    }

    [Fact]
    public void SwitchCell_rows_toggle_on_tap()
    {
        var (_, platform, handler) = CreateList(l =>
        {
            l.ItemsSource = new[] { "a" };
            l.ItemTemplate = new DataTemplate(() => new SwitchCell { Text = "Flag" });
        });
        var cell = (SwitchCell)handler.Rows[0].Cell;

        TapRow(platform, 0);

        cell.On.Should().BeTrue();
        RowView(platform, 0).Accessory.Should().BeOfType<SkiaSwitch>().Which.IsOn.Should().BeTrue();
    }
}
