// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Hosting;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// Platform bugs found by the third-party compatibility suite (tests/Compat),
/// pinned here without the third-party packages.
/// </summary>
[Collection(HeadlessMaui.Collection)]
public class ThirdPartyCompatFixTests
{
    /// <summary>
    /// MAUI's default LineBreakMode is WordWrap, which draws through the
    /// multi-line path; that path ignored VerticalTextAlignment (found through
    /// CommunityToolkit's AvatarView, whose initials sat at the top).
    /// </summary>
    [Theory]
    [InlineData(TextAlignment.Start, 0, 30)]
    [InlineData(TextAlignment.Center, 35, 65)]
    [InlineData(TextAlignment.End, 70, 100)]
    public void Wrapping_label_honours_vertical_text_alignment(TextAlignment alignment, int bandTop, int bandBottom)
    {
        var label = new SkiaLabel
        {
            Text = "AB",
            FontSize = 14,
            TextColor = Colors.Black,
            LineBreakMode = LineBreakMode.WordWrap,
            VerticalTextAlignment = alignment,
        };
        label.Measure(new Size(100, 100));
        label.Arrange(new Rect(0, 0, 100, 100));

        using var surface = SKSurface.Create(new SKImageInfo(100, 100));
        surface.Canvas.Clear(SKColors.White);
        label.Draw(surface.Canvas);
        using var image = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);

        int top = -1, bottom = -1;
        for (int y = 0; y < 100; y++)
            for (int x = 0; x < 100; x++)
                if (bitmap.GetPixel(x, y).Red < 128)
                {
                    if (top < 0) top = y;
                    bottom = y;
                    break;
                }

        top.Should().BeGreaterThanOrEqualTo(bandTop, "the ink starts inside the expected band");
        bottom.Should().BeLessThan(bandBottom, "the ink ends inside the expected band");
    }

    /// <summary>
    /// Children present before the handler connects (XAML, or a layout that
    /// fills itself in its constructor such as LiveCharts' MotionCanvas) were
    /// added without their LayoutBounds/LayoutFlags and arranged at 0x0.
    /// </summary>
    [Fact]
    public void AbsoluteLayout_applies_bounds_of_children_added_before_the_handler()
    {
        var fill = new BoxView { Color = Colors.Red };
        var corner = new BoxView { Color = Colors.Blue };
        var layout = new AbsoluteLayout();
        AbsoluteLayout.SetLayoutBounds(fill, new Rect(0, 0, 1, 1));
        AbsoluteLayout.SetLayoutFlags(fill, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.All);
        AbsoluteLayout.SetLayoutBounds(corner, new Rect(10, 20, 30, 40));
        layout.Children.Add(fill);
        layout.Children.Add(corner);

        var context = HeadlessMaui.CreateContext();
        var handler = HeadlessMaui.AttachHandler<AbsoluteLayoutHandler>(layout, context);
        var platform = (SkiaView)handler.PlatformView!;
        platform.Measure(new Size(400, 300));
        platform.Arrange(new Rect(0, 0, 400, 300));

        ((SkiaView)fill.Handler!.PlatformView!).Bounds.Should().Be(new Rect(0, 0, 400, 300));
        ((SkiaView)corner.Handler!.PlatformView!).Bounds.Should().Be(new Rect(10, 20, 30, 40));
    }

    /// <summary>
    /// Application's default CreateWindow resolves an IWindowCreator from the
    /// activation state's services (how Prism creates its window). OpenMaui
    /// passed a null activation state, so such apps started without a window.
    /// </summary>
    [Fact]
    public void Startup_window_comes_from_a_registered_window_creator()
    {
        var page = new ContentPage();
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<PlainApplication>();
        builder.Services.AddSingleton<IWindowCreator>(new FixedWindowCreator(page));
        using var app = builder.Build();
        var application = app.Services.GetRequiredService<IApplication>();

        var window = StartupWindowFactory.Create(application, new MauiContext(app.Services));

        window.Should().BeOfType<Window>().Which.Page.Should().BeSameAs(page);
    }

    public sealed class PlainApplication : Application
    {
    }

    private sealed class FixedWindowCreator(Page page) : IWindowCreator
    {
        public Window CreateWindow(Application app, IActivationState? activationState) => new(page);
    }
}
