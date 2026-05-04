using OpenMaui.Platform.Linux;

namespace $safeprojectname$;

public class Program
{
    public static void Main(string[] args)
    {
        var app = new LinuxApplication();

        // Configure the application
        app.Title = "$projectname$";

        // Set the main page
        app.MainPage = new MainPage();

        // Run the application
        app.Run();
    }
}
