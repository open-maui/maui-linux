using ParityHarness.Dump;
using ParityHarness.Gallery;

namespace ParityHarness;

public class App : Application
{
    protected override Window CreateWindow(IActivationState? activationState)
    {
        Page start;
        if (HarnessOptions.DumpMode)
        {
            // A blank sizing page; the runner swaps in each gallery page.
            start = new ContentPage { AutomationId = "SizingPage", BackgroundColor = Colors.White };
        }
        else if (HarnessOptions.StartPage != null && GalleryCatalog.Find(HarnessOptions.StartPage) is { } entry)
        {
            start = entry.Create();
        }
        else
        {
            start = new IndexPage();
        }

        var window = new Window(start)
        {
            Title = "ParityHarness",
            Width = HarnessOptions.Width,
            Height = HarnessOptions.Height,
        };

        if (HarnessOptions.DumpMode)
        {
            bool started = false;
            window.Created += (_, _) =>
            {
                if (started) return;
                started = true;
                var runner = new DumpRunner(window);
                window.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(300), () => _ = runner.RunAsync());
            };
        }

        return window;
    }
}

/// <summary>Interactive mode: one button per gallery page (relaunch to come back).</summary>
internal sealed class IndexPage : ContentPage
{
    public IndexPage()
    {
        Title = "ParityHarness";
        var stack = new VerticalStackLayout { Spacing = 6, Padding = 16 };
        foreach (var entry in GalleryCatalog.All)
        {
            var e = entry;
            var b = new Button { Text = e.Name, FontFamily = Ui.FontFamily, HorizontalOptions = LayoutOptions.Start };
            b.Clicked += (_, _) =>
            {
                if (Window != null) Window.Page = e.Create();
            };
            stack.Children.Add(b);
        }
        Content = new ScrollView { Content = stack };
    }
}
