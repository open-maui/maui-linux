using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace ShellDemo.Pages;

public partial class DialogsPage : ContentPage
{
    private int _eventCount = 0;

    public DialogsPage()
    {
        InitializeComponent();
    }

    private void LogEvent(string message)
    {
        _eventCount++;
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        EventLog.Text = $"[{timestamp}] {_eventCount}. {message}\n{EventLog.Text}";
    }

    // Alert Dialogs
    private async void OnSimpleAlertClicked(object? sender, EventArgs e)
    {
        await DisplayAlert("Information", "This is a simple alert dialog.", "OK");
        AlertResultLabel.Text = "Result: Alert dismissed";
        LogEvent("Simple alert shown");
    }

    private async void OnConfirmationClicked(object? sender, EventArgs e)
    {
        bool answer = await DisplayAlert("Confirm", "Do you want to proceed with this action?", "Yes", "No");
        AlertResultLabel.Text = $"Result: {(answer ? "Yes" : "No")}";
        LogEvent($"Confirmation: {(answer ? "Yes" : "No")}");
    }

    // Action Sheets
    private async void OnActionSheetClicked(object? sender, EventArgs e)
    {
        string action = await DisplayActionSheet("Choose an option", "Cancel", null, "Option 1", "Option 2", "Option 3");
        ActionResultLabel.Text = $"Selection: {action ?? "None"}";
        LogEvent($"Action sheet: {action ?? "Cancelled"}");
    }

    private async void OnDestructiveActionSheetClicked(object? sender, EventArgs e)
    {
        string action = await DisplayActionSheet("Danger Zone", "Cancel", "Delete Everything", "Edit", "Share", "Archive");
        ActionResultLabel.Text = $"Selection: {action ?? "None"}";
        LogEvent($"Destructive action: {action ?? "Cancelled"}");
    }

    // Prompts
    private async void OnTextPromptClicked(object? sender, EventArgs e)
    {
        string result = await DisplayPromptAsync("Name", "What is your name?", placeholder: "Enter name");
        PromptResultLabel.Text = $"Input: {result ?? "(cancelled)"}";
        LogEvent($"Text prompt: {result ?? "Cancelled"}");
    }

    private async void OnNumericPromptClicked(object? sender, EventArgs e)
    {
        string result = await DisplayPromptAsync("Age", "Enter your age:", keyboard: Keyboard.Numeric, placeholder: "0");
        PromptResultLabel.Text = $"Input: {result ?? "(cancelled)"}";
        LogEvent($"Numeric prompt: {result ?? "Cancelled"}");
    }

    // File Pickers
    private async void OnPickFileClicked(object? sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync();
            if (result != null)
            {
                FileResultLabel.Text = $"Selected: {result.FileName}";
                LogEvent($"File: {result.FileName}");
            }
            else
            {
                FileResultLabel.Text = "Selected: (cancelled)";
                LogEvent("File picker cancelled");
            }
        }
        catch (Exception ex)
        {
            FileResultLabel.Text = $"Error: {ex.Message}";
            LogEvent($"File picker error: {ex.Message}");
        }
    }

    private async void OnPickMultipleFilesClicked(object? sender, EventArgs e)
    {
        try
        {
            var results = await FilePicker.Default.PickMultipleAsync();
            if (results != null && results.Any())
            {
                FileResultLabel.Text = $"Selected: {results.Count()} files";
                LogEvent($"Multiple files: {results.Count()} selected");
            }
            else
            {
                FileResultLabel.Text = "Selected: (cancelled)";
                LogEvent("Multiple file picker cancelled");
            }
        }
        catch (Exception ex)
        {
            FileResultLabel.Text = $"Error: {ex.Message}";
            LogEvent($"Multiple file picker error: {ex.Message}");
        }
    }

    private async void OnPickImageClicked(object? sender, EventArgs e)
    {
        try
        {
            var options = new PickOptions
            {
                PickerTitle = "Select an image",
                FileTypes = FilePickerFileType.Images
            };
            var result = await FilePicker.Default.PickAsync(options);
            if (result != null)
            {
                FileResultLabel.Text = $"Selected: {result.FileName}";
                LogEvent($"Image: {result.FileName}");
            }
            else
            {
                FileResultLabel.Text = "Selected: (cancelled)";
                LogEvent("Image picker cancelled");
            }
        }
        catch (Exception ex)
        {
            FileResultLabel.Text = $"Error: {ex.Message}";
            LogEvent($"Image picker error: {ex.Message}");
        }
    }

    private async void OnPickFolderClicked(object? sender, EventArgs e)
    {
        try
        {
            var result = await FolderPicker.Default.PickAsync();
            if (result.IsSuccessful)
            {
                FileResultLabel.Text = $"Selected: {result.Folder?.Path}";
                LogEvent($"Folder: {result.Folder?.Name}");
            }
            else
            {
                FileResultLabel.Text = "Selected: (cancelled)";
                LogEvent("Folder picker cancelled");
            }
        }
        catch (Exception ex)
        {
            FileResultLabel.Text = $"Error: {ex.Message}";
            LogEvent($"Folder picker error: {ex.Message}");
        }
    }
}
