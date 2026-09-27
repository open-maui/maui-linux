// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

[Collection("LinuxApplication.Current")]
/// <summary>
/// An Image under Fill takes its whole slot and fits its picture inside it, as MAUI's
/// ComputeFrame places it; the platform image used to shrink to its fitted size and sit
/// at the slot's left edge (Strikeline's welcome rotator).
/// </summary>
public class ImageInStarRowTests
{
    private static string WritePng(int w, int h)
    {
        var path = Path.Combine(Path.GetTempPath(), $"openmaui-test-{w}x{h}.png");
        using var bmp = new SKBitmap(w, h);
        bmp.Erase(SKColors.Red);
        using var data = bmp.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    [Fact]
    public async Task An_AspectFit_image_filling_a_star_row_is_centred()
    {
        // Strikeline's welcome rotator item: Frame > Grid(5*,1*,Auto,Auto) > Image AspectFit, Fill.
        var image = new Image { Source = ImageSource.FromFile(WritePng(200, 100)), Aspect = Aspect.AspectFit, Margin = 10, HorizontalOptions = LayoutOptions.Fill };
        var grid = new Grid { RowSpacing = 5, RowDefinitions = new RowDefinitionCollection(new RowDefinition(new GridLength(5, GridUnitType.Star)), new RowDefinition(new GridLength(1, GridUnitType.Star))) };
        grid.Add(image, 0, 0);
        grid.Add(new Label { Text = "Trade", HorizontalOptions = LayoutOptions.Center }, 0, 1);
        var frame = new Frame { HasShadow = false, Margin = 20, Content = grid };
        var stack = new StackLayout { Children = { frame } };
        using var host = new HeadlessMauiHost(new ContentPage { Content = stack }, withEngine: true);
        host.Context.Render();
        await Task.Delay(300);
        host.Context.Render();
        host.Context.Render();

        var g = ((Microsoft.Maui.Platform.SkiaView)grid.Handler!.PlatformView!).Bounds;
        var i = ((Microsoft.Maui.Platform.SkiaView)image.Handler!.PlatformView!).Bounds;
        i.Width.Should().BeApproximately(g.Width - 20, 1, "Fill: the image view spans the cell");
        (i.Center.X).Should().BeApproximately(g.Center.X, 1);
    }
}
