using static ParityHarness.Ui;

namespace ParityHarness.Gallery;

public static class ScrollPages
{
    public static Page ScrollShort()
    {
        // Full-page ScrollView whose content is shorter than the viewport.
        var shortContent = new VerticalStackLayout
        {
            AutomationId = "short_stack",
            Spacing = 5,
            Padding = 5,
            Children = { Box("short_a", -1, 40), Box("short_b", 120, 40).Align(LayoutOptions.Center, LayoutOptions.Fill) },
        };
        var topScroll = new ScrollView { AutomationId = "top_scroll", Content = shortContent };

        // ScrollView centred inside a fixed frame, smaller content, explicit size.
        var centred = new ScrollView
        {
            AutomationId = "centred_scroll",
            WidthRequest = 300,
            HeightRequest = 150,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            BackgroundColor = Color.FromArgb("#EEEEEE"),
            Content = new VerticalStackLayout
            {
                AutomationId = "centred_content",
                Children = { Box("centred_a", 100, 30), Box("centred_b", 200, 30).Align(LayoutOptions.End, LayoutOptions.Fill) },
            },
        };
        var frame = Frame("scroll_frame", centred, stroke: 2);
        frame.HeightRequest = 260;

        // Unsized ScrollView (Center alignment) inside a frame: sizes to its content.
        var autoSized = new ScrollView
        {
            AutomationId = "autosize_scroll",
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Content = Box("autosize_content", 140, 60),
        };
        var frame2 = Frame("autosize_frame", autoSized, stroke: 2, padding: 4);
        frame2.HeightRequest = 120;

        var grid = new Grid
        {
            AutomationId = "root_grid",
            Padding = 10,
            RowSpacing = 10,
            RowDefinitions = Rows(Px(120), Auto, Auto),
        };
        grid.Add(Frame("top_frame", topScroll).At(0, 0));
        grid.Add(frame.At(1, 0));
        grid.Add(frame2.At(2, 0));
        return Page("ScrollShort", grid);
    }

    public static Page ScrollTall()
    {
        var stack = new VerticalStackLayout { AutomationId = "tall_stack", Spacing = 4, Padding = new Thickness(8, 0) };
        for (int i = 0; i < 20; i++)
            stack.Children.Add(Box($"tall_{i}", -1, 60));

        var vertical = new ScrollView { AutomationId = "v_scroll", Content = stack };

        var hstack = new HorizontalStackLayout { AutomationId = "wide_stack", Spacing = 4 };
        for (int i = 0; i < 12; i++)
            hstack.Children.Add(Box($"wide_{i}", 100, -1));
        var horizontal = new ScrollView
        {
            AutomationId = "h_scroll",
            Orientation = ScrollOrientation.Horizontal,
            HeightRequest = 80,
            Content = hstack,
        };

        var grid = new Grid { AutomationId = "root_grid", RowDefinitions = Rows(Px(80), Star()), RowSpacing = 6 };
        grid.Add(horizontal.At(0, 0));
        grid.Add(vertical.At(1, 0));
        return Page("ScrollTall", grid);
    }
}
