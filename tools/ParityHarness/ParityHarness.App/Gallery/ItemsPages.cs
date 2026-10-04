using ParityHarness.Dump;
using static ParityHarness.Ui;

namespace ParityHarness.Gallery;

public sealed record Item(string Key, bool Tall);

/// <summary>Two templates of different fixed heights, chosen per item.</summary>
public sealed class HeightTemplateSelector : DataTemplateSelector
{
    private readonly DataTemplate _short = Make(40, "#64B5F6");
    private readonly DataTemplate _tall = Make(70, "#FFB74D");

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
        => item is Item { Tall: true } ? _tall : _short;

    private static DataTemplate Make(double height, string color) => new(() =>
    {
        // Templated views can be recycled (AutomationId may be set only once), so they
        // carry the harness key as a re-settable attached property bound to the item.
        var root = new Grid { HeightRequest = height, Padding = new Thickness(8, 4), BackgroundColor = Color.FromArgb(color) };
        root.SetBinding(DumpKey.KeyProperty, nameof(Item.Key));
        var inner = new BoxView { Color = Colors.White, WidthRequest = 60, HorizontalOptions = LayoutOptions.Start };
        inner.SetBinding(DumpKey.KeyProperty, nameof(Item.Key), stringFormat: "{0}_inner");
        root.Add(inner);
        return root;
    });
}

public static class ItemsPages
{
    public static Page Collection()
    {
        var items = Enumerable.Range(0, 6).Select(i => new Item($"item_{i}", i % 3 == 1)).ToList();
        var cv = new CollectionView
        {
            AutomationId = "cv",
            ItemsSource = items,
            ItemTemplate = new HeightTemplateSelector(),
            ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 10 },
            Header = new Grid
            {
                AutomationId = "cv_header",
                HeightRequest = 50,
                BackgroundColor = Color.FromArgb("#E57373"),
                Children = { Box("cv_header_box", 100, 20).Align(LayoutOptions.Center, LayoutOptions.Center) },
            },
            Footer = new Grid
            {
                AutomationId = "cv_footer",
                HeightRequest = 30,
                BackgroundColor = Color.FromArgb("#81C784"),
            },
        };

        var grid = new Grid
        {
            AutomationId = "root_grid",
            Padding = 10,
            ColumnSpacing = 10,
            ColumnDefinitions = Columns(Star(), Px(150)),
        };
        grid.Add(cv.At(0, 0));
        grid.Add(Box("side_box", -1, 100).Align(LayoutOptions.Fill, LayoutOptions.Start).At(0, 1));
        return Page("CollectionView", grid);
    }

    public static Page CollectionEmpty()
    {
        var cv = new CollectionView
        {
            AutomationId = "cv_empty",
            ItemsSource = Array.Empty<Item>(),
            ItemTemplate = new HeightTemplateSelector(),
            HeightRequest = 300,
            EmptyView = new Grid
            {
                AutomationId = "empty_view",
                BackgroundColor = Color.FromArgb("#EEEEEE"),
                Children = { Box("empty_box", 120, 60).Align(LayoutOptions.Center, LayoutOptions.Center) },
            },
        };
        var stack = new VerticalStackLayout
        {
            AutomationId = "root",
            Padding = 10,
            Spacing = 10,
            Children = { Box("above", -1, 40), Frame("cv_frame", cv, stroke: 2), Box("below", -1, 40) },
        };
        return Page("CollectionEmpty", stack);
    }
}
