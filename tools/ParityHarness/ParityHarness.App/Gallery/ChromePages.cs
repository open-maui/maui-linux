using static ParityHarness.Ui;

namespace ParityHarness.Gallery;

/// <summary>
/// Pages whose root is a navigation container. The title bar itself is native chrome and
/// not in the MAUI visual tree; its height shows up as the offset of the hosted page.
/// </summary>
public static class ChromePages
{
    private static ContentPage Content(string name)
    {
        var grid = new Grid
        {
            AutomationId = $"{name}_grid",
            Padding = 10,
            RowSpacing = 10,
            RowDefinitions = Rows(Px(60), Star(), Px(60)),
        };
        grid.Add(Box($"{name}_top", -1, -1).At(0, 0));
        grid.Add(Box($"{name}_middle", 200, -1).Align(LayoutOptions.Center, LayoutOptions.Fill).At(1, 0));
        grid.Add(Box($"{name}_bottom", -1, -1).At(2, 0));
        return new ContentPage
        {
            AutomationId = $"{name}_content_page",
            Title = name,
            BackgroundColor = Colors.White,
            Content = grid,
        };
    }

    public static Page Navigation()
        => new NavigationPage(Content("nav")) { AutomationId = "NavigationPage", Title = "NavigationPage" };

    public static Page ShellPage()
    {
        var shell = new Shell
        {
            AutomationId = "Shell",
            Title = "Shell",
            FlyoutBehavior = FlyoutBehavior.Disabled,
        };
        shell.Items.Add(new ShellContent { Title = "Shell", Content = Content("shell"), Route = "main" });
        return shell;
    }
}
