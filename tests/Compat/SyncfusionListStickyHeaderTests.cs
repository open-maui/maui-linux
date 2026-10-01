// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Syncfusion;
using Syncfusion.Maui.DataSource;
using Syncfusion.Maui.ListView;
using Xunit;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// An SfListView's sticky group header stays at the top of the list as it scrolls, both ways.
/// Its platform-neutral build never laid the rows out again after a scroll (the call that does
/// it is an empty iOS-only stub there), so scrolled back up the header kept an earlier place,
/// a header's height down, with a gap above it (CiteLynq's Economy list).
/// </summary>
[Collection("LinuxApplication.Current")]
public sealed class SyncfusionListStickyHeaderTests
{
    public sealed class Item
    {
        public string Group { get; init; } = "";
        public string Name { get; init; } = "";
    }

    [Fact]
    public async Task The_sticky_header_stays_at_the_top_scrolling_down_and_back_up()
    {
        var items = Enumerable.Range(0, 40).Select(i => new Item { Group = $"G{i / 10}", Name = $"Item {i}" }).ToList();
        var list = new SfListView
        {
            ItemsSource = items,
            AutoFitMode = AutoFitMode.Height,
            SelectionMode = Syncfusion.Maui.ListView.SelectionMode.None,
            Margin = new Thickness(8, 4),
            IsStickyGroupHeader = true,
            ItemTemplate = new DataTemplate(() =>
            {
                var label = new Label { HeightRequest = 80, BackgroundColor = Colors.Blue };
                label.SetBinding(Label.TextProperty, nameof(Item.Name));
                return label;
            }),
            GroupHeaderTemplate = new DataTemplate(() => new Grid { HeightRequest = 40, BackgroundColor = Colors.Red }),
        };
        list.DataSource.GroupDescriptors.Add(new GroupDescriptor { PropertyName = nameof(Item.Group), KeySelector = o => ((Item)o).Group });
        var page = new Grid { RowDefinitions = new RowDefinitionCollection(new RowDefinition(100), new RowDefinition(GridLength.Star)) };
        page.Add(list, 0, 1);
        using var host = new CompatHost(new ContentPage { BackgroundColor = Colors.Black, Content = page }, b => b.UseLinuxSyncfusion(), 600, 500);

        async Task Settle()
        {
            for (int i = 0; i < 15; i++)
            {
                host.Render();
                await Task.Delay(30);
            }
        }

        // The list's top is 104 (the 100 row and the 4 margin); the header is red.
        void HeaderAtTop(string when)
        {
            var top = host.DisplayWindow.PixelAt(50, 106);
            (top.R > 200 && top.B < 80).Should().BeTrue($"the header is at the list's top {when}, got {top}");
        }

        await Settle();
        HeaderAtTop("at first");
        for (int i = 0; i < 6; i++)
        {
            host.DisplayWindow.RaiseScroll(50, 300, 1);
            await Settle();
            HeaderAtTop($"scrolled down {i + 1}");
        }
        for (int i = 0; i < 8; i++)
        {
            host.DisplayWindow.RaiseScroll(50, 300, -1);
            await Settle();
            HeaderAtTop($"scrolled back up {i + 1}");
        }
    }
}
