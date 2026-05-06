using Microsoft.Maui.Controls;

namespace $safeprojectname$;

/// <summary>
/// The main page of the application.
/// </summary>
public partial class MainPage : ContentPage
{
    private int _count = 0;

    public MainPage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Title = "Home";

        var layout = new VerticalStackLayout
        {
            Spacing = 25,
            Padding = new Thickness(30, 0),
            VerticalOptions = LayoutOptions.Center
        };

        var welcomeLabel = new Label
        {
            Text = "Hello, OpenMaui!",
            FontSize = 32,
            HorizontalOptions = LayoutOptions.Center
        };

        var instructionLabel = new Label
        {
            Text = "Welcome to .NET MAUI on Linux",
            FontSize = 18,
            HorizontalOptions = LayoutOptions.Center
        };

        var counterButton = new Button
        {
            Text = "Click me",
            HorizontalOptions = LayoutOptions.Center
        };

        counterButton.Clicked += OnCounterClicked;

        var image = new Image
        {
            Source = "dotnet_bot.png",
            HeightRequest = 185,
            HorizontalOptions = LayoutOptions.Center
        };

        layout.Children.Add(welcomeLabel);
        layout.Children.Add(instructionLabel);
        layout.Children.Add(counterButton);
        layout.Children.Add(image);

        Content = new ScrollView { Content = layout };
    }

    private void OnCounterClicked(object? sender, EventArgs e)
    {
        _count++;

        if (sender is Button button)
        {
            button.Text = _count == 1
                ? $"Clicked {_count} time"
                : $"Clicked {_count} times";
        }
    }
}
