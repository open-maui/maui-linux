// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

/// <summary>
/// Shell.SearchHandler as MAUI's Windows implementation shows it (ShellItemHandler.Windows.cs):
/// an AutoSuggestBox at the right of the top navigation pane, which is shown for it; typing sets
/// Query, Enter confirms it, ItemsSource fills a suggestion list under the box, a pick clears the
/// box and reports the item, SearchBoxVisibility.Hidden collapses the box, and the box follows the
/// presented page's handler. The keyboard and assistive technology reach the box and its list.
/// </summary>
[Collection("LinuxApplication.Current")]
public class ShellSearchHandlerTests
{
    private sealed class AnimalSearchHandler : SearchHandler
    {
        public readonly List<string> Animals = new() { "Cat", "Camel", "Dog" };
        public readonly List<(string? Old, string? New)> QueryChanges = new();
        public readonly List<object> Selected = new();
        public int Confirmed;

        protected override void OnQueryChanged(string oldValue, string newValue)
        {
            base.OnQueryChanged(oldValue, newValue);
            QueryChanges.Add((oldValue, newValue));
            ItemsSource = string.IsNullOrEmpty(newValue)
                ? null
                : Animals.Where(a => a.StartsWith(newValue, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        protected override void OnQueryConfirmed()
        {
            Confirmed++;
            base.OnQueryConfirmed();
        }

        protected override void OnItemSelected(object item)
        {
            Selected.Add(item);
            base.OnItemSelected(item);
        }
    }

    private sealed record Animal(string Name);

    private sealed class Fixture : IDisposable
    {
        public Shell Shell = null!;
        public ContentPage Home = null!;
        public ContentPage Other = null!;
        public AnimalSearchHandler Handler = null!;
        public HeadlessMauiHost Host = null!;
        public SkiaShell Platform = null!;

        public SkiaShell.ShellSearchBoxView Box => Platform.SearchBox;

        public void Dispose() => Host.Dispose();
    }

    private static Fixture Build(Action<AnimalSearchHandler>? configure = null)
    {
        var f = new Fixture
        {
            Home = new ContentPage { Title = "Home", Content = new Label { Text = "home" } },
            Other = new ContentPage { Title = "Other", Content = new Label { Text = "other" } },
            // ShowsResults is false by default in MAUI; the suggestion tests turn it on as apps that show results do.
            Handler = new AnimalSearchHandler { Placeholder = "Search animals", ShowsResults = true },
        };
        configure?.Invoke(f.Handler);
        Shell.SetSearchHandler(f.Home, f.Handler);
        f.Shell = new Shell();
        f.Shell.Items.Add(new FlyoutItem { Title = "Home", Route = "home", Items = { new ShellContent { Route = "page", Content = f.Home } } });
        f.Shell.Items.Add(new FlyoutItem { Title = "Other", Route = "other", Items = { new ShellContent { Route = "page", Content = f.Other } } });
        f.Host = new HeadlessMauiHost(f.Shell, withEngine: true);
        f.Host.Context.Render();
        f.Platform = (SkiaShell)f.Shell.Handler!.PlatformView!;
        return f;
    }

    private static void Click(HeadlessMauiHost host, SKRect rect)
    {
        host.DisplayWindow.RaisePointerPressed(rect.MidX, rect.MidY);
        host.DisplayWindow.RaisePointerReleased(rect.MidX, rect.MidY);
        host.Context.Render();
    }

    private static SKRect ToSK(Rect r) => new((float)r.Left, (float)r.Top, (float)r.Right, (float)r.Bottom);

    private static void FocusBox(Fixture f) => Click(f.Host, ToSK(f.Box.Entry.Bounds));

    [Fact]
    public void A_pages_SearchHandler_shows_the_pane_with_a_300_wide_box_at_its_right()
    {
        using var f = Build();
        var pane = f.Platform.TabBarBounds;

        pane.IsEmpty.Should().BeFalse("Windows shows the top pane for a SearchHandler");
        f.Platform.CurrentSearchHandler.Should().BeSameAs(f.Handler);
        f.Platform.IsSearchBoxShown.Should().BeTrue();
        var box = f.Box.Bounds;
        box.Width.Should().Be(300, "Windows' AutoSuggestBox is 300 wide");
        box.Right.Should().BeLessThanOrEqualTo(pane.Right);
        box.Right.Should().BeGreaterThan(pane.Right - 40, "the box is at the pane's right end");
        box.Top.Should().BeGreaterThanOrEqualTo(pane.Top);
        box.Bottom.Should().BeLessThanOrEqualTo(pane.Bottom);
        f.Box.Entry.Placeholder.Should().Be("Search animals");
        foreach (var tab in f.Platform.TopNavigation.PrimaryItems)
            f.Platform.TopNavigation.ItemBounds(tab.Data).Right.Should().BeLessThanOrEqualTo((float)box.Left, "the tabs keep clear of the box");
    }

    [Fact]
    public void Typing_sets_the_query_and_Enter_confirms_it_with_the_command()
    {
        object? executed = null;
        using var f = Build(h =>
        {
            h.Command = new Command<object>(p => executed = p);
            h.CommandParameter = "param";
        });

        FocusBox(f);
        f.Host.Context.FocusedView.Should().BeSameAs(f.Box.Entry);
        f.Handler.IsFocused.Should().BeTrue();

        f.Host.DisplayWindow.RaiseTextInput("do");
        f.Host.Context.Render();
        f.Handler.Query.Should().Be("do");
        f.Handler.QueryChanges.Should().Contain(c => c.New == "do");

        f.Host.DisplayWindow.RaiseKeyDown(Key.Enter);
        f.Handler.Confirmed.Should().Be(1);
        executed.Should().Be("param");
        f.Box.IsListOpen.Should().BeFalse("confirming closes the suggestions");
    }

    [Fact]
    public void The_query_set_from_code_shows_in_the_box()
    {
        using var f = Build();
        f.Handler.Query = "Cat";
        f.Host.Context.Render();
        f.Box.Entry.Text.Should().Be("Cat");

        f.Handler.TextTransform = TextTransform.Uppercase;
        f.Host.Context.Render();
        f.Box.Entry.Text.Should().Be("CAT");
        f.Handler.Query.Should().Be("Cat", "a programmatic change of the box does not set the query");
    }

    [Fact]
    public void Suggestions_show_under_the_box_and_a_pick_clears_the_box_and_reports_the_item()
    {
        using var f = Build();
        FocusBox(f);
        f.Host.DisplayWindow.RaiseTextInput("ca");
        f.Host.Context.Render();

        f.Box.IsListOpen.Should().BeTrue();
        f.Box.SuggestionTexts.Should().Equal("Cat", "Camel");
        var first = f.Box.SuggestionBounds(0);
        first.Top.Should().BeGreaterThanOrEqualTo((float)f.Box.Bounds.Bottom, "the list opens under the box");
        first.Left.Should().BeGreaterThanOrEqualTo((float)f.Box.Bounds.Left);

        Click(f.Host, f.Box.SuggestionBounds(1));

        f.Handler.Selected.Should().Equal("Camel");
        f.Handler.SelectedItem.Should().Be("Camel");
        f.Box.IsListOpen.Should().BeFalse();
        f.Box.Entry.Text.Should().BeEmpty("Windows clears the box when a suggestion is chosen");
        f.Handler.Query.Should().Be("ca", "the clearing is a programmatic change, which Windows does not report as the query");
        f.Handler.Confirmed.Should().Be(1, "WinUI submits the query after the suggestion is chosen");
    }

    [Fact]
    public void A_press_outside_or_Escape_closes_the_list_and_typing_opens_it_again()
    {
        using var f = Build();
        FocusBox(f);
        f.Host.DisplayWindow.RaiseTextInput("ca");
        f.Host.Context.Render();
        f.Box.IsListOpen.Should().BeTrue();

        f.Host.DisplayWindow.RaiseKeyDown(Key.Escape);
        f.Box.IsListOpen.Should().BeFalse();
        f.Handler.Selected.Should().BeEmpty();
        f.Handler.Query.Should().Be("ca");

        f.Host.DisplayWindow.RaiseTextInput("m");
        f.Host.Context.Render();
        f.Box.IsListOpen.Should().BeTrue();
        f.Box.SuggestionTexts.Should().Equal("Camel");

        Click(f.Host, new SKRect(100, 400, 120, 420));
        f.Box.IsListOpen.Should().BeFalse("a press outside the list closes it");
        f.Handler.Selected.Should().BeEmpty();
    }

    [Fact]
    public void The_box_is_a_tab_stop_and_Down_Up_and_Enter_pick_a_suggestion()
    {
        using var f = Build();
        var pane = f.Platform.TopNavigation;

        f.Host.DisplayWindow.RaiseKeyDown(Key.Tab);
        f.Host.Context.FocusedView.Should().BeSameAs(pane, "the pane's tabs come first");
        f.Host.DisplayWindow.RaiseKeyDown(Key.Tab);
        f.Host.Context.FocusedView.Should().BeSameAs(f.Box.Entry, "then the search box");

        f.Host.DisplayWindow.RaiseTextInput("ca");
        f.Host.Context.Render();
        f.Box.HighlightedIndex.Should().Be(-1);

        f.Host.DisplayWindow.RaiseKeyDown(Key.Down);
        f.Box.HighlightedIndex.Should().Be(0);
        f.Host.DisplayWindow.RaiseKeyDown(Key.Down);
        f.Box.HighlightedIndex.Should().Be(1);
        f.Host.DisplayWindow.RaiseKeyDown(Key.Down);
        f.Box.HighlightedIndex.Should().Be(1, "the last suggestion stays highlighted");
        f.Host.DisplayWindow.RaiseKeyDown(Key.Up);
        f.Box.HighlightedIndex.Should().Be(0);
        f.Handler.Query.Should().Be("ca", "moving through the suggestions does not change the query");

        f.Host.DisplayWindow.RaiseKeyDown(Key.Enter);
        f.Handler.Selected.Should().Equal("Cat");
        f.Box.IsListOpen.Should().BeFalse();
        f.Host.Context.FocusedView.Should().BeSameAs(f.Box.Entry, "the box keeps the focus");

        // Down opens the list again on the current suggestions.
        f.Host.DisplayWindow.RaiseKeyDown(Key.Down);
        f.Box.IsListOpen.Should().BeTrue();
        f.Box.HighlightedIndex.Should().Be(0);
        f.Host.DisplayWindow.RaiseKeyDown(Key.Up);
        f.Box.HighlightedIndex.Should().Be(-1, "Up from the first suggestion goes back to the box");
        f.Host.DisplayWindow.RaiseKeyDown(Key.Enter);
        f.Handler.Selected.Should().ContainSingle("Enter in the box confirms the query, no suggestion");
        f.Handler.Confirmed.Should().Be(2);
    }

    [Fact]
    public void Without_ShowsResults_no_list_opens()
    {
        using var f = Build(h => h.ShowsResults = false);
        FocusBox(f);
        f.Host.DisplayWindow.RaiseTextInput("ca");
        f.Host.Context.Render();

        f.Handler.Query.Should().Be("ca");
        f.Box.IsListOpen.Should().BeFalse();
        f.Box.SuggestionTexts.Should().BeEmpty();
        f.Host.DisplayWindow.RaiseKeyDown(Key.Down);
        f.Box.IsListOpen.Should().BeFalse();
    }

    [Fact]
    public void Suggestions_show_DisplayMemberName_or_the_ItemTemplate()
    {
        using var f = Build(h => h.DisplayMemberName = nameof(Animal.Name));
        f.Handler.ItemsSource = new[] { new Animal("Cat"), new Animal("Camel") };
        FocusBox(f);
        f.Host.DisplayWindow.RaiseKeyDown(Key.Down);
        f.Host.Context.Render();
        f.Box.SuggestionTexts.Should().Equal("Cat", "Camel");
        f.Box.SuggestionViews.Should().OnlyContain(v => v == null);

        f.Handler.ItemTemplate = new DataTemplate(() =>
        {
            var label = new Label();
            label.SetBinding(Label.TextProperty, nameof(Animal.Name));
            return label;
        });
        f.Host.Context.Render();
        f.Box.SuggestionViews.Should().HaveCount(2).And.OnlyContain(v => v != null);
        var label = (Label)f.Box.SuggestionTemplateViews[1]!;
        label.Text.Should().Be("Camel", "the item is the template's BindingContext");

        Click(f.Host, f.Box.SuggestionBounds(0));
        f.Handler.Selected.Should().ContainSingle().Which.Should().Be(new Animal("Cat"));
    }

    [Fact]
    public void SearchBoxVisibility_Hidden_collapses_the_box_but_keeps_the_pane()
    {
        using var f = Build(h => h.SearchBoxVisibility = SearchBoxVisibility.Hidden);

        f.Platform.TabBarBounds.IsEmpty.Should().BeFalse("Windows shows the pane for any SearchHandler");
        f.Platform.IsSearchBoxShown.Should().BeFalse();
        f.Platform.ChromeTabStops().Should().NotContain(f.Box.Entry);

        f.Handler.SearchBoxVisibility = SearchBoxVisibility.Collapsible;
        f.Host.Context.Render();
        f.Platform.IsSearchBoxShown.Should().BeTrue("Collapsible shows the box, as on Windows");

        f.Handler.SearchBoxVisibility = SearchBoxVisibility.Expanded;
        f.Host.Context.Render();
        f.Platform.IsSearchBoxShown.Should().BeTrue();
        f.Platform.ChromeTabStops().Should().Contain(f.Box.Entry);
    }

    [Fact]
    public void IsSearchEnabled_false_disables_the_box()
    {
        using var f = Build();
        f.Handler.IsSearchEnabled = false;
        f.Host.Context.Render();

        f.Box.Entry.IsEnabled.Should().BeFalse();
        f.Platform.ChromeTabStops().Should().NotContain(f.Box.Entry);
        FocusBox(f);
        f.Host.Context.FocusedView.Should().NotBeSameAs(f.Box.Entry);
        f.Handler.IsFocused.Should().BeFalse();
    }

    [Fact]
    public void Focus_and_Unfocus_move_the_keyboard_focus()
    {
        using var f = Build();

        f.Handler.Focus().Should().BeTrue();
        f.Host.Context.FocusedView.Should().BeSameAs(f.Box.Entry);
        f.Handler.IsFocused.Should().BeTrue();

        f.Handler.Unfocus();
        f.Host.Context.FocusedView.Should().BeNull();
        f.Handler.IsFocused.Should().BeFalse();
    }

    [Fact]
    public async Task The_box_follows_the_presented_pages_SearchHandler()
    {
        using var f = Build();
        f.Platform.CurrentSearchHandler.Should().BeSameAs(f.Handler);

        FocusBox(f);
        f.Host.DisplayWindow.RaiseTextInput("ca");
        f.Host.Context.Render();
        f.Box.IsListOpen.Should().BeTrue();

        await f.Shell.GoToAsync("//other");
        f.Host.Context.Render();
        f.Platform.CurrentSearchHandler.Should().BeNull();
        f.Platform.IsSearchBoxShown.Should().BeFalse();
        f.Platform.TabBarBounds.IsEmpty.Should().BeTrue("one content and no SearchHandler: no pane, as on Windows");
        f.Box.IsListOpen.Should().BeFalse();
        f.Handler.IsFocused.Should().BeFalse();

        await f.Shell.GoToAsync("//home");
        f.Host.Context.Render();
        f.Platform.CurrentSearchHandler.Should().BeSameAs(f.Handler);
        f.Platform.IsSearchBoxShown.Should().BeTrue();
        f.Box.Entry.Text.Should().Be("ca", "the box shows the handler's query");
    }

    [Fact]
    public void Disconnecting_the_handler_closes_the_list_overlay()
    {
        using var f = Build();
        FocusBox(f);
        f.Host.DisplayWindow.RaiseTextInput("ca");
        f.Host.Context.Render();
        f.Box.IsListOpen.Should().BeTrue();

        f.Platform.CloseTopNavigationDropDown();
        f.Box.IsListOpen.Should().BeFalse("the handler's disconnect unregisters the list's popup overlay");
        SkiaView.GetPopupOwnerAt(100, 400).Should().NotBeSameAs(f.Box);
    }

    [Fact]
    public void Assistive_technology_reads_a_combo_box_with_an_entry_and_a_list_of_suggestions()
    {
        using var f = Build();
        ((IAccessible)f.Platform).Children.Should().Contain(f.Box);
        IAccessible box = f.Box;
        box.Role.Should().Be(AccessibleRole.ComboBox);
        box.AccessibleName.Should().Be("Search animals");
        box.Children.Should().ContainSingle();
        IAccessible entry = f.Box.Entry;
        entry.Role.Should().Be(AccessibleRole.Entry);
        entry.AccessibleName.Should().Be("Search animals");

        FocusBox(f);
        f.Host.DisplayWindow.RaiseTextInput("ca");
        f.Host.Context.Render();
        box.States.HasFlag(AccessibleStates.Expanded).Should().BeTrue();
        var list = box.Children.Single(c => c.Role == AccessibleRole.List);
        list.Children.Select(c => c.AccessibleName).Should().Equal("Cat", "Camel");
        list.Children.Should().OnlyContain(c => c.Role == AccessibleRole.ListItem);

        list.Children[0].DoAction("click").Should().BeTrue();
        f.Handler.Selected.Should().Equal("Cat");
        box.Children.Should().NotContain(c => c.Role == AccessibleRole.List);
    }
}

/// <summary>
/// Shell.FlyoutContent with a Scroll or CollapseOnScroll header: as on Windows, where the header
/// behaviour follows only the NavigationView's own menu-items ScrollViewer
/// (ShellHandler.Windows.cs OnScrollViewerViewChanged) and FlyoutContent is the pane's custom
/// content (RootNavigationView.ReplacePaneMenuItemsWithCustomContent), the header stays fixed while
/// the app's content scrolls itself.
/// </summary>
[Collection("LinuxApplication.Current")]
public class ShellFlyoutContentHeaderTests
{
    [Theory]
    [InlineData(FlyoutHeaderBehavior.Scroll)]
    [InlineData(FlyoutHeaderBehavior.CollapseOnScroll)]
    public void The_header_stays_fixed_above_FlyoutContent(FlyoutHeaderBehavior behavior)
    {
        var header = new BoxView { HeightRequest = 100, Color = Colors.Blue };
        var rows = new VerticalStackLayout();
        for (int i = 0; i < 40; i++)
            rows.Children.Add(new Label { Text = $"Row {i}", HeightRequest = 40 });
        var scroll = new ScrollView { Content = rows };
        var shell = new Shell { FlyoutBehavior = FlyoutBehavior.Locked, FlyoutHeader = header, FlyoutHeaderBehavior = behavior, FlyoutContent = scroll };
        shell.Items.Add(new FlyoutItem { Title = "Home", Items = { new ShellContent { Content = new ContentPage() } } });
        using var host = new HeadlessMauiHost(shell, withEngine: true);
        host.Context.Render();
        var platform = (SkiaShell)shell.Handler!.PlatformView!;
        var headerView = (SkiaView)header.Handler!.PlatformView!;
        var scrollView = (SkiaView)scroll.Handler!.PlatformView!;

        headerView.Bounds.Top.Should().Be(0);
        headerView.Bounds.Height.Should().Be(100);
        scrollView.Bounds.Top.Should().BeGreaterThanOrEqualTo(100, "the content is under the header");

        host.DisplayWindow.RaiseScroll(100, 400, 3);
        host.Context.Render();

        platform.FlyoutScrollOffset.Should().Be(0, "the shell's own list scroll is not used");
        headerView.Bounds.Top.Should().Be(0, "the header stays, as on Windows");
        headerView.Bounds.Height.Should().Be(100);
    }
}
