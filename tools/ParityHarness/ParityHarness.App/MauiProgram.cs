using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
#if OPENMAUI_LINUX
using Microsoft.Maui.Platform.Linux.Hosting;
#endif

namespace ParityHarness;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => fonts.AddFont("OpenSans-Regular.ttf", Ui.FontFamily));
#if OPENMAUI_LINUX
        builder.UseLinux();
#endif
        return builder.Build();
    }
}
