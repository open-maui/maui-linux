// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// An Auto column is as wide as its child plus the child's margin, so a margin
/// moves the child without shrinking it (MAToolbar's icon-only buttons carry
/// their spacing as a trailing margin in its main-panel grid).
/// </summary>
[Collection("LinuxApplication.Current")]
public class GridAutoMarginTests
{
    [Fact]
    public void A_margin_does_not_shrink_a_child_in_an_Auto_column()
    {
        var plain = new Button { Text = "X" };
        var spaced = new Button { Text = "X", Margin = new Thickness(2, 0, 6, 0) };
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) },
            HorizontalOptions = LayoutOptions.Start,
        };
        grid.Add(plain, 0, 0);
        grid.Add(spaced, 1, 0);
        using var host = new HeadlessMauiHost(new ContentPage { Content = grid }, withEngine: true);
        host.Context.Render();

        spaced.Width.Should().BeApproximately(plain.Width, 0.5);
        spaced.X.Should().BeApproximately(plain.Width + 2, 0.5);
    }

    [Fact]
    public void A_margin_is_around_a_stack_child_not_taken_out_of_it()
    {
        // MAToolbar's menu separator: a 1 px BoxView with a (8, 4) margin.
        var separator = new BoxView { HeightRequest = 1, Margin = new Thickness(8, 4) };
        var above = new Label { Text = "About" };
        var below = new Label { Text = "Report Issue" };
        var menu = new VerticalStackLayout { WidthRequest = 200, HorizontalOptions = LayoutOptions.Start, Children = { above, separator, below } };
        using var host = new HeadlessMauiHost(new ContentPage { Content = menu }, withEngine: true);
        host.Context.Render();

        var sep = ((Microsoft.Maui.Platform.SkiaView)separator.Handler!.PlatformView!).Bounds;
        var a = ((Microsoft.Maui.Platform.SkiaView)above.Handler!.PlatformView!).Bounds;
        var b = ((Microsoft.Maui.Platform.SkiaView)below.Handler!.PlatformView!).Bounds;
        sep.Height.Should().BeApproximately(1, 0.1);
        sep.Top.Should().BeApproximately(a.Bottom + 4, 0.5);
        b.Top.Should().BeApproximately(sep.Bottom + 4, 0.5);
        sep.Left.Should().BeApproximately(a.Left + 8, 0.5);
    }

    [Fact]
    public void A_ContentViews_content_keeps_its_margin()
    {
        // MAFlyoutPanel's rail header: the stack's top margin clears the rail toggle.
        var avatar = new BoxView { HeightRequest = 40, WidthRequest = 40 };
        var rail = new VerticalStackLayout { Margin = new Thickness(0, 28, 0, 0), Padding = new Thickness(0, 12), Children = { avatar } };
        var header = new ContentView { Content = rail, VerticalOptions = LayoutOptions.Start };
        using var host = new HeadlessMauiHost(new ContentPage { Content = new Grid { Children = { header } } }, withEngine: true);
        host.Context.Render();

        ((Microsoft.Maui.Platform.SkiaView)avatar.Handler!.PlatformView!).Bounds.Top.Should().BeApproximately(40, 0.5);
    }

    [Fact]
    public void A_child_spanning_columns_centres_across_the_spacing_too()
    {
        // MAFlyoutPanel's rail settings chip: spans an Auto and a Star column, centred.
        var chip = new BoxView { WidthRequest = 40, HeightRequest = 40, HorizontalOptions = LayoutOptions.Center };
        var row = new Grid
        {
            WidthRequest = 64, HorizontalOptions = LayoutOptions.Start,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 12,
        };
        row.Add(chip, 0, 0);
        Grid.SetColumnSpan(chip, 2);
        using var host = new HeadlessMauiHost(new ContentPage { Content = row }, withEngine: true);
        host.Context.Render();

        var b = ((Microsoft.Maui.Platform.SkiaView)chip.Handler!.PlatformView!).Bounds;
        b.Center.X.Should().BeApproximately(32, 0.5);
    }

    [Fact]
    public void A_span_changed_after_the_child_was_added_applies()
    {
        var chip = new BoxView { WidthRequest = 40, HeightRequest = 40, HorizontalOptions = LayoutOptions.Center };
        var row = new Grid
        {
            WidthRequest = 64, HorizontalOptions = LayoutOptions.Start,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 14,
        };
        row.Add(chip, 0, 0);
        using var host = new HeadlessMauiHost(new ContentPage { Content = row }, withEngine: true);
        host.Context.Render();

        Grid.SetColumnSpan(chip, 2);
        host.Context.Render();

        ((Microsoft.Maui.Platform.SkiaView)chip.Handler!.PlatformView!).Bounds.Center.X.Should().BeApproximately(32, 0.5);
    }
}
