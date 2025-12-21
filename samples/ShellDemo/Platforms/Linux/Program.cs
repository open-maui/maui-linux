// Platforms/Linux/Program.cs - Linux platform entry point
// Same pattern as Android's MainActivity or iOS's AppDelegate

using Microsoft.Maui.Platform.Linux;
using Microsoft.Maui.Platform.Linux.Hosting;

namespace ShellDemo;

class Program
{
    static void Main(string[] args)
    {
        // Create the shared MAUI app
        var app = MauiProgram.CreateMauiApp();
        
        // Run on Linux platform
        LinuxApplication.Run(app, args);
    }
}
