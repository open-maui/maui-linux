using Microsoft.Maui.Platform.Linux.Hosting;

namespace TodoApp;

public class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        LinuxProgramHost.Run(args, MauiProgram.CreateMauiApp);
    }
}
