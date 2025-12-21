// ShellDemo App - Comprehensive Control Demo

using Microsoft.Maui.Controls;

namespace ShellDemo;

/// <summary>
/// Main application class with Shell navigation.
/// </summary>
public class App : Application
{
    public App()
    {
        MainPage = new AppShell();
    }
}

/// <summary>
/// Shell definition with flyout menu - comprehensive control demo.
/// </summary>
public class AppShell : Shell
{
    public AppShell()
    {
        FlyoutBehavior = FlyoutBehavior.Flyout;
        Title = "OpenMaui Controls Demo";

        // Register routes for push navigation (pages not in flyout)
        Routing.RegisterRoute("detail", typeof(DetailPage));

        // Home
        Items.Add(CreateFlyoutItem("Home", typeof(HomePage)));

        // Buttons Demo
        Items.Add(CreateFlyoutItem("Buttons", typeof(ButtonsPage)));

        // Text Input Demo
        Items.Add(CreateFlyoutItem("Text Input", typeof(TextInputPage)));

        // Selection Controls Demo
        Items.Add(CreateFlyoutItem("Selection", typeof(SelectionPage)));

        // Pickers Demo
        Items.Add(CreateFlyoutItem("Pickers", typeof(PickersPage)));

        // Lists Demo
        Items.Add(CreateFlyoutItem("Lists", typeof(ListsPage)));

        // Progress Demo
        Items.Add(CreateFlyoutItem("Progress", typeof(ProgressPage)));

        // Grids Demo
        Items.Add(CreateFlyoutItem("Grids", typeof(GridsPage)));

        // About
        Items.Add(CreateFlyoutItem("About", typeof(AboutPage)));
    }

    private FlyoutItem CreateFlyoutItem(string title, Type pageType)
    {
        // Route is required for Shell.GoToAsync navigation to work
        var route = title.Replace(" ", "");
        return new FlyoutItem
        {
            Title = title,
            Route = route,
            Items =
            {
                new ShellContent
                {
                    Title = title,
                    Route = route,
                    ContentTemplate = new DataTemplate(pageType)
                }
            }
        };
    }
}
