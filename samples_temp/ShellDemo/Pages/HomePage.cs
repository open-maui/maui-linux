// HomePage - Welcome page for the demo

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Hosting;

namespace ShellDemo;

public class HomePage : ContentPage
{
    public HomePage()
    {
        Title = "Home";

        Content = new ScrollView
        {
            Orientation = ScrollOrientation.Both,  // Enable horizontal scrolling when window is too narrow
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(30),
                Spacing = 20,
                Children =
                {
                    new Label
                    {
                        Text = "OpenMaui Linux",
                        FontSize = 32,
                        FontAttributes = FontAttributes.Bold,
                        HorizontalOptions = LayoutOptions.Center,
                        TextColor = Color.FromArgb("#2196F3")
                    },

                    new Label
                    {
                        Text = "Controls Demo",
                        FontSize = 20,
                        HorizontalOptions = LayoutOptions.Center,
                        TextColor = Colors.Gray
                    },

                    new BoxView
                    {
                        HeightRequest = 2,
                        Color = Color.FromArgb("#E0E0E0"),
                        Margin = new Thickness(0, 10)
                    },

                    new Label
                    {
                        Text = "Welcome to the comprehensive controls demonstration for OpenMaui Linux. " +
                               "This app showcases all the major UI controls available in the framework.",
                        FontSize = 14,
                        LineBreakMode = LineBreakMode.WordWrap,
                        HorizontalTextAlignment = TextAlignment.Center
                    },

                    CreateFeatureSection(),

                    new Label
                    {
                        Text = "Use the flyout menu (swipe from left or tap the hamburger icon) to navigate between different control demos.",
                        FontSize = 12,
                        TextColor = Colors.Gray,
                        LineBreakMode = LineBreakMode.WordWrap,
                        HorizontalTextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 20, 0, 0)
                    },

                    CreateQuickLinksSection(),

                    CreateNavigationDemoSection()
                }
            }
        };
    }

    private View CreateFeatureSection()
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            },
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            ColumnSpacing = 15,
            RowSpacing = 15,
            Margin = new Thickness(0, 20)
        };

        var features = new[]
        {
            ("Buttons", "Various button styles and events"),
            ("Text Input", "Entry, Editor, SearchBar"),
            ("Selection", "CheckBox, Switch, Slider"),
            ("Pickers", "Picker, DatePicker, TimePicker"),
            ("Lists", "CollectionView with selection"),
            ("Progress", "ProgressBar, ActivityIndicator")
        };

        for (int i = 0; i < features.Length; i++)
        {
            var (title, desc) = features[i];
            var card = CreateFeatureCard(title, desc);
            Grid.SetRow(card, i / 2);
            Grid.SetColumn(card, i % 2);
            grid.Children.Add(card);
        }

        return grid;
    }

    private Frame CreateFeatureCard(string title, string description)
    {
        return new Frame
        {
            CornerRadius = 8,
            Padding = new Thickness(15),
            BackgroundColor = Colors.White,
            HasShadow = true,
            Content = new VerticalStackLayout
            {
                Spacing = 5,
                Children =
                {
                    new Label
                    {
                        Text = title,
                        FontSize = 14,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#2196F3")
                    },
                    new Label
                    {
                        Text = description,
                        FontSize = 11,
                        TextColor = Colors.Gray,
                        LineBreakMode = LineBreakMode.WordWrap
                    }
                }
            }
        };
    }

    private View CreateQuickLinksSection()
    {
        var layout = new VerticalStackLayout
        {
            Spacing = 10,
            Margin = new Thickness(0, 20, 0, 0)
        };

        layout.Children.Add(new Label
        {
            Text = "Quick Actions",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center
        });

        var buttonRow = new HorizontalStackLayout
        {
            Spacing = 10,
            HorizontalOptions = LayoutOptions.Center
        };

        var buttonsBtn = new Button
        {
            Text = "Try Buttons",
            BackgroundColor = Color.FromArgb("#2196F3"),
            TextColor = Colors.White
        };
        buttonsBtn.Clicked += (s, e) => LinuxViewRenderer.NavigateToRoute("Buttons");

        var listsBtn = new Button
        {
            Text = "Try Lists",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White
        };
        listsBtn.Clicked += (s, e) => LinuxViewRenderer.NavigateToRoute("Lists");

        buttonRow.Children.Add(buttonsBtn);
        buttonRow.Children.Add(listsBtn);
        layout.Children.Add(buttonRow);

        return layout;
    }

    private View CreateNavigationDemoSection()
    {
        var frame = new Frame
        {
            CornerRadius = 8,
            Padding = new Thickness(20),
            BackgroundColor = Color.FromArgb("#F3E5F5"),
            Margin = new Thickness(0, 20, 0, 0),
            Content = new VerticalStackLayout
            {
                Spacing = 15,
                Children =
                {
                    new Label
                    {
                        Text = "Navigation Stack Demo",
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#9C27B0"),
                        HorizontalOptions = LayoutOptions.Center
                    },

                    new Label
                    {
                        Text = "Demonstrate push/pop navigation using Shell.GoToAsync()",
                        FontSize = 12,
                        TextColor = Colors.Gray,
                        HorizontalTextAlignment = TextAlignment.Center
                    },

                    CreatePushButton("Push Detail Page", "detail"),

                    new Label
                    {
                        Text = "Click the button to push a new page onto the navigation stack. " +
                               "Use the back button or 'Go Back' to pop it off.",
                        FontSize = 11,
                        TextColor = Colors.Gray,
                        HorizontalTextAlignment = TextAlignment.Center,
                        LineBreakMode = LineBreakMode.WordWrap
                    }
                }
            }
        };

        return frame;
    }

    private Button CreatePushButton(string text, string route)
    {
        var btn = new Button
        {
            Text = text,
            BackgroundColor = Color.FromArgb("#9C27B0"),
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center,
            Padding = new Thickness(30, 10)
        };

        btn.Clicked += (s, e) =>
        {
            Console.WriteLine($"[HomePage] Push button clicked, navigating to {route}");
            // Use LinuxViewRenderer.PushPage for Skia-based navigation
            var success = LinuxViewRenderer.PushPage(new DetailPage());
            Console.WriteLine($"[HomePage] PushPage result: {success}");
        };

        return btn;
    }
}
