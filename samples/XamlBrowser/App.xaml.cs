using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace XamlBrowser;

public partial class BrowserApp : Application
{
    public BrowserApp()
    {
        InitializeComponent();
        UserAppTheme = AppTheme.Dark;
        MainPage = new MainPage();
    }

    public void ToggleTheme()
    {
        UserAppTheme = UserAppTheme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light;
    }
}
