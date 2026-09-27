// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Layouts;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Services;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// Third-party and app-defined layouts: a Layout subclass with its own
/// ILayoutManager (the shape of MarketAlly's MALayout) renders through its own
/// measure/arrange, and MAUI-side measurement returns real sizes.
/// </summary>
[Collection("LinuxApplication.Current")]
public class CustomLayoutTests
{
    /// <summary>Places children side by side, each in a fixed-width column.</summary>
    private sealed class ColumnsLayout : Layout, ILayoutManager
    {
        public double ColumnWidth { get; set; } = 150;
        public int MeasureCalls { get; private set; }
        public int ArrangeCalls { get; private set; }

        protected override ILayoutManager CreateLayoutManager() => this;

        public Size Measure(double widthConstraint, double heightConstraint)
        {
            MeasureCalls++;
            double height = 0;
            foreach (var child in this)
                height = Math.Max(height, child.Measure(ColumnWidth, heightConstraint).Height);
            return new Size(ColumnWidth * Count, height);
        }

        public Size ArrangeChildren(Rect bounds)
        {
            ArrangeCalls++;
            double x = bounds.X;
            foreach (var child in this)
            {
                child.Arrange(new Rect(x, bounds.Y, ColumnWidth, bounds.Height));
                x += ColumnWidth;
            }
            return bounds.Size;
        }
    }

    [Fact]
    public void A_custom_layout_gets_the_cross_platform_layout_handler()
    {
        var layout = new ColumnsLayout();
        var platform = HeadlessMauiContext.Realize<SkiaView>(layout);

        layout.Handler.Should().BeOfType<CrossPlatformLayoutHandler>();
        platform.Should().BeOfType<SkiaCrossPlatformLayout>();
    }

    [Fact]
    public void A_custom_layout_places_children_with_its_own_manager()
    {
        var first = new BoxView { Color = Colors.Red, HeightRequest = 40 };
        var second = new BoxView { Color = Colors.Blue, HeightRequest = 40 };
        var layout = new ColumnsLayout { first, second };
        using var host = new HeadlessMauiHost(new ContentPage { Content = layout }, withEngine: true);
        host.Context.Render();

        layout.MeasureCalls.Should().BeGreaterThan(0);
        layout.ArrangeCalls.Should().BeGreaterThan(0);
        var a = ((SkiaView)first.Handler!.PlatformView!).Bounds;
        var b = ((SkiaView)second.Handler!.PlatformView!).Bounds;
        // Each child gets a 150x600 slot; MAUI's own ComputeFrame then centres
        // the 40-high request vertically in it, as on every platform.
        a.Should().Be(new Rect(0, 280, 150, 40));
        b.Should().Be(new Rect(150, 280, 150, 40));
    }

    [Fact]
    public void A_custom_layout_paints_its_children()
    {
        var layout = new ColumnsLayout { new BoxView { Color = Colors.Red } };
        using var host = new HeadlessMauiHost(new ContentPage { Content = layout }, withEngine: true);
        host.Context.Render();

        var (r, g, bl, _) = host.DisplayWindow.PixelAt(75, 300);
        r.Should().BeGreaterThan(200);
        g.Should().BeLessThan(60);
        bl.Should().BeLessThan(60);
    }

    [Fact]
    public void A_custom_layout_arranges_in_its_own_coordinates()
    {
        // MAUI hands CrossPlatformArrange local bounds on every platform, and
        // many managers place children from (0,0) (Syncfusion's lists do).
        var bounds = new List<Rect>();
        var probe = new ProbeLayout(bounds) { new BoxView { Color = Colors.Green } };
        var outer = new ColumnsLayout { new BoxView(), probe };
        using var host = new HeadlessMauiHost(new ContentPage { Content = outer }, withEngine: true);
        host.Context.Render();

        bounds.Should().NotBeEmpty();
        bounds[^1].X.Should().Be(0);
        bounds[^1].Y.Should().Be(0);
        ((SkiaView)probe[0].Handler!.PlatformView!).Bounds.X.Should().Be(150);
    }

    /// <summary>Records the arrange bounds and fills them from (0,0).</summary>
    private sealed class ProbeLayout(List<Rect> seen) : Layout, ILayoutManager
    {
        protected override ILayoutManager CreateLayoutManager() => this;

        public Size Measure(double widthConstraint, double heightConstraint)
        {
            foreach (var child in this)
                child.Measure(widthConstraint, heightConstraint);
            return new Size(widthConstraint, heightConstraint);
        }

        public Size ArrangeChildren(Rect bounds)
        {
            seen.Add(bounds);
            foreach (var child in this)
                child.Arrange(new Rect(0, 0, bounds.Width, bounds.Height));
            return bounds.Size;
        }
    }

    [Fact]
    public void Nested_custom_layouts_arrange_in_window_coordinates()
    {
        var inner = new ColumnsLayout { ColumnWidth = 50 };
        var leaf = new BoxView { Color = Colors.Green };
        inner.Add(leaf);
        var outer = new ColumnsLayout { new BoxView(), inner };
        using var host = new HeadlessMauiHost(new ContentPage { Content = outer }, withEngine: true);
        host.Context.Render();

        ((SkiaView)leaf.Handler!.PlatformView!).Bounds.X.Should().Be(150, "the inner layout starts at the outer's second column");
    }

    [Fact]
    public void MAUI_side_measure_returns_real_sizes()
    {
        var label = new Label { Text = "Measure me", FontSize = 20 };
        HeadlessMauiContext.Realize<SkiaLabel>(label);

        var size = ((IView)label).Measure(double.PositiveInfinity, double.PositiveInfinity);

        size.Width.Should().BeGreaterThan(50);
        size.Height.Should().BeGreaterThan(10);
    }

    [Fact]
    public void MAUI_side_measure_honours_explicit_and_maximum_sizes()
    {
        var box = new BoxView { WidthRequest = 80, HeightRequest = 30, MaximumWidthRequest = 60 };
        HeadlessMauiContext.Realize<SkiaView>(box);

        var size = ((IView)box).Measure(500, 500);

        size.Width.Should().Be(60);
        size.Height.Should().Be(30);
    }
}

public class ShellPageFactoryTests
{
    public sealed class Dependency { }

    public sealed class InjectedPage : ContentPage
    {
        public InjectedPage(Dependency dependency) => Dependency = dependency;
        public Dependency Dependency { get; }
    }

    public sealed class FactoryPage : ContentPage
    {
        public FactoryPage(string label) => Title = label;
    }

    public sealed class UnresolvablePage : ContentPage
    {
        public UnresolvablePage(Dependency dependency) { }
    }

    [Fact]
    public void A_registered_page_is_resolved_from_its_registration()
    {
        var services = new ServiceCollection()
            .AddTransient(_ => new FactoryPage("from factory"))
            .BuildServiceProvider();

        var page = ShellPageFactory.Create(new DataTemplate(typeof(FactoryPage)), services);

        page.Should().BeOfType<FactoryPage>().Which.Title.Should().Be("from factory");
    }

    [Fact]
    public void An_unregistered_page_gets_constructor_injection()
    {
        var dependency = new Dependency();
        var services = new ServiceCollection().AddSingleton(dependency).BuildServiceProvider();

        var page = ShellPageFactory.Create(new DataTemplate(typeof(InjectedPage)), services);

        page.Should().BeOfType<InjectedPage>().Which.Dependency.Should().BeSameAs(dependency);
    }

    [Fact]
    public void A_scoped_dependency_resolves_from_a_window_scope()
    {
        var services = new ServiceCollection()
            .AddScoped<Dependency>()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = services.CreateScope();

        var page = ShellPageFactory.Create(new DataTemplate(typeof(InjectedPage)), scope.ServiceProvider);

        page.Should().BeOfType<InjectedPage>();
    }

    [Fact]
    public void A_page_that_cannot_be_built_yields_null_instead_of_throwing()
    {
        var services = new ServiceCollection().BuildServiceProvider();

        var page = ShellPageFactory.Create(new DataTemplate(typeof(UnresolvablePage)), services);

        page.Should().BeNull();
    }
}

public class AppIconCacheTests
{
    [Fact]
    public void A_generated_icon_goes_to_the_user_cache_when_the_app_folder_is_read_only()
    {
        var root = Path.Combine(Path.GetTempPath(), "openmaui-icon-" + Guid.NewGuid().ToString("N"));
        var appDir = Path.Combine(root, "app");
        var cacheDir = Path.Combine(root, "cache");
        Directory.CreateDirectory(appDir);
        var meta = Path.Combine(appDir, "appicon.meta");
        File.WriteAllText(meta, "Size=64\nColor=#FF0000\nScale=0.5\n");
        File.SetUnixFileMode(appDir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        var previous = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        try
        {
            Environment.SetEnvironmentVariable("XDG_CACHE_HOME", cacheDir);

            var icon = MauiIconGenerator.GenerateIcon(meta);

            icon.Should().NotBeNull();
            icon!.Should().StartWith(cacheDir);
            File.Exists(icon).Should().BeTrue();
            File.Exists(Path.Combine(appDir, "appicon.png")).Should().BeFalse();

            // A second start reuses the cached icon instead of regenerating it.
            var stamp = File.GetLastWriteTimeUtc(icon);
            MauiIconGenerator.GenerateIcon(meta).Should().Be(icon);
            File.GetLastWriteTimeUtc(icon).Should().Be(stamp);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CACHE_HOME", previous);
            File.SetUnixFileMode(appDir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Directory.Delete(root, true);
        }
    }
    [Fact]
    public void A_single_file_icon_is_drawn_whole_on_a_transparent_background()
    {
        var root = Path.Combine(Path.GetTempPath(), "openmaui-icon-" + Guid.NewGuid().ToString("N"));
        var appDir = Path.Combine(root, "app");
        Directory.CreateDirectory(appDir);
        // An offset viewBox, like Illustrator exports: the drawing must still
        // land centred in the icon.
        File.WriteAllText(Path.Combine(appDir, "appicon_bg.svg"),
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='100 100 40 40' width='40' height='40'>" +
            "<rect x='110' y='110' width='20' height='20' fill='#00FF00'/></svg>");
        var meta = Path.Combine(appDir, "appicon.meta");
        File.WriteAllText(meta, "Format=2\nBackground=appicon_bg.svg\nBackgroundColor=\nTintColor=\nSize=64\nScale=0.65\n");
        var previous = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        try
        {
            Environment.SetEnvironmentVariable("XDG_CACHE_HOME", Path.Combine(root, "cache"));

            var icon = MauiIconGenerator.GenerateIcon(meta);

            using var bitmap = SkiaSharp.SKBitmap.Decode(icon);
            bitmap.GetPixel(32, 32).Should().Be(new SkiaSharp.SKColor(0, 255, 0));
            bitmap.GetPixel(2, 2).Alpha.Should().Be(0);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CACHE_HOME", previous);
            Directory.Delete(root, true);
        }
    }
}
