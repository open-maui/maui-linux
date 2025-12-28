// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform.Linux;
using Microsoft.Maui.Platform.Linux.Hosting;

namespace WebViewDemo;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        builder.UseLinux();
        return builder.Build();
    }
}

public static class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("[Program] Starting WebView Demo");

        var app = MauiProgram.CreateMauiApp();
        LinuxApplication.Run(app, args);
    }
}

public class App : Application
{
    public App()
    {
        MainPage = new NavigationPage(new WebViewPage())
        {
            Title = "WebView Demo"
        };
    }
}

public class WebViewPage : ContentPage
{
    private readonly WebView _webView;
    private readonly Entry _urlEntry;
    private readonly Label _statusLabel;

    public WebViewPage()
    {
        Title = "WebView Demo";

        _webView = new WebView
        {
            HeightRequest = 400,
            VerticalOptions = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill,
            Source = new UrlWebViewSource { Url = "https://dotnet.microsoft.com" }
        };

        _webView.Navigating += OnNavigating;
        _webView.Navigated += OnNavigated;

        _urlEntry = new Entry
        {
            Placeholder = "Enter URL...",
            Text = "https://dotnet.microsoft.com",
            HorizontalOptions = LayoutOptions.Fill
        };
        _urlEntry.Completed += OnUrlSubmitted;

        _statusLabel = new Label
        {
            Text = "Ready",
            TextColor = Colors.Gray,
            FontSize = 12
        };

        var goButton = new Button
        {
            Text = "Go",
            WidthRequest = 60
        };
        goButton.Clicked += (s, e) => Navigate();

        var backButton = new Button
        {
            Text = "Back",
            WidthRequest = 60
        };
        backButton.Clicked += (s, e) => _webView.GoBack();

        var forwardButton = new Button
        {
            Text = "Forward",
            WidthRequest = 80
        };
        forwardButton.Clicked += (s, e) => _webView.GoForward();

        var reloadButton = new Button
        {
            Text = "Reload",
            WidthRequest = 70
        };
        reloadButton.Clicked += (s, e) => _webView.Reload();

        var loadHtmlButton = new Button
        {
            Text = "Load HTML",
            WidthRequest = 100
        };
        loadHtmlButton.Clicked += OnLoadHtmlClicked;

        var evalJsButton = new Button
        {
            Text = "Run JS",
            WidthRequest = 80
        };
        evalJsButton.Clicked += OnEvalJsClicked;

        // Navigation bar
        var navBar = new HorizontalStackLayout
        {
            Spacing = 5,
            Children = { backButton, forwardButton, reloadButton }
        };

        // URL bar
        var urlBar = new HorizontalStackLayout
        {
            Spacing = 5,
            Children = { _urlEntry, goButton }
        };

        // Action buttons
        var actionBar = new HorizontalStackLayout
        {
            Spacing = 5,
            Children = { loadHtmlButton, evalJsButton }
        };

        Content = new VerticalStackLayout
        {
            Padding = 10,
            Spacing = 10,
            Children =
            {
                new Label { Text = "WebView Demo - WebKitGTK", FontSize = 20, FontAttributes = FontAttributes.Bold },
                navBar,
                urlBar,
                _webView,
                actionBar,
                _statusLabel
            }
        };
    }

    private void Navigate()
    {
        var url = _urlEntry.Text?.Trim();
        if (string.IsNullOrEmpty(url))
            return;

        // Add https:// if not present
        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            url = "https://" + url;

        _webView.Source = new UrlWebViewSource { Url = url };
        _urlEntry.Text = url;
    }

    private void OnUrlSubmitted(object? sender, EventArgs e)
    {
        Navigate();
    }

    private void OnNavigating(object? sender, WebNavigatingEventArgs e)
    {
        _statusLabel.Text = $"Loading: {e.Url}";
        Console.WriteLine($"[WebViewPage] Navigating to: {e.Url}");
    }

    private void OnNavigated(object? sender, WebNavigatedEventArgs e)
    {
        _statusLabel.Text = e.Result == WebNavigationResult.Success
            ? $"Loaded: {e.Url}"
            : $"Failed: {e.Result}";

        _urlEntry.Text = e.Url;
        Console.WriteLine($"[WebViewPage] Navigated: {e.Result} - {e.Url}");
    }

    private void OnLoadHtmlClicked(object? sender, EventArgs e)
    {
        var html = @"
<!DOCTYPE html>
<html>
<head>
    <title>OpenMaui WebView</title>
    <style>
        body {
            font-family: 'Segoe UI', Arial, sans-serif;
            margin: 40px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            min-height: 100vh;
        }
        h1 {
            font-size: 2.5em;
            margin-bottom: 20px;
        }
        p {
            font-size: 1.2em;
            line-height: 1.6;
        }
        .feature-list {
            background: rgba(255,255,255,0.1);
            padding: 20px;
            border-radius: 10px;
            margin-top: 20px;
        }
        li {
            margin: 10px 0;
            font-size: 1.1em;
        }
        button {
            background: #4CAF50;
            color: white;
            border: none;
            padding: 15px 30px;
            font-size: 1.1em;
            border-radius: 5px;
            cursor: pointer;
            margin-top: 20px;
        }
        button:hover {
            background: #45a049;
        }
    </style>
</head>
<body>
    <h1>Hello from OpenMaui Linux!</h1>
    <p>This HTML content is rendered by WebKitGTK inside your .NET MAUI application.</p>

    <div class='feature-list'>
        <h2>WebView Features:</h2>
        <ul>
            <li>Full HTML5 support</li>
            <li>CSS3 animations and transitions</li>
            <li>JavaScript execution</li>
            <li>Navigation history (back/forward)</li>
            <li>WebGL and canvas support</li>
        </ul>
    </div>

    <button onclick=""alert('Hello from JavaScript!')"">Click Me!</button>

    <p style='margin-top: 30px; opacity: 0.8;'>
        Powered by WebKitGTK - the same engine used by GNOME Web (Epiphany)
    </p>
</body>
</html>";

        _webView.Source = new HtmlWebViewSource { Html = html };
        _statusLabel.Text = "Loaded custom HTML";
    }

    private async void OnEvalJsClicked(object? sender, EventArgs e)
    {
        try
        {
            var result = await _webView.EvaluateJavaScriptAsync("document.title");
            _statusLabel.Text = $"JS Result: {result ?? "(null)"}";
            Console.WriteLine($"[WebViewPage] JS Eval result: {result}");
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"JS Error: {ex.Message}";
        }
    }
}
