// ControlsPage - Demonstrates various MAUI controls

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace ShellDemo;

public class ControlsPage : ContentPage
{
    public ControlsPage()
    {
        Title = "Controls";

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20),
                Spacing = 15,
                Children =
                {
                    new Label
                    {
                        Text = "Control Gallery",
                        FontSize = 24,
                        FontAttributes = FontAttributes.Bold
                    },

                    // Buttons
                    CreateSection("Buttons", new View[]
                    {
                        CreateButtonRow()
                    }),

                    // CheckBox & Switch
                    CreateSection("Selection", new View[]
                    {
                        CreateCheckBoxRow(),
                        CreateSwitchRow()
                    }),

                    // Slider
                    CreateSection("Slider", new View[]
                    {
                        CreateSliderRow()
                    }),

                    // Picker
                    CreateSection("Picker", new View[]
                    {
                        CreatePickerRow()
                    }),

                    // Progress
                    CreateSection("Progress", new View[]
                    {
                        CreateProgressRow()
                    })
                }
            }
        };
    }

    private Frame CreateSection(string title, View[] content)
    {
        var layout = new VerticalStackLayout { Spacing = 10 };
        layout.Children.Add(new Label
        {
            Text = title,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold
        });

        foreach (var view in content)
        {
            layout.Children.Add(view);
        }

        return new Frame
        {
            CornerRadius = 8,
            Padding = new Thickness(15),
            BackgroundColor = Colors.White,
            Content = layout
        };
    }

    private View CreateButtonRow()
    {
        var resultLabel = new Label { TextColor = Colors.Gray, FontSize = 12 };

        var layout = new VerticalStackLayout { Spacing = 10 };

        var buttonRow = new HorizontalStackLayout { Spacing = 10 };

        var primaryBtn = new Button { Text = "Primary", BackgroundColor = Color.FromArgb("#2196F3"), TextColor = Colors.White };
        primaryBtn.Clicked += (s, e) => resultLabel.Text = "Primary clicked!";

        var successBtn = new Button { Text = "Success", BackgroundColor = Color.FromArgb("#4CAF50"), TextColor = Colors.White };
        successBtn.Clicked += (s, e) => resultLabel.Text = "Success clicked!";

        var dangerBtn = new Button { Text = "Danger", BackgroundColor = Color.FromArgb("#F44336"), TextColor = Colors.White };
        dangerBtn.Clicked += (s, e) => resultLabel.Text = "Danger clicked!";

        buttonRow.Children.Add(primaryBtn);
        buttonRow.Children.Add(successBtn);
        buttonRow.Children.Add(dangerBtn);

        layout.Children.Add(buttonRow);
        layout.Children.Add(resultLabel);

        return layout;
    }

    private View CreateCheckBoxRow()
    {
        var layout = new HorizontalStackLayout { Spacing = 20 };

        var cb1 = new CheckBox { IsChecked = true };
        var cb2 = new CheckBox { IsChecked = false };

        layout.Children.Add(cb1);
        layout.Children.Add(new Label { Text = "Option 1", VerticalOptions = LayoutOptions.Center });
        layout.Children.Add(cb2);
        layout.Children.Add(new Label { Text = "Option 2", VerticalOptions = LayoutOptions.Center });

        return layout;
    }

    private View CreateSwitchRow()
    {
        var label = new Label { Text = "Off", VerticalOptions = LayoutOptions.Center };
        var sw = new Switch { IsToggled = false };
        sw.Toggled += (s, e) => label.Text = e.Value ? "On" : "Off";

        return new HorizontalStackLayout
        {
            Spacing = 10,
            Children = { sw, label }
        };
    }

    private View CreateSliderRow()
    {
        var label = new Label { Text = "Value: 50" };
        var slider = new Slider { Minimum = 0, Maximum = 100, Value = 50 };
        slider.ValueChanged += (s, e) => label.Text = $"Value: {(int)e.NewValue}";

        return new VerticalStackLayout
        {
            Spacing = 5,
            Children = { slider, label }
        };
    }

    private View CreatePickerRow()
    {
        var label = new Label { Text = "Selected: (none)", TextColor = Colors.Gray };
        var picker = new Picker { Title = "Select a fruit" };
        picker.Items.Add("Apple");
        picker.Items.Add("Banana");
        picker.Items.Add("Cherry");
        picker.Items.Add("Date");
        picker.Items.Add("Elderberry");

        picker.SelectedIndexChanged += (s, e) =>
        {
            if (picker.SelectedIndex >= 0)
                label.Text = $"Selected: {picker.Items[picker.SelectedIndex]}";
        };

        return new VerticalStackLayout
        {
            Spacing = 5,
            Children = { picker, label }
        };
    }

    private View CreateProgressRow()
    {
        var progress = new ProgressBar { Progress = 0.7 };
        var activity = new ActivityIndicator { IsRunning = true };

        return new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                progress,
                new Label { Text = "70% Complete", FontSize = 12, TextColor = Colors.Gray },
                new HorizontalStackLayout
                {
                    Spacing = 10,
                    Children =
                    {
                        activity,
                        new Label { Text = "Loading...", VerticalOptions = LayoutOptions.Center, TextColor = Colors.Gray }
                    }
                }
            }
        };
    }
}
