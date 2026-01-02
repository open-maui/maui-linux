using System;
using Microsoft.Maui.Controls;

namespace ShellDemo.Pages;

public partial class TextInputPage : ContentPage
{
    public TextInputPage()
    {
        InitializeComponent();
    }

    private void OnNameEntryTextChanged(object? sender, TextChangedEventArgs e)
    {
        EntryOutput.Text = $"You typed: {e.NewTextValue}";
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        SearchOutput.Text = $"Searching: {e.NewTextValue}";
    }

    private void OnSearchButtonPressed(object? sender, EventArgs e)
    {
        SearchOutput.Text = $"Search submitted: {DemoSearchBar.Text}";
    }

    private void OnEditorTextChanged(object? sender, TextChangedEventArgs e)
    {
        var lineCount = string.IsNullOrEmpty(e.NewTextValue) ? 0 : e.NewTextValue.Split('\n').Length;
        EditorOutput.Text = $"Lines: {lineCount}, Characters: {e.NewTextValue?.Length ?? 0}";
    }
}
