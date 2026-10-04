// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// Image and ImageButton load through MAUI's image-source services: the handler resolves
/// the service registered for the source's type (ConfigureImageSources / AddService) and
/// shows the bitmap its Linux load method returns, so an app's own IImageSource type
/// loads, a replaced load is cancelled, and the built-in services render vector sources
/// for the window's scale.
/// </summary>
[Collection("LinuxApplication.Current")]
public class ImageSourceServiceTests
{
    public interface IColorImageSource : IImageSource
    {
        SKColor Color { get; }
    }

    // An app's own source type: a Controls ImageSource (what Image.Source takes) that the
    // app's service loads.
    private sealed class ColorImageSource : ImageSource, IColorImageSource
    {
        public ColorImageSource(SKColor color) => Color = color;
        public SKColor Color { get; }
        public override bool IsEmpty => false;
    }

    /// <summary>A custom service: a 10x10 bitmap of the source's colour, optionally held until released.</summary>
    private sealed class ColorImageSourceService : IImageSourceService<IColorImageSource>, ILinuxImageSourceService
    {
        public TaskCompletionSource? Gate { get; set; }
        public Exception? Failure { get; set; }
        public List<float> Scales { get; } = new();
        public List<CancellationToken> Tokens { get; } = new();
        public List<LinuxImageSourceServiceResult> Results { get; } = new();
        public Dictionary<SKColor, LinuxImageSourceServiceResult> ResultFor { get; } = new();
        public int Disposed;

        public async Task<IImageSourceServiceResult<SKBitmap>?> GetImageAsync(IImageSource imageSource, float scale = 1, CancellationToken cancellationToken = default)
        {
            var source = (IColorImageSource)imageSource;
            Scales.Add(scale);
            Tokens.Add(cancellationToken);
            var gate = Gate;
            Gate = null;
            if (gate != null)
                await gate.Task;
            if (Failure != null)
                throw Failure;

            var bitmap = new SKBitmap(10, 10);
            bitmap.Erase(source.Color);
            var result = new LinuxImageSourceServiceResult(bitmap, false, () =>
            {
                Interlocked.Increment(ref Disposed);
                bitmap.Dispose();
            });
            Results.Add(result);
            ResultFor[source.Color] = result;
            return result;
        }
    }

    private static (IMauiContext Context, ColorImageSourceService Service) CreateContext()
    {
        var service = new ColorImageSourceService();
        var builder = MauiApp.CreateBuilder();
        builder.UseLinux(_ => { });
        builder.ConfigureImageSources(services => services.AddService<IColorImageSource>(_ => service));
        return (new MauiContext(builder.Build().Services), service);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (int i = 0; i < 200 && !condition(); i++)
            await Task.Delay(10);
        condition().Should().BeTrue("the condition was not met in time");
    }

    [Fact]
    public void UseLinux_registers_the_Linux_services_for_MAUIs_source_types()
    {
        var provider = HeadlessMauiContext.Instance.Services.GetRequiredService<IImageSourceServiceProvider>();

        provider.GetImageSourceService(typeof(FileImageSource)).Should().BeOfType<LinuxFileImageSourceService>();
        provider.GetImageSourceService(typeof(UriImageSource)).Should().BeOfType<LinuxUriImageSourceService>();
        provider.GetImageSourceService(typeof(StreamImageSource)).Should().BeOfType<LinuxStreamImageSourceService>();
        provider.GetImageSourceService(typeof(FontImageSource)).Should().BeOfType<LinuxFontImageSourceService>();
        provider.GetImageSourceService(typeof(IFileImageSource)).Should().BeOfType<LinuxFileImageSourceService>();
    }

    [Fact]
    public async Task An_image_source_type_with_a_registered_service_loads_through_it()
    {
        var (context, service) = CreateContext();
        var image = new Image { Source = new ColorImageSource(SKColors.Red) };
        var handler = HeadlessMaui.AttachHandler<ImageHandler>(image, context);
        var view = handler.PlatformView;

        await WaitUntil(() => view.Bitmap != null && !image.IsLoading);

        view.Bitmap!.GetPixel(5, 5).Should().Be(SKColors.Red);
        service.Scales.Should().ContainSingle().Which.Should().BeGreaterThan(0f);
    }

    [Fact]
    public async Task ImageButton_loads_a_custom_source_through_its_service()
    {
        var (context, _) = CreateContext();
        var button = new ImageButton { Source = new ColorImageSource(SKColors.Lime) };
        var handler = HeadlessMaui.AttachHandler<ImageButtonHandler>(button, context);

        await WaitUntil(() => handler.PlatformView.Bitmap != null && !button.IsLoading);

        handler.PlatformView.Bitmap!.GetPixel(5, 5).Should().Be(SKColors.Lime);
    }

    [Fact]
    public async Task A_load_replaced_by_a_newer_one_is_cancelled_and_never_shown()
    {
        var (context, service) = CreateContext();
        var image = new Image();
        var handler = HeadlessMaui.AttachHandler<ImageHandler>(image, context);
        var view = handler.PlatformView;
        var shown = new List<SKColor>();
        view.ImageLoaded += (_, _) => shown.Add(view.Bitmap!.GetPixel(0, 0));

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        service.Gate = gate;
        image.Source = new ColorImageSource(SKColors.Blue);   // held at the gate
        image.Source = new ColorImageSource(SKColors.Red);    // replaces it
        await WaitUntil(() => shown.Count == 1);

        gate.SetResult();                                      // the first load finishes late
        await WaitUntil(() => service.Results.Count == 2 && !image.IsLoading);
        await Task.Delay(50);

        service.Tokens[0].IsCancellationRequested.Should().BeTrue("the newer load cancels the first");
        shown.Should().Equal(new[] { SKColors.Red }, "the replaced load is not shown");
        view.Bitmap!.GetPixel(0, 0).Should().Be(SKColors.Red);
        service.ResultFor[SKColors.Blue].IsDisposed.Should().BeTrue("a result that is never shown is released");
        service.ResultFor[SKColors.Red].IsDisposed.Should().BeFalse("the shown result stays alive while it is shown");
    }

    [Fact]
    public async Task Showing_another_picture_releases_the_previous_result()
    {
        var (context, service) = CreateContext();
        var image = new Image { Source = new ColorImageSource(SKColors.Red) };
        var handler = HeadlessMaui.AttachHandler<ImageHandler>(image, context);
        await WaitUntil(() => service.Results.Count == 1 && !image.IsLoading);

        image.Source = new ColorImageSource(SKColors.Blue);
        await WaitUntil(() => service.Results.Count == 2 && !image.IsLoading);
        service.Results[0].IsDisposed.Should().BeTrue();

        image.Source = null;
        handler.PlatformView.Bitmap.Should().BeNull();
        service.Results[1].IsDisposed.Should().BeTrue("clearing the picture releases its result");
    }

    [Fact]
    public async Task A_service_that_throws_reports_LoadingFailed_and_clears_the_picture()
    {
        var (context, service) = CreateContext();
        var image = new Image { Source = new ColorImageSource(SKColors.Red) };
        var handler = HeadlessMaui.AttachHandler<ImageHandler>(image, context);
        await WaitUntil(() => handler.PlatformView.Bitmap != null && !image.IsLoading);

        service.Failure = new InvalidOperationException("boom");
        var cleared = false;
        handler.PlatformView.ImageCleared += (_, _) => cleared = true;
        image.Source = new ColorImageSource(SKColors.Blue);
        await WaitUntil(() => cleared && !image.IsLoading);

        handler.PlatformView.Bitmap.Should().BeNull();
    }

    [Fact]
    public async Task A_source_type_without_a_service_fails_to_load()
    {
        var image = new Image();
        var handler = HeadlessMaui.AttachHandler<ImageHandler>(image, HeadlessMauiContext.Instance);
        var cleared = false;
        handler.PlatformView.ImageCleared += (_, _) => cleared = true;

        // No service is registered for IColorImageSource in this app.
        image.Source = new ColorImageSource(SKColors.Red);

        await WaitUntil(() => cleared && !image.IsLoading);
        handler.PlatformView.Bitmap.Should().BeNull();
    }

    [Fact]
    public async Task The_font_service_renders_the_glyph_for_the_scale_asked_for()
    {
        var service = (ILinuxImageSourceService)HeadlessMauiContext.Instance.Services
            .GetRequiredService<IImageSourceServiceProvider>().GetImageSourceService(typeof(FontImageSource))!;

        using var one = await service.GetImageAsync(new FontImageSource { Glyph = "A" }, 1f);
        using var two = await service.GetImageAsync(new FontImageSource { Glyph = "A" }, 2f);

        one!.Value.Width.Should().Be(24);
        two!.Value.Width.Should().Be(48);
        two.IsResolutionDependent.Should().BeTrue();
    }

    [Fact]
    public async Task The_file_service_renders_an_SVG_for_the_scale_and_shares_raster_images()
    {
        var dir = Path.Combine(Path.GetTempPath(), "openmaui-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var svg = Path.Combine(dir, "icon.svg");
        File.WriteAllText(svg, "<svg xmlns='http://www.w3.org/2000/svg' width='32' height='16' viewBox='0 0 32 16'><rect width='32' height='16' fill='red'/></svg>");
        var png = Path.Combine(dir, "photo.png");
        using (var bitmap = new SKBitmap(8, 8))
        {
            bitmap.Erase(SKColors.Blue);
            using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(png, data.ToArray());
        }
        var service = new LinuxFileImageSourceService();

        // A .png reference falls back to the SVG next to it.
        using var vector = await service.GetImageAsync(new FileImageSource { File = Path.Combine(dir, "icon.png") }, 2f);
        vector!.Value.Width.Should().Be(64);
        vector.Value.Height.Should().Be(32);
        vector.IsResolutionDependent.Should().BeTrue();

        var first = await service.GetImageAsync(new FileImageSource { File = png }, 2f);
        var second = await service.GetImageAsync(new FileImageSource { File = png }, 1f);
        first!.Value.Width.Should().Be(8, "a raster image is not scaled");
        second!.Value.Should().BeSameAs(first.Value, "raster images are shared through the image cache");
        first.Dispose();
        second.Value.Handle.Should().NotBe(IntPtr.Zero, "a shared cached bitmap is never disposed by a result");

        var missing = () => service.GetImageAsync(new FileImageSource { File = Path.Combine(dir, "missing.png") });
        await missing.Should().ThrowAsync<FileNotFoundException>();
        (await service.GetImageAsync(new FileImageSource { File = "" })).Should().BeNull("an empty source has no image");
    }

    [Fact]
    public void A_resolution_dependent_picture_measures_at_its_logical_size()
    {
        var view = new SkiaImage();
        view.ApplyResult(new LinuxImageSourceServiceResult(new SKBitmap(48, 24), true), 2f);
        view.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)).Should().Be(new Size(24, 12));

        view.ApplyResult(new LinuxImageSourceServiceResult(new SKBitmap(48, 24), false), 2f);
        view.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)).Should().Be(new Size(48, 24));
    }
}
