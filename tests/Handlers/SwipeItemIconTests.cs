// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls.Linux.Tests.Golden;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using Xunit;
using OpenSwipeItem = Microsoft.Maui.OpenSwipeItem;
using SwipeItem = Microsoft.Maui.Controls.SwipeItem;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// SwipeItem icons as MAUI 11's Windows SwipeItemMenuItemHandler shows them: any
/// IconImageSource loaded through its image-source service (font glyphs, streams, an app's own
/// source), a load replaced by a newer source or a disconnect released, and the tint resolved as
/// Windows resolves it (IconColor, then the glyph's colour, then the text colour, for font icons;
/// IconColor for the app's file icons; other images keep their colours).
/// </summary>
[Collection("LinuxApplication.Current")]
public class SwipeItemIconTests
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
        condition().Should().BeTrue("the icon did not load in time");
    }

    private static Microsoft.Maui.Platform.SwipeItem PlatformItem(SwipeItem item) =>
        (Microsoft.Maui.Platform.SwipeItem)((IElement)item).Handler!.PlatformView!;

    private static FontImageSource Glyph(Color? color = null) => new() { Glyph = "A", Color = color, Size = 24 };

    private static (SwipeView Swipe, HeadlessMauiHost Host) Host(SwipeItem item, Action<MauiAppBuilder>? configure = null)
    {
        var swipe = new SwipeView
        {
            HeightRequest = 60,
            Content = new Grid { BackgroundColor = Colors.White, Children = { new Label { Text = "row" } } },
            RightItems = new SwipeItems { item },
        };
        var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { swipe } }, withEngine: true, configure: configure);
        host.Context.Render();
        return (swipe, host);
    }

    private static byte[] Png(SKColor color)
    {
        using var bitmap = new SKBitmap(10, 10);
        bitmap.Erase(color);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    [Fact]
    public async Task A_font_icon_loads_and_follows_the_tint_chain()
    {
        var item = new SwipeItem { Text = "Delete", IconImageSource = Glyph() };
        var (_, host) = Host(item);
        using var _ = host;
        var platform = PlatformItem(item);

        await WaitUntil(() => platform.Icon != null, host.Context.Render);
        platform.Icon!.Width.Should().Be(16, "the glyph is rendered at the item's icon size at scale 1");
        platform.IconTintColor.Should().Be(Colors.White, "a glyph without a colour, on an item without colours, is white as Windows renders it");

        item.TextColor = Colors.Green;
        platform.IconTintColor.Should().Be(Colors.Green, "a glyph without its own colour follows the item's TextColor");

        item.IconImageSource = Glyph(Colors.Red);
        await WaitUntil(() => platform.IconTintColor == Colors.Red, host.Context.Render);
        platform.Icon.Should().NotBeNull();

        item.IconColor = Colors.Orange;
        platform.IconTintColor.Should().Be(Colors.Orange, "IconColor wins over the glyph's colour, without loading the icon again");

        item.IconColor = null;
        platform.IconTintColor.Should().Be(Colors.Red);
    }

    [Fact]
    public void A_font_icon_without_colours_contrasts_with_the_background()
    {
        var item = new SwipeItem { Text = "Flag", IconImageSource = Glyph(), BackgroundColor = Colors.Yellow };
        var (_, host) = Host(item);
        using var _ = host;
        var platform = PlatformItem(item);

        platform.TextColor.Should().Be(Colors.Black, "without a TextColor the text contrasts with a light background (MAUI's GetTextColor)");
        platform.IconTintColor.Should().Be(Colors.Black);

        item.BackgroundColor = Colors.DarkBlue;
        platform.TextColor.Should().Be(Colors.White);
        platform.IconTintColor.Should().Be(Colors.White);
    }

    [Fact]
    public async Task A_stream_icon_loads_in_its_own_colours_even_with_an_icon_colour()
    {
        var png = Png(SKColors.Blue);
        var item = new SwipeItem { Text = "Mail", IconColor = Colors.Red, IconImageSource = ImageSource.FromStream(() => new MemoryStream(png)) };
        var (_, host) = Host(item);
        using var _ = host;
        var platform = PlatformItem(item);

        await WaitUntil(() => platform.Icon != null, host.Context.Render);
        platform.Icon!.GetPixel(5, 5).Should().Be(SKColors.Blue);
        platform.IconTintColor.Should().BeNull("as on Windows, a stream image keeps its colours");
    }

    [Fact]
    public void File_icons_take_the_icon_colour_only_when_they_are_the_apps_own()
    {
        var services = HeadlessMaui.CreateContext().Services;
        var item = new SwipeItem { IconImageSource = "delete.png" };
        SwipeItemMenuItemHandler.IconTint(item, item.IconImageSource, services).Should().BeNull("without IconColor an image keeps its colours");

        item.IconColor = Colors.Red;
        SwipeItemMenuItemHandler.IconTint(item, item.IconImageSource, services).Should().Be(Colors.Red, "an app file is tinted as a monochrome mask, as Windows' BitmapIconSource does");

        var rooted = ImageSource.FromFile("/usr/share/icons/delete.png");
        SwipeItemMenuItemHandler.IconTint(item, rooted, services).Should().BeNull("a rooted file keeps its colours, as on Windows");

        var uri = ImageSource.FromUri(new Uri("https://example.com/delete.png"));
        SwipeItemMenuItemHandler.IconTint(item, uri, services).Should().BeNull("a URI image keeps its colours, as on Windows");
    }

    [Fact]
    public async Task An_apps_own_source_loads_through_its_service_and_replaced_or_disconnected_icons_are_released()
    {
        var service = new SolidImageSourceService();
        var item = new SwipeItem { Text = "Pin", IconColor = Colors.Red, IconImageSource = new SolidImageSource(SKColors.Blue) };
        var (swipe, host) = Host(item, b => b.ConfigureImageSources(s => s.AddService<ISolidImageSource>(_ => service)));
        using var _ = host;
        var platform = PlatformItem(item);

        await WaitUntil(() => platform.Icon != null, host.Context.Render);
        platform.Icon!.GetPixel(4, 4).Should().Be(SKColors.Blue);
        platform.IconTintColor.Should().BeNull("an app's own image service keeps the image's colours");

        item.IconImageSource = new SolidImageSource(SKColors.Green);
        await WaitUntil(() => platform.Icon?.GetPixel(4, 4) == SKColors.Green, host.Context.Render);
        service.Disposed.Should().Be(1, "the replaced icon is released");

        item.IconImageSource = null;
        platform.Icon.Should().BeNull("no source, no icon");
        service.Disposed.Should().Be(2);

        item.IconImageSource = new SolidImageSource(SKColors.Red);
        await WaitUntil(() => platform.Icon != null, host.Context.Render);
        ((IElement)item).Handler!.DisconnectHandler();
        platform.Icon.Should().BeNull("a disconnect releases the icon");
        service.Disposed.Should().Be(3);
        swipe.Should().NotBeNull();
    }

    [Fact]
    public void The_icon_is_drawn_above_the_text_tinted()
    {
        using var icon = new SKBitmap(16, 16);
        icon.Erase(SKColors.Black);
        var item = new Microsoft.Maui.Platform.SwipeItem
        {
            Text = "Delete",
            Icon = icon,
            IconTintColor = Colors.Lime,
            BackgroundColor = Colors.DarkRed,
        };
        var swipe = new SkiaSwipeView { Content = new SkiaLabel { Text = "row" } };
        swipe.RightItems.Add(item);

        swipe.Measure(new Size(300, 60));
        swipe.Arrange(new Rect(0, 0, 300, 60));
        swipe.Open(OpenSwipeItem.RightItems);
        using var bitmap = GoldenHarness.Render(swipe, 300, 60, 1f);

        using var font = Microsoft.Maui.Platform.Linux.Rendering.SkiaFontFactory.Create(SkiaSwipeView.ItemFontSize);
        var (iconRect, baseline) = SkiaSwipeView.ItemContentLayout(item, new SKRect(200, 0, 300, 60), font.Metrics);
        iconRect.Should().NotBeNull();
        var r = iconRect!.Value;
        r.Width.Should().Be(16);
        r.Height.Should().Be(16);
        r.MidX.Should().Be(250, "the icon is centred in the item");
        baseline.Should().BeGreaterThan(r.Bottom, "the text is below the icon");
        float blockTop = r.Top, blockBottom = baseline!.Value + font.Metrics.Descent;
        ((blockTop + blockBottom) / 2).Should().BeApproximately(30, 0.5f, "icon and text are centred together");

        bitmap.GetPixel((int)r.MidX, (int)r.MidY).Should().Be(SKColors.Lime, "the icon is drawn in its tint");
    }
}
