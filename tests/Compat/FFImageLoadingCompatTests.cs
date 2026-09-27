// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FFImageLoading.Maui;
using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// FFImageLoading.Maui 1.3.2 (the maintained fork; ships net8.0/net9.0 assets,
/// consumed on net10.0). INCOMPATIBLE: the generic-TFM build contains no
/// CachedImage handler and registers no <c>IImageService</c>; decoding,
/// caching and the view are implemented per platform (Android, iOS/Mac
/// Catalyst, Windows) only. <c>UseFFImageLoading()</c> registers caches and a
/// scheduler, and CachedImage's constructor then fails to resolve the image
/// service. Supporting it needs an OpenMaui CachedImage handler plus an
/// FFImageLoading <c>IImageService</c> implementation over SkiaSharp.
/// </summary>
[Collection(CompatHost.Collection)]
public class FFImageLoadingCompatTests
{
    private const string Reason =
        "Incompatible: FFImageLoading.Maui 1.3.2's generic-TFM build registers no IImageService and has no CachedImage handler " +
        "(implementations exist only for Android, iOS/Mac Catalyst and Windows); new CachedImage() throws " +
        "'No service for type FFImageLoading.IImageService has been registered'.";

    private static string WritePng(SKColor color)
    {
        var path = Path.Combine(Path.GetTempPath(), $"openmaui-compat-{Guid.NewGuid():N}.png");
        using var bitmap = new SKBitmap(20, 20);
        bitmap.Erase(color);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    [Fact(Skip = Reason)]
    public void CachedImage_renders_its_source()
    {
        var png = WritePng(SKColors.Red);
        try
        {
            using var host = new CompatHost(new ContentPage { BackgroundColor = Colors.White }, b => b.UseFFImageLoading());
            // CachedImage resolves its services from the running app, so it is created after startup.
            ((ContentPage)host.Page).Content = new CachedImage { Source = ImageSource.FromFile(png), WidthRequest = 20, HeightRequest = 20, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
            host.Render();

            host.CountPixelsNear(SKColors.Red, new SKRectI(0, 0, 20, 20)).Should().BeGreaterThan(300);
        }
        finally
        {
            File.Delete(png);
        }
    }

    /// <summary>
    /// Pins the reason above: when this starts failing, FFImageLoading gained a
    /// generic-TFM image service and the library should be re-evaluated.
    /// </summary>
    [Fact]
    public void Finding_CachedImage_cannot_be_constructed_on_the_generic_tfm()
    {
        using var host = new CompatHost(new ContentPage(), b => b.UseFFImageLoading());

        var construct = () => new CachedImage();

        construct.Should().Throw<InvalidOperationException>().WithMessage("*FFImageLoading.IImageService*");
    }
}
