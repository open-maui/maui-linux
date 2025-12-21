// MauiProgram.cs - Shared MAUI app configuration
// Works across all platforms (iOS, Android, Windows, Linux)

using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform.Linux.Hosting;

namespace ShellDemo;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        
        // Configure the app (shared across all platforms)
        builder.UseMauiApp<App>();
        
        // Add Linux platform support
        // On other platforms, this would be iOS/Android/Windows specific
        builder.UseLinux();
        
        return builder.Build();
    }
}
