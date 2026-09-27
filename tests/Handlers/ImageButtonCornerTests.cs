// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// An ImageButton's background takes its CornerRadius, as on the other platforms: the
/// base view filled a square under the rounded fill (Strikeline's Copilot send button).
/// </summary>
[Collection("LinuxApplication.Current")]
public class ImageButtonCornerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_round_ImageButton_leaves_its_corners_unpainted(bool gradient)
    {
        var button = new ImageButton { WidthRequest = 40, HeightRequest = 40, CornerRadius = 20, Padding = 8,
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
        if (gradient)
            button.Background = new LinearGradientBrush(new GradientStopCollection { new GradientStop(Colors.Red, 0), new GradientStop(Colors.Red, 1) });
        else
            button.BackgroundColor = Colors.Red;
        using var host = new HeadlessMauiHost(new ContentPage { BackgroundColor = Colors.White, Content = button }, withEngine: true);
        host.Context.Render();
        host.Context.Render();

        var b = ((Microsoft.Maui.Platform.SkiaView)button.Handler!.PlatformView!).Bounds;
        var (r, g, bl, _) = host.DisplayWindow.PixelAt((int)b.Left + 1, (int)b.Top + 1);
        (r > 240 && g > 240 && bl > 240).Should().BeTrue($"the corner shows the page, got {r},{g},{bl}");
        var (cr, cg, cb, _) = host.DisplayWindow.PixelAt((int)b.Center.X, (int)b.Top + 3);
        (cr > 200 && cg < 60 && cb < 60).Should().BeTrue($"the button is filled inside its circle, got {cr},{cg},{cb}");
    }
}
