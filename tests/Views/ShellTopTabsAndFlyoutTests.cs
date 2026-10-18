// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

/// <summary>
/// A ShellSection with several ShellContents, as MAUI's Windows implementation shows it: the
/// section's tab in the top navigation pane has its contents as menu items, so it shows a chevron
/// and opens a drop-down of them; picking one selects it through MAUI (Navigating with
/// ShellContentChanged, Navigated, CurrentState). Routes select the content's menu item, hidden
/// contents are left out, tabs that do not fit move to the More drop-down, the pane takes
/// Shell.TabBarBackgroundColor, the keyboard works as on WinUI's top pane, and assistive technology
/// reads a page tab list and a menu.
/// </summary>
[Collection("LinuxApplication.Current")]
public class ShellTopTabsTests
{
    private sealed class TopTabsShell
    {
        public Shell Shell = null!;
        public ContentPage Cats = null!;
        public ContentPage Dogs = null!;
        public ShellContent CatsContent = null!;
        public ShellContent DogsContent = null!;
        public ShellContent SettingsContent = null!;
        public Tab Browse = null!;
    }

    private static TopTabsShell Build()
    {
        var t = new TopTabsShell
        {
            Cats = new ContentPage { Title = "Cats", Content = new Label { Text = "cats" } },
            Dogs = new ContentPage { Title = "Dogs", Content = new Label { Text = "dogs" } },
        };
        t.CatsContent = new ShellContent { Route = "cats", Title = "Cats", Content = t.Cats };
        t.DogsContent = new ShellContent { Route = "dogs", Title = "Dogs", Content = t.Dogs };
        t.SettingsContent = new ShellContent { Route = "settings", Title = "Settings", Content = new ContentPage { Content = new Label { Text = "settings" } } };
        t.Browse = new Tab { Route = "browse", Title = "Browse", Items = { t.CatsContent, t.DogsContent } };
        t.Shell = new Shell();
        t.Shell.Items.Add(new TabBar
        {
            Route = "main",
            Items = { t.Browse, t.SettingsContent },
        });
        return t;
    }

    private static void Click(HeadlessMauiHost host, SKRect rect)
    {
        host.DisplayWindow.RaisePointerPressed(rect.MidX, rect.MidY);
        host.DisplayWindow.RaisePointerReleased(rect.MidX, rect.MidY);
        host.Context.Render();
    }

    private static SKRect RowOf(SkiaShell platform, object data) =>
        platform.TopNavigation.DropDownRows.Single(r => ReferenceEquals(r.Item.Data, data)).Bounds;

    [Fact]
    public async Task The_sections_tab_lists_its_contents_and_a_route_selects_one()
    {
        var t = Build();
        using var host = new HeadlessMauiHost(t.Shell, withEngine: true);
        host.Context.Render();
        var platform = (SkiaShell)t.Shell.Handler!.PlatformView!;
        var pane = platform.TopNavigation;

        pane.Items.Select(i => i.Title).Should().Equal("Browse", "Settings");
        pane.Items[0].Data.Should().BeSameAs(t.Browse);
        pane.Items[0].Children!.Select(c => c.Data).Should().Equal(t.CatsContent, t.DogsContent);
        pane.Items[1].Children.Should().BeNull("a single content is not a drop-down");
        pane.Items[0].IsChildSelected.Should().BeTrue("the selection is the section's current content");
        pane.Items[0].Children![0].IsSelected.Should().BeTrue();

        var bar = platform.TabBarBounds;
        bar.Top.Should().Be(platform.NavBarHeight, "the pane is under the navigation bar");
        platform.CurrentContent!.Bounds.Top.Should().BeGreaterThanOrEqualTo(bar.Bottom, "the page is laid out under the pane");

        await t.Shell.GoToAsync("//main/browse/dogs");
        host.Context.Render();

        t.Shell.CurrentItem.CurrentItem.CurrentItem.Should().BeSameAs(t.DogsContent);
        t.Shell.CurrentState.Location.OriginalString.Should().Be("//main/browse/dogs");
        platform.CurrentMauiPage.Should().BeSameAs(t.Dogs);
        pane.Items[0].Children![1].IsSelected.Should().BeTrue();
        pane.Items[0].Children![0].IsSelected.Should().BeFalse();
    }

    [Fact]
    public void A_click_on_the_sections_tab_opens_its_drop_down_and_a_pick_selects_through_MAUI()
    {
        var t = Build();
        using var host = new HeadlessMauiHost(t.Shell, withEngine: true);
        host.Context.Render();
        var platform = (SkiaShell)t.Shell.Handler!.PlatformView!;
        var pane = platform.TopNavigation;
        var navigating = new List<ShellNavigatingEventArgs>();
        var navigated = new List<ShellNavigatedEventArgs>();
        t.Shell.Navigating += (_, e) => navigating.Add(e);
        t.Shell.Navigated += (_, e) => navigated.Add(e);

        var browseTab = pane.ItemBounds(t.Browse);
        Click(host, browseTab);
        pane.IsDropDownOpen.Should().BeTrue();
        navigating.Should().BeEmpty("opening the drop-down selects nothing");
        var rows = pane.DropDownRows;
        rows.Select(r => r.Item.Title).Should().Equal("Cats", "Dogs");
        rows[0].Bounds.Top.Should().BeGreaterThanOrEqualTo((float)platform.TabBarBounds.Bottom, "the drop-down opens under the tab");
        rows[0].Bounds.Left.Should().BeGreaterThanOrEqualTo(browseTab.Left);

        Click(host, RowOf(platform, t.DogsContent));

        pane.IsDropDownOpen.Should().BeFalse();
        t.Browse.CurrentItem.Should().BeSameAs(t.DogsContent);
        navigating.Should().ContainSingle().Which.Source.Should().Be(ShellNavigationSource.ShellContentChanged);
        navigated.Should().ContainSingle().Which.Current.Location.OriginalString.Should().Be("//main/browse/dogs");
        platform.CurrentMauiPage.Should().BeSameAs(t.Dogs);
    }

    [Fact]
    public void A_press_outside_the_drop_down_closes_it_and_selects_nothing()
    {
        var t = Build();
        using var host = new HeadlessMauiHost(t.Shell, withEngine: true);
        host.Context.Render();
        var platform = (SkiaShell)t.Shell.Handler!.PlatformView!;
        var pane = platform.TopNavigation;

        Click(host, pane.ItemBounds(t.Browse));
        pane.IsDropDownOpen.Should().BeTrue();
        Click(host, new SKRect(600, 400, 620, 420));
        pane.IsDropDownOpen.Should().BeFalse("WinUI's flyouts close on a press outside them");
        t.Browse.CurrentItem.Should().BeSameAs(t.CatsContent);

        // Another tab's press while it is open acts on that tab.
        Click(host, pane.ItemBounds(t.Browse));
        Click(host, pane.ItemBounds(t.SettingsContent));
        pane.IsDropDownOpen.Should().BeFalse();
        t.Shell.CurrentItem.CurrentItem.Should().BeSameAs(t.SettingsContent.Parent);
    }

    [Fact]
    public async Task Hidden_contents_are_left_out_and_a_pushed_page_keeps_the_pane()
    {
        var t = Build();
        using var host = new HeadlessMauiHost(t.Shell, withEngine: true);
        host.Context.Render();
        var platform = (SkiaShell)t.Shell.Handler!.PlatformView!;
        var pane = platform.TopNavigation;

        t.DogsContent.IsVisible = false;
        host.Context.Render();
        pane.Items[0].Children.Should().BeNull("one visible content is no drop-down");
        pane.Items[0].IsSelected.Should().BeTrue("the section's tab is the selection again");

        t.DogsContent.IsVisible = true;
        host.Context.Render();
        pane.Items[0].Children.Should().HaveCount(2);

        await t.Shell.Navigation.PushAsync(new ContentPage { Title = "Pushed", Content = new Label { Text = "pushed" } });
        host.Context.Render();
        platform.TabBarBounds.IsEmpty.Should().BeFalse("on Windows the pane stays for a pushed page");
        pane.Items[0].Children.Should().HaveCount(2);

        await t.Shell.Navigation.PopAsync();
        host.Context.Render();
        pane.Items[0].Children.Should().HaveCount(2);
    }

    [Fact]
    public void One_section_with_several_contents_shows_the_pane_as_on_Windows()
    {
        var cats = new ShellContent { Route = "cats", Title = "Cats", Content = new ContentPage() };
        var dogs = new ShellContent { Route = "dogs", Title = "Dogs", Content = new ContentPage() };
        var shell = new Shell();
        shell.Items.Add(new FlyoutItem { Route = "pets", Title = "Pets", Items = { new Tab { Route = "browse", Title = "Browse", Items = { cats, dogs } } } });
        using var host = new HeadlessMauiHost(shell, withEngine: true);
        host.Context.Render();
        var platform = (SkiaShell)shell.Handler!.PlatformView!;

        platform.TabBarBounds.IsEmpty.Should().BeFalse("Windows shows tabs when the current section has more than one content");
        platform.TopNavigation.Items.Should().ContainSingle().Which.Children!.Select(c => c.Data).Should().Equal(cats, dogs);
    }

    [Fact]
    public void The_pane_is_a_tab_stop_and_the_keyboard_moves_selects_and_opens_drop_downs()
    {
        var t = Build();
        using var host = new HeadlessMauiHost(t.Shell, withEngine: true);
        host.Context.Render();
        var platform = (SkiaShell)t.Shell.Handler!.PlatformView!;
        var pane = platform.TopNavigation;

        host.DisplayWindow.RaiseKeyDown(Key.Tab);
        host.Context.FocusedView.Should().BeSameAs(pane, "the pane is the first tab stop");
        pane.FocusSlot.Should().Be(0, "focus starts on the selected tab");

        // Enter on a section with contents opens its drop-down, with the selected content highlighted.
        host.DisplayWindow.RaiseKeyDown(Key.Enter);
        pane.IsDropDownOpen.Should().BeTrue();
        host.DisplayWindow.RaiseKeyDown(Key.Down);
        host.DisplayWindow.RaiseKeyDown(Key.Enter);
        host.Context.Render();
        pane.IsDropDownOpen.Should().BeFalse();
        t.Browse.CurrentItem.Should().BeSameAs(t.DogsContent);
        platform.CurrentMauiPage.Should().BeSameAs(t.Dogs);

        // Escape closes a drop-down without a selection.
        host.DisplayWindow.RaiseKeyDown(Key.Space);
        pane.IsDropDownOpen.Should().BeTrue();
        host.DisplayWindow.RaiseKeyDown(Key.Up);
        host.DisplayWindow.RaiseKeyDown(Key.Escape);
        pane.IsDropDownOpen.Should().BeFalse();
        t.Browse.CurrentItem.Should().BeSameAs(t.DogsContent);

        // Arrows move the focus, not the selection; Enter selects a plain tab.
        host.DisplayWindow.RaiseKeyDown(Key.Right);
        pane.FocusSlot.Should().Be(1);
        t.Shell.CurrentItem.CurrentItem.Should().BeSameAs(t.Browse);
        host.DisplayWindow.RaiseKeyDown(Key.Enter);
        host.Context.Render();
        t.Shell.CurrentItem.CurrentItem.Should().BeSameAs(t.SettingsContent.Parent);

        host.DisplayWindow.RaiseKeyDown(Key.Home);
        pane.FocusSlot.Should().Be(0);
        host.DisplayWindow.RaiseKeyDown(Key.End);
        pane.FocusSlot.Should().Be(1);
    }

    [Fact]
    public void Assistive_technology_reads_a_page_tab_list_and_the_drop_down_as_a_menu()
    {
        var t = Build();
        using var host = new HeadlessMauiHost(t.Shell, withEngine: true);
        host.Context.Render();
        var platform = (SkiaShell)t.Shell.Handler!.PlatformView!;

        var shellChildren = ((IAccessible)platform).Children;
        shellChildren.Should().Contain(platform.TopNavigation);
        IAccessible pane = platform.TopNavigation;
        pane.Role.Should().Be(AccessibleRole.PageTabList);
        pane.Children.Select(c => c.AccessibleName).Should().Equal("Browse", "Settings");
        pane.Children.Should().OnlyContain(c => c.Role == AccessibleRole.PageTab);
        pane.Children[0].States.HasFlag(AccessibleStates.Selected).Should().BeTrue();
        pane.Children[0].States.HasFlag(AccessibleStates.Expandable).Should().BeTrue();
        pane.Children[1].States.HasFlag(AccessibleStates.Selected).Should().BeFalse();

        pane.Children[0].DoAction("click").Should().BeTrue();
        var menu = pane.Children.Single(c => c.Role == AccessibleRole.Menu);
        menu.Children.Select(c => c.AccessibleName).Should().Equal("Cats", "Dogs");
        menu.Children.Should().OnlyContain(c => c.Role == AccessibleRole.MenuItem);
        menu.Children[0].States.HasFlag(AccessibleStates.Selected).Should().BeTrue();

        menu.Children[1].DoAction("click").Should().BeTrue();
        t.Browse.CurrentItem.Should().BeSameAs(t.DogsContent);
        pane.Children.Should().NotContain(c => c.Role == AccessibleRole.Menu, "the drop-down closed");

        pane.Children[1].DoAction("click").Should().BeTrue();
        t.Shell.CurrentItem.CurrentItem.Should().BeSameAs(t.SettingsContent.Parent);
    }

    [Fact]
    public void The_pane_takes_the_tab_bar_background_colour()
    {
        var t = Build();
        Shell.SetTabBarBackgroundColor(t.Shell, Colors.Red);
        using var host = new HeadlessMauiHost(t.Shell, withEngine: true);
        host.Context.Render();
        var platform = (SkiaShell)t.Shell.Handler!.PlatformView!;

        using var surface = SKSurface.Create(new SKImageInfo(800, 600));
        platform.Draw(surface.Canvas);
        using var image = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        var bar = platform.TabBarBounds;
        // The pane's right end has no tab.
        bitmap.GetPixel((int)bar.Right - 4, (int)bar.Top + 4).Should().Be(SKColors.Red);
    }
}

/// <summary>
/// WinUI's top pane moves the tabs that do not fit into the More (…) overflow drop-down, keeping
/// the selected one in the pane; a pick there selects through MAUI, and an overflowed section with
/// several contents expands in place to list them.
/// </summary>
[Collection("LinuxApplication.Current")]
public class ShellTopTabsOverflowTests
{
    private static Shell ManyTabs(out Tab multi)
    {
        var shell = new Shell();
        var bar = new TabBar { Route = "main" };
        for (int i = 0; i < 7; i++)
            bar.Items.Add(new ShellContent { Route = $"tab{i}", Title = $"Section number {i}", Content = new ContentPage { Content = new Label { Text = $"{i}" } } });
        multi = new Tab
        {
            Route = "multi",
            Title = "Multiple contents",
            Items =
            {
                new ShellContent { Route = "one", Title = "One", Content = new ContentPage() },
                new ShellContent { Route = "two", Title = "Two", Content = new ContentPage() },
            },
        };
        bar.Items.Add(multi);
        shell.Items.Add(bar);
        return shell;
    }

    private static void Click(HeadlessMauiHost host, SKRect rect)
    {
        host.DisplayWindow.RaisePointerPressed(rect.MidX, rect.MidY);
        host.DisplayWindow.RaisePointerReleased(rect.MidX, rect.MidY);
        host.Context.Render();
    }

    [Fact]
    public void Tabs_that_do_not_fit_move_to_More_and_a_pick_there_selects_through_MAUI()
    {
        var shell = ManyTabs(out var multi);
        using var host = new HeadlessMauiHost(shell, withEngine: true, width: 480);
        host.Context.Render();
        var platform = (SkiaShell)shell.Handler!.PlatformView!;
        var pane = platform.TopNavigation;

        pane.OverflowItems.Should().NotBeEmpty();
        (pane.PrimaryItems.Count + pane.OverflowItems.Count).Should().Be(8);
        pane.PrimaryItems[0].Title.Should().Be("Section number 0", "the tabs keep their order");
        var more = pane.MoreButtonBounds;
        more.IsEmpty.Should().BeFalse();
        more.Right.Should().BeLessThanOrEqualTo((float)platform.TabBarBounds.Right);
        ((IAccessible)pane).Children.Should().Contain(c => c.Role == AccessibleRole.Button && c.AccessibleName == "More");

        Click(host, more);
        pane.IsDropDownOpen.Should().BeTrue();
        pane.DropDownRows.Select(r => r.Item.Title).Should().Equal(pane.OverflowItems.Select(i => i.Title));

        var last = pane.DropDownRows.Single(r => r.Item.Title == "Section number 6");
        Click(host, last.Bounds);
        pane.IsDropDownOpen.Should().BeFalse();
        shell.CurrentItem.CurrentItem.Title.Should().Be("Section number 6");
        pane.PrimaryItems.Should().Contain(i => i.Title == "Section number 6", "the selected tab is always in the pane");

        // A section with several contents expands in the More drop-down; a pick selects its content.
        Click(host, pane.MoreButtonBounds);
        var multiRow = pane.DropDownRows.Single(r => ReferenceEquals(r.Item.Data, multi));
        Click(host, multiRow.Bounds);
        pane.IsDropDownOpen.Should().BeTrue("the section expands in place");
        var two = pane.DropDownRows.Single(r => r.Item.Title == "Two");
        Click(host, two.Bounds);
        shell.CurrentItem.CurrentItem.Should().BeSameAs(multi);
        multi.CurrentItem.Title.Should().Be("Two");
        shell.CurrentState.Location.OriginalString.Should().Be("//main/multi/two");
    }

    [Fact]
    public void Every_tab_fits_in_a_wide_window()
    {
        var shell = ManyTabs(out _);
        using var host = new HeadlessMauiHost(shell, withEngine: true, width: 2400);
        host.Context.Render();
        var platform = (SkiaShell)shell.Handler!.PlatformView!;

        platform.TopNavigation.OverflowItems.Should().BeEmpty();
        platform.TopNavigation.MoreButtonBounds.IsEmpty.Should().BeTrue();
    }
}

/// <summary>
/// The flyout lists MAUI's flyout items (IShellController.GenerateFlyoutGrouping, as Windows'
/// ShellView does): no TabBar sections, hidden items left out, AsMultipleItems listing sections,
/// MenuItems, bare contents, and a separator between groups.
/// </summary>
[Collection("LinuxApplication.Current")]
public class ShellFlyoutItemsTests
{
    [Fact]
    public void The_flyout_lists_MAUIs_flyout_items()
    {
        var shell = new Shell();
        shell.Items.Add(new FlyoutItem { Title = "Home", Items = { new ShellContent { Route = "home", Content = new ContentPage() } } });
        shell.Items.Add(new TabBar { Items = { new ShellContent { Title = "T1", Content = new ContentPage() }, new ShellContent { Title = "T2", Content = new ContentPage() } } });
        shell.Items.Add(new FlyoutItem { Title = "Hidden", FlyoutItemIsVisible = false, Items = { new ShellContent { Content = new ContentPage() } } });
        shell.Items.Add(new FlyoutItem { Title = "Gone", IsVisible = false, Items = { new ShellContent { Content = new ContentPage() } } });
        shell.Items.Add(new FlyoutItem
        {
            Title = "Multi",
            FlyoutDisplayOptions = FlyoutDisplayOptions.AsMultipleItems,
            Items =
            {
                new Tab { Title = "A", Items = { new ShellContent { Route = "a", Content = new ContentPage() } } },
                new Tab { Title = "B", Items = { new ShellContent { Route = "b", Content = new ContentPage() } } },
            },
        });
        shell.Items.Add(new MenuItem { Text = "Help" });
        shell.Items.Add(new ShellContent { Title = "About", Route = "about", Content = new ContentPage() });
        shell.Items.Add(new ShellContent { Title = "Secret", Route = "secret", Content = new ContentPage() });
        Shell.SetFlyoutItemIsVisible(shell.Items[^1].Items[0].Items[0], false);

        using var host = new HeadlessMauiHost(shell, withEngine: true);
        host.Context.Render();
        var platform = (SkiaShell)shell.Handler!.PlatformView!;

        platform.FlyoutItemTitles().Should().Equal("Home", "-", "A", "B", "-", "Help", "About");
    }

    [Fact]
    public void A_press_on_a_listed_section_navigates_to_it()
    {
        var a = new Tab { Title = "A", Route = "a", Items = { new ShellContent { Route = "ac", Content = new ContentPage() } } };
        var b = new Tab { Title = "B", Route = "b", Items = { new ShellContent { Route = "bc", Content = new ContentPage() } } };
        var shell = new Shell();
        shell.Items.Add(new FlyoutItem { Route = "multi", FlyoutDisplayOptions = FlyoutDisplayOptions.AsMultipleItems, Items = { a, b } });
        using var host = new HeadlessMauiHost(shell, withEngine: true);
        host.Context.Render();
        var platform = (SkiaShell)shell.Handler!.PlatformView!;

        platform.FlyoutIsPresented = true;
        host.Context.Render();
        var row = platform.FlyoutRowBounds(b);
        row.IsEmpty.Should().BeFalse();
        platform.OnPointerPressed(new Microsoft.Maui.Platform.PointerEventArgs(row.MidX, row.MidY, Microsoft.Maui.Platform.PointerButton.Left));
        host.Context.Render();

        shell.CurrentItem.CurrentItem.Should().BeSameAs(b);
        shell.CurrentState.Location.OriginalString.Should().Be("//multi/b/bc");
        platform.FlyoutIsPresented.Should().BeFalse();
    }
}

/// <summary>
/// MAUI's effective flyout behaviour (Shell.GetEffectiveFlyoutBehavior): a TabBar root has no
/// flyout, a FlyoutItem root has one, a lone bare page has none, an explicit value wins, and a
/// page can set its own.
/// </summary>
[Collection("LinuxApplication.Current")]
public class ShellEffectiveFlyoutBehaviorTests
{
    private static ShellFlyoutBehavior BehaviorOf(Shell shell)
    {
        using var host = new HeadlessMauiHost(shell, withEngine: true);
        host.Context.Render();
        return ((SkiaShell)shell.Handler!.PlatformView!).FlyoutBehavior;
    }

    [Fact]
    public void A_TabBar_root_has_no_flyout()
    {
        var shell = new Shell();
        shell.Items.Add(new TabBar { Items = { new ShellContent { Content = new ContentPage() }, new ShellContent { Content = new ContentPage() } } });
        BehaviorOf(shell).Should().Be(ShellFlyoutBehavior.Disabled);
    }

    [Fact]
    public void A_TabBar_root_with_an_explicit_FlyoutBehavior_keeps_it()
    {
        var shell = new Shell { FlyoutBehavior = FlyoutBehavior.Flyout };
        shell.Items.Add(new TabBar { Items = { new ShellContent { Content = new ContentPage() }, new ShellContent { Content = new ContentPage() } } });
        BehaviorOf(shell).Should().Be(ShellFlyoutBehavior.Flyout);
    }

    [Fact]
    public void A_FlyoutItem_root_has_a_flyout_and_a_lone_bare_page_has_none()
    {
        var withItem = new Shell();
        withItem.Items.Add(new FlyoutItem { Title = "Home", Items = { new ShellContent { Content = new ContentPage() } } });
        BehaviorOf(withItem).Should().Be(ShellFlyoutBehavior.Flyout);

        var lone = new Shell { Items = { new ShellContent { Content = new ContentPage() } } };
        BehaviorOf(lone).Should().Be(ShellFlyoutBehavior.Disabled);

        var two = new Shell { Items = { new ShellContent { Content = new ContentPage() }, new ShellContent { Content = new ContentPage() } } };
        BehaviorOf(two).Should().Be(ShellFlyoutBehavior.Flyout);
    }

    [Fact]
    public async Task A_page_sets_its_own_flyout_behaviour()
    {
        var first = new ContentPage { Content = new Label { Text = "a" } };
        var second = new ContentPage { Content = new Label { Text = "b" } };
        Shell.SetFlyoutBehavior(second, FlyoutBehavior.Disabled);
        var shell = new Shell();
        shell.Items.Add(new FlyoutItem { Route = "first", Items = { new ShellContent { Content = first } } });
        shell.Items.Add(new FlyoutItem { Route = "second", Items = { new ShellContent { Content = second } } });
        using var host = new HeadlessMauiHost(shell, withEngine: true);
        host.Context.Render();
        var platform = (SkiaShell)shell.Handler!.PlatformView!;
        platform.FlyoutBehavior.Should().Be(ShellFlyoutBehavior.Flyout);

        await shell.GoToAsync("//second");
        host.Context.Render();
        platform.FlyoutBehavior.Should().Be(ShellFlyoutBehavior.Disabled);

        await shell.GoToAsync("//first");
        host.Context.Render();
        platform.FlyoutBehavior.Should().Be(ShellFlyoutBehavior.Flyout);
    }
}

/// <summary>
/// Shell.FlyoutHeaderBehavior as the items scroll: Fixed keeps the header, Scroll scrolls it away
/// with the items, CollapseOnScroll shrinks it to its minimum height (MinimumHeightRequest, else 56)
/// and then the items scroll under it.
/// </summary>
[Collection("LinuxApplication.Current")]
public class ShellFlyoutHeaderScrollTests
{
    private static (HeadlessMauiHost Host, SkiaShell Platform, Shell Shell, SkiaView Header) Build(FlyoutHeaderBehavior behavior, double minimumHeight = -1)
    {
        var header = new BoxView { HeightRequest = 100, Color = Colors.Blue };
        if (minimumHeight >= 0)
            header.MinimumHeightRequest = minimumHeight;
        var shell = new Shell { FlyoutBehavior = FlyoutBehavior.Locked, FlyoutHeader = header, FlyoutHeaderBehavior = behavior };
        for (int i = 0; i < 30; i++)
            shell.Items.Add(new FlyoutItem { Title = $"Item {i}", Route = $"item{i}", Items = { new ShellContent { Content = new ContentPage() } } });
        var host = new HeadlessMauiHost(shell, withEngine: true);
        host.Context.Render();
        var platform = (SkiaShell)shell.Handler!.PlatformView!;
        return (host, platform, shell, (SkiaView)header.Handler!.PlatformView!);
    }

    private static void Scroll(HeadlessMauiHost host, SkiaShell platform, float steps)
    {
        // 30 px per wheel step.
        platform.OnScroll(new ScrollEventArgs(100, 400, 0, steps));
        host.Context.Render();
    }

    [Fact]
    public void A_Fixed_header_stays_while_the_items_scroll()
    {
        var (host, platform, shell, header) = Build(FlyoutHeaderBehavior.Fixed);
        using var _ = host;
        Scroll(host, platform, 2);

        platform.FlyoutScrollOffset.Should().Be(60);
        header.Bounds.Top.Should().Be(0);
        header.Bounds.Height.Should().Be(100);
        platform.FlyoutRowBounds(shell.Items[0]).Top.Should().Be(40);
    }

    [Fact]
    public void A_Scroll_header_scrolls_away_with_the_items()
    {
        var (host, platform, shell, header) = Build(FlyoutHeaderBehavior.Scroll);
        using var _ = host;
        Scroll(host, platform, 2);

        header.Bounds.Top.Should().Be(-60, "the header moves up with the items");
        header.Bounds.Height.Should().Be(100);
        platform.FlyoutRowBounds(shell.Items[0]).Top.Should().Be(40, "the first item follows the header");

        Scroll(host, platform, -10);
        header.Bounds.Top.Should().Be(0, "scrolled back, the header is in place again");
    }

    [Fact]
    public void A_CollapseOnScroll_header_shrinks_to_56_then_the_items_scroll_under_it()
    {
        var (host, platform, shell, header) = Build(FlyoutHeaderBehavior.CollapseOnScroll);
        using var _ = host;

        Scroll(host, platform, 1);
        header.Bounds.Top.Should().Be(0);
        header.Bounds.Height.Should().Be(70, "the header gives up what the items scrolled");
        platform.FlyoutRowBounds(shell.Items[0]).Top.Should().Be(70);

        Scroll(host, platform, 2);
        header.Bounds.Height.Should().Be(56, "it stops at its collapsed height");
        platform.FlyoutRowBounds(shell.Items[0]).Top.Should().Be(10, "then the items scroll under it");
    }

    [Fact]
    public void A_CollapseOnScroll_header_keeps_its_MinimumHeightRequest()
    {
        var (host, platform, _, header) = Build(FlyoutHeaderBehavior.CollapseOnScroll, minimumHeight: 80);
        using var _h = host;

        Scroll(host, platform, 3);
        header.Bounds.Height.Should().Be(80);
    }
}
