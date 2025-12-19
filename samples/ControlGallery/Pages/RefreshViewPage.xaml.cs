namespace ControlGallery.Pages;

public partial class RefreshViewPage : ContentPage
{
    private readonly Random _random = new();
    private readonly string[] _headlines = new[]
    {
        "OpenMaui 1.0 Released!",
        "Linux Desktop Apps Made Easy",
        "SkiaSharp Powers Modern UIs",
        "Cross-Platform Development Grows",
        ".NET 9 Performance Boost",
        "XAML Hot Reload Coming Soon",
        "Wayland Support Expanding",
        "Community Contributions Welcome",
        "New Controls Added Weekly",
        "Accessibility Features Improved"
    };

    public RefreshViewPage()
    {
        InitializeComponent();
        UpdateNews();
    }

    private async void OnRefreshing(object sender, EventArgs e)
    {
        // Simulate network delay
        await Task.Delay(1500);

        UpdateNews();
        LastRefreshLabel.Text = $"Last refreshed: {DateTime.Now:HH:mm:ss}";

        RefreshContainer.IsRefreshing = false;
    }

    private void OnManualRefreshClicked(object sender, EventArgs e)
    {
        RefreshContainer.IsRefreshing = true;
    }

    private void UpdateNews()
    {
        NewsItem1.Text = _headlines[_random.Next(_headlines.Length)];
        NewsItem2.Text = _headlines[_random.Next(_headlines.Length)];
        NewsItem3.Text = _headlines[_random.Next(_headlines.Length)];
    }
}
