using System;
using Microsoft.Maui.Controls;

namespace ShellDemo.Pages;

public partial class ButtonsPage : ContentPage
{
    private int _eventCount = 0;

    public ButtonsPage()
    {
        InitializeComponent();
    }

    private void LogEvent(string message)
    {
        _eventCount++;
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        EventLog.Text = $"[{timestamp}] {_eventCount}. {message}\n{EventLog.Text}";
    }

    private void OnDefaultButtonClicked(object? sender, EventArgs e)
    {
        LogEvent("Default Button clicked");
    }

    private void OnDefaultButtonPressed(object? sender, EventArgs e)
    {
        LogEvent("Default Button pressed");
    }

    private void OnDefaultButtonReleased(object? sender, EventArgs e)
    {
        LogEvent("Default Button released");
    }

    private void OnTextButtonClicked(object? sender, EventArgs e)
    {
        LogEvent("Text Button clicked");
    }

    private void OnPrimaryClicked(object? sender, EventArgs e)
    {
        LogEvent("Primary button clicked");
    }

    private void OnSuccessClicked(object? sender, EventArgs e)
    {
        LogEvent("Success button clicked");
    }

    private void OnWarningClicked(object? sender, EventArgs e)
    {
        LogEvent("Warning button clicked");
    }

    private void OnDangerClicked(object? sender, EventArgs e)
    {
        LogEvent("Danger button clicked");
    }

    private void OnPurpleClicked(object? sender, EventArgs e)
    {
        LogEvent("Purple button clicked");
    }

    private void OnEnabledClicked(object? sender, EventArgs e)
    {
        LogEvent("Enabled button clicked");
    }

    private void OnToggleClicked(object? sender, EventArgs e)
    {
        DisabledButton.IsEnabled = !DisabledButton.IsEnabled;
        DisabledButton.Text = DisabledButton.IsEnabled ? "Now Enabled!" : "Disabled Button";
        LogEvent($"Toggled button to: {(DisabledButton.IsEnabled ? "Enabled" : "Disabled")}");
    }

    private void OnWideClicked(object? sender, EventArgs e)
    {
        LogEvent("Wide button clicked");
    }

    private void OnTallClicked(object? sender, EventArgs e)
    {
        LogEvent("Tall button clicked");
    }

    private void OnRoundClicked(object? sender, EventArgs e)
    {
        LogEvent("Round button clicked");
    }
}
