// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Maui.Behaviors;
using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SkiaSharp;
using Xunit;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// CommunityToolkit's IconTintColorBehavior tints an Image's icon: its platform-neutral build only
/// declares TintColor, so on Linux the icon kept its own colours (CiteLynq's category tiles).
/// </summary>
[Collection("LinuxApplication.Current")]
public sealed class ToolkitIconTintTests
{
    // A white disc on transparency: the shape an icon has.
    private static string WriteIcon()
    {
        var path = Path.Combine(Path.GetTempPath(), "openmaui-tint-icon.png");
        using var bmp = new SKBitmap(new SKImageInfo(40, 40, SKColorType.Bgra8888, SKAlphaType.Premul));
        bmp.Erase(SKColors.Transparent);
        using (var canvas = new SKCanvas(bmp))
        using (var white = new SKPaint { Color = SKColors.White, IsAntialias = false })
            canvas.DrawCircle(20, 20, 15, white);
        using var data = bmp.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    [Fact]
    public async Task The_icon_takes_the_behaviors_colour_follows_it_and_loses_it_when_removed()
    {
        // Set directly: MAUI gives a behavior no BindingContext on any platform, so {Binding} on one
        // needs an explicit Source there as here.
        var behavior = new IconTintColorBehavior { TintColor = Colors.Red };
        var image = new Image { Source = ImageSource.FromFile(WriteIcon()), WidthRequest = 40, HeightRequest = 40,
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
        image.Behaviors.Add(behavior);
        using var host = new CompatHost(new ContentPage { BackgroundColor = Colors.Black, Content = image }, null, 200, 200);

        async Task<SKColor> CentreAndCorner(Action? change = null)
        {
            change?.Invoke();
            for (int i = 0; i < 4; i++) { host.Render(); await Task.Delay(60); }
            return SKColor.Empty;
        }

        await CentreAndCorner();
        var (r, g, b, _) = host.DisplayWindow.PixelAt(20, 20);
        (r > 200 && g < 60 && b < 60).Should().BeTrue($"the white icon is drawn red, got {r},{g},{b}");
        var corner = host.DisplayWindow.PixelAt(2, 2);
        (corner.R < 30 && corner.G < 30 && corner.B < 30).Should().BeTrue("its transparent corners stay transparent over the black page");

        await CentreAndCorner(() => behavior.TintColor = Colors.Blue);
        (r, g, b, _) = host.DisplayWindow.PixelAt(20, 20);
        (b > 200 && r < 60 && g < 60).Should().BeTrue($"the colour changed to blue, got {r},{g},{b}");

        await CentreAndCorner(() => image.Behaviors.Remove(behavior));
        (r, g, b, _) = host.DisplayWindow.PixelAt(20, 20);
        (r > 200 && g > 200 && b > 200).Should().BeTrue($"without the behavior the icon is white again, got {r},{g},{b}");
    }
}
