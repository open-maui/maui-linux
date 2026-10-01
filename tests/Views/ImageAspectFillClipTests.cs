// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

/// <summary>
/// An Image with AspectFill crops the picture to its bounds, as on every platform: it drew the
/// scaled picture past them, over the views around it (CiteLynq's Daily Discovery photo).
/// </summary>
[Collection("LinuxApplication.Current")]
public class ImageAspectFillClipTests
{
    [Fact]
    public async Task An_aspect_fill_picture_is_cropped_to_the_image()
    {
        var file = Path.Combine(Path.GetTempPath(), $"openmaui-fill-{Guid.NewGuid():N}.png");
        using (var bitmap = new SKBitmap(160, 90))
        {
            bitmap.Erase(SKColors.Red);
            using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(file, data.ToArray());
        }
        try
        {
            // 300 x 100: filled, the 16:9 picture is 300 x 169, 34 past the top and the bottom.
            var image = new Image { Source = ImageSource.FromFile(file), Aspect = Aspect.AspectFill, WidthRequest = 300, HeightRequest = 100 };
            var page = new ContentPage
            {
                BackgroundColor = Colors.Blue,
                Content = new VerticalStackLayout { Padding = new Thickness(0, 100), Children = { image } },
            };
            using var host = new HeadlessMauiHost(page, withEngine: true);
            for (int i = 0; i < 20 && host.DisplayWindow.LastFrame == null | (host.DisplayWindow.LastFrame != null && host.DisplayWindow.PixelAt(400, 150).R < 200); i++)
            {
                host.Context.Render();
                await Task.Delay(50);
            }

            var inside = host.DisplayWindow.PixelAt(400, 150);
            (inside.R > 200 && inside.B < 60).Should().BeTrue($"the picture fills the image, got {inside}");
            foreach (var y in new[] { 85, 215 })
            {
                var outside = host.DisplayWindow.PixelAt(400, y);
                (outside.B > 200 && outside.R < 60).Should().BeTrue($"nothing is drawn at y={y}, outside the image, got {outside}");
            }
        }
        finally
        {
            File.Delete(file);
        }
    }
}
