// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// The built-in layouts (Grid, the stack layouts, FlexLayout, AbsoluteLayout)
/// are laid out by MAUI's own layout managers, as on every platform, rather
/// than by re-implementations that drifted from them.
/// </summary>
[Collection("LinuxApplication.Current")]
public class MauiLayoutManagerTests
{
    private static Rect BoundsOf(VisualElement view) => ((SkiaView)view.Handler!.PlatformView!).Bounds;

    [Fact]
    public void Built_in_layouts_get_the_cross_platform_layout_view()
    {
        var grid = new Grid();
        var vertical = new VerticalStackLayout();
        var horizontal = new HorizontalStackLayout();
        var legacy = new StackLayout();
        var flex = new FlexLayout();
        var absolute = new AbsoluteLayout();
        var root = new VerticalStackLayout { grid, vertical, horizontal, legacy, flex, absolute };
        using var host = new HeadlessMauiHost(new ContentPage { Content = root }, withEngine: true);
        host.Context.Render();

        foreach (var layout in new Layout[] { root, grid, vertical, horizontal, legacy, flex, absolute })
            layout.Handler!.PlatformView.Should().BeOfType<SkiaCrossPlatformLayout>(layout.GetType().Name);
    }

    [Fact]
    public void Child_frames_are_local_and_bounds_are_window_space()
    {
        var box = new BoxView { WidthRequest = 40, HeightRequest = 20, HorizontalOptions = LayoutOptions.Start };
        var inner = new VerticalStackLayout { Padding = new Thickness(10, 5, 0, 0), Children = { box } };
        var outer = new VerticalStackLayout { Padding = new Thickness(30, 20, 0, 0), Children = { inner } };
        using var host = new HeadlessMauiHost(new ContentPage { Content = outer }, withEngine: true);
        host.Context.Render();

        box.Frame.X.Should().BeApproximately(10, 0.5);
        box.Frame.Y.Should().BeApproximately(5, 0.5);
        var outerBounds = BoundsOf(outer);
        BoundsOf(box).X.Should().BeApproximately(outerBounds.X + 40, 0.5);
        BoundsOf(box).Y.Should().BeApproximately(outerBounds.Y + 25, 0.5);
    }

    [Fact]
    public void Changing_a_childs_Row_moves_it()
    {
        var box = new BoxView();
        var grid = new Grid
        {
            RowDefinitions = { new RowDefinition(new GridLength(50)), new RowDefinition(new GridLength(70)) },
            Children = { box },
        };
        using var host = new HeadlessMauiHost(new ContentPage { Content = grid }, withEngine: true);
        host.Context.Render();
        BoundsOf(box).Height.Should().BeApproximately(50, 0.5);

        Grid.SetRow(box, 1);
        host.Context.Render();

        (BoundsOf(box).Top - BoundsOf(grid).Top).Should().BeApproximately(50, 0.5);
        BoundsOf(box).Height.Should().BeApproximately(70, 0.5);
    }

    [Fact]
    public void A_right_to_left_grid_places_its_first_column_on_the_right()
    {
        var first = new BoxView();
        var second = new BoxView();
        var grid = new Grid
        {
            FlowDirection = FlowDirection.RightToLeft,
            WidthRequest = 300,
            HorizontalOptions = LayoutOptions.Start,
            ColumnDefinitions = { new ColumnDefinition(new GridLength(100)), new ColumnDefinition(GridLength.Star) },
        };
        grid.Add(first, 0, 0);
        grid.Add(second, 1, 0);
        using var host = new HeadlessMauiHost(new ContentPage { Content = grid }, withEngine: true);
        host.Context.Render();

        var gridLeft = BoundsOf(grid).Left;
        (BoundsOf(first).Left - gridLeft).Should().BeApproximately(200, 0.5);
        BoundsOf(first).Width.Should().BeApproximately(100, 0.5);
        (BoundsOf(second).Left - gridLeft).Should().BeApproximately(0, 0.5);
        // The MAUI frame stays the one MAUI computed, as on Android and iOS.
        first.Frame.X.Should().BeApproximately(0, 0.5);
    }

    [Fact]
    public void Collapsed_children_take_no_space_or_spacing()
    {
        var a = new BoxView { HeightRequest = 30 };
        var hidden = new BoxView { HeightRequest = 30, IsVisible = false };
        var b = new BoxView { HeightRequest = 30 };
        var stack = new VerticalStackLayout { Spacing = 10, Children = { a, hidden, b } };
        using var host = new HeadlessMauiHost(new ContentPage { Content = stack }, withEngine: true);
        host.Context.Render();

        (BoundsOf(b).Top - BoundsOf(a).Top).Should().BeApproximately(40, 0.5);
    }

    [Fact]
    public void A_fill_child_with_an_explicit_width_is_centred()
    {
        var box = new BoxView { WidthRequest = 100, HeightRequest = 20 };
        var stack = new VerticalStackLayout { WidthRequest = 300, HorizontalOptions = LayoutOptions.Start, Children = { box } };
        using var host = new HeadlessMauiHost(new ContentPage { Content = stack }, withEngine: true);
        host.Context.Render();

        (BoundsOf(box).Left - BoundsOf(stack).Left).Should().BeApproximately(100, 0.5);
        BoundsOf(box).Width.Should().BeApproximately(100, 0.5);
    }

    [Fact]
    public void A_legacy_StackLayout_follows_its_Orientation()
    {
        var a = new BoxView { WidthRequest = 40, HeightRequest = 20 };
        var b = new BoxView { WidthRequest = 40, HeightRequest = 20 };
        var stack = new StackLayout { Spacing = 6, Children = { a, b } };
        using var host = new HeadlessMauiHost(new ContentPage { Content = stack }, withEngine: true);
        host.Context.Render();
        (BoundsOf(b).Top - BoundsOf(a).Top).Should().BeApproximately(26, 0.5);

        stack.Orientation = StackOrientation.Horizontal;
        host.Context.Render();

        (BoundsOf(b).Left - BoundsOf(a).Left).Should().BeApproximately(46, 0.5);
        BoundsOf(b).Top.Should().BeApproximately(BoundsOf(a).Top, 0.5);
    }

    private sealed class PortableView : View
    {
    }

    /// <summary>A library-style handler on the portable ViewHandler: its PlatformArrange does nothing.</summary>
    private sealed class PortableViewHandler : Microsoft.Maui.Handlers.ViewHandler<IView, SkiaBoxView>
    {
        public PortableViewHandler() : base(Microsoft.Maui.Handlers.ViewHandler.ViewMapper) { }

        protected override SkiaBoxView CreatePlatformView() => new();
    }

    [Fact]
    public void A_child_whose_handler_does_not_place_it_is_placed_in_its_cell()
    {
        var portable = new PortableView();
        var grid = new Grid
        {
            WidthRequest = 300,
            HorizontalOptions = LayoutOptions.Start,
            RowDefinitions = { new RowDefinition(new GridLength(40)), new RowDefinition(new GridLength(60)) },
        };
        grid.Add(new BoxView(), 0, 0);
        grid.Add(portable, 0, 1);
        using var host = new HeadlessMauiHost(new ContentPage { Content = grid }, withEngine: true,
            configure: b => b.ConfigureMauiHandlers(h => h.AddHandler<PortableView, PortableViewHandler>()));
        host.Context.Render();

        var bounds = BoundsOf(portable);
        (bounds.Top - BoundsOf(grid).Top).Should().BeApproximately(40, 0.5);
        bounds.Width.Should().BeApproximately(300, 0.5);
        bounds.Height.Should().BeApproximately(60, 0.5);
    }

    [Fact]
    public void A_star_row_grid_in_a_vertical_scroll_view_sizes_to_its_content()
    {
        // Measured against infinite height, star rows take their content, as GridLayoutManager
        // does: content taller than the viewport is laid out at its own height, not the viewport's.
        var box = new BoxView { HeightRequest = 900 };
        var grid = new Grid { RowDefinitions = { new RowDefinition(GridLength.Star) }, Children = { box } };
        using var host = new HeadlessMauiHost(new ContentPage { Content = new ScrollView { Content = grid } }, withEngine: true);
        host.Context.Render();

        BoundsOf(grid).Height.Should().BeApproximately(900, 0.5);
        BoundsOf(box).Height.Should().BeApproximately(900, 0.5);
    }
}
