using System;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;

namespace ShellDemo.Pages;

public partial class MoreControlsPage : ContentPage
{
    private int _eventCount = 0;

    public MoreControlsPage()
    {
        InitializeComponent();
    }

    private void LogEvent(string message)
    {
        _eventCount++;
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        EventLog.Text = $"[{timestamp}] {_eventCount}. {message}\n{EventLog.Text}";
    }

    // Stepper
    private void OnStepperChanged(object? sender, ValueChangedEventArgs e)
    {
        StepperValueLabel.Text = $"Value: {(int)e.NewValue}";
        LogEvent($"Stepper: {(int)e.NewValue}");
    }

    private void OnCustomStepperChanged(object? sender, ValueChangedEventArgs e)
    {
        CustomStepperLabel.Text = $"Value: {(int)e.NewValue}";
        LogEvent($"Custom Stepper: {(int)e.NewValue}");
    }

    // RadioButton
    private void OnRadioChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (e.Value && sender is RadioButton rb)
        {
            RadioResultLabel.Text = $"Selected: {rb.Content}";
            LogEvent($"Size: {rb.Content}");
        }
    }

    private void OnColorRadioChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (e.Value && sender is RadioButton rb)
        {
            LogEvent($"Color: {rb.Content}");
        }
    }

    // Clipboard
    private async void OnCopyClicked(object? sender, EventArgs e)
    {
        var text = ClipboardEntry.Text;
        if (!string.IsNullOrEmpty(text))
        {
            await Clipboard.Default.SetTextAsync(text);
            ClipboardResultLabel.Text = $"Copied: {text}";
            LogEvent($"Copied to clipboard: {text}");
        }
        else
        {
            ClipboardResultLabel.Text = "Nothing to copy";
            LogEvent("Copy failed: empty text");
        }
    }

    private async void OnPasteClicked(object? sender, EventArgs e)
    {
        if (Clipboard.Default.HasText)
        {
            var text = await Clipboard.Default.GetTextAsync();
            ClipboardEntry.Text = text;
            ClipboardResultLabel.Text = $"Pasted: {text}";
            LogEvent($"Pasted from clipboard: {text}");
        }
        else
        {
            ClipboardResultLabel.Text = "Clipboard is empty";
            LogEvent("Paste failed: clipboard empty");
        }
    }

    // Share & Launcher
    private async void OnShareTextClicked(object? sender, EventArgs e)
    {
        try
        {
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Text = "Check out OpenMaui Linux - .NET MAUI for Linux!",
                Title = "Share OpenMaui"
            });
            LogEvent("Share dialog opened");
        }
        catch (Exception ex)
        {
            LogEvent($"Share error: {ex.Message}");
        }
    }

    private async void OnOpenUrlClicked(object? sender, EventArgs e)
    {
        try
        {
            await Launcher.Default.OpenAsync("https://github.com/pablotoledo/OpenMaui-Linux");
            LogEvent("Opened URL in browser");
        }
        catch (Exception ex)
        {
            LogEvent($"Launcher error: {ex.Message}");
        }
    }

    private async void OnOpenEmailClicked(object? sender, EventArgs e)
    {
        try
        {
            await Launcher.Default.OpenAsync("mailto:info@example.com?subject=OpenMaui%20Feedback");
            LogEvent("Opened email client");
        }
        catch (Exception ex)
        {
            LogEvent($"Email error: {ex.Message}");
        }
    }
}
