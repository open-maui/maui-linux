// TodoService - Manages todo items

using System.Collections.ObjectModel;

namespace TodoApp;

public class TodoService
{
    private static TodoService? _instance;
    public static TodoService Instance => _instance ??= new TodoService();

    private int _nextId = 1;

    public ObservableCollection<TodoItem> Todos { get; } = new();

    private TodoService()
    {
        // Add sample todos with varying lengths to test MaxLines=2 with ellipsis
        AddTodo("Learn OpenMaui Linux", "Explore the SkiaSharp-based rendering engine for .NET MAUI on Linux desktop. This is a very long description that should wrap to multiple lines and demonstrate the ellipsis truncation feature when MaxLines is set to 2.");
        AddTodo("Build amazing apps", "Create cross-platform applications that run on Windows, macOS, iOS, Android, and Linux! With OpenMaui, you can write once and deploy everywhere.");
        AddTodo("Share with the community", "Contribute to the open-source project and help others build great Linux apps. Join our growing community of developers who are passionate about bringing .NET MAUI to Linux.");
    }

    public TodoItem AddTodo(string title, string notes = "")
    {
        var todo = new TodoItem
        {
            Id = _nextId++,
            Index = Todos.Count, // Set index for alternating row colors
            Title = title,
            Notes = notes,
            DueDate = DateTime.Today.AddDays(7)
        };
        Todos.Add(todo);
        return todo;
    }

    /// <summary>
    /// Refreshes the Index property on all items for alternating row colors.
    /// </summary>
    public void RefreshIndexes()
    {
        for (int i = 0; i < Todos.Count; i++)
        {
            Todos[i].Index = i;
        }
    }

    public TodoItem? GetTodo(int id)
    {
        return Todos.FirstOrDefault(t => t.Id == id);
    }

    public void DeleteTodo(TodoItem todo)
    {
        Todos.Remove(todo);
    }

    public int CompletedCount => Todos.Count(t => t.IsCompleted);
    public int TotalCount => Todos.Count;
}
