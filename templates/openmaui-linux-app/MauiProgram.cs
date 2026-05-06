using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform.Linux.Hosting;

namespace OpenMauiLinuxApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseLinux();

        return builder.Build();
    }
}
