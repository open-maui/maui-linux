using Microsoft.Maui.Controls;

namespace ShellDemo;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("detail", typeof(Pages.DetailPage));
    }
}
