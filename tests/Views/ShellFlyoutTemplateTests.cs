// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

/// <summary>
/// Shell.ItemTemplate rows and a flyout collapsed to a rail at run time
/// (the pattern GitCleaner uses: FlyoutWidth 280 -> 64, rows bound to a flag).
/// </summary>
[Collection("LinuxApplication.Current")]
public class ShellFlyoutTemplateTests
{
    private static Shell BuildShell(DataTemplate? template)
    {
        var shell = new Shell { FlyoutBehavior = FlyoutBehavior.Locked, FlyoutWidth = 280 };
        if (template != null)
            shell.ItemTemplate = template;
        shell.Items.Add(new FlyoutItem { Title = "Dashboard", Items = { new ShellContent { Content = new ContentPage() } } });
        shell.Items.Add(new FlyoutItem { Title = "History", Items = { new ShellContent { Content = new ContentPage() } } });
        return shell;
    }

    [Fact]
    public void Item_template_rows_are_bound_to_their_shell_item()
    {
        var template = new DataTemplate(() =>
        {
            var label = new Label { HeightRequest = 30 };
            label.SetBinding(Label.TextProperty, nameof(BaseShellItem.Title));
            return label;
        });

        using var host = new HeadlessMauiHost(BuildShell(template), withEngine: true);
        host.Context.Render();

        var skiaShell = host.RootView.Should().BeOfType<SkiaShell>().Subject;
        skiaShell.Sections.Should().HaveCount(2);
        skiaShell.Sections.Select(s => (s.TemplateView?.MauiView as Label)?.Text)
            .Should().Equal("Dashboard", "History");
    }

    [Fact]
    public void Without_a_template_the_built_in_rows_are_used()
    {
        using var host = new HeadlessMauiHost(BuildShell(null), withEngine: true);
        host.Context.Render();

        ((SkiaShell)host.RootView!).Sections.Should().OnlyContain(s => s.TemplateView == null);
    }

    [Fact]
    public void FlyoutWidth_changes_at_run_time_reach_the_flyout_including_a_narrow_rail()
    {
        var shell = BuildShell(null);
        using var host = new HeadlessMauiHost(shell, withEngine: true);
        host.Context.Render();
        var skiaShell = (SkiaShell)host.RootView!;
        skiaShell.FlyoutWidth.Should().Be(280);

        shell.FlyoutWidth = 64;
        host.Context.Render();

        skiaShell.FlyoutWidth.Should().Be(64);
    }

    [Fact]
    public void Replacing_a_shell_pages_content_at_run_time_shows_the_new_content()
    {
        // MAToolbar opens its overflow menu this way: the page's content is
        // wrapped in an AbsoluteLayout holding the original content and the menu.
        var original = new Label { Text = "page" };
        var page = new ContentPage { Content = original };
        var shell = new Shell { FlyoutBehavior = FlyoutBehavior.Disabled };
        shell.Items.Add(new ShellContent { Content = page });
        using var host = new HeadlessMauiHost(shell, withEngine: true);
        host.Context.Render();
        var originalView = (SkiaView)original.Handler!.PlatformView!;

        var menu = new Label { Text = "menu" };
        var overlay = new AbsoluteLayout();
        page.Content = null;
        AbsoluteLayout.SetLayoutBounds(original, new Microsoft.Maui.Graphics.Rect(0, 0, 1, 1));
        AbsoluteLayout.SetLayoutFlags(original, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.All);
        overlay.Children.Add(original);
        overlay.Children.Add(menu);
        page.Content = overlay;
        host.Context.Render();

        menu.Handler.Should().NotBeNull("the new content is rendered");
        ((SkiaView)menu.Handler!.PlatformView!).Bounds.Width.Should().BeGreaterThan(0);
        original.Handler!.PlatformView.Should().BeSameAs(originalView, "the moved content keeps its views");
    }

    [Fact]
    public void Children_added_to_a_swapped_in_absolute_layout_are_shown()
    {
        var original = new Label { Text = "page" };
        var page = new ContentPage { Content = original };
        var shell = new Shell { FlyoutBehavior = FlyoutBehavior.Disabled };
        shell.Items.Add(new ShellContent { Content = page });
        using var host = new HeadlessMauiHost(shell, withEngine: true);
        host.Context.Render();

        var overlay = new AbsoluteLayout();
        page.Content = null;
        AbsoluteLayout.SetLayoutBounds(original, new Microsoft.Maui.Graphics.Rect(0, 0, 1, 1));
        AbsoluteLayout.SetLayoutFlags(original, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.All);
        overlay.Children.Add(original);
        page.Content = overlay;

        // After the swap, as MAToolbar adds its dismiss layer and menu.
        var dismiss = new BoxView { BackgroundColor = Colors.Transparent };
        AbsoluteLayout.SetLayoutBounds(dismiss, new Microsoft.Maui.Graphics.Rect(0, 0, 1, 1));
        AbsoluteLayout.SetLayoutFlags(dismiss, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.All);
        overlay.Children.Add(dismiss);
        var menu = new Label { Text = "menu" };
        AbsoluteLayout.SetLayoutBounds(menu, new Microsoft.Maui.Graphics.Rect(100, 20, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
        overlay.Children.Add(menu);
        host.Context.Render();

        var bounds = ((SkiaView)menu.Handler!.PlatformView!).Bounds;
        bounds.Width.Should().BeGreaterThan(0);
        bounds.X.Should().BeApproximately(100, 1);
    }

    [Fact]
    public void An_auto_sized_menu_shown_after_being_added_measures_its_content()
    {
        // MAToolbar's overflow menu: a hidden Border with a minimum width,
        // added to the overlay at AutoSize, then made visible.
        var original = new Label { Text = "page" };
        var page = new ContentPage { Content = original };
        var shell = new Shell { FlyoutBehavior = FlyoutBehavior.Disabled };
        shell.Items.Add(new ShellContent { Content = page });
        using var host = new HeadlessMauiHost(shell, withEngine: true);
        host.Context.Render();

        var overlay = new AbsoluteLayout();
        page.Content = null;
        AbsoluteLayout.SetLayoutBounds(original, new Microsoft.Maui.Graphics.Rect(0, 0, 1, 1));
        AbsoluteLayout.SetLayoutFlags(original, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.All);
        overlay.Children.Add(original);
        page.Content = overlay;

        var items = new VerticalStackLayout
        {
            new Border { Padding = new Thickness(12, 8), Content = new Label { Text = "Refresh all" } },
            new Border { Padding = new Thickness(12, 8), Content = new Label { Text = "Settings" } },
        };
        var menu = new Border { IsVisible = false, Padding = 4, StrokeThickness = 1, MinimumWidthRequest = 150, Content = items };
        AbsoluteLayout.SetLayoutBounds(menu, new Microsoft.Maui.Graphics.Rect(100, 20, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
        overlay.Children.Add(menu);
        menu.IsVisible = true;
        host.Context.Render();

        var bounds = ((SkiaView)menu.Handler!.PlatformView!).Bounds;
        bounds.Width.Should().BeGreaterThanOrEqualTo(150, "MinimumWidthRequest");
        bounds.Height.Should().BeGreaterThan(40, "two rows of text plus padding");
    }

    [Fact]
    public void Shell_FlyoutContent_replaces_the_item_list_and_takes_input()
    {
        // Claude Toolkit: a custom navigation rail in FlyoutContent, the items hidden.
        var tapped = false;
        var railButton = new Button { Text = "Scanner" };
        railButton.Clicked += (_, _) => tapped = true;
        var shell = new Shell { FlyoutBehavior = FlyoutBehavior.Locked, FlyoutWidth = 240, FlyoutContent = new VerticalStackLayout { Children = { railButton } } };
        shell.Items.Add(new FlyoutItem { Title = "Scanner", FlyoutItemIsVisible = false, Items = { new ShellContent { Content = new ContentPage() } } });
        using var host = new HeadlessMauiHost(shell, withEngine: true);
        host.Context.Render();

        var button = (SkiaView)railButton.Handler!.PlatformView!;
        button.Bounds.Width.Should().BeGreaterThan(0, "the flyout content is laid out in the flyout");
        button.Bounds.Right.Should().BeLessThanOrEqualTo(241);

        host.DisplayWindow.RaisePointerPressed((float)button.Bounds.Center.X, (float)button.Bounds.Center.Y);
        host.DisplayWindow.RaisePointerReleased((float)button.Bounds.Center.X, (float)button.Bounds.Center.Y);
        tapped.Should().BeTrue();
    }
}
