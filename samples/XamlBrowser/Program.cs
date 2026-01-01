using Microsoft.Maui.Platform.Linux;
using Microsoft.Maui.Platform.Linux.Hosting;

namespace XamlBrowser;

public class Program
{
    public static void Main(string[] args)
    {
        LinuxProgramHost.Run(args, MauiProgram.CreateMauiApp, new LinuxApplicationOptions
        {
            Title = "XAML Browser",
            Width = 1280,
            Height = 800,
            UseGtk = true
        });
    }
}
