using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace TodoApp;

public class TodoService
{
    private static TodoService? _instance;
    public static TodoService Instance => _instance ??= new TodoService();

    public ObservableCollection<TodoItem> Todos { get; } = new();

    private int _nextId = 1;

    private TodoService()
    {
        // Add sample data
        AddTodo("Learn OpenMaui Linux",
            "Explore the SkiaSharp-based rendering engine for .NET MAUI on Linux desktop. " +
            "This is a very long description that should wrap to multiple lines and demonstrate " +
            "the ellipsis truncation feature when MaxLines is set to 2.");
        AddTodo("Build amazing apps",
            "Create cross-platform applications that run on Windows, macOS, iOS, Android, and Linux! " +
            "With OpenMaui, you can write once and deploy everywhere.");
        AddTodo("Share with the community",
            "Contribute to the open-source project and help others build great Linux apps. " +
            "Join our growing community of developers who are passionate about bringing .NET MAUI to Linux.");
    }

    public void AddTodo(string title, string notes)
    {
        var todo = new TodoItem
        {
            Id = _nextId++,
            Title = title,
            Notes = notes,
            Index = Todos.Count
        };
        Todos.Add(todo);
    }

    public void RemoveTodo(TodoItem item)
    {
        Todos.Remove(item);
        RefreshIndexes();
    }

    public void RefreshIndexes()
    {
        for (int i = 0; i < Todos.Count; i++)
        {
            Todos[i].Index = i;
        }
    }

    public int CompletedCount => Todos.Count(t => t.IsCompleted);
    public int TotalCount => Todos.Count;
}
