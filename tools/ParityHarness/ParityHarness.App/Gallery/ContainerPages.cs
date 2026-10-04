using Microsoft.Maui.Controls.Shapes;
using static ParityHarness.Ui;

namespace ParityHarness.Gallery;

public static class ContainerPages
{
    public static Page BorderFrame()
    {
        // Auto-sized Border around fixed content: 200x50 + 2*padding + 2*stroke.
        var autoBorder = new Microsoft.Maui.Controls.Border
        {
            AutomationId = "border_auto",
            Stroke = Colors.Black,
            StrokeThickness = 4,
            Padding = 10,
            HorizontalOptions = LayoutOptions.Start,
            Content = Box("border_auto_content", 200, 50),
        };

        // Fixed-size Border, Fill content, asymmetric padding, rounded shape.
        var fixedBorder = new Microsoft.Maui.Controls.Border
        {
            AutomationId = "border_fixed",
            Stroke = Colors.DarkBlue,
            StrokeThickness = 2,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Padding = new Thickness(5, 10, 15, 20),
            WidthRequest = 300,
            HeightRequest = 100,
            HorizontalOptions = LayoutOptions.End,
            Content = Box("border_fixed_content"),
        };

        // Content centred inside a Border.
        var centreBorder = new Microsoft.Maui.Controls.Border
        {
            AutomationId = "border_centre",
            Stroke = Colors.Gray,
            StrokeThickness = 1,
            HeightRequest = 80,
            Content = Box("border_centre_content", 60, 30).Align(LayoutOptions.Center, LayoutOptions.Center),
        };

        // Frame with its default padding (20) and an explicit one.
        var frameDefault = new Frame
        {
            AutomationId = "frame_default",
            HasShadow = false,
            BorderColor = Colors.Black,
            HorizontalOptions = LayoutOptions.Start,
            Content = Box("frame_default_content", 120, 30),
        };
        var framePadded = new Frame
        {
            AutomationId = "frame_padded",
            HasShadow = false,
            BorderColor = Colors.Black,
            Padding = new Thickness(5),
            CornerRadius = 0,
            Content = Box("frame_padded_content", -1, 30),
        };

        // Border with margin inside a fixed-size grid cell.
        var cellBorder = new Microsoft.Maui.Controls.Border
        {
            AutomationId = "border_cell",
            Stroke = Colors.Green,
            StrokeThickness = 6,
            Margin = new Thickness(10, 5),
            Content = Box("border_cell_content"),
        };
        var cellGrid = new Grid { AutomationId = "cell_grid", HeightRequest = 70, ColumnDefinitions = Columns(Star(), Px(200)) };
        cellGrid.Add(cellBorder.At(0, 1));

        var root = new VerticalStackLayout
        {
            AutomationId = "root",
            Spacing = 8,
            Padding = 10,
            Children = { autoBorder, fixedBorder, centreBorder, frameDefault, framePadded, cellGrid },
        };
        return Page("BorderFrame", root);
    }

    public static Page Labels()
    {
        const string Long = "The quick brown fox jumps over the lazy dog. Pack my box with five dozen liquor jugs. How vexingly quick daft zebras jump.";

        var wrap = Text("label_wrap", Long);
        wrap.LineBreakMode = LineBreakMode.WordWrap;
        wrap.WidthRequest = 220;
        wrap.HorizontalOptions = LayoutOptions.Start;

        var tail = Text("label_tail", Long);
        tail.LineBreakMode = LineBreakMode.TailTruncation;
        tail.WidthRequest = 220;
        tail.HorizontalOptions = LayoutOptions.Start;

        var maxLines = Text("label_maxlines2", Long);
        maxLines.MaxLines = 2;
        maxLines.LineBreakMode = LineBreakMode.WordWrap;
        maxLines.WidthRequest = 220;
        maxLines.HorizontalOptions = LayoutOptions.Start;

        var chr = Text("label_charwrap", Long);
        chr.LineBreakMode = LineBreakMode.CharacterWrap;
        chr.WidthRequest = 160;
        chr.HorizontalOptions = LayoutOptions.Start;

        var padded = Text("label_padded", "Padded");
        padded.Padding = new Thickness(10, 6);
        padded.BackgroundColor = Color.FromArgb("#EEEEEE");
        padded.HorizontalOptions = LayoutOptions.Start;

        var centred = Text("label_centre", "Centred");
        centred.HorizontalOptions = LayoutOptions.Center;

        var fill = Text("label_fill_end", "Fill, end-aligned text");
        fill.HorizontalTextAlignment = TextAlignment.End;

        // A label in an Auto grid cell next to a fixed box: the box follows the label width.
        var grid = new Grid { AutomationId = "auto_grid", ColumnDefinitions = Columns(Auto, Px(50)), ColumnSpacing = 4 };
        grid.Add(Text("label_in_auto", "Auto cell").At(0, 0));
        var after = Box("box_after_auto", 50, 20).At(0, 1);
        Dump.DumpKey.SetTextDependent(after, true);
        grid.Add(after);

        var big = Text("label_large", "Large 28", 28);
        big.HorizontalOptions = LayoutOptions.Start;

        var root = new VerticalStackLayout
        {
            AutomationId = "root",
            Spacing = 10,
            Padding = 10,
            Children = { wrap, tail, maxLines, chr, padded, centred, fill, grid, big },
        };
        return Page("Labels", root);
    }

    public static Page Images()
    {
        var grid = new Grid
        {
            AutomationId = "image_grid",
            Padding = 10,
            ColumnSpacing = 10,
            RowSpacing = 10,
            ColumnDefinitions = Columns(Px(200), Px(200), Px(200)),
            RowDefinitions = Rows(Px(100), Px(100), Auto, Auto),
        };

        // Fixed boxes (the image fills its cell; Aspect changes drawing, not the frame).
        grid.Add(MakeImage("img_aspectfit", Aspect.AspectFit).At(0, 0));
        grid.Add(MakeImage("img_aspectfill", Aspect.AspectFill).At(0, 1));
        grid.Add(MakeImage("img_fill", Aspect.Fill).At(0, 2));
        grid.Add(MakeImage("img_center", Aspect.Center).At(1, 0));

        // Unsized images aligned in a cell: the frame comes from the image's natural size
        // constrained by the cell and the Aspect.
        grid.Add(MakeImage("img_natural_start", Aspect.AspectFit).Align(LayoutOptions.Start, LayoutOptions.Start).At(1, 1));
        var tall = MakeImage("img_fit_narrow", Aspect.AspectFit).Align(LayoutOptions.Center, LayoutOptions.Center).At(1, 2);
        tall.WidthRequest = 80;
        grid.Add(tall);

        // Auto row/column: image natural size drives the cell.
        grid.Add(MakeImage("img_auto_row", Aspect.AspectFit).Align(LayoutOptions.Start, LayoutOptions.Start).At(2, 0));
        var heightOnly = MakeImage("img_height_only", Aspect.AspectFit).Align(LayoutOptions.Start, LayoutOptions.Start).At(2, 1);
        heightOnly.HeightRequest = 40;
        grid.Add(heightOnly);

        // In a Border, padded, centred.
        var border = new Microsoft.Maui.Controls.Border
        {
            AutomationId = "img_border",
            Stroke = Colors.Black,
            StrokeThickness = 2,
            Padding = 6,
            HorizontalOptions = LayoutOptions.Center,
            Content = MakeImage("img_in_border", Aspect.AspectFit),
        };
        grid.Add(border.At(3, 0, columnSpan: 3));
        return Page("Images", grid);
    }

    private static Image MakeImage(string id, Aspect aspect)
        => new() { AutomationId = id, Source = TestImage(), Aspect = aspect, BackgroundColor = Color.FromArgb("#EEEEEE") };
}
