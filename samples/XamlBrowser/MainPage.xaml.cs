using System;
using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace XamlBrowser;

public partial class MainPage : ContentPage
{
    private const string HomeUrl = "https://openmaui.net";

    public MainPage()
    {
        InitializeComponent();
        AddressBar.Text = HomeUrl;
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        Console.WriteLine($"[MainPage] OnBackClicked, CanGoBack={BrowserWebView.CanGoBack}");
        if (BrowserWebView.CanGoBack)
        {
            BrowserWebView.GoBack();
        }
    }

    private void OnForwardClicked(object? sender, EventArgs e)
    {
        Console.WriteLine($"[MainPage] OnForwardClicked, CanGoForward={BrowserWebView.CanGoForward}");
        if (BrowserWebView.CanGoForward)
        {
            BrowserWebView.GoForward();
        }
    }

    private void OnRefreshClicked(object? sender, EventArgs e)
    {
        Console.WriteLine("[MainPage] OnRefreshClicked");
        BrowserWebView.Reload();
    }

    private void OnStopClicked(object? sender, EventArgs e)
    {
        LoadingProgress.IsVisible = false;
        StatusLabel.Text = "Stopped";
    }

    private void OnHomeClicked(object? sender, EventArgs e)
    {
        NavigateTo(HomeUrl);
    }

    private void OnAddressBarCompleted(object? sender, EventArgs e)
    {
        NavigateTo(AddressBar.Text);
    }

    private void OnGoClicked(object? sender, EventArgs e)
    {
        NavigateTo(AddressBar.Text);
    }

    private void NavigateTo(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        // Add protocol if missing
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            // If it looks like a URL (contains dot and no spaces), add https
            // Otherwise treat as a search query
            url = url.Contains('.') && !url.Contains(' ')
                ? "https://" + url
                : "https://www.google.com/search?q=" + Uri.EscapeDataString(url);
        }

        AddressBar.Text = url;
        BrowserWebView.Source = new UrlWebViewSource { Url = url };
    }

    private void OnWebViewNavigating(object? sender, WebNavigatingEventArgs e)
    {
        Console.WriteLine("[MainPage] Navigating to: " + e.Url);
        StatusLabel.Text = $"Loading {e.Url}...";

        // Reset and show progress bar with animation
        LoadingProgress.AbortAnimation("Progress");
        LoadingProgress.Progress = 0;
        LoadingProgress.IsVisible = true;

        // Animate progress from 0 to 90%
        LoadingProgress.Animate("Progress",
            new Animation(v => LoadingProgress.Progress = v, 0, 0.9),
            length: 2000,
            easing: Easing.CubicOut);

        AddressBar.Text = e.Url;
    }

    private void OnWebViewNavigated(object? sender, WebNavigatedEventArgs e)
    {
        Console.WriteLine($"[MainPage] Navigated: {e.Url} - Result: {e.Result}");

        StatusLabel.Text = e.Result == WebNavigationResult.Success ? "Done" : $"Error: {e.Result}";

        // Complete progress bar
        LoadingProgress.AbortAnimation("Progress");
        LoadingProgress.Progress = 1;
        AddressBar.Text = e.Url;

        // Hide progress bar after a short delay
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(300), () =>
        {
            LoadingProgress.IsVisible = false;
            LoadingProgress.Progress = 0;
        });
    }

    private void OnThemeToggleClicked(object? sender, EventArgs e)
    {
        if (Application.Current is BrowserApp app)
        {
            app.ToggleTheme();
            Console.WriteLine($"[MainPage] Theme changed to: {Application.Current.UserAppTheme}");
        }
    }
}
