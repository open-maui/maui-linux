// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// The platform back arrow of a MAUI NavigationPage pops MAUI's own stack (and
/// through it the platform), so the two never disagree; a page's
/// NavigationPage.HasNavigationBar / HasBackButton reach its bar.
/// </summary>
[Collection(HeadlessMaui.Collection)]
public class NavigationPageParityTests
{
    [Fact]
    public async Task Platform_back_pops_the_MAUI_navigation_stack()
    {
        var root = new ContentPage { Title = "Root" };
        var detail = new ContentPage { Title = "Detail" };
        var navigationPage = new NavigationPage(root);
        HeadlessMaui.HostInWindow(navigationPage);
        var context = HeadlessMaui.CreateContext();
        var skia = (SkiaNavigationPage)Microsoft.Maui.Platform.Linux.Hosting.MauiHandlerExtensions.ToHandler(navigationPage, context).PlatformView!;

        await navigationPage.PushAsync(detail, false);
        navigationPage.Navigation.NavigationStack.Should().HaveCount(2);

        skia.OnKeyDown(new KeyEventArgs(Key.Escape, KeyModifiers.None));

        navigationPage.Navigation.NavigationStack.Should().HaveCount(1);
        skia.StackDepth.Should().Be(1, "the platform follows MAUI's pop (its page animates in)");
    }

    [Fact]
    public async Task HasNavigationBar_and_HasBackButton_reach_the_page_bar()
    {
        var root = new ContentPage();
        var detail = new ContentPage();
        var navigationPage = new NavigationPage(root);
        HeadlessMaui.HostInWindow(navigationPage);
        var skia = (SkiaNavigationPage)Microsoft.Maui.Platform.Linux.Hosting.MauiHandlerExtensions.ToHandler(navigationPage, HeadlessMaui.CreateContext()).PlatformView!;
        await navigationPage.PushAsync(detail, false);
        var detailView = (SkiaPage)detail.Handler!.PlatformView!;

        skia.IsBackButtonVisible.Should().BeTrue();
        NavigationPage.SetHasBackButton(detail, false);
        skia.IsBackButtonVisible.Should().BeFalse();

        NavigationPage.SetHasNavigationBar(detail, false);
        detailView.ShowNavigationBar.Should().BeFalse();
    }

    private static (NavigationPage Navigation, SkiaNavigationPage Skia) Host(Page root)
    {
        var navigationPage = new NavigationPage(root);
        HeadlessMaui.HostInWindow(navigationPage);
        var skia = (SkiaNavigationPage)Microsoft.Maui.Platform.Linux.Hosting.MauiHandlerExtensions.ToHandler(navigationPage, HeadlessMaui.CreateContext()).PlatformView!;
        return (navigationPage, skia);
    }

    private static SkiaPage Draw(SkiaNavigationPage skia)
    {
        using var bitmap = new SkiaSharp.SKBitmap(400, 300);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        skia.Measure(new Microsoft.Maui.Graphics.Size(400, 300));
        skia.Arrange(new Microsoft.Maui.Graphics.Rect(0, 0, 400, 300));
        skia.Draw(canvas);
        return skia.CurrentPage!;
    }

    [Fact]
    public void TitleView_takes_the_title_place_in_the_bar_and_its_input()
    {
        int clicks = 0;
        var button = new Button { Text = "In bar" };
        button.Clicked += (_, _) => clicks++;
        var root = new ContentPage { Title = "Root" };
        NavigationPage.SetTitleView(root, button);
        var (_, skia) = Host(root);

        var page = Draw(skia);
        page.TitleView.Should().BeSameAs(button.Handler!.PlatformView, "the TitleView is realized in the bar");
        page.TitleViewBounds.Height.Should().BeApproximately(page.NavigationBarHeight, 0.5, "it takes the bar's height");
        page.TitleViewBounds.Width.Should().BeGreaterThan(200, "it takes the rest of the bar");

        var center = page.TitleViewBounds.Center;
        var hit = skia.HitTest((float)center.X, (float)center.Y);
        hit.Should().NotBeNull();
        hit!.OnPointerPressed(new Microsoft.Maui.Platform.PointerEventArgs((float)center.X, (float)center.Y, Microsoft.Maui.Platform.PointerButton.Left));
        hit.OnPointerReleased(new Microsoft.Maui.Platform.PointerEventArgs((float)center.X, (float)center.Y, Microsoft.Maui.Platform.PointerButton.Left));
        clicks.Should().Be(1, "the TitleView takes presses in the bar");

        NavigationPage.SetTitleView(root, null);
        Draw(skia).TitleView.Should().BeNull();
    }

    [Fact]
    public async Task IconColor_BackButtonTitle_and_a_gradient_BarBackground_reach_the_bar()
    {
        var root = new ContentPage { Title = "Root" };
        NavigationPage.SetBackButtonTitle(root, "Back to root");
        var detail = new ContentPage { Title = "Detail" };
        var (navigationPage, skia) = Host(root);
        NavigationPage.SetIconColor(navigationPage, Colors.Red);
        await navigationPage.PushAsync(detail, false);
        var detailView = (SkiaPage)detail.Handler!.PlatformView!;

        detailView.IconColor.Should().Be(Colors.Red, "the NavigationPage's IconColor applies to its pages");
        NavigationPage.SetIconColor(detail, Colors.Green);
        detailView.IconColor.Should().Be(Colors.Green, "a page's own IconColor wins");
        detailView.BackButtonTitle.Should().Be("Back to root", "the page beneath names the back button");

        var gradient = new LinearGradientBrush(new GradientStopCollection { new GradientStop(Colors.Blue, 0), new GradientStop(Colors.Yellow, 1) });
        navigationPage.BarBackground = gradient;
        detailView.TitleBarBrush.Should().BeSameAs(gradient);

        // The back arrow is drawn in the icon colour.
        using var bitmap = new SkiaSharp.SKBitmap(400, 300);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        skia.Measure(new Microsoft.Maui.Graphics.Size(400, 300));
        skia.Arrange(new Microsoft.Maui.Graphics.Rect(0, 0, 400, 300));
        await Task.Delay(400); // the push animation (when animated) has ended
        skia.Draw(canvas);
        bool green = false;
        for (int x = 10; x < 40 && !green; x++)
            for (int y = 10; y < 46 && !green; y++)
            {
                var c = bitmap.GetPixel(x, y);
                green = c.Green > 100 && c.Red < 60 && c.Blue < 60;
            }
        green.Should().BeTrue("the back arrow takes NavigationPage.IconColor");
    }

    [Fact]
    public async Task TitleIconImageSource_is_loaded_and_drawn_left_of_the_title()
    {
        using var source = new SkiaSharp.SKBitmap(32, 32);
        source.Erase(SkiaSharp.SKColors.Red);
        using var image = SkiaSharp.SKImage.FromBitmap(source);
        var png = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100).ToArray();

        var root = new ContentPage { Title = "Titled" };
        NavigationPage.SetTitleIconImageSource(root, ImageSource.FromStream(() => new MemoryStream(png)));
        var (_, skia) = Host(root);
        var page = (SkiaPage)root.Handler!.PlatformView!;

        for (int i = 0; i < 50 && page.TitleIcon == null; i++)
            await Task.Delay(20);
        page.TitleIcon.Should().NotBeNull("the icon loads through its image-source service");

        Draw(skia);
        page.TitleIconBounds.IsEmpty.Should().BeFalse();
        ((double)page.TitleIconBounds.Height).Should().BeApproximately(24, 0.5, "drawn at the bar's icon size");

        NavigationPage.SetTitleIconImageSource(root, null);
        page.TitleIcon.Should().BeNull();
    }
}
