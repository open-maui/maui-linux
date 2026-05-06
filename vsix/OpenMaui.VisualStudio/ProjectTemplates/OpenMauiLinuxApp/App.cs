using Microsoft.Maui.Controls;

namespace $safeprojectname$;

public class App : Application
{
    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new MainPage())
        {
            Title = "$projectname$"
        };
    }
}
