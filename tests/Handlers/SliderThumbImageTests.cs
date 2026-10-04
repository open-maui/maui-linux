// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Hosting;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>Slider.ThumbImageSource replaces the round thumb with the image, as on Windows.</summary>
[Collection(HeadlessMaui.Collection)]
public class SliderThumbImageTests
{
    [Fact]
    public async Task ThumbImageSource_is_drawn_as_the_thumb()
    {
        using var source = new SKBitmap(20, 20);
        source.Erase(SKColors.Red);
        using var encoded = SKImage.FromBitmap(source).Encode(SKEncodedImageFormat.Png, 100);
        var png = encoded.ToArray();

        var slider = new Slider { Minimum = 0, Maximum = 1, Value = 0.5, ThumbImageSource = ImageSource.FromStream(() => new MemoryStream(png)) };
        HeadlessMaui.HostInWindow(new ContentPage { Content = slider });
        var platform = (SkiaSlider)MauiHandlerExtensions.ToHandler(slider, HeadlessMaui.CreateContext()).PlatformView!;
        for (int i = 0; i < 50 && platform.ThumbImage == null; i++)
            await Task.Delay(20);
        platform.ThumbImage.Should().NotBeNull("the image loads through its image-source service");

        using var bitmap = new SKBitmap(200, 40);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        platform.Measure(new Microsoft.Maui.Graphics.Size(200, 40));
        platform.Arrange(new Microsoft.Maui.Graphics.Rect(0, 0, 200, 40));
        platform.Draw(canvas);
        var centre = bitmap.GetPixel(100, 20);
        (centre.Red > 200 && centre.Green < 60).Should().BeTrue($"the image is the thumb, got {centre}");

        slider.ThumbImageSource = null;
        platform.ThumbImage.Should().BeNull("without an image the circle thumb is drawn");
    }
}
