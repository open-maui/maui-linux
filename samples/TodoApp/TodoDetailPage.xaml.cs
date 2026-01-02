using System;
using Microsoft.Maui.Controls;

namespace TodoApp;

public partial class TodoDetailPage : ContentPage
{
    private readonly TodoService _service = TodoService.Instance;
    private readonly TodoItem _item;

    public TodoDetailPage(TodoItem item)
    {
        InitializeComponent();
        _item = item;

        TitleEntry.Text = item.Title;
        NotesEditor.Text = item.Notes;
        CompletedCheckBox.IsChecked = item.IsCompleted;
        CreatedLabel.Text = $"Created {item.CreatedAt:MMMM d, yyyy} at {item.CreatedAt:h:mm tt}";
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        _item.Title = TitleEntry.Text ?? "";
        _item.Notes = NotesEditor.Text ?? "";
        _item.IsCompleted = CompletedCheckBox.IsChecked;
        await Navigation.PopAsync();
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        bool confirm = await DisplayAlert("Delete Task",
            "Are you sure you want to delete this task?",
            "Delete", "Cancel");

        if (confirm)
        {
            _service.RemoveTodo(_item);
            await Navigation.PopAsync();
        }
    }
}
