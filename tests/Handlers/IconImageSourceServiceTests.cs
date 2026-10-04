// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// Button images, Shell toolbar and flyout icons and NavigationPage toolbar icons load through
/// MAUI's image-source services, as Image does: any source kind (file, font, URI, stream, an
/// app's own), at the icon size and the screen's density. Button images supported files,
/// streams and URIs only; the icons supported files (and toolbar font glyphs) only.
/// </summary>
[Collection("LinuxApplication.Current")]
public class IconImageSourceServiceTests
{
    public interface ISolidImageSource : IImageSource
    {
        SKColor Color { get; }
    }

    private sealed class SolidImageSource : ImageSource, ISolidImageSource
    {
        public SolidImageSource(SKColor color) => Color = color;
        public SKColor Color { get; }
        public override bool IsEmpty => false;
    }

    private sealed class SolidImageSourceService : IImageSourceService<ISolidImageSource>, ILinuxImageSourceService
    {
        public int Disposed;

        public Task<IImageSourceServiceResult<SKBitmap>?> GetImageAsync(IImageSource imageSource, float scale = 1, CancellationToken cancellationToken = default)
        {
            var bitmap = new SKBitmap(8, 8);
            bitmap.Erase(((ISolidImageSource)imageSource).Color);
            IImageSourceServiceResult<SKBitmap> result = new LinuxImageSourceServiceResult(bitmap, false, () =>
            {
                Interlocked.Increment(ref Disposed);
                bitmap.Dispose();
            });
            return Task.FromResult<IImageSourceServiceResult<SKBitmap>?>(result);
        }
    }

    private static async Task WaitUntil(Func<bool> condition, Action? pump = null)
    {
        for (int i = 0; i < 200 && !condition(); i++)
        {
            pump?.Invoke();
            await Task.Delay(10);
        }
        condition().Should().BeTrue("the image did not load in time");
    }

    private static FontImageSource Glyph() => new() { Glyph = "A", Color = Colors.Red, Size = 24 };

    [Fact]
    public async Task A_button_shows_a_font_image()
    {
        var button = new Button { Text = "Go", ImageSource = Glyph() };
        using var host = new HeadlessMauiHost(new ContentPage { Content = button }, withEngine: true);
        var skia = (SkiaButton)button.Handler!.PlatformView!;

        await WaitUntil(() => skia.LoadedImage != null, host.Context.Render);
    }

    [Fact]
    public async Task A_button_loads_an_apps_own_source_through_its_service_and_releases_a_replaced_one()
    {
        var service = new SolidImageSourceService();
        var builder = MauiApp.CreateBuilder();
        builder.UseLinux(_ => { });
        builder.ConfigureImageSources(services => services.AddService<ISolidImageSource>(_ => service));
        var context = new MauiContext(builder.Build().Services);

        var button = new Button { ImageSource = new SolidImageSource(SKColors.Blue) };
        var handler = HeadlessMaui.AttachHandler<TextButtonHandler>(button, context);
        var skia = handler.PlatformView;
        await WaitUntil(() => skia.LoadedImage != null);
        skia.LoadedImage!.GetPixel(4, 4).Should().Be(SKColors.Blue);

        button.ImageSource = new SolidImageSource(SKColors.Green);
        await WaitUntil(() => skia.LoadedImage?.GetPixel(4, 4) == SKColors.Green);
        service.Disposed.Should().Be(1, "the replaced picture is released");
    }

    [Fact]
    public async Task Shell_toolbar_and_flyout_icons_load_through_the_service()
    {
        var toolbarIcon = Glyph();
        var flyoutIcon = Glyph();
        var page = new ContentPage { Title = "Home", Content = new Label { Text = "a" } };
        page.ToolbarItems.Add(new ToolbarItem { IconImageSource = toolbarIcon });
        var shell = new Shell { FlyoutBehavior = FlyoutBehavior.Locked };
        shell.Items.Add(new FlyoutItem { Title = "Home", Icon = flyoutIcon, Items = { new ShellContent { Route = "home", Content = page } } });
        using var host = new HeadlessMauiHost(shell, withEngine: true);
        var platform = (SkiaShell)shell.Handler!.PlatformView!;

        await WaitUntil(() => platform.LoadedToolbarIcon(page.ToolbarItems[0]) != null, host.Context.Render);
        await WaitUntil(() => platform.LoadedFlyoutIcon(flyoutIcon) != null, host.Context.Render);
    }

    [Fact]
    public async Task A_navigation_page_toolbar_icon_loads_through_the_service()
    {
        var page = new ContentPage { Title = "Home", Content = new Label { Text = "a" } };
        page.ToolbarItems.Add(new ToolbarItem { IconImageSource = Glyph() });
        var navigation = new NavigationPage(page);
        using var host = new HeadlessMauiHost(navigation, withEngine: true);
        var skiaPage = (SkiaContentPage)page.Handler!.PlatformView!;

        await WaitUntil(() => skiaPage.ToolbarItems.Count == 1 && skiaPage.ToolbarItems[0].Icon != null, host.Context.Render);
    }

    private static SkiaCellView FirstRow(ListView list)
    {
        var skia = (SkiaCollectionView)list.Handler!.PlatformView!;
        return (SkiaCellView)skia.GetItemView(0)!;
    }

    [Fact]
    public async Task An_image_cell_shows_a_font_image()
    {
        var list = new ListView
        {
            ItemsSource = new[] { "one" },
            ItemTemplate = new DataTemplate(() => new ImageCell { Text = "One", ImageSource = Glyph() }),
        };
        using var host = new HeadlessMauiHost(new ContentPage { Content = list }, withEngine: true);

        await WaitUntil(() => list.Handler != null && (((SkiaCollectionView)list.Handler.PlatformView!).GetItemView(0) as SkiaCellView)?.Image != null, host.Context.Render);
    }

    [Fact]
    public async Task An_image_cell_loads_an_apps_own_source_and_releases_a_replaced_one()
    {
        var service = new SolidImageSourceService();
        var builder = MauiApp.CreateBuilder();
        builder.UseLinux(_ => { });
        builder.ConfigureImageSources(services => services.AddService<ISolidImageSource>(_ => service));
        var context = new MauiContext(builder.Build().Services);

        var cell = new ImageCell { Text = "One", ImageSource = new SolidImageSource(SKColors.Blue) };
        var row = CellViewFactory.Create(cell, context);
        await WaitUntil(() => row.Image != null);
        row.Image!.GetPixel(4, 4).Should().Be(SKColors.Blue);

        cell.ImageSource = new SolidImageSource(SKColors.Green);
        await WaitUntil(() => row.Image?.GetPixel(4, 4) == SKColors.Green);
        service.Disposed.Should().Be(1, "the replaced picture is released");

        row.Detach!();
        service.Disposed.Should().Be(2, "a detached row releases its picture");
    }
}
