// MauiProgram.cs - MAUI app configuration

using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform.Linux.Hosting;

namespace TodoApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        // Configure the app
        builder.UseMauiApp<App>();

        // Add Linux platform support with all handlers
        builder.UseLinux();

        return builder.Build();
    }
}
