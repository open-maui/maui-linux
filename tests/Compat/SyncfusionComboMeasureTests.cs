// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Syncfusion;
using Syncfusion.Maui.Inputs;
using Xunit;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// SfComboBox and SfAutocomplete get the size they get on Windows when nothing sizes them. The
/// Windows build measures an infinite width or height as 0 with single selection
/// (<c>DropDownListBase.MeasureContent</c>), and its <c>SfDropdownEntry</c> sets
/// <c>MinimumHeightRequest = 32</c>, which WinUI applies as the platform view's MinHeight. So in a
/// vertical stack the box fills the width and is 32 high, while a horizontal stack, an Auto grid
/// column or anything else that offers infinite width gives it no width. When the box's parent sits
/// in a FlexLayout, its measure is the constraint clamped to that parent's current size
/// (<c>UpdateBoundsSize</c>). The layouts themselves are MAUI's cross-platform managers, which
/// OpenMaui shares with Windows.
/// </summary>
[Collection(CompatHost.Collection)]
public sealed class SyncfusionComboMeasureTests
{
    private static SfComboBox Combo() => new() { ItemsSource = new[] { "Alpha", "Bravo" } };

    /// <summary>The page is built after UseLinuxSyncfusion, as in an app (the controls' constructors are patched there).</summary>
    private static CompatHost Host(Func<View> content)
    {
        var host = new CompatHost(_ => new ContentPage { Content = content() }, b => b.UseLinuxSyncfusion(), 600, 600);
        Pump(host);
        return host;
    }

    private static void Pump(CompatHost host)
    {
        for (int i = 0; i < 4; i++)
            host.Render();
    }

    [Fact]
    public void In_a_vertical_stack_it_fills_the_width_and_is_32_high()
    {
        SfComboBox combo = null!;
        using var host = Host(() => new VerticalStackLayout { (combo = Combo()) });
        combo.MinimumHeightRequest.Should().Be(32, "the Windows SfDropdownEntry constructor sets it to its button size");
        combo.Bounds.Width.Should().Be(600);
        combo.Bounds.Height.Should().Be(32, "the infinite height is measured as 0, and WinUI raises it to MinHeight");
    }

    [Fact]
    public void In_a_horizontal_stack_it_gets_no_width()
    {
        SfComboBox combo = null!;
        SfAutocomplete autocomplete = null!;
        using var host = Host(() => new VerticalStackLayout
        {
            new HorizontalStackLayout { (combo = Combo()) },
            new HorizontalStackLayout { (autocomplete = new SfAutocomplete { ItemsSource = new[] { "Alpha" } }) },
        });
        combo.Bounds.Size.Should().Be(new Size(0, 32), "the infinite width is measured as 0, so the stack gives it none, as on Windows");
        autocomplete.Bounds.Size.Should().Be(new Size(0, 32), "SfAutocomplete shares the measure (DropDownListBase)");
    }

    [Fact]
    public void A_width_request_keeps_its_width_in_a_horizontal_stack()
    {
        SfComboBox sized = null!;
        using var host = Host(() => new VerticalStackLayout
        {
            new HorizontalStackLayout { (sized = new SfComboBox { WidthRequest = 200, ItemsSource = new[] { "Alpha" } }) },
        });
        sized.Bounds.Size.Should().Be(new Size(200, 32));
    }

    [Fact]
    public void In_an_Auto_grid_column_it_gets_no_width()
    {
        SfComboBox combo = null!;
        using var host = Host(() =>
        {
            var grid = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
                RowDefinitions = { new RowDefinition(GridLength.Auto) },
            };
            grid.Add(combo = Combo(), 0, 0);
            return new VerticalStackLayout { grid };
        });
        combo.Bounds.Size.Should().Be(new Size(0, 32));
    }

    [Fact]
    public void In_a_FlexLayout_it_takes_the_line_width_and_its_minimum_height()
    {
        SfComboBox combo = null!;
        using var host = Host(() => new VerticalStackLayout { new FlexLayout { (combo = Combo()) } });
        combo.Bounds.Size.Should().Be(new Size(600, 32), "the flex root's width is a finite constraint, its unset height an infinite one");
    }

    [Fact]
    public void Inside_a_FlexLayout_item_it_is_clamped_to_that_items_current_size()
    {
        SfComboBox combo = null!;
        FlexLayout flex = null!;
        using var host = Host(() =>
        {
            flex = new FlexLayout { WidthRequest = 300, HorizontalOptions = LayoutOptions.Start };
            flex.Children.Add(new Border { StrokeThickness = 0, Padding = 0, Content = combo = Combo() });
            return new VerticalStackLayout { flex };
        });
        combo.Bounds.Size.Should().Be(new Size(300, 32));

        flex.WidthRequest = 500;
        Pump(host);
        combo.Bounds.Size.Should().Be(new Size(300, 32),
            "UpdateBoundsSize clamps the wider constraint to the Border's current width, so the item does not grow, as on Windows");
    }
}
