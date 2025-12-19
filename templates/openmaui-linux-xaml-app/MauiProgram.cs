using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using OpenMaui.Platform.Linux.Hosting;

namespace OpenMauiXamlApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseOpenMauiLinux()  // Enable Linux platform with full XAML support
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        return builder.Build();
    }
}
