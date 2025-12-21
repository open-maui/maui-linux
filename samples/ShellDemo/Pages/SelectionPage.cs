// SelectionPage - CheckBox, Switch, Slider Demo

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace ShellDemo;

public class SelectionPage : ContentPage
{
    private readonly Label _eventLog;
    private int _eventCount = 0;

    public SelectionPage()
    {
        Title = "Selection Controls";

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
                    new Label { Text = "Selection Controls", FontSize = 24, FontAttributes = FontAttributes.Bold },

                    CreateSection("CheckBox", CreateCheckBoxDemo()),
                    CreateSection("Switch", CreateSwitchDemo()),
                    CreateSection("Slider", CreateSliderDemo())
                }
            }
        };
    }

    private View CreateCheckBoxDemo()
    {
        var layout = new VerticalStackLayout { Spacing = 15 };

        // Basic checkboxes
        var basicRow = new HorizontalStackLayout { Spacing = 20 };
        
        var cb1 = new CheckBox { IsChecked = false };
        cb1.CheckedChanged += (s, e) => LogEvent($"Checkbox 1: {(e.Value ? "Checked" : "Unchecked")}");
        basicRow.Children.Add(cb1);
        basicRow.Children.Add(new Label { Text = "Option 1", VerticalOptions = LayoutOptions.Center });

        var cb2 = new CheckBox { IsChecked = true };
        cb2.CheckedChanged += (s, e) => LogEvent($"Checkbox 2: {(e.Value ? "Checked" : "Unchecked")}");
        basicRow.Children.Add(cb2);
        basicRow.Children.Add(new Label { Text = "Option 2 (default checked)", VerticalOptions = LayoutOptions.Center });

        layout.Children.Add(basicRow);

        // Colored checkboxes
        var colorRow = new HorizontalStackLayout { Spacing = 20 };
        var colors = new[] { Colors.Red, Colors.Green, Colors.Blue, Colors.Purple };
        foreach (var color in colors)
        {
            var cb = new CheckBox { Color = color, IsChecked = true };
            cb.CheckedChanged += (s, e) => LogEvent($"{color} checkbox: {(e.Value ? "Checked" : "Unchecked")}");
            colorRow.Children.Add(cb);
        }
        layout.Children.Add(new Label { Text = "Colored Checkboxes:", FontSize = 12 });
        layout.Children.Add(colorRow);

        // Disabled checkbox
        var disabledRow = new HorizontalStackLayout { Spacing = 10 };
        var disabledCb = new CheckBox { IsChecked = true, IsEnabled = false };
        disabledRow.Children.Add(disabledCb);
        disabledRow.Children.Add(new Label { Text = "Disabled (checked)", VerticalOptions = LayoutOptions.Center, TextColor = Colors.Gray });
        layout.Children.Add(disabledRow);

        return layout;
    }

    private View CreateSwitchDemo()
    {
        var layout = new VerticalStackLayout { Spacing = 15 };

        // Basic switch
        var basicRow = new HorizontalStackLayout { Spacing = 15 };
        var statusLabel = new Label { Text = "Off", VerticalOptions = LayoutOptions.Center, WidthRequest = 50 };
        var sw1 = new Switch { IsToggled = false };
        sw1.Toggled += (s, e) =>
        {
            statusLabel.Text = e.Value ? "On" : "Off";
            LogEvent($"Switch toggled: {(e.Value ? "ON" : "OFF")}");
        };
        basicRow.Children.Add(sw1);
        basicRow.Children.Add(statusLabel);
        layout.Children.Add(basicRow);

        // Colored switches
        var colorRow = new HorizontalStackLayout { Spacing = 20 };
        var switchColors = new[] { Colors.Green, Colors.Orange, Colors.Purple };
        foreach (var color in switchColors)
        {
            var sw = new Switch { IsToggled = true, OnColor = color };
            sw.Toggled += (s, e) => LogEvent($"{color} switch: {(e.Value ? "ON" : "OFF")}");
            colorRow.Children.Add(sw);
        }
        layout.Children.Add(new Label { Text = "Colored Switches:", FontSize = 12 });
        layout.Children.Add(colorRow);

        // Disabled switch
        var disabledRow = new HorizontalStackLayout { Spacing = 10 };
        var disabledSw = new Switch { IsToggled = true, IsEnabled = false };
        disabledRow.Children.Add(disabledSw);
        disabledRow.Children.Add(new Label { Text = "Disabled (on)", VerticalOptions = LayoutOptions.Center, TextColor = Colors.Gray });
        layout.Children.Add(disabledRow);

        return layout;
    }

    private View CreateSliderDemo()
    {
        var layout = new VerticalStackLayout { Spacing = 15 };

        // Basic slider
        var valueLabel = new Label { Text = "Value: 50" };
        var slider1 = new Slider { Minimum = 0, Maximum = 100, Value = 50 };
        slider1.ValueChanged += (s, e) =>
        {
            valueLabel.Text = $"Value: {(int)e.NewValue}";
            LogEvent($"Slider value: {(int)e.NewValue}");
        };
        layout.Children.Add(slider1);
        layout.Children.Add(valueLabel);

        // Slider with custom range
        layout.Children.Add(new Label { Text = "Temperature (0-40°C):", FontSize = 12, Margin = new Thickness(0, 10, 0, 0) });
        var tempLabel = new Label { Text = "20°C" };
        var tempSlider = new Slider { Minimum = 0, Maximum = 40, Value = 20 };
        tempSlider.ValueChanged += (s, e) =>
        {
            tempLabel.Text = $"{(int)e.NewValue}°C";
            LogEvent($"Temperature: {(int)e.NewValue}°C");
        };
        layout.Children.Add(tempSlider);
        layout.Children.Add(tempLabel);

        // Colored slider
        layout.Children.Add(new Label { Text = "Colored Slider:", FontSize = 12, Margin = new Thickness(0, 10, 0, 0) });
        var colorSlider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            Value = 75,
            MinimumTrackColor = Colors.Green,
            MaximumTrackColor = Colors.LightGray,
            ThumbColor = Colors.DarkGreen
        };
        colorSlider.ValueChanged += (s, e) => LogEvent($"Colored slider: {(int)e.NewValue}");
        layout.Children.Add(colorSlider);

        // Disabled slider
        layout.Children.Add(new Label { Text = "Disabled Slider:", FontSize = 12, Margin = new Thickness(0, 10, 0, 0) });
        var disabledSlider = new Slider { Minimum = 0, Maximum = 100, Value = 30, IsEnabled = false };
        layout.Children.Add(disabledSlider);

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
