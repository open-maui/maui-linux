// AboutPage - Information about OpenMaui Linux

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace ShellDemo;

public class AboutPage : ContentPage
{
    public AboutPage()
    {
        Title = "About";

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20),
                Spacing = 20,
                Children =
                {
                    new Label
                    {
                        Text = "OpenMaui Linux",
                        FontSize = 32,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#1A237E"),
                        HorizontalOptions = LayoutOptions.Center
                    },
                    new Label
                    {
                        Text = "Version 1.0.0",
                        FontSize = 16,
                        TextColor = Colors.Gray,
                        HorizontalOptions = LayoutOptions.Center
                    },
                    new BoxView { HeightRequest = 1, Color = Colors.LightGray },
                    new Label
                    {
                        Text = "OpenMaui Linux brings .NET MAUI to Linux desktops using SkiaSharp for rendering. " +
                               "It provides a native Linux experience while maintaining compatibility with MAUI's cross-platform API.",
                        FontSize = 14,
                        LineBreakMode = LineBreakMode.WordWrap
                    },
                    CreateInfoCard("Platform", "Linux (X11/Wayland)"),
                    CreateInfoCard("Rendering", "SkiaSharp"),
                    CreateInfoCard("Framework", ".NET MAUI"),
                    CreateInfoCard("License", "MIT License"),
                    new BoxView { HeightRequest = 1, Color = Colors.LightGray },
                    new Label
                    {
                        Text = "Features",
                        FontSize = 20,
                        FontAttributes = FontAttributes.Bold
                    },
                    CreateFeatureItem("Full XAML support with styles and resources"),
                    CreateFeatureItem("Shell navigation with flyout menus"),
                    CreateFeatureItem("All standard MAUI controls"),
                    CreateFeatureItem("Data binding and MVVM"),
                    CreateFeatureItem("Keyboard and mouse input"),
                    CreateFeatureItem("High DPI support"),
                    new BoxView { HeightRequest = 1, Color = Colors.LightGray },
                    new Label
                    {
                        Text = "https://github.com/pablotoledo/OpenMaui-Linux",
                        FontSize = 12,
                        TextColor = Colors.Blue,
                        HorizontalOptions = LayoutOptions.Center
                    }
                }
            }
        };
    }

    private Frame CreateInfoCard(string label, string value)
    {
        return new Frame
        {
            CornerRadius = 8,
            Padding = new Thickness(15),
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            HasShadow = false,
            Content = new HorizontalStackLayout
            {
                Children =
                {
                    new Label
                    {
                        Text = label + ":",
                        FontAttributes = FontAttributes.Bold,
                        WidthRequest = 100
                    },
                    new Label
                    {
                        Text = value,
                        TextColor = Colors.Gray
                    }
                }
            }
        };
    }

    private View CreateFeatureItem(string text)
    {
        return new HorizontalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label { Text = "✓", TextColor = Color.FromArgb("#4CAF50"), FontSize = 16 },
                new Label { Text = text, FontSize = 14 }
            }
        };
    }
}
