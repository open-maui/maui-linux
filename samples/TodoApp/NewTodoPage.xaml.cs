// NewTodoPage - Create a new todo item

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace TodoApp;

public partial class NewTodoPage : ContentPage
{
    private readonly TodoService _service = TodoService.Instance;

    public NewTodoPage()
    {
        InitializeComponent();
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var title = TitleEntry.Text?.Trim();

        if (string.IsNullOrEmpty(title))
        {
            TitleEntry.Placeholder = "Title is required!";
            TitleEntry.PlaceholderColor = Colors.Red;
            return;
        }

        _service.AddTodo(title, NotesEditor.Text ?? "");
        await Navigation.PopAsync();
    }
}
