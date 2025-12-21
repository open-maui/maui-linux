// TodoDetailPage - View and edit a todo item

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;

namespace TodoApp;

public partial class TodoDetailPage : ContentPage
{
    private readonly TodoItem _todo;
    private readonly TodoService _service = TodoService.Instance;

    // Colors for status label
    private static readonly Color AccentColor = Color.FromArgb("#26A69A");
    private static readonly Color TextPrimary = Color.FromArgb("#212121");

    public TodoDetailPage(TodoItem todo)
    {
        try
        {
            Console.WriteLine($"[TodoDetailPage] Constructor starting for: {todo.Title}");
            InitializeComponent();
            Console.WriteLine($"[TodoDetailPage] InitializeComponent complete");

            _todo = todo;

            // Populate fields
            Console.WriteLine($"[TodoDetailPage] Setting TitleEntry.Text");
            TitleEntry.Text = _todo.Title;
            Console.WriteLine($"[TodoDetailPage] Setting NotesEditor.Text");
            NotesEditor.Text = _todo.Notes;
            Console.WriteLine($"[TodoDetailPage] Setting CompletedCheckBox.IsChecked");
            CompletedCheckBox.IsChecked = _todo.IsCompleted;
            Console.WriteLine($"[TodoDetailPage] Calling UpdateStatusLabel");
            UpdateStatusLabel(_todo.IsCompleted);
            Console.WriteLine($"[TodoDetailPage] Setting CreatedLabel.Text");
            CreatedLabel.Text = $"Created {_todo.CreatedAt:MMMM d, yyyy} at {_todo.CreatedAt:h:mm tt}";
            Console.WriteLine($"[TodoDetailPage] Constructor complete");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TodoDetailPage] EXCEPTION in constructor: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"[TodoDetailPage] Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    private void OnCompletedChanged(object? sender, Microsoft.Maui.Controls.CheckedChangedEventArgs e)
    {
        Console.WriteLine($"[TodoDetailPage] OnCompletedChanged: {e.Value}");
        UpdateStatusLabel(e.Value);
    }

    private void UpdateStatusLabel(bool isCompleted)
    {
        if (StatusLabel == null)
        {
            Console.WriteLine($"[TodoDetailPage] UpdateStatusLabel: StatusLabel is null, skipping");
            return;
        }
        Console.WriteLine($"[TodoDetailPage] UpdateStatusLabel: setting to {(isCompleted ? "Completed" : "In Progress")}");
        StatusLabel.Text = isCompleted ? "Completed" : "In Progress";
        StatusLabel.TextColor = isCompleted ? AccentColor : TextPrimary;
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        _todo.Title = TitleEntry.Text ?? "";
        _todo.Notes = NotesEditor.Text ?? "";
        _todo.IsCompleted = CompletedCheckBox.IsChecked;

        await Navigation.PopAsync();
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        // Show confirmation dialog
        var confirmed = await LinuxDialogService.ShowAlertAsync(
            "Delete Task",
            $"Are you sure you want to delete \"{_todo.Title}\"? This action cannot be undone.",
            "Delete",
            "Cancel");

        if (confirmed)
        {
            _service.DeleteTodo(_todo);
            await Navigation.PopAsync();
        }
    }
}
