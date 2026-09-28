// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

/// <summary>
/// Views showing the same file share its cached bitmap. Evicting it from the cache, or a view
/// letting it go, must not free pixels another view still draws: it did (Dispose on eviction),
/// and building the next image from them crashed the process in memcpy (Strikeline, SIGSEGV).
/// </summary>
[Collection("LinuxApplication.Current")]
public class ImageCacheEvictionTests
{
    private static string WritePng(string name, SKColor color)
    {
        var path = Path.Combine(Path.GetTempPath(), $"openmaui-evict-{name}.png");
        using var bmp = new SKBitmap(40, 40);
        bmp.Erase(color);
        using var data = bmp.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    private static async Task Settle(HeadlessMauiHost host)
    {
        for (int i = 0; i < 3; i++) { host.Context.Render(); await Task.Delay(50); }
    }

    [Fact]
    public async Task A_shared_image_evicted_from_the_cache_still_draws_in_every_view()
    {
        Microsoft.Maui.Platform.SkiaImage.ClearCache();
        var shared = WritePng("shared", SKColors.Red);
        var a = new Image { Source = ImageSource.FromFile(shared), WidthRequest = 40, HeightRequest = 40, HorizontalOptions = LayoutOptions.Start };
        var b = new Image { Source = ImageSource.FromFile(shared), WidthRequest = 40, HeightRequest = 40, HorizontalOptions = LayoutOptions.Start };
        var others = new VerticalStackLayout();
        var stack = new VerticalStackLayout { a, b, others };
        using var host = new HeadlessMauiHost(new ContentPage { BackgroundColor = Colors.White, Content = stack }, withEngine: true);
        await Settle(host);

        // More sources than the cache keeps: the shared one is the oldest and is evicted.
        for (int i = 0; i < 70; i++)
            others.Add(new Image { Source = ImageSource.FromFile(WritePng($"o{i}", SKColors.Blue)), WidthRequest = 2, HeightRequest = 2 });
        await Settle(host);

        // Freed pixels do not always crash when read; a disposed bitmap has no native handle.
        var bBitmap = ((Microsoft.Maui.Platform.SkiaImage)b.Handler!.PlatformView!).Bitmap;
        bBitmap.Should().NotBeNull();
        bBitmap!.Handle.Should().NotBe(IntPtr.Zero, "evicting the cache entry must not free the bitmap view b shows");

        // A view letting the shared bitmap go, and another loading the same file again.
        stack.Remove(a);
        var c = new Image { Source = ImageSource.FromFile(shared), WidthRequest = 40, HeightRequest = 40, HorizontalOptions = LayoutOptions.Start };
        stack.Insert(0, c);
        await Settle(host);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        host.Context.RenderingEngine!.InvalidateAll();
        await Settle(host);

        bBitmap.Handle.Should().NotBe(IntPtr.Zero, "view a going must not free the bitmap view b shows");
        foreach (var view in new[] { c, b })
        {
            var r = ((Microsoft.Maui.Platform.SkiaView)view.Handler!.PlatformView!).Bounds;
            var (pr, pg, pb, _) = host.DisplayWindow.PixelAt((int)r.Center.X, (int)r.Center.Y);
            (pr > 200 && pg < 60 && pb < 60).Should().BeTrue($"the shared image still draws, got {pr},{pg},{pb}");
        }
    }
}
