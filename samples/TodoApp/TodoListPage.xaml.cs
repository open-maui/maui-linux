// TodoListPage - Main page for viewing todos with XAML support

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System.Globalization;

namespace TodoApp;

public partial class TodoListPage : ContentPage
{
    private readonly TodoService _service = TodoService.Instance;

    public TodoListPage()
    {
        Console.WriteLine("[TodoListPage] Constructor starting");
        InitializeComponent();

        TodoCollectionView.ItemsSource = _service.Todos;
        UpdateStats();

        Console.WriteLine("[TodoListPage] Constructor finished");
    }

    protected override void OnAppearing()
    {
        Console.WriteLine("[TodoListPage] OnAppearing called - refreshing CollectionView");
        base.OnAppearing();

        // Refresh indexes for alternating row colors
        _service.RefreshIndexes();

        // Refresh the collection view
        TodoCollectionView.ItemsSource = null;
        TodoCollectionView.ItemsSource = _service.Todos;
        Console.WriteLine($"[TodoListPage] ItemsSource set with {_service.Todos.Count} items");
        UpdateStats();
    }

    private async void OnAddClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new NewTodoPage());
    }

    private async void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        try
        {
            Console.WriteLine($"[TodoListPage] OnSelectionChanged: {e.CurrentSelection.Count} items selected");
            if (e.CurrentSelection.FirstOrDefault() is TodoItem todo)
            {
                Console.WriteLine($"[TodoListPage] Navigating to TodoDetailPage for: {todo.Title}");
                TodoCollectionView.SelectedItem = null; // Deselect
                var detailPage = new TodoDetailPage(todo);
                Console.WriteLine($"[TodoListPage] Created TodoDetailPage, pushing...");
                await Navigation.PushAsync(detailPage);
                Console.WriteLine($"[TodoListPage] Navigation complete");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TodoListPage] EXCEPTION in OnSelectionChanged: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"[TodoListPage] Stack trace: {ex.StackTrace}");
        }
    }

    private void UpdateStats()
    {
        var completed = _service.CompletedCount;
        var total = _service.TotalCount;

        if (total == 0)
        {
            StatsLabel.Text = "";
        }
        else
        {
            StatsLabel.Text = $"{completed} of {total} completed";
        }
    }
}

/// <summary>
/// Converter for alternating row background colors.
/// </summary>
public class AlternatingRowColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int index)
        {
            return index % 2 == 0 ? Colors.White : Color.FromArgb("#F5F5F5");
        }
        return Colors.White;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for completed task text color and indicator color.
/// </summary>
public class CompletedToColorConverter : IValueConverter
{
    // Define colors
    private static readonly Color PrimaryColor = Color.FromArgb("#5C6BC0");
    private static readonly Color AccentColor = Color.FromArgb("#26A69A");
    private static readonly Color CompletedColor = Color.FromArgb("#9E9E9E");
    private static readonly Color TextPrimary = Color.FromArgb("#212121");
    private static readonly Color TextSecondary = Color.FromArgb("#757575");

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isCompleted = value is bool b && b;
        string param = parameter as string ?? "";

        // Indicator bar color
        if (param == "indicator")
        {
            return isCompleted ? CompletedColor : AccentColor;
        }

        // Text colors
        if (isCompleted)
        {
            return CompletedColor;
        }
        else
        {
            return param == "notes" ? TextSecondary : TextPrimary;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for completed task text decorations (strikethrough).
/// </summary>
public class CompletedToTextDecorationsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isCompleted = value is bool b && b;
        return isCompleted ? TextDecorations.Strikethrough : TextDecorations.None;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for completed task opacity (slightly faded when complete).
/// </summary>
public class CompletedToOpacityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isCompleted = value is bool b && b;
        return isCompleted ? 0.7 : 1.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
