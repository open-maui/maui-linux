// ButtonsPage - Comprehensive Button Control Demo

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace ShellDemo;

public class ButtonsPage : ContentPage
{
    private readonly Label _eventLog;
    private int _eventCount = 0;

    public ButtonsPage()
    {
        Title = "Buttons Demo";

        _eventLog = new Label
        {
            Text = "Events will appear here...",
            FontSize = 11,
            TextColor = Colors.Gray,
            LineBreakMode = LineBreakMode.WordWrap
        };

        Content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = new GridLength(120) }
            },
            Children =
            {
                CreateMainContent(),
                CreateEventLogPanel()
            }
        };

        Grid.SetRow((View)((Grid)Content).Children[0], 0);
        Grid.SetRow((View)((Grid)Content).Children[1], 1);
    }

    private View CreateMainContent()
    {
        return new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20),
                Spacing = 20,
                Children =
                {
                    new Label { Text = "Button Styles & Events", FontSize = 24, FontAttributes = FontAttributes.Bold },

                    // Basic Buttons
                    CreateSection("Basic Buttons", CreateBasicButtons()),

                    // Styled Buttons
                    CreateSection("Styled Buttons", CreateStyledButtons()),

                    // Button States
                    CreateSection("Button States", CreateButtonStates()),

                    // Button with Icons (text simulation)
                    CreateSection("Button Variations", CreateButtonVariations())
                }
            }
        };
    }

    private View CreateBasicButtons()
    {
        var layout = new VerticalStackLayout { Spacing = 10 };

        var defaultBtn = new Button { Text = "Default Button" };
        defaultBtn.Clicked += (s, e) => LogEvent("Default Button clicked");
        defaultBtn.Pressed += (s, e) => LogEvent("Default Button pressed");
        defaultBtn.Released += (s, e) => LogEvent("Default Button released");

        var textBtn = new Button { Text = "Text Only", BackgroundColor = Colors.Transparent, TextColor = Colors.Blue };
        textBtn.Clicked += (s, e) => LogEvent("Text Button clicked");

        layout.Children.Add(defaultBtn);
        layout.Children.Add(textBtn);

        return layout;
    }

    private View CreateStyledButtons()
    {
        var layout = new HorizontalStackLayout { Spacing = 10 };

        var colors = new[]
        {
            ("#2196F3", "Primary"),
            ("#4CAF50", "Success"),
            ("#FF9800", "Warning"),
            ("#F44336", "Danger"),
            ("#9C27B0", "Purple")
        };

        foreach (var (color, name) in colors)
        {
            var btn = new Button
            {
                Text = name,
                BackgroundColor = Color.FromArgb(color),
                TextColor = Colors.White,
                CornerRadius = 5
            };
            btn.Clicked += (s, e) => LogEvent($"{name} button clicked");
            layout.Children.Add(btn);
        }

        return layout;
    }

    private View CreateButtonStates()
    {
        var layout = new VerticalStackLayout { Spacing = 10 };

        var enabledBtn = new Button { Text = "Enabled Button", IsEnabled = true };
        enabledBtn.Clicked += (s, e) => LogEvent("Enabled button clicked");

        var disabledBtn = new Button { Text = "Disabled Button", IsEnabled = false };

        var toggleBtn = new Button { Text = "Toggle Above Button" };
        toggleBtn.Clicked += (s, e) =>
        {
            disabledBtn.IsEnabled = !disabledBtn.IsEnabled;
            disabledBtn.Text = disabledBtn.IsEnabled ? "Now Enabled!" : "Disabled Button";
            LogEvent($"Toggled button to: {(disabledBtn.IsEnabled ? "Enabled" : "Disabled")}");
        };

        layout.Children.Add(enabledBtn);
        layout.Children.Add(disabledBtn);
        layout.Children.Add(toggleBtn);

        return layout;
    }

    private View CreateButtonVariations()
    {
        var layout = new VerticalStackLayout { Spacing = 10 };

        var wideBtn = new Button
        {
            Text = "Wide Button",
            HorizontalOptions = LayoutOptions.Fill,
            BackgroundColor = Color.FromArgb("#673AB7"),
            TextColor = Colors.White
        };
        wideBtn.Clicked += (s, e) => LogEvent("Wide button clicked");

        var tallBtn = new Button
        {
            Text = "Tall Button",
            HeightRequest = 60,
            BackgroundColor = Color.FromArgb("#009688"),
            TextColor = Colors.White
        };
        tallBtn.Clicked += (s, e) => LogEvent("Tall button clicked");

        var roundBtn = new Button
        {
            Text = "Round",
            WidthRequest = 80,
            HeightRequest = 80,
            CornerRadius = 40,
            BackgroundColor = Color.FromArgb("#E91E63"),
            TextColor = Colors.White
        };
        roundBtn.Clicked += (s, e) => LogEvent("Round button clicked");

        layout.Children.Add(wideBtn);
        layout.Children.Add(tallBtn);
        layout.Children.Add(new HorizontalStackLayout { Children = { roundBtn } });

        return layout;
    }

    private Frame CreateSection(string title, View content)
    {
        return new Frame
        {
            CornerRadius = 8,
            Padding = new Thickness(15),
            BackgroundColor = Colors.White,
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    new Label { Text = title, FontSize = 16, FontAttributes = FontAttributes.Bold },
                    content
                }
            }
        };
    }

    private View CreateEventLogPanel()
    {
        return new Frame
        {
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            Padding = new Thickness(10),
            CornerRadius = 0,
            Content = new VerticalStackLayout
            {
                Children =
                {
                    new Label { Text = "Event Log:", FontSize = 12, FontAttributes = FontAttributes.Bold },
                    new ScrollView
                    {
                        HeightRequest = 80,
                        Content = _eventLog
                    }
                }
            }
        };
    }

    private void LogEvent(string message)
    {
        _eventCount++;
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        _eventLog.Text = $"[{timestamp}] {_eventCount}. {message}\n{_eventLog.Text}";
    }
}
