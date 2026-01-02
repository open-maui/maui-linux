using Microsoft.Maui.Controls;

namespace ShellDemo;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        MainPage = new AppShell();
    }
}
