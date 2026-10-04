using Microsoft.Maui.Platform.Linux;

namespace ParityHarness;

public static class Program
{
    public static void Main(string[] args)
    {
        HarnessOptions.Parse(args);
        var app = MauiProgram.CreateMauiApp();
        LinuxApplication.Run(app, args);
    }
}
