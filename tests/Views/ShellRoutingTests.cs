// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Handlers;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

using PointerEventArgs = Microsoft.Maui.Platform.PointerEventArgs;
using PointerButton = Microsoft.Maui.Platform.PointerButton;

/// <summary>
/// Shell routing through MAUI's own Shell API with the Linux ShellHandler
/// attached: MAUI's ShellNavigationManager resolves routes, applies query
/// attributes and raises the navigation events; SkiaShell mirrors the current
/// content and the section's page stack. Every test drives navigation the
/// way an app does (Shell.Current.GoToAsync / Navigation) and asserts both
/// the MAUI state and what the platform presents.
/// </summary>
[Collection(HeadlessMaui.Collection)]
public class ShellRoutingTests
{
    private const string DetailsRoute = "routing-details";

    static ShellRoutingTests()
    {
        Routing.RegisterRoute(DetailsRoute, typeof(DetailsPage));
    }

    #region Pages

    private static readonly List<string> s_lifecycle = new();

    private class LoggingPage : ContentPage
    {
        public string Name { get; }
        public LoggingPage(string name)
        {
            Name = name;
            Title = name;
            Content = new Label { Text = name };
        }
        protected override void OnAppearing() { s_lifecycle.Add(Name + ":Appearing"); base.OnAppearing(); }
        protected override void OnDisappearing() { s_lifecycle.Add(Name + ":Disappearing"); base.OnDisappearing(); }
    }

    private sealed class HomePage : LoggingPage { public HomePage() : base("Home") { } }
    private sealed class AboutPage : LoggingPage { public AboutPage() : base("About") { } }
    private sealed class SecondTabPage : LoggingPage { public SecondTabPage() : base("Tab2") { } }

    [QueryProperty(nameof(ItemId), "id")]
    [QueryProperty(nameof(ItemName), "name")]
    private sealed class DetailsPage : LoggingPage, IQueryAttributable
    {
        public string? ItemId { get; set; }
        public string? ItemName { get; set; }
        public IDictionary<string, object>? AppliedQuery { get; private set; }

        public DetailsPage() : base("Details") { }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
            => AppliedQuery = new Dictionary<string, object>(query);
    }

    #endregion

    #region Host

    private sealed class Host
    {
        public Shell Shell = null!;
        public SkiaShell Platform = null!;
        public ShellHandler Handler = null!;
        public HomePage Home = null!;
        public AboutPage About = null!;
        public SecondTabPage Tab2 = null!;
        public List<ShellNavigatingEventArgs> Navigating = new();
        public List<ShellNavigatedEventArgs> Navigated = new();
    }

    /// <summary>
    /// Shell: FlyoutItem "home" (contents "homec" and "tab2") and FlyoutItem
    /// "about" (content "aboutc"); hosted in a window; ShellHandler attached
    /// and laid out at 800x600 so pointer input can be injected.
    /// </summary>
    private static Host CreateHost()
    {
        s_lifecycle.Clear();
        var ctx = HeadlessMaui.CreateContext();

        var host = new Host
        {
            Home = new HomePage(),
            About = new AboutPage(),
            Tab2 = new SecondTabPage(),
        };

        var shell = new Shell { Title = "Test Shell" };
        shell.Items.Add(new FlyoutItem
        {
            Route = "home",
            Title = "Home",
            Items =
            {
                new ShellContent { Route = "homec", Title = "Home", Content = host.Home },
                new ShellContent { Route = "tab2", Title = "Tab2", Content = host.Tab2 },
            }
        });
        shell.Items.Add(new FlyoutItem
        {
            Route = "about",
            Title = "About",
            Items = { new ShellContent { Route = "aboutc", Title = "About", Content = host.About } }
        });
        shell.Navigating += (s, e) => host.Navigating.Add(e);
        shell.Navigated += (s, e) => host.Navigated.Add(e);

        HeadlessMaui.HostInWindow(shell);

        var handler = (ShellHandler)Microsoft.Maui.Platform.Linux.Hosting.MauiHandlerExtensions.ToHandler(shell, ctx);
        var platform = (SkiaShell)handler.PlatformView!;
        platform.Measure(new Size(800, 600));
        platform.Arrange(new Rect(0, 0, 800, 600));

        host.Shell = shell;
        host.Platform = platform;
        host.Handler = handler;
        // MAUI raises its initial ShellItemChanged navigation once the shell
        // is parented; tests observe only what they trigger.
        host.Navigating.Clear();
        host.Navigated.Clear();
        return host;
    }

    private static SkiaView? PlatformViewOf(Page page) => page.Handler?.PlatformView as SkiaView;

    private static string Describe(IEnumerable<ShellNavigatedEventArgs> events)
        => string.Join(", ", events.Select(e => $"{e.Source}:{e.Previous?.Location.OriginalString}->{e.Current?.Location.OriginalString}"));

    #endregion

    [Fact]
    public void Hosted_shell_is_Shell_Current_and_platform_shows_first_content()
    {
        var h = CreateHost();

        Shell.Current.Should().BeSameAs(h.Shell);
        h.Shell.CurrentState.Location.OriginalString.Should().Be("//home/homec");
        h.Platform.CurrentSectionIndex.Should().Be(0);
        h.Platform.CurrentItemIndex.Should().Be(0);
        h.Platform.CurrentContent.Should().BeSameAs(PlatformViewOf(h.Home));
        h.Platform.NavigationStackDepth.Should().Be(0);
    }

    [Fact]
    public async Task Absolute_route_switches_the_presented_section()
    {
        var h = CreateHost();

        await Shell.Current.GoToAsync("//about");

        h.Shell.CurrentState.Location.OriginalString.Should().Be("//about/aboutc");
        h.Platform.CurrentSectionIndex.Should().Be(1);
        h.Platform.CurrentContent.Should().BeSameAs(PlatformViewOf(h.About));
        h.Platform.CurrentMauiPage.Should().BeSameAs(h.About);
        h.Platform.Title.Should().Be("About");
    }

    [Fact]
    public async Task Absolute_route_to_a_content_within_a_section_selects_that_tab()
    {
        var h = CreateHost();

        await Shell.Current.GoToAsync("//home/tab2");

        h.Shell.CurrentState.Location.OriginalString.Should().Be("//home/tab2");
        h.Platform.CurrentSectionIndex.Should().Be(0);
        h.Platform.CurrentItemIndex.Should().Be(1);
        h.Platform.CurrentContent.Should().BeSameAs(PlatformViewOf(h.Tab2));
    }

    [Fact]
    public async Task Registered_route_pushes_the_page_and_the_platform_presents_it()
    {
        var h = CreateHost();

        await Shell.Current.GoToAsync(DetailsRoute);

        h.Shell.CurrentPage.Should().BeOfType<DetailsPage>();
        h.Shell.CurrentState.Location.OriginalString.Should().Be($"//home/homec/{DetailsRoute}");
        h.Platform.NavigationStackDepth.Should().Be(1);
        h.Platform.CanGoBack.Should().BeTrue();
        h.Platform.CurrentContent.Should().BeSameAs(PlatformViewOf(h.Shell.CurrentPage));
        h.Platform.CurrentMauiPage.Should().BeSameAs(h.Shell.CurrentPage);
        h.Platform.Title.Should().Be("Details");
    }

    [Fact]
    public async Task DotDot_pops_back_to_the_section_root()
    {
        var h = CreateHost();
        await Shell.Current.GoToAsync(DetailsRoute);

        await Shell.Current.GoToAsync("..");

        h.Shell.CurrentState.Location.OriginalString.Should().Be("//home/homec");
        h.Shell.CurrentPage.Should().BeSameAs(h.Home);
        h.Platform.NavigationStackDepth.Should().Be(0);
        h.Platform.CanGoBack.Should().BeFalse();
        h.Platform.CurrentContent.Should().BeSameAs(PlatformViewOf(h.Home));
        h.Platform.Title.Should().Be("Home");
    }

    [Fact]
    public async Task Query_string_sets_QueryProperty_attributes_on_the_target_page()
    {
        var h = CreateHost();

        await Shell.Current.GoToAsync($"{DetailsRoute}?id=42&name=x");

        var details = h.Shell.CurrentPage.Should().BeOfType<DetailsPage>().Subject;
        details.ItemId.Should().Be("42");
        details.ItemName.Should().Be("x");
        h.Platform.CurrentContent.Should().BeSameAs(PlatformViewOf(details));
    }

    [Fact]
    public async Task Query_string_calls_IQueryAttributable_with_every_parameter()
    {
        var h = CreateHost();

        await Shell.Current.GoToAsync($"{DetailsRoute}?id=42&name=x");

        var details = (DetailsPage)h.Shell.CurrentPage;
        details.AppliedQuery.Should().NotBeNull();
        details.AppliedQuery!.Should().ContainKey("id").WhoseValue.Should().Be("42");
        details.AppliedQuery.Should().ContainKey("name").WhoseValue.Should().Be("x");
    }

    [Fact]
    public async Task Dictionary_parameters_reach_IQueryAttributable_as_objects()
    {
        var h = CreateHost();
        var item = new object();

        await Shell.Current.GoToAsync(DetailsRoute, new Dictionary<string, object> { ["item"] = item });

        var details = (DetailsPage)h.Shell.CurrentPage;
        details.AppliedQuery.Should().NotBeNull();
        details.AppliedQuery!["item"].Should().BeSameAs(item);
    }

    [Fact]
    public async Task CurrentState_Location_tracks_each_navigation()
    {
        var h = CreateHost();
        var locations = new List<string>();

        await Shell.Current.GoToAsync("//about");
        locations.Add(h.Shell.CurrentState.Location.OriginalString);
        await Shell.Current.GoToAsync(DetailsRoute);
        locations.Add(h.Shell.CurrentState.Location.OriginalString);
        await Shell.Current.GoToAsync("..");
        locations.Add(h.Shell.CurrentState.Location.OriginalString);
        await Shell.Current.GoToAsync("//home");
        locations.Add(h.Shell.CurrentState.Location.OriginalString);

        locations.Should().Equal("//about/aboutc", $"//about/aboutc/{DetailsRoute}", "//about/aboutc", "//home/homec");
    }

    [Fact]
    public async Task Navigating_and_Navigated_fire_with_source_and_target_for_a_push()
    {
        var h = CreateHost();

        await Shell.Current.GoToAsync(DetailsRoute);

        var navigating = h.Navigating.Should().ContainSingle().Subject;
        navigating.Source.Should().Be(ShellNavigationSource.Push);
        navigating.Current.Location.OriginalString.Should().Be("//home/homec");
        navigating.Target.Location.OriginalString.Should().Be(DetailsRoute);

        var navigated = h.Navigated.Should().ContainSingle(Describe(h.Navigated)).Subject;
        navigated.Source.Should().Be(ShellNavigationSource.Push);
        navigated.Previous.Location.OriginalString.Should().Be("//home/homec");
        navigated.Current.Location.OriginalString.Should().Be($"//home/homec/{DetailsRoute}");
    }

    [Fact]
    public async Task Navigating_and_Navigated_fire_for_a_section_switch_and_a_pop()
    {
        var h = CreateHost();

        await Shell.Current.GoToAsync("//about");
        h.Navigating.Last().Source.Should().Be(ShellNavigationSource.ShellItemChanged);
        h.Navigated.Last().Source.Should().Be(ShellNavigationSource.ShellItemChanged);
        h.Navigated.Last().Current.Location.OriginalString.Should().Be("//about/aboutc");

        await Shell.Current.GoToAsync(DetailsRoute);
        await Shell.Current.GoToAsync("..");
        // MAUI reports a pop that lands on the section root as PopToRoot.
        h.Navigating.Last().Source.Should().BeOneOf(ShellNavigationSource.Pop, ShellNavigationSource.PopToRoot);
        h.Navigating.Last().Target.Location.OriginalString.Should().Be("..");
        h.Navigated.Last().Source.Should().BeOneOf(ShellNavigationSource.Pop, ShellNavigationSource.PopToRoot);
        h.Navigated.Last().Previous.Location.OriginalString.Should().Be($"//about/aboutc/{DetailsRoute}");
        h.Navigated.Last().Current.Location.OriginalString.Should().Be("//about/aboutc");
    }

    [Fact]
    public async Task Cancelled_navigation_leaves_the_platform_untouched()
    {
        var h = CreateHost();
        h.Shell.Navigating += (s, e) => e.Cancel();

        await Shell.Current.GoToAsync(DetailsRoute);

        h.Navigated.Should().BeEmpty(Describe(h.Navigated));
        h.Shell.CurrentPage.Should().BeSameAs(h.Home);
        h.Platform.NavigationStackDepth.Should().Be(0);
        h.Platform.CurrentContent.Should().BeSameAs(PlatformViewOf(h.Home));
    }

    [Fact]
    public async Task Page_lifecycle_order_on_push_and_pop()
    {
        var h = CreateHost();
        s_lifecycle.Should().Equal("Home:Appearing");
        s_lifecycle.Clear();

        await Shell.Current.GoToAsync(DetailsRoute);
        s_lifecycle.Should().Equal("Home:Disappearing", "Details:Appearing");

        s_lifecycle.Clear();
        await Shell.Current.GoToAsync("..");
        s_lifecycle.Should().Equal("Details:Disappearing", "Home:Appearing");
    }

    [Fact]
    public async Task Page_lifecycle_order_on_section_switch()
    {
        var h = CreateHost();
        s_lifecycle.Clear();

        await Shell.Current.GoToAsync("//about");
        s_lifecycle.Should().Equal("Home:Disappearing", "About:Appearing");

        s_lifecycle.Clear();
        await Shell.Current.GoToAsync("//home");
        s_lifecycle.Should().Equal("About:Disappearing", "Home:Appearing");
    }

    [Fact]
    public async Task Absolute_route_back_to_a_section_pops_its_stack_as_MAUI_does()
    {
        var h = CreateHost();
        await Shell.Current.GoToAsync(DetailsRoute);

        await Shell.Current.GoToAsync("//about");
        h.Platform.NavigationStackDepth.Should().Be(0);
        h.Platform.CurrentContent.Should().BeSameAs(PlatformViewOf(h.About));

        // "//home" is an absolute request: MAUI replaces the section's stack.
        await Shell.Current.GoToAsync("//home");
        h.Shell.CurrentPage.Should().BeSameAs(h.Home);
        h.Platform.NavigationStackDepth.Should().Be(0);
        h.Platform.CurrentContent.Should().BeSameAs(PlatformViewOf(h.Home));
    }

    [Fact]
    public async Task Flyout_selection_back_to_a_section_restores_its_pushed_pages()
    {
        var h = CreateHost();
        await Shell.Current.GoToAsync(DetailsRoute);
        var details = h.Shell.CurrentPage;

        await Shell.Current.GoToAsync("//about");
        h.Platform.CurrentContent.Should().BeSameAs(PlatformViewOf(h.About));

        // A flyout selection navigates to the item with its existing stack
        // (Shell.OnFlyoutItemSelectedAsync), so the pushed page comes back.
        h.Platform.FlyoutIsPresented = true;
        h.Platform.OnPointerPressed(new PointerEventArgs(40, 24, PointerButton.Left));

        h.Shell.CurrentPage.Should().BeSameAs(details);
        h.Shell.CurrentState.Location.OriginalString.Should().Be($"//home/homec/{DetailsRoute}");
        h.Platform.NavigationStackDepth.Should().Be(1);
        h.Platform.CurrentContent.Should().BeSameAs(PlatformViewOf(details));
    }

    [Fact]
    public async Task Navigation_PushAsync_and_PopAsync_are_mirrored()
    {
        var h = CreateHost();
        var pushed = new DetailsPage { Title = "Pushed" };

        await Shell.Current.Navigation.PushAsync(pushed);
        h.Platform.NavigationStackDepth.Should().Be(1);
        h.Platform.CurrentContent.Should().BeSameAs(PlatformViewOf(pushed));
        h.Platform.Title.Should().Be("Pushed");

        await Shell.Current.Navigation.PopAsync();
        h.Platform.NavigationStackDepth.Should().Be(0);
        h.Platform.CurrentContent.Should().BeSameAs(PlatformViewOf(h.Home));
    }

    [Fact]
    public async Task Nav_bar_back_press_pops_through_MAUI()
    {
        var h = CreateHost();
        await Shell.Current.GoToAsync(DetailsRoute);
        h.Navigated.Clear();

        // Back chevron: nav bar, left 56px.
        h.Platform.OnPointerPressed(new PointerEventArgs(20, h.Platform.NavBarHeight / 2, PointerButton.Left));

        h.Shell.CurrentPage.Should().BeSameAs(h.Home);
        h.Shell.CurrentState.Location.OriginalString.Should().Be("//home/homec");
        h.Navigated.Should().ContainSingle();
        h.Platform.NavigationStackDepth.Should().Be(0);
    }

    [Fact]
    public void Flyout_item_press_selects_the_section_through_MAUI()
    {
        var h = CreateHost();
        h.Platform.FlyoutIsPresented = true;
        h.Navigated.Clear();

        // Second flyout item: 48px rows starting at the top (no header).
        h.Platform.OnPointerPressed(new PointerEventArgs(40, 48 + 24, PointerButton.Left));

        h.Shell.CurrentState.Location.OriginalString.Should().Be("//about/aboutc");
        h.Navigated.Should().ContainSingle().Which.Source.Should().Be(ShellNavigationSource.ShellItemChanged);
        h.Platform.CurrentSectionIndex.Should().Be(1);
        h.Platform.FlyoutIsPresented.Should().BeFalse();
    }

    [Fact]
    public void Platform_GoToAsync_is_routed_through_MAUI_so_query_attributes_apply()
    {
        var h = CreateHost();

        h.Platform.GoToAsync($"{DetailsRoute}?id=7");

        var details = h.Shell.CurrentPage.Should().BeOfType<DetailsPage>().Subject;
        details.ItemId.Should().Be("7");
        h.Platform.NavigationStackDepth.Should().Be(1);
        h.Platform.CurrentContent.Should().BeSameAs(PlatformViewOf(details));
    }

    [Fact]
    public async Task Shell_draws_after_navigation_without_throwing()
    {
        var h = CreateHost();
        await Shell.Current.GoToAsync($"{DetailsRoute}?id=1");

        using var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(800, 600));
        var exception = Record.Exception(() => h.Platform.Draw(surface.Canvas));

        exception.Should().BeNull();
    }
}
