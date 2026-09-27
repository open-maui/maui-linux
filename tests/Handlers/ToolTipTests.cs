// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>MAUI's ToolTipProperties.Text: shown on hover, as on Windows and Mac.</summary>
[Collection("LinuxApplication.Current")]
public class ToolTipTests
{
    private static Microsoft.Maui.Platform.PointerEventArgs At(SkiaView view) =>
        new((float)view.Bounds.Center.X, (float)view.Bounds.Center.Y, PointerButton.None);

    [Fact]
    public void Hovering_a_view_with_a_tooltip_shows_it_and_leaving_hides_it()
    {
        var button = new Button { Text = "Refresh", WidthRequest = 120, HeightRequest = 40 };
        ToolTipProperties.SetText(button, "Refresh the list");
        var plain = new Label { Text = "plain", HeightRequest = 40 };
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { button, plain } }, withEngine: true);
        host.Context.Render();
        var tips = host.Context.ToolTips;
        tips.Schedule = (_, _) => { }; // the test decides when the delay has passed

        host.Context.OnPointerMoved(null, At((SkiaView)button.Handler!.PlatformView!));
        tips.ShownText.Should().BeNull("it waits for the pointer to rest");
        tips.ShowPending();
        tips.ShownText.Should().Be("Refresh the list");

        host.Context.OnPointerMoved(null, At((SkiaView)plain.Handler!.PlatformView!));
        tips.ShownText.Should().BeNull();
        tips.Target.Should().BeNull();
    }

    [Fact]
    public void A_tooltip_on_a_container_shows_over_its_content_and_a_press_dismisses_it()
    {
        var label = new Label { Text = "inside" };
        var border = new Border { Content = label, HeightRequest = 40 };
        ToolTipProperties.SetText(border, "Container tip");
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { border } }, withEngine: true);
        host.Context.Render();
        var tips = host.Context.ToolTips;
        tips.Schedule = (_, _) => { }; // the test decides when the delay has passed

        var at = At((SkiaView)label.Handler!.PlatformView!);
        host.Context.OnPointerMoved(null, at);
        tips.Target.Should().BeSameAs(border);
        tips.ShowPending();
        tips.ShownText.Should().Be("Container tip");

        host.Context.OnPointerPressed(null, new Microsoft.Maui.Platform.PointerEventArgs(at.X, at.Y, PointerButton.Left));
        host.Context.OnPointerReleased(null, new Microsoft.Maui.Platform.PointerEventArgs(at.X, at.Y, PointerButton.Left));
        tips.ShownText.Should().BeNull();
        tips.ShowPending();
        tips.ShownText.Should().BeNull("after a press it stays hidden until the pointer reaches another element");
    }

    [Fact]
    public void The_card_is_drawn_inside_the_window()
    {
        var tips = new ToolTipController(() => { });
        var button = new Button();
        ToolTipProperties.SetText(button, "A tip near the right edge");
        typeof(ToolTipController).GetField("_target", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(tips, button);
        typeof(ToolTipController).GetField("_pointerX", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(tips, 195f);
        tips.ShowPending();

        using var bitmap = new SKBitmap(200, 60);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Magenta);
        tips.Draw(canvas, 200, 60);

        bitmap.GetPixel(199, 30).Should().Be(SKColors.Magenta, "the card is clamped inside the window");
        Enumerable.Range(0, 200).Any(x => bitmap.GetPixel(x, 30) != SKColors.Magenta).Should().BeTrue("the card is drawn");
    }

    [Fact]
    public void Hovering_a_button_reaches_a_pointer_recognizer_on_the_view_around_it()
    {
        // MAToolTip's rich cards: a PointerGestureRecognizer on a ContentView
        // around the button. Buttons handled enter/exit without passing them on.
        var button = new Button { Text = "Go", WidthRequest = 80, HeightRequest = 40 };
        var wrapper = new ContentView { Content = button };
        var entered = 0;
        var exited = 0;
        var pointer = new PointerGestureRecognizer();
        pointer.PointerEntered += (_, _) => entered++;
        pointer.PointerExited += (_, _) => exited++;
        wrapper.GestureRecognizers.Add(pointer);
        var below = new Label { Text = "below", HeightRequest = 40 };
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { wrapper, below } }, withEngine: true);
        host.Context.Render();

        host.Context.OnPointerMoved(null, At((SkiaView)button.Handler!.PlatformView!));
        entered.Should().BeGreaterThan(0);
        host.Context.OnPointerMoved(null, At((SkiaView)below.Handler!.PlatformView!));
        exited.Should().BeGreaterThan(0);
    }
}
