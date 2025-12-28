// DetailPage - Demonstrates push/pop navigation

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Hosting;

namespace ShellDemo;

/// <summary>
/// A detail page that can be pushed onto the navigation stack.
/// </summary>
public class DetailPage : ContentPage
{
    private readonly string _itemName;

    public DetailPage() : this("Detail Item")
    {
    }

    public DetailPage(string itemName)
    {
        _itemName = itemName;
        Title = "Detail Page";

        Content = new VerticalStackLayout
        {
            Padding = new Thickness(30),
            Spacing = 20,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = "Pushed Page",
                    FontSize = 28,
                    FontAttributes = FontAttributes.Bold,
                    HorizontalOptions = LayoutOptions.Center,
                    TextColor = Color.FromArgb("#9C27B0")
                },

                new Label
                {
                    Text = $"You navigated to: {_itemName}",
                    FontSize = 16,
                    HorizontalOptions = LayoutOptions.Center
                },

                new Label
                {
                    Text = "This page was pushed onto the navigation stack using Shell.Current.GoToAsync()",
                    FontSize = 14,
                    TextColor = Colors.Gray,
                    HorizontalTextAlignment = TextAlignment.Center,
                    LineBreakMode = LineBreakMode.WordWrap
                },

                new BoxView
                {
                    HeightRequest = 2,
                    Color = Color.FromArgb("#E0E0E0"),
                    Margin = new Thickness(0, 20)
                },

                CreateBackButton(),

                new Label
                {
                    Text = "Use the back button above or the hardware/gesture back to pop this page",
                    FontSize = 12,
                    TextColor = Colors.Gray,
                    HorizontalTextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 20, 0, 0)
                }
            }
        };
    }

    private Button CreateBackButton()
    {
        var backBtn = new Button
        {
            Text = "Go Back (Pop)",
            BackgroundColor = Color.FromArgb("#9C27B0"),
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center,
            Padding = new Thickness(30, 10)
        };

        backBtn.Clicked += (s, e) =>
        {
            // Pop this page off the navigation stack using LinuxViewRenderer
            Console.WriteLine("[DetailPage] Go Back clicked");
            var success = LinuxViewRenderer.PopPage();
            Console.WriteLine($"[DetailPage] PopPage result: {success}");
        };

        return backBtn;
    }
}

/// <summary>
/// Query property for passing data to DetailPage.
/// </summary>
[QueryProperty(nameof(ItemName), "item")]
public class DetailPageWithQuery : DetailPage
{
    private string _itemName = "Item";

    public string ItemName
    {
        get => _itemName;
        set
        {
            _itemName = value;
            // Update the title when the property is set
            Title = $"Detail: {value}";
        }
    }

    public DetailPageWithQuery() : base()
    {
    }
}
