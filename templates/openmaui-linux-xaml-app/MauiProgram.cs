using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform.Linux.Hosting;

namespace OpenMauiXamlApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            // Backend selection (use ONE of UseLinux / UseX11 / UseWayland):
            //   .UseLinux()    auto-detect from session (current default)
            //   .UseX11()      force X11/XWayland — most stable, recommended for WebView-heavy apps
            //   .UseWayland()  prefer native Wayland, auto-fallback to X11 if unavailable
            // All three are no-ops on Windows/Android/iOS, so this stays cross-platform safe.
            .UseLinux()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        return builder.Build();
    }
}
