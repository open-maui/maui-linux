// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// Measured with a finite size, a Grid reports what its star rows' and columns' content
/// needs, as MAUI's GridLayoutManager does (MinimizeStarsForMeasurement); they fill the
/// space only when the grid is arranged.
/// </summary>
[Collection("LinuxApplication.Current")]
public class GridStarMeasureTests
{
    [Fact]
    public void A_star_row_measures_at_its_content_and_fills_when_arranged()
    {
        var label = new Label { Text = "Sep 28 Call 405.00", FontSize = 16 };
        var grid = new Grid { Children = { label } };
        using var host = new HeadlessMauiHost(new ContentPage { Content = new Grid { Children = { grid } } }, withEngine: true);
        host.Context.Render();

        var measured = ((IView)grid).Measure(500, 200);
        var line = ((IView)label).Measure(500, double.PositiveInfinity).Height;
        measured.Height.Should().BeApproximately(line, 1, "the star row needs one line, not the 200 offered");
        measured.Width.Should().BeLessThan(500, "the star column needs the text's width");

        var platform = (Microsoft.Maui.Platform.SkiaView)grid.Handler!.PlatformView!;
        platform.Bounds.Height.Should().BeGreaterThan(500, "arranged, the star row fills the page");
    }

    [Fact]
    public void A_star_row_is_capped_at_its_share()
    {
        var tall = new BoxView { HeightRequest = 300 };
        var grid = new Grid { RowDefinitions = new RowDefinitionCollection(new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Star)) };
        grid.Add(tall, 0, 0);
        using var host = new HeadlessMauiHost(new ContentPage { Content = new Grid { Children = { grid } } }, withEngine: true);
        host.Context.Render();

        // Two star rows share 200: the first needs 300 but gets at most its 100.
        ((IView)grid).Measure(500, 200).Height.Should().BeApproximately(100, 1);
    }
}
