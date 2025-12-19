using OpenMaui.Platform.Linux;

namespace OpenMauiXamlApp;

public class Program
{
    public static void Main(string[] args)
    {
        // Create the MAUI app using standard MAUI bootstrapping
        var app = MauiProgram.CreateMauiApp();

        // Run with Linux platform
        // This connects MAUI's virtual views to our Skia platform views
        LinuxApplication.Run(app);
    }
}
