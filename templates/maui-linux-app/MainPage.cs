// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace MauiLinuxApp;

public class MainPage : ContentPage
{
    private int _count = 0;
    private readonly Label _counterLabel;

    public MainPage()
    {
        Title = "MauiLinuxApp";

        _counterLabel = new Label
        {
            Text = "Click the button",
            HorizontalOptions = LayoutOptions.Center,
            FontSize = 18
        };

        var button = new Button
        {
            Text = "Click me",
            HorizontalOptions = LayoutOptions.Center
        };
        button.Clicked += OnCounterClicked;

        var image = new Image
        {
            Source = "dotnet_bot.png",
            HeightRequest = 200,
            HorizontalOptions = LayoutOptions.Center
        };

        Content = new VerticalStackLayout
        {
            Spacing = 25,
            Padding = new Thickness(30, 0),
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = "Hello, .NET MAUI on Linux!",
                    FontSize = 32,
                    HorizontalOptions = LayoutOptions.Center
                },
                image,
                _counterLabel,
                button
            }
        };
    }

    private void OnCounterClicked(object? sender, EventArgs e)
    {
        _count++;
        _counterLabel.Text = _count == 1 ? "Clicked 1 time" : $"Clicked {_count} times";
    }
}
