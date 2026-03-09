using Microsoft.Maui.Platform.Linux;

namespace OpenMauiLinuxApp;

public class Program
{
    public static void Main(string[] args)
    {
        var app = MauiProgram.CreateMauiApp();
        LinuxApplication.Run(app, args);
    }
}
