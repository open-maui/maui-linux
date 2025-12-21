// ProgressPage - ProgressBar and ActivityIndicator Demo

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace ShellDemo;

public class ProgressPage : ContentPage
{
    private readonly Label _eventLog;
    private int _eventCount = 0;
    private ProgressBar? _animatedProgress;
    private bool _isAnimating = false;

    public ProgressPage()
    {
        Title = "Progress";

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
                    new Label { Text = "Progress Indicators", FontSize = 24, FontAttributes = FontAttributes.Bold },

                    CreateSection("ProgressBar", CreateProgressBarDemo()),
                    CreateSection("ActivityIndicator", CreateActivityIndicatorDemo()),
                    CreateSection("Interactive Demo", CreateInteractiveDemo())
                }
            }
        };
    }

    private View CreateProgressBarDemo()
    {
        var layout = new VerticalStackLayout { Spacing = 15 };

        // Various progress values
        var values = new[] { 0.0, 0.25, 0.5, 0.75, 1.0 };
        foreach (var value in values)
        {
            var row = new HorizontalStackLayout { Spacing = 10 };
            var progress = new ProgressBar { Progress = value, WidthRequest = 200 };
            var label = new Label { Text = $"{value * 100:0}%", VerticalOptions = LayoutOptions.Center, WidthRequest = 50 };
            row.Children.Add(progress);
            row.Children.Add(label);
            layout.Children.Add(row);
        }

        // Colored progress bars
        layout.Children.Add(new Label { Text = "Colored Progress Bars:", FontSize = 12, Margin = new Thickness(0, 10, 0, 0) });

        var colors = new[] { Colors.Red, Colors.Green, Colors.Blue, Colors.Orange, Colors.Purple };
        foreach (var color in colors)
        {
            var progress = new ProgressBar { Progress = 0.7, ProgressColor = color };
            layout.Children.Add(progress);
        }

        return layout;
    }

    private View CreateActivityIndicatorDemo()
    {
        var layout = new VerticalStackLayout { Spacing = 15 };

        // Running indicator
        var runningRow = new HorizontalStackLayout { Spacing = 15 };
        var runningIndicator = new ActivityIndicator { IsRunning = true };
        runningRow.Children.Add(runningIndicator);
        runningRow.Children.Add(new Label { Text = "Loading...", VerticalOptions = LayoutOptions.Center });
        layout.Children.Add(runningRow);

        // Toggle indicator
        var toggleRow = new HorizontalStackLayout { Spacing = 15 };
        var toggleIndicator = new ActivityIndicator { IsRunning = false };
        var toggleBtn = new Button { Text = "Start/Stop" };
        toggleBtn.Clicked += (s, e) =>
        {
            toggleIndicator.IsRunning = !toggleIndicator.IsRunning;
            LogEvent($"ActivityIndicator: {(toggleIndicator.IsRunning ? "Started" : "Stopped")}");
        };
        toggleRow.Children.Add(toggleIndicator);
        toggleRow.Children.Add(toggleBtn);
        layout.Children.Add(toggleRow);

        // Colored indicators
        layout.Children.Add(new Label { Text = "Colored Indicators:", FontSize = 12, Margin = new Thickness(0, 10, 0, 0) });
        var colorRow = new HorizontalStackLayout { Spacing = 20 };
        var indicatorColors = new[] { Colors.Red, Colors.Green, Colors.Blue, Colors.Orange };
        foreach (var color in indicatorColors)
        {
            var indicator = new ActivityIndicator { IsRunning = true, Color = color };
            colorRow.Children.Add(indicator);
        }
        layout.Children.Add(colorRow);

        return layout;
    }

    private View CreateInteractiveDemo()
    {
        var layout = new VerticalStackLayout { Spacing = 15 };

        // Slider-controlled progress
        var progressLabel = new Label { Text = "Progress: 50%" };
        _animatedProgress = new ProgressBar { Progress = 0.5 };

        var slider = new Slider { Minimum = 0, Maximum = 100, Value = 50 };
        slider.ValueChanged += (s, e) =>
        {
            var value = e.NewValue / 100.0;
            _animatedProgress.Progress = value;
            progressLabel.Text = $"Progress: {e.NewValue:0}%";
        };

        layout.Children.Add(_animatedProgress);
        layout.Children.Add(slider);
        layout.Children.Add(progressLabel);

        // Animated progress buttons
        var buttonRow = new HorizontalStackLayout { Spacing = 10, Margin = new Thickness(0, 10, 0, 0) };

        var resetBtn = new Button { Text = "Reset", BackgroundColor = Colors.Gray, TextColor = Colors.White };
        resetBtn.Clicked += async (s, e) =>
        {
            _animatedProgress.Progress = 0;
            slider.Value = 0;
            LogEvent("Progress reset to 0%");
        };

        var animateBtn = new Button { Text = "Animate to 100%", BackgroundColor = Colors.Blue, TextColor = Colors.White };
        animateBtn.Clicked += async (s, e) =>
        {
            if (_isAnimating) return;
            _isAnimating = true;
            LogEvent("Animation started");

            for (int i = (int)(slider.Value); i <= 100; i += 5)
            {
                _animatedProgress.Progress = i / 100.0;
                slider.Value = i;
                await Task.Delay(100);
            }

            _isAnimating = false;
            LogEvent("Animation completed");
        };

        var simulateBtn = new Button { Text = "Simulate Download", BackgroundColor = Colors.Green, TextColor = Colors.White };
        simulateBtn.Clicked += async (s, e) =>
        {
            if (_isAnimating) return;
            _isAnimating = true;
            LogEvent("Download simulation started");

            _animatedProgress.Progress = 0;
            slider.Value = 0;

            var random = new Random();
            double progress = 0;
            while (progress < 1.0)
            {
                progress += random.NextDouble() * 0.1;
                if (progress > 1.0) progress = 1.0;
                _animatedProgress.Progress = progress;
                slider.Value = progress * 100;
                await Task.Delay(200 + random.Next(300));
            }

            _isAnimating = false;
            LogEvent("Download simulation completed");
        };

        buttonRow.Children.Add(resetBtn);
        buttonRow.Children.Add(animateBtn);
        buttonRow.Children.Add(simulateBtn);
        layout.Children.Add(buttonRow);

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
