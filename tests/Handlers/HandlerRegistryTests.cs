// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Hosting;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// The platform resolves handlers from two places: its own map
/// (MauiHandlerExtensions, consulted first by the renderer) and the DI
/// registrations in UseLinux(). They once disagreed, which sent AbsoluteLayout
/// and FlexLayout to a StackLayout handler. These tests pin both to the correct
/// handler and to each other.
/// </summary>
[Collection("LinuxApplication.Current")]
public class HandlerRegistryTests
{
    private static IMauiHandlersFactory BuildFactory()
    {
        var builder = MauiApp.CreateBuilder(useDefaults: false);
        builder.UseMauiApp<Application>();
        builder.UseLinux();
        var app = builder.Build();
        return app.Services.GetRequiredService<IMauiHandlersFactory>();
    }

    [Theory]
    [InlineData(typeof(AbsoluteLayout), typeof(AbsoluteLayoutHandler))]
    [InlineData(typeof(FlexLayout), typeof(FlexLayoutHandler))]
    [InlineData(typeof(ContentView), typeof(ContentViewHandler))]
    [InlineData(typeof(Grid), typeof(GridHandler))]
    [InlineData(typeof(StackLayout), typeof(StackLayoutHandler))]
    [InlineData(typeof(VerticalStackLayout), typeof(StackLayoutHandler))]
    [InlineData(typeof(HorizontalStackLayout), typeof(StackLayoutHandler))]
    [InlineData(typeof(ScrollView), typeof(ScrollViewHandler))]
    [InlineData(typeof(Border), typeof(BorderHandler))]
    [InlineData(typeof(Frame), typeof(FrameHandler))]
    [InlineData(typeof(Microsoft.Maui.Controls.Shapes.Path), typeof(ShapePathHandler))]
    [InlineData(typeof(Rectangle), typeof(RectangleHandler))]
    [InlineData(typeof(Ellipse), typeof(EllipseHandler))]
    [InlineData(typeof(Line), typeof(LineHandler))]
    [InlineData(typeof(Polygon), typeof(PolygonHandler))]
    [InlineData(typeof(Polyline), typeof(PolylineHandler))]
    [InlineData(typeof(MenuBar), typeof(MenuBarHandler))]
    [InlineData(typeof(MenuFlyout), typeof(MenuFlyoutHandler))]
    [InlineData(typeof(CarouselView), typeof(CarouselViewHandler))]
    [InlineData(typeof(SwipeView), typeof(SwipeViewHandler))]
    [InlineData(typeof(RefreshView), typeof(RefreshViewHandler))]
    [InlineData(typeof(IndicatorView), typeof(IndicatorViewHandler))]
    [InlineData(typeof(GraphicsView), typeof(GraphicsViewHandler))]
    [InlineData(typeof(TabbedPage), typeof(TabbedPageHandler))]
    [InlineData(typeof(FlyoutPage), typeof(FlyoutPageHandler))]
    [InlineData(typeof(Shell), typeof(ShellHandler))]
    public void Platform_map_resolves_the_dedicated_handler(Type control, Type handler)
    {
        MauiHandlerExtensions.GetLinuxHandlerType(control).Should().Be(handler);
    }

    [Fact]
    public void DI_registrations_agree_with_the_platform_map_for_every_mapped_control()
    {
        var factory = BuildFactory();
        var disagreements = MauiHandlerExtensions.MappedControlTypes
            .Where(t => t != typeof(WebView)) // WebView is chosen at runtime (WPE vs WebKitGTK) in both places
            .Select(t => (Control: t, Map: MauiHandlerExtensions.GetLinuxHandlerType(t), Di: factory.GetHandlerType(t)))
            .Where(x => x.Di != null && x.Map != x.Di)
            .Select(x => $"{x.Control.Name}: map={x.Map!.Name} di={x.Di!.Name}")
            .ToList();

        disagreements.Should().BeEmpty("the renderer map and UseLinux() must resolve the same handler");
    }

    [Fact]
    public void Layouts_never_fall_back_to_the_generic_handler()
    {
        foreach (var t in new[] { typeof(AbsoluteLayout), typeof(FlexLayout), typeof(Grid), typeof(StackLayout) })
            MauiHandlerExtensions.GetLinuxHandlerType(t).Should().NotBe(typeof(LayoutHandler), $"{t.Name} must have its own handler");
    }

    [Fact]
    public void WebView_resolves_to_the_selected_backend_in_both_registries()
    {
        var expected = WebViewBackend.Resolve() == WebViewBackend.Kind.Wpe ? typeof(WpeWebViewHandler) : typeof(GtkWebViewHandler);
        MauiHandlerExtensions.GetLinuxHandlerType(typeof(WebView)).Should().Be(expected);
        BuildFactory().GetHandlerType(typeof(WebView)).Should().Be(expected);
    }
}
