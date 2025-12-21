// TodoApp - Main Application with NavigationPage

using Microsoft.Maui.Controls;

namespace TodoApp;

public class App : Application
{
    public static NavigationPage? NavigationPage { get; private set; }

    public App()
    {
        NavigationPage = new NavigationPage(new TodoListPage())
        {
            Title = "OpenMaui Todo App",
            BarBackgroundColor = Color.FromArgb("#2196F3"),
            BarTextColor = Colors.White
        };
        MainPage = NavigationPage;
    }
}
