// Program.cs - Linux platform entry point

using Microsoft.Maui.Platform.Linux;

namespace TodoApp;

class Program
{
    static void Main(string[] args)
    {
        // Redirect console output to a log file for debugging
        var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "todoapp.log");
        using var logWriter = new StreamWriter(logPath, append: false) { AutoFlush = true };
        var multiWriter = new MultiTextWriter(Console.Out, logWriter);
        Console.SetOut(multiWriter);
        Console.SetError(multiWriter);

        // Global exception handler
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            Console.WriteLine($"[FATAL] Unhandled exception: {ex?.GetType().Name}: {ex?.Message}");
            Console.WriteLine($"[FATAL] Stack trace: {ex?.StackTrace}");
            if (ex?.InnerException != null)
            {
                Console.WriteLine($"[FATAL] Inner exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                Console.WriteLine($"[FATAL] Inner stack trace: {ex.InnerException.StackTrace}");
            }
        };

        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            Console.WriteLine($"[FATAL] Unobserved task exception: {e.Exception?.GetType().Name}: {e.Exception?.Message}");
            Console.WriteLine($"[FATAL] Stack trace: {e.Exception?.StackTrace}");
            e.SetObserved(); // Prevent crash
        };

        Console.WriteLine($"[Program] Starting TodoApp at {DateTime.Now}");
        Console.WriteLine($"[Program] Log file: {logPath}");

        try
        {
            // Create the MAUI app with all handlers registered
            var app = MauiProgram.CreateMauiApp();

            // Run on Linux platform
            LinuxApplication.Run(app, args);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FATAL] Exception in Main: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"[FATAL] Stack trace: {ex.StackTrace}");
            throw;
        }
    }
}

// Helper to write to both console and file
class MultiTextWriter : TextWriter
{
    private readonly TextWriter[] _writers;
    public MultiTextWriter(params TextWriter[] writers) => _writers = writers;
    public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;
    public override void Write(char value) { foreach (var w in _writers) w.Write(value); }
    public override void WriteLine(string? value) { foreach (var w in _writers) w.WriteLine(value); }
    public override void Flush() { foreach (var w in _writers) w.Flush(); }
}
