// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// Columns added to (or cleared from) a grid's existing definition collection
/// after the grid is shown take effect. MAUI only invalidates the grid's measure
/// for that; SfPopup's footer builds its button columns this way, and its
/// buttons were laid out in a single column, one past the popup's edge.
/// </summary>
[Collection("LinuxApplication.Current")]
public class GridDefinitionsChangeTests
{
    [Fact]
    public void Columns_added_after_the_grid_is_shown_are_used()
    {
        var grid = new Grid { WidthRequest = 300, HorizontalOptions = LayoutOptions.Start, ColumnDefinitions = { new ColumnDefinition(GridLength.Star) } };
        using var host = new HeadlessMauiHost(new ContentPage { Content = grid }, withEngine: true);
        host.Context.Render();

        grid.ColumnDefinitions.Clear();
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(60)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(8)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(70)));
        var decline = new BoxView();
        var accept = new BoxView();
        grid.Add(decline);
        Grid.SetColumn(decline, 1);
        grid.Add(accept);
        Grid.SetColumn(accept, 3);
        host.Context.Render();

        var gridLeft = ((Microsoft.Maui.Platform.SkiaView)grid.Handler!.PlatformView!).Bounds.Left;
        var d = ((Microsoft.Maui.Platform.SkiaView)decline.Handler!.PlatformView!).Bounds;
        var a = ((Microsoft.Maui.Platform.SkiaView)accept.Handler!.PlatformView!).Bounds;
        (d.Left - gridLeft).Should().BeApproximately(162, 0.5);
        d.Width.Should().BeApproximately(60, 0.5);
        (a.Left - gridLeft).Should().BeApproximately(230, 0.5);
        a.Width.Should().BeApproximately(70, 0.5);
    }
}
