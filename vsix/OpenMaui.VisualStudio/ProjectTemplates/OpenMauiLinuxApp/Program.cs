using OpenMaui.Platform.Linux;

namespace $safeprojectname$;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseOpenMauiLinux();

        var mauiApp = builder.Build();
        LinuxApplication.Run(mauiApp, args);
    }
}
