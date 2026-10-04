// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Media;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// The core handlers and view behaviour the MAUI Core conformance tests found missing
/// (docs/CONFORMANCE.md, "Core round 2"): handlers for core IRefreshView, IIndicatorView,
/// page and IStackNavigationView views, the Frame command on arrange, the tooltip on the
/// platform view, view capture, the size of a window without a display and the width of
/// wrapped label text.
/// </summary>
[Collection("LinuxApplication.Current")]
public class CoreHandlerParityTests
{
    private sealed class CoreRefreshView : View, IRefreshView
    {
        public bool IsRefreshing { get; set; }
        public Paint? RefreshColor { get; set; }
        public IView Content { get; set; } = null!;
        public bool IsRefreshEnabled { get; set; } = true;
    }

    private sealed class CoreIndicatorView : View, IIndicatorView
    {
        public int Count { get; set; }
        public int Position { get; set; }
        public double IndicatorSize { get; set; } = 10;
        public int MaximumVisible { get; set; } = int.MaxValue;
        public bool HideSingle { get; set; }
        public Paint? IndicatorColor { get; set; } = new SolidPaint(Colors.Gray);
        public Paint? SelectedIndicatorColor { get; set; } = new SolidPaint(Colors.Blue);
        public IShape IndicatorsShape { get; set; } = new Microsoft.Maui.Controls.Shapes.Rectangle();
    }

    private static IMauiContext Context() => HeadlessMauiContext.Instance;

    private static THandler Connect<THandler>(IElement view, IMauiContext context) where THandler : IElementHandler, new()
    {
        var handler = new THandler();
        handler.SetMauiContext(context);
        view.Handler = handler;
        handler.SetVirtualView(view);
        return handler;
    }

    [Fact]
    public void A_core_refresh_view_gets_the_core_handler_and_reports_a_pull()
    {
        var context = Context();
        var view = new CoreRefreshView { IsRefreshing = true, RefreshColor = new SolidPaint(Colors.Red), Content = new Label { Text = "list" } };

        var handler = Microsoft.Maui.Platform.Linux.Hosting.MauiHandlerExtensions.ToViewHandler(view, context);

        handler.Should().BeOfType<CoreRefreshViewHandler>();
        var platform = (SkiaRefreshView)handler!.PlatformView!;
        platform.IsRefreshing.Should().BeTrue();
        platform.RefreshColor.Should().Be(Colors.Red);
        platform.Content.Should().BeSameAs(((Label)view.Content).Handler!.PlatformView);

        view.IsRefreshing = false;
        handler.UpdateValue(nameof(IRefreshView.IsRefreshing));
        platform.IsRefreshing.Should().BeFalse();

        view.IsRefreshEnabled = false;
        handler.UpdateValue(nameof(IRefreshView.IsRefreshEnabled));
        platform.IsPullEnabled.Should().BeFalse();
    }

    [Fact]
    public void A_core_indicator_view_maps_its_shape_and_a_click_selects_the_position()
    {
        var context = Context();
        var view = new CoreIndicatorView { Count = 4, Position = 1 };

        var handler = Connect<CoreIndicatorViewHandler>(view, context);

        handler.PlatformView.Count.Should().Be(4);
        handler.PlatformView.Position.Should().Be(1);
        handler.PlatformView.IndicatorShape.Should().Be(Microsoft.Maui.Platform.IndicatorShape.Square);
        handler.PlatformView.SelectedIndicatorColor.Should().Be(Colors.Blue);

        handler.PlatformView.Position = 3; // what a click on the fourth indicator does
        view.Position.Should().Be(3);
    }

    [Fact]
    public void A_core_page_shows_its_content_and_title()
    {
        var context = Context();
        var content = new Label { Text = "content" };
        var page = new CorePage { Content = content, Title = "Core" };

        var handler = Connect<CorePageHandler>(page, context);

        handler.PlatformView.Content.Should().BeSameAs(content.Handler!.PlatformView);
        handler.PlatformView.Title.Should().Be("Core");
    }

    private sealed class CorePage : ContentView, ITitledElement
    {
        public string? Title { get; set; }
    }

    private sealed class CoreStack : View, IStackNavigationView
    {
        public readonly List<IReadOnlyList<IView>> Finished = new();
        public void RequestNavigation(NavigationRequest eventArgs) => Handler?.Invoke(nameof(IStackNavigation.RequestNavigation), eventArgs);
        public void NavigationFinished(IReadOnlyList<IView> newStack) => Finished.Add(newStack);
    }

    [Fact]
    public void A_core_stack_navigation_view_shows_the_requested_stack_and_reports_it_after_the_request()
    {
        var context = Context();
        var stack = new CoreStack();
        var handler = Connect<CoreNavigationViewHandler>(stack, context);

        var pages = new List<IView> { new Label { Text = "one" }, new Label { Text = "two" } };
        stack.RequestNavigation(new NavigationRequest(pages, false));

        handler.PlatformView.StackDepth.Should().Be(2);
        handler.PlatformView.CurrentPage!.Content.Should().BeSameAs(((Label)pages[1]).Handler!.PlatformView);
    }

    [Fact]
    public void Arranging_a_view_runs_the_frame_command()
    {
        var context = Context();
        var frames = new List<Rect>();
        var commands = new CommandMapper<IView, IViewHandler>(ViewHandler.ViewCommandMapper)
        {
            [nameof(IView.Frame)] = (h, v, a) => { if (a is Rect r) frames.Add(r); },
        };
        var label = new Label { Text = "x" };
        var handler = new Microsoft.Maui.Platform.Linux.Handlers.LabelHandler(Microsoft.Maui.Platform.Linux.Handlers.LabelHandler.Mapper, commands);
        handler.SetMauiContext(context);
        label.Handler = handler;

        handler.PlatformArrange(new Rect(0, 0, 100, 30));
        handler.PlatformArrange(new Rect(0, 0, -1, -1)); // Controls' "not laid out yet": no command

        frames.Should().Equal(new Rect(0, 0, 100, 30));
    }

    [Fact]
    public void A_tooltip_reaches_the_platform_view()
    {
        var context = Context();
        var button = new Button { Text = "go" };
        ToolTipProperties.SetText(button, "Go now");
        var handler = button.ToHandler(context);

        ((SkiaView)handler.PlatformView!).ToolTipText.Should().Be("Go now");

        ToolTipProperties.SetText(button, "Changed");
        ((SkiaView)handler.PlatformView!).ToolTipText.Should().Be("Changed");
    }

    [Fact]
    public async Task A_view_can_be_captured_to_an_image()
    {
        var context = Context();
        var box = new BoxView { Color = Colors.Red, WidthRequest = 20, HeightRequest = 10 };
        var handler = (IViewHandler)box.ToHandler(context);
        var platform = (SkiaView)handler.PlatformView!;
        platform.Measure(new Size(20, 10));
        platform.Arrange(new Rect(0, 0, 20, 10));

        var result = await ((IView)box).CaptureAsync();

        result.Should().NotBeNull();
        result!.Width.Should().BeGreaterThan(0);
        using var stream = await result.OpenReadAsync(ScreenshotFormat.Png);
        using var bitmap = SKBitmap.Decode(stream);
        bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2).Should().Be(SKColors.Red);
    }

    [Fact]
    public void A_window_without_a_display_takes_the_size_it_is_asked_for_within_its_limits()
    {
        using var app = new LinuxApplication();
        var context = app.AttachWindowContext(null, null, raisesMauiLifecycle: false);
        var window = new Window(new ContentPage()) { Width = 300, Height = 200, MinimumWidth = 250 };

        context.MauiWindow = window;
        context.IsHeadless.Should().BeTrue();
        context.LogicalSize.Should().Be(new Size(300, 200));
        window.Width.Should().Be(300);

        window.Width = 200; // below the minimum: the window stays at 250 and MAUI is told
        context.LogicalSize.Width.Should().Be(250);
        window.Width.Should().Be(250);

        window.MinimumWidth = 400;
        context.LogicalSize.Width.Should().Be(400);
        window.Width.Should().Be(400);
    }

    [Fact]
    public void Wrapped_label_text_is_as_wide_as_its_widest_line()
    {
        var label = new SkiaLabel { Text = "one two three four five six seven eight nine ten", LineBreakMode = LineBreakMode.WordWrap };

        var size = label.Measure(new Size(120, double.PositiveInfinity));

        size.Width.Should().BeLessThan(120);
        size.Height.Should().BeGreaterThan(label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)).Height);
    }
}
