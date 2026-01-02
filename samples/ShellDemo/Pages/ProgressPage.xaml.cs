using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace ShellDemo.Pages;

public partial class ProgressPage : ContentPage
{
    private int _eventCount = 0;
    private bool _isAnimating = false;

    public ProgressPage()
    {
        InitializeComponent();
    }

    private void LogEvent(string message)
    {
        _eventCount++;
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        EventLog.Text = $"[{timestamp}] {_eventCount}. {message}\n{EventLog.Text}";
    }

    private void OnToggleIndicatorClicked(object? sender, EventArgs e)
    {
        ToggleIndicator.IsRunning = !ToggleIndicator.IsRunning;
        LogEvent($"ActivityIndicator: {(ToggleIndicator.IsRunning ? "Started" : "Stopped")}");
    }

    private void OnProgressSliderChanged(object? sender, ValueChangedEventArgs e)
    {
        var value = e.NewValue / 100.0;
        AnimatedProgress.Progress = value;
        ProgressLabel.Text = $"Progress: {e.NewValue:0}%";
    }

    private void OnResetClicked(object? sender, EventArgs e)
    {
        AnimatedProgress.Progress = 0;
        ProgressSlider.Value = 0;
        LogEvent("Progress reset to 0%");
    }

    private async void OnAnimateClicked(object? sender, EventArgs e)
    {
        if (_isAnimating) return;
        _isAnimating = true;
        LogEvent("Animation started");

        for (int i = (int)ProgressSlider.Value; i <= 100; i += 5)
        {
            AnimatedProgress.Progress = i / 100.0;
            ProgressSlider.Value = i;
            await Task.Delay(100);
        }

        _isAnimating = false;
        LogEvent("Animation completed");
    }

    private async void OnSimulateClicked(object? sender, EventArgs e)
    {
        if (_isAnimating) return;
        _isAnimating = true;
        LogEvent("Download simulation started");

        AnimatedProgress.Progress = 0;
        ProgressSlider.Value = 0;

        var random = new Random();
        double progress = 0;
        while (progress < 1.0)
        {
            progress += random.NextDouble() * 0.1;
            if (progress > 1.0) progress = 1.0;
            AnimatedProgress.Progress = progress;
            ProgressSlider.Value = progress * 100;
            await Task.Delay(200 + random.Next(300));
        }

        _isAnimating = false;
        LogEvent("Download simulation completed");
    }
}
