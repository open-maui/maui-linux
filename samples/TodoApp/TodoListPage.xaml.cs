using System;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace TodoApp;

public partial class TodoListPage : ContentPage
{
    private readonly TodoService _service = TodoService.Instance;
    private bool _isNavigating;

    public TodoListPage()
    {
        InitializeComponent();
        TodoCollectionView.ItemsSource = _service.Todos;
        UpdateStats();
        ThemeSwitch.IsToggled = Application.Current?.UserAppTheme == AppTheme.Dark;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _isNavigating = false;
        _service.RefreshIndexes();
        TodoCollectionView.ItemsSource = null;
        TodoCollectionView.ItemsSource = _service.Todos;
        UpdateStats();
    }

    private void OnThemeToggled(object? sender, ToggledEventArgs e)
    {
        if (Application.Current != null)
        {
            Application.Current.UserAppTheme = e.Value ? AppTheme.Dark : AppTheme.Light;
            // Refresh to apply theme
            var items = TodoCollectionView.ItemsSource;
            TodoCollectionView.ItemsSource = null;
            TodoCollectionView.ItemsSource = items;
        }
    }

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new NewTodoPage());
    }

    private async void OnItemTapped(object? sender, TappedEventArgs e)
    {
        if (_isNavigating || e.Parameter is not TodoItem todoItem)
            return;

        _isNavigating = true;
        try
        {
            await Navigation.PushAsync(new TodoDetailPage(todoItem));
        }
        catch
        {
            _isNavigating = false;
        }
    }

    private void UpdateStats()
    {
        int completed = _service.CompletedCount;
        int total = _service.TotalCount;
        StatsLabel.Text = total == 0 ? "" : $"Tasks: {completed} of {total}";
    }
}
