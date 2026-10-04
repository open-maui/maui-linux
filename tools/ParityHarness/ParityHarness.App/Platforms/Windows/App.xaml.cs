using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace ParityHarness.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        HarnessOptions.Parse(Environment.GetCommandLineArgs().Skip(1).ToArray());
        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
