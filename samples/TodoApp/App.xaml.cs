using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace TodoApp;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        UserAppTheme = AppTheme.Light;
        MainPage = new NavigationPage(new TodoListPage());
    }
}
