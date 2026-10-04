// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Handlers;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// The Image and ImageButton handlers load a source with MAUI's notifications
/// (IsLoading, LoadingStarted / LoadingCompleted / LoadingFailed) and clear the
/// picture for a null source or one that fails to load, as MAUI's handlers do.
/// </summary>
[Collection("LinuxApplication.Current")]
public class ImageSourceLoadingTests
{
    private sealed class Part : IImageSourcePart, IImageSourcePartEvents
    {
        public List<string> Log { get; } = new();
        public IImageSource? Source { get; set; }
        public bool IsAnimationPlaying { get; set; }
        public bool IsLoading { get; private set; }
        public void UpdateIsLoading(bool isLoading) => IsLoading = isLoading;
        public void LoadingStarted() => Log.Add("LoadingStarted");
        public void LoadingCompleted(bool successful) => Log.Add($"LoadingCompleted({successful})");
        public void LoadingFailed(Exception exception) => Log.Add("LoadingFailed");
    }

    private static string CreatePng(SKColor color)
    {
        var dir = Path.Combine(Path.GetTempPath(), "openmaui-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "image.png");
        using var bitmap = new SKBitmap(8, 8);
        bitmap.Erase(color);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        using var file = File.Create(path);
        data.SaveTo(file);
        return path;
    }

    [Fact]
    public async Task A_successful_load_reports_started_then_completed()
    {
        var part = new Part { Source = new FileImageSource { File = "x.png" } };
        var cleared = false;

        await ImageSourcePartLoading.RunAsync(part, CancellationToken.None,
            (_, _) => Task.FromResult<Exception?>(null), () => cleared = true);

        part.Log.Should().Equal("LoadingStarted", "LoadingCompleted(True)");
        part.IsLoading.Should().BeFalse();
        cleared.Should().BeFalse();
    }

    [Fact]
    public async Task A_null_source_clears_the_picture_without_load_events()
    {
        var part = new Part();
        var cleared = false;

        await ImageSourcePartLoading.RunAsync(part, CancellationToken.None,
            (_, _) => throw new InvalidOperationException("no load for a null source"), () => cleared = true);

        cleared.Should().BeTrue();
        part.Log.Should().BeEmpty();
        part.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task A_failed_load_clears_the_picture_and_reports_the_failure()
    {
        var part = new Part { Source = new FileImageSource { File = "missing.png" } };
        var cleared = false;

        await ImageSourcePartLoading.RunAsync(part, CancellationToken.None,
            (_, _) => Task.FromResult<Exception?>(new FileNotFoundException()), () => cleared = true);

        cleared.Should().BeTrue();
        part.Log.Should().Equal("LoadingStarted", "LoadingFailed");
        part.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task A_load_replaced_by_a_newer_one_completes_unsuccessfully_and_changes_nothing()
    {
        var part = new Part { Source = new FileImageSource { File = "first.png" } };
        using var cts = new CancellationTokenSource();
        var cleared = false;

        await ImageSourcePartLoading.RunAsync(part, cts.Token, (_, _) =>
        {
            // A newer load starts while this one runs.
            part.Source = new FileImageSource { File = "second.png" };
            cts.Cancel();
            return Task.FromResult<Exception?>(new IOException());
        }, () => cleared = true);

        part.Log.Should().Equal("LoadingStarted", "LoadingCompleted(False)");
        cleared.Should().BeFalse();
    }

    [Fact]
    public async Task ClearImage_releases_a_cached_bitmap_without_disposing_it()
    {
        var path = CreatePng(SKColors.Red);
        var first = new SkiaImage();
        var second = new SkiaImage();
        await first.LoadFromFileAsync(path);
        await second.LoadFromFileAsync(path);
        var shared = second.Bitmap;
        shared.Should().NotBeNull();
        var clearedRaised = false;
        first.ImageCleared += (_, _) => clearedRaised = true;

        first.ClearImage();

        clearedRaised.Should().BeTrue();
        first.Bitmap.Should().BeNull();
        shared!.Handle.Should().NotBe(IntPtr.Zero, "the cached bitmap is still shown by the other view");
        shared.GetPixel(0, 0).Should().Be(SKColors.Red);
    }

    [Fact]
    public void ImageButton_ClearImage_removes_the_picture()
    {
        var button = new SkiaImageButton();
        button.LoadFromBitmap(new SKBitmap(4, 4));
        var clearedRaised = false;
        button.ImageCleared += (_, _) => clearedRaised = true;

        button.ClearImage();

        clearedRaised.Should().BeTrue();
        button.Bitmap.Should().BeNull();
    }

    [Fact]
    public async Task Image_source_set_to_null_or_a_missing_file_clears_the_picture()
    {
        var path = CreatePng(SKColors.Blue);
        var image = new Image { Source = ImageSource.FromFile(path), IsAnimationPlaying = true };
        using var host = new HeadlessMauiHost(new ContentPage { Content = image });
        var view = (SkiaImage)image.Handler!.PlatformView!;

        view.IsAnimationPlaying.Should().BeTrue("IsAnimationPlaying is mapped");
        for (var i = 0; i < 100 && (view.Bitmap is null || image.IsLoading); i++)
            await Task.Delay(20);
        view.Bitmap.Should().NotBeNull();
        image.IsLoading.Should().BeFalse();

        image.Source = null;
        view.Bitmap.Should().BeNull("a null source clears the picture");
        image.IsLoading.Should().BeFalse();

        image.Source = ImageSource.FromFile(path);
        for (var i = 0; i < 100 && view.Bitmap is null; i++)
            await Task.Delay(20);
        view.Bitmap.Should().NotBeNull();

        image.Source = ImageSource.FromFile(Path.Combine(Path.GetDirectoryName(path)!, "missing.png"));
        for (var i = 0; i < 100 && image.IsLoading; i++)
            await Task.Delay(20);
        view.Bitmap.Should().BeNull("a source that fails to load clears the picture");
        image.IsLoading.Should().BeFalse();
    }
}
