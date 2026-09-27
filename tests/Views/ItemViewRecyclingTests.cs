// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

/// <summary>
/// CollectionView item views are recycled: scrolling a long list end to end
/// keeps a bounded number of views instead of one per item, and rows scrolled
/// back into view are created again.
/// </summary>
public class ItemViewRecyclingTests
{
    private static (SkiaCollectionView View, List<int> Created) List(int count)
    {
        var created = new List<int>();
        var view = new SkiaCollectionView { ItemHeight = 40 };
        view.ItemViewCreator = item =>
        {
            created.Add((int)item);
            return new SkiaLabel { Text = item.ToString() };
        };
        view.ItemsSource = Enumerable.Range(0, count).ToList();
        view.Measure(new Size(300, 400));
        view.Arrange(new Rect(0, 0, 300, 400));
        return (view, created);
    }

    private static void Draw(SkiaView view)
    {
        using var bitmap = new SKBitmap(300, 400);
        using var canvas = new SKCanvas(bitmap);
        view.Draw(canvas);
    }

    [Fact]
    public void Scrolling_end_to_end_keeps_a_bounded_number_of_views()
    {
        var (view, created) = List(2000);

        for (int index = 0; index < 2000; index += 10)
        {
            view.ScrollToIndex(index, animate: false);
            Draw(view);
        }

        created.Count.Should().BeGreaterThan(1500, "every row was drawn once");
        int kept = Enumerable.Range(0, 2000).Count(i => view.GetItemView(i) != null);
        kept.Should().BeLessThan(200, "views far from the visible rows are released");
        view.GetItemView(1995).Should().NotBeNull("the rows on screen keep their views");
        view.GetItemView(0).Should().BeNull("the first rows scrolled far out of view");
    }

    [Fact]
    public void A_row_scrolled_back_into_view_is_created_again()
    {
        var (view, created) = List(2000);
        Draw(view);
        for (int index = 0; index < 2000; index += 10)
        {
            view.ScrollToIndex(index, animate: false);
            Draw(view);
        }
        created.Clear();

        view.ScrollToIndex(0, animate: false);
        Draw(view);

        created.Should().Contain(0);
        view.GetItemView(0).Should().NotBeNull();
    }

    [Fact]
    public void Short_lists_keep_every_view()
    {
        var (view, created) = List(50);
        for (int index = 0; index < 50; index += 5)
        {
            view.ScrollToIndex(index, animate: false);
            Draw(view);
        }

        Enumerable.Range(0, 50).Count(i => view.GetItemView(i) != null).Should().Be(created.Distinct().Count());
    }
}
