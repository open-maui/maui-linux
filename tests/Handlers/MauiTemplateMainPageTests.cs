// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// The MainPage of Microsoft's `dotnet new maui` template (issue #28): the dotnet bot, an
/// AspectFit Image with only a HeightRequest in a padded VerticalStackLayout in a ScrollView,
/// is drawn centred, as on the other platforms.
/// </summary>
[Collection("LinuxApplication.Current")]
public class MauiTemplateMainPageTests
{
    private static string WritePng(int w, int h)
    {
        var path = Path.Combine(Path.GetTempPath(), $"openmaui-test-bot-{w}x{h}.png");
        using var bmp = new SKBitmap(w, h);
        bmp.Erase(SKColors.Red);
        using var data = bmp.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    [Fact]
    public async Task The_template_dotnet_bot_is_drawn_centred()
    {
        var bot = new Image { Source = ImageSource.FromFile(WritePng(456, 500)), HeightRequest = 185, Aspect = Aspect.AspectFit };
        var page = new ContentPage
        {
            BackgroundColor = Colors.White,
            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = new Thickness(30, 0), Spacing = 25,
                    Children =
                    {
                        bot,
                        new Label { Text = "Hello, World!", FontSize = 32 },
                        new Label { Text = "Welcome to \n.NET Multi-platform App UI", FontSize = 24 },
                        new Button { Text = "Click me", HorizontalOptions = LayoutOptions.Fill },
                    },
                },
            },
        };
        using var host = new HeadlessMauiHost(page, withEngine: true);
        host.Context.Render();
        await Task.Delay(300);
        host.Context.Render();
        host.Context.Render();

        var b = ((Microsoft.Maui.Platform.SkiaView)bot.Handler!.PlatformView!).Bounds;
        b.Width.Should().BeApproximately(800 - 60, 1, "Fill: the image view spans the padded stack");
        b.Height.Should().BeApproximately(185, 0.5);

        // Where the picture's pixels are: 456x500 fitted into 185 high is ~169 wide.
        int y = (int)b.Center.Y, left = -1, right = -1;
        for (int x = 0; x < 800; x++)
        {
            var (r, g, bl, _) = host.DisplayWindow.PixelAt(x, y);
            if (r > 200 && g < 60 && bl < 60) { if (left < 0) left = x; right = x; }
        }
        left.Should().BeGreaterThan(0, "the picture is drawn");
        ((left + right) / 2.0).Should().BeApproximately(400, 2, $"centred in the window, drawn at {left}..{right}");
    }
}
