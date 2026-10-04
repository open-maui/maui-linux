using Microsoft.Maui.Layouts;
using static ParityHarness.Ui;

namespace ParityHarness.Gallery;

public static class LayoutPages
{
    public static Page GridStars()
    {
        var grid = new Grid
        {
            AutomationId = "grid",
            Padding = new Thickness(12, 8, 16, 20),
            ColumnSpacing = 10,
            RowSpacing = 6,
            ColumnDefinitions = Columns(Px(100), Star(), Star(2)),
            RowDefinitions = Rows(Px(50), Star(), Px(80), Star(3)),
        };
        grid.Add(Box("r0c0").At(0, 0));
        grid.Add(Box("r0c1").At(0, 1));
        grid.Add(Box("r0c2").At(0, 2));
        grid.Add(Box("r1c0").At(1, 0));
        grid.Add(Box("span_r1c1_2x2").At(1, 1, rowSpan: 2, columnSpan: 2));
        grid.Add(Box("r2c0").At(2, 0));
        grid.Add(Box("r3_allcols").At(3, 0, columnSpan: 3));
        // Overlapping child in the same cell, inset by margin.
        grid.Add(Box("r3_margin", color: Colors.Black).At(3, 1));
        ((View)grid.Children[^1]).Margin = new Thickness(8, 12, 20, 4);
        return Page("GridStars", grid);
    }

    public static Page GridAutos()
    {
        var grid = new Grid
        {
            AutomationId = "grid",
            Padding = 10,
            ColumnSpacing = 4,
            RowSpacing = 4,
            ColumnDefinitions = Columns(Auto, Auto, Star(), Px(150)),
            RowDefinitions = Rows(Auto, Auto, Star(), Auto),
        };
        // Auto column 0 sized by its widest child (60 + margins 5+5).
        grid.Add(Box("auto_c0_a", 60, 30).At(0, 0).Margin5());
        grid.Add(Box("auto_c0_b", 40, 50).At(1, 0));
        // Auto column 1 sized by a spanning-free child; the second child is narrower and Fill.
        grid.Add(Box("auto_c1_wide", 90, 20).At(0, 1));
        grid.Add(Box("auto_c1_fill", -1, 20).At(1, 1));

        // Alignment inside the star cell (row 2, column 2): each child its own explicit size.
        grid.Add(Box("star_start", 40, 40).At(2, 2).Align(LayoutOptions.Start, LayoutOptions.Start));
        grid.Add(Box("star_center", 40, 40).At(2, 2).Align(LayoutOptions.Center, LayoutOptions.Center));
        grid.Add(Box("star_end", 40, 40).At(2, 2).Align(LayoutOptions.End, LayoutOptions.End));
        // Fill with an explicit size: MAUI keeps the requested size and centres it.
        grid.Add(Box("star_fill_explicit", 70, 30).At(2, 2).Align(LayoutOptions.Fill, LayoutOptions.Fill));
        // Fill with no size: the whole cell minus margin.
        var fill = Box("fixedcol_fill").At(2, 3);
        fill.Margin = new Thickness(10, 20, 30, 40);
        grid.Add(fill);

        // Auto row 3 spanning all columns, height from the tallest child.
        grid.Add(Box("autorow_a", 100, 24).At(3, 0, columnSpan: 2).Align(LayoutOptions.Start, LayoutOptions.Start));
        grid.Add(Box("autorow_b", 50, 36).At(3, 2).Align(LayoutOptions.End, LayoutOptions.Center));
        grid.Add(Box("autorow_c", -1, -1).At(3, 3));

        // Unspecified row/column for a child: defaults to (0,0) alongside others.
        grid.Add(Box("default_cell", 10, 10, Colors.Black).Align(LayoutOptions.End, LayoutOptions.End));
        return Page("GridAutos", grid);
    }

    private static T Margin5<T>(this T v) where T : View
    {
        v.Margin = 5;
        return v;
    }

    public static Page Stacks()
    {
        var vertical = new VerticalStackLayout
        {
            AutomationId = "vstack",
            Spacing = 8,
            Padding = new Thickness(6, 10),
            BackgroundColor = Color.FromArgb("#EEEEEE"),
            Children =
            {
                Box("v_fill", -1, 30),
                Box("v_start", 80, 30).Align(LayoutOptions.Start, LayoutOptions.Fill),
                Box("v_center", 80, 30).Align(LayoutOptions.Center, LayoutOptions.Fill),
                Box("v_end", 80, 30).Align(LayoutOptions.End, LayoutOptions.Fill),
                Box("v_fill_explicit", 80, 30).Align(LayoutOptions.Fill, LayoutOptions.Fill),
                Box("v_margin", -1, 30),
            },
        };
        ((View)vertical.Children[^1]).Margin = new Thickness(20, 5, 40, 5);

        var horizontal = new HorizontalStackLayout
        {
            AutomationId = "hstack",
            Spacing = 12,
            HeightRequest = 100,
            BackgroundColor = Color.FromArgb("#DDDDDD"),
            Children =
            {
                Box("h_fill", 40, -1),
                Box("h_start", 40, 30).Align(LayoutOptions.Fill, LayoutOptions.Start),
                Box("h_center", 40, 30).Align(LayoutOptions.Fill, LayoutOptions.Center),
                Box("h_end", 40, 30).Align(LayoutOptions.Fill, LayoutOptions.End),
                Box("h_margin", 40, -1),
            },
        };
        ((View)horizontal.Children[^1]).Margin = new Thickness(5, 15, 5, 15);

        // Legacy StackLayout, horizontal, aligned to the end of its parent.
        var legacy = new StackLayout
        {
            AutomationId = "legacy_hstack",
            Orientation = StackOrientation.Horizontal,
            Spacing = 3,
            HorizontalOptions = LayoutOptions.End,
            Children = { Box("l_a", 30, 30), Box("l_b", 50, 20), Box("l_c", 20, 40) },
        };

        // A horizontal stack centred in its parent, auto-sized from its children.
        var centred = new HorizontalStackLayout
        {
            AutomationId = "centred_hstack",
            Spacing = 0,
            HorizontalOptions = LayoutOptions.Center,
            Children = { Box("c_a", 60, 20), Box("c_b", 60, 20) },
        };

        var root = new VerticalStackLayout
        {
            AutomationId = "root",
            Spacing = 10,
            Padding = 10,
            Children = { vertical, horizontal, legacy, centred },
        };
        return Page("Stacks", root);
    }

    public static Page Flex()
    {
        var wrap = new FlexLayout
        {
            AutomationId = "flex_wrap",
            Direction = FlexDirection.Row,
            Wrap = FlexWrap.Wrap,
            JustifyContent = FlexJustify.SpaceBetween,
            AlignItems = FlexAlignItems.Center,
            AlignContent = FlexAlignContent.Start,
            HeightRequest = 220,
            BackgroundColor = Color.FromArgb("#EEEEEE"),
        };
        for (int i = 0; i < 9; i++)
            wrap.Children.Add(Box($"wrap_{i}", 150, 30 + (i % 3) * 15));

        var column = new FlexLayout
        {
            AutomationId = "flex_column",
            Direction = FlexDirection.Column,
            JustifyContent = FlexJustify.Start,
            AlignItems = FlexAlignItems.Stretch,
            HeightRequest = 200,
            WidthRequest = 300,
            HorizontalOptions = LayoutOptions.Start,
            BackgroundColor = Color.FromArgb("#DDDDDD"),
        };
        var g1 = Box("col_grow1", -1, 20);
        FlexLayout.SetGrow(g1, 1);
        var g2 = Box("col_grow2", -1, 20);
        FlexLayout.SetGrow(g2, 2);
        var basis = Box("col_basis40", -1, -1);
        FlexLayout.SetBasis(basis, new FlexBasis(40));
        var self = Box("col_alignself_end", 80, 20);
        FlexLayout.SetAlignSelf(self, FlexAlignSelf.End);
        column.Children.Add(g1);
        column.Children.Add(g2);
        column.Children.Add(basis);
        column.Children.Add(self);

        var row = new FlexLayout
        {
            AutomationId = "flex_row_evenly",
            Direction = FlexDirection.Row,
            JustifyContent = FlexJustify.SpaceEvenly,
            AlignItems = FlexAlignItems.End,
            HeightRequest = 80,
            BackgroundColor = Color.FromArgb("#CCCCCC"),
            Children = { Box("ev_a", 60, 20), Box("ev_b", 60, 40), Box("ev_c", 60, 60) },
        };

        return Page("Flex", new VerticalStackLayout
        {
            AutomationId = "root",
            Spacing = 10,
            Padding = 10,
            Children = { wrap, column, row },
        });
    }

    public static Page Absolute()
    {
        var abs = new AbsoluteLayout { AutomationId = "abs", BackgroundColor = Color.FromArgb("#EEEEEE"), Margin = 10 };

        var a = Box("abs_fixed");
        AbsoluteLayout.SetLayoutBounds(a, new Rect(20, 30, 100, 50));
        abs.Add(a);

        var b = Box("abs_pos_proportional");
        AbsoluteLayout.SetLayoutBounds(b, new Rect(1, 0, 120, 40));
        AbsoluteLayout.SetLayoutFlags(b, AbsoluteLayoutFlags.PositionProportional);
        abs.Add(b);

        var c = Box("abs_all_proportional");
        AbsoluteLayout.SetLayoutBounds(c, new Rect(0.5, 0.5, 0.25, 0.2));
        AbsoluteLayout.SetLayoutFlags(c, AbsoluteLayoutFlags.All);
        abs.Add(c);

        var d = Box("abs_width_proportional");
        AbsoluteLayout.SetLayoutBounds(d, new Rect(0, 1, 1, 30));
        AbsoluteLayout.SetLayoutFlags(d, AbsoluteLayoutFlags.WidthProportional | AbsoluteLayoutFlags.YProportional);
        abs.Add(d);

        var e = Box("abs_autosize", 70, 45);
        AbsoluteLayout.SetLayoutBounds(e, new Rect(300, 150, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
        abs.Add(e);

        var f = Box("abs_margin");
        f.Margin = new Thickness(10, 5, 0, 0);
        AbsoluteLayout.SetLayoutBounds(f, new Rect(400, 300, 60, 60));
        abs.Add(f);

        return Page("Absolute", abs);
    }

    public static Page Nested()
    {
        var flex = new FlexLayout
        {
            AutomationId = "inner_flex",
            Wrap = FlexWrap.Wrap,
            JustifyContent = FlexJustify.Start,
            AlignContent = FlexAlignContent.Start,
            Margin = new Thickness(4),
        };
        for (int i = 0; i < 6; i++)
        {
            var bx = Box($"flex_item_{i}", 50, 30);
            bx.Margin = 3;
            flex.Children.Add(bx);
        }

        var grid = new Grid
        {
            AutomationId = "inner_grid",
            Padding = 6,
            ColumnSpacing = 6,
            RowSpacing = 6,
            ColumnDefinitions = Columns(Px(80), Star()),
            RowDefinitions = Rows(Px(60), Auto),
        };
        grid.Add(Box("grid_cell_00").At(0, 0));
        grid.Add(flex.At(0, 1, rowSpan: 2));
        grid.Add(Box("grid_cell_10", -1, 45).At(1, 0));

        var border = new Microsoft.Maui.Controls.Border
        {
            AutomationId = "inner_border",
            Stroke = Colors.DarkGray,
            StrokeThickness = 3,
            Padding = new Thickness(8, 4),
            Margin = new Thickness(12, 0),
            Content = grid,
        };

        var stack = new VerticalStackLayout
        {
            AutomationId = "inner_stack",
            Spacing = 12,
            Padding = new Thickness(16, 10),
            Children =
            {
                Box("before_border", -1, 40),
                border,
                Box("after_border", 200, 40).Align(LayoutOptions.Center, LayoutOptions.Start),
            },
        };
        for (int i = 0; i < 8; i++)
            stack.Children.Add(Box($"tail_{i}", -1, 50));

        var scroll = new ScrollView { AutomationId = "outer_scroll", Content = stack, Margin = new Thickness(0, 8) };
        return Page("Nested", scroll);
    }
}
