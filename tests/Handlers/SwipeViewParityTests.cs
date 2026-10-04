// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Handlers;
using Xunit;
using OpenSwipeItem = Microsoft.Maui.OpenSwipeItem;
using SwipeDirection = Microsoft.Maui.SwipeDirection;
using SwipeItem = Microsoft.Maui.Controls.SwipeItem;
using SwipeMode = Microsoft.Maui.SwipeMode;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// SwipeView as MAUI's SwipeView handlers define it, for what MAUI's Core device tests (which
/// only run the generic handler tests on an ISwipeView stub) do not reach: a library's own
/// ISwipeView gets the Linux handler, the swipe is reported back to the view (SwipeStarted,
/// SwipeChanging, SwipeEnded, IsOpen), RequestOpen and RequestClose, MAUI's open distance and
/// Threshold, Execute mode, SwipeBehaviorOnInvoked, top and bottom items, item views, hidden and
/// disabled items.
/// </summary>
[Collection("LinuxApplication.Current")]
public class SwipeViewParityTests
{
    private const float Height = 60;

    private static void Render(HeadlessMauiHost host, int frames = 3)
    {
        for (int i = 0; i < frames; i++)
            host.Context.Render();
    }

    // The host registers a minimal handler set (no UseLinux): add the ISwipeView registration UseLinux makes.
    private static void LinuxSwipeHandlers(MauiAppBuilder builder) =>
        builder.ConfigureMauiHandlers(handlers => Microsoft.Maui.Hosting.MauiHandlersCollectionExtensions.AddHandler<ISwipeView, CoreSwipeViewHandler>(handlers));

    private static SkiaSwipeView Platform(IView view) => (SkiaSwipeView)view.Handler!.PlatformView!;

    private static Grid Row() => new() { HeightRequest = Height, BackgroundColor = Colors.White, Children = { new Label { Text = "row" } } };

    /// <summary>Drags across the window from (x0, y0) to (x1, y1) in a few moves.</summary>
    private static void Drag(HeadlessMauiHost host, float x0, float y0, float x1, float y1)
    {
        host.DisplayWindow.RaisePointerPressed(x0, y0);
        for (int i = 1; i <= 4; i++)
            host.DisplayWindow.RaisePointerMoved(x0 + (x1 - x0) * i / 4, y0 + (y1 - y0) * i / 4);
        host.DisplayWindow.RaisePointerReleased(x1, y1);
        Render(host);
    }

    private static void Tap(HeadlessMauiHost host, float x, float y)
    {
        host.DisplayWindow.RaisePointerPressed(x, y);
        host.DisplayWindow.RaisePointerReleased(x, y);
        Render(host);
    }

    /// <summary>UseLinux registers the Linux handlers for MAUI's swipe interfaces.</summary>
    [Fact]
    public void UseLinux_registers_the_swipe_interfaces()
    {
        var builder = MauiApp.CreateBuilder(useDefaults: false);
        builder.UseMauiApp<Application>();
        builder.UseLinux();
        var factory = builder.Build().Services.GetRequiredService<IMauiHandlersFactory>();

        factory.GetHandlerType(typeof(LibrarySwipeView)).Should().Be(typeof(CoreSwipeViewHandler));
        factory.GetHandlerType(typeof(SwipeView)).Should().Be(typeof(SwipeViewHandler));
        factory.GetHandlerType(typeof(LibrarySwipeItem)).Should().Be(typeof(SwipeItemMenuItemHandler));
        factory.GetHandlerType(typeof(SwipeItem)).Should().Be(typeof(SwipeItemMenuItemHandler));
        factory.GetHandlerType(typeof(SwipeItemView)).Should().Be(typeof(SwipeItemViewHandler));
    }

    [Fact]
    public void A_library_swipe_view_gets_the_linux_handler()
    {
        var content = new Label { Text = "library row" };
        var swipe = new LibrarySwipeView { Content = content, HeightRequest = Height };
        swipe.RightItems.Add(new SwipeItem { Text = "Delete" });
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { swipe } }, withEngine: true, configure: LinuxSwipeHandlers);
        Render(host);

        swipe.Handler.Should().BeOfType<CoreSwipeViewHandler>("an ISwipeView that is not a Controls SwipeView gets the Linux ISwipeView handler");
        var platform = Platform(swipe);
        platform.Content.Should().BeSameAs(content.Handler!.PlatformView, "the content is realized");
        platform.RightItems.Should().ContainSingle().Which.Text.Should().Be("Delete");
        ((IElement)swipe.RightItems[0]).Handler.Should().BeOfType<SwipeItemMenuItemHandler>();
    }

    [Fact]
    public void Request_open_and_close_update_is_open()
    {
        var swipe = new SwipeView { Content = Row(), LeftItems = new SwipeItems { new SwipeItem { Text = "Pin" } } };
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { swipe } }, withEngine: true);
        Render(host);
        var platform = Platform(swipe);

        swipe.Open(OpenSwipeItem.LeftItems, false);
        ((ISwipeView)swipe).IsOpen.Should().BeTrue("RequestOpen opens the view and reports it");
        platform.IsOpen.Should().BeTrue();
        platform.SwipeOffset.Should().Be(100, "one menu item opens to MAUI's item width (100)");

        swipe.Close(false);
        ((ISwipeView)swipe).IsOpen.Should().BeFalse();
        platform.SwipeOffset.Should().Be(0);
    }

    [Fact]
    public void A_swipe_is_reported_to_the_swipe_view()
    {
        var events = new List<string>();
        var swipe = new SwipeView
        {
            Content = Row(),
            RightItems = new SwipeItems { new SwipeItem { Text = "Archive" }, new SwipeItem { Text = "Delete" } },
        };
        swipe.SwipeStarted += (s, e) => events.Add($"started {e.SwipeDirection}");
        swipe.SwipeChanging += (s, e) => events.Add($"changing {e.SwipeDirection}");
        swipe.SwipeEnded += (s, e) => events.Add($"ended {e.SwipeDirection} {e.IsOpen}");
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { swipe } }, withEngine: true);
        Render(host);
        var b = Platform(swipe).Bounds;

        Drag(host, (float)b.Right - 20, (float)b.Center.Y, (float)b.Right - 200, (float)b.Center.Y);

        events.Should().StartWith("started Left");
        events.Should().Contain("changing Left");
        events.Should().EndWith("ended Left True");
        ((ISwipeView)swipe).IsOpen.Should().BeTrue();
        Platform(swipe).SwipeOffset.Should().Be(-200, "two menu items open to 2 x 100");
    }

    /// <summary>
    /// A swipe that starts on a button in the content swipes (the button's press is cancelled,
    /// it does not click); a tap on the button still clicks it.
    /// </summary>
    [Fact]
    public void A_swipe_starting_on_a_button_swipes_and_a_tap_clicks()
    {
        int clicks = 0;
        var button = new Button { Text = "Reply", WidthRequest = 300, HeightRequest = 40 };
        button.Clicked += (s, e) => clicks++;
        var swipe = new SwipeView
        {
            Content = new Grid { HeightRequest = Height, Children = { button } },
            RightItems = new SwipeItems { new SwipeItem { Text = "Delete" } },
        };
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { swipe } }, withEngine: true);
        Render(host);
        var bb = ((SkiaView)button.Handler!.PlatformView!).Bounds;

        Tap(host, (float)bb.Center.X, (float)bb.Center.Y);
        clicks.Should().Be(1, "a tap on a button in a closed swipe view clicks it");

        Drag(host, (float)bb.Center.X, (float)bb.Center.Y, (float)bb.Center.X - 120, (float)bb.Center.Y);
        ((ISwipeView)swipe).IsOpen.Should().BeTrue("a drag that starts on the button opens the right items");
        clicks.Should().Be(1, "the swipe does not click the button it started on");
    }

    /// <summary>
    /// A SwipeView row in a CollectionView swipes (the list keeps presses on plain row content,
    /// the row's swipe view takes a horizontal drag over), and a tap on its open item invokes the
    /// item rather than selecting the row.
    /// </summary>
    [Fact]
    public void A_swipe_view_row_in_a_collection_view_swipes()
    {
        int invoked = 0;
        SwipeView? swipe = null;
        var list = new CollectionView
        {
            SelectionMode = SelectionMode.Single,
            ItemsSource = new[] { "a" },
            ItemTemplate = new DataTemplate(() =>
            {
                var delete = new SwipeItem { Text = "Delete", BackgroundColor = Colors.Red };
                delete.Invoked += (s, e) => invoked++;
                swipe = new SwipeView { Content = Row(), RightItems = new SwipeItems { delete } };
                return swipe;
            }),
        };
        using var host = new HeadlessMauiHost(new ContentPage { Content = list }, withEngine: true);
        Render(host);
        var b = Platform(swipe!).Bounds;
        float y = (float)b.Center.Y;

        Drag(host, (float)b.Right - 20, y, (float)b.Right - 150, y);
        ((ISwipeView)swipe!).IsOpen.Should().BeTrue("a horizontal drag on a list row opens its swipe items");

        Tap(host, (float)b.Right - 20, y);
        invoked.Should().Be(1, "a tap on the open item invokes it");
        list.SelectedItem.Should().BeNull("the tap went to the swipe item, not the row");
    }

    [Fact]
    public void Threshold_is_the_distance_that_opens_the_items()
    {
        var swipe = new SwipeView { Content = Row(), LeftItems = new SwipeItems { new SwipeItem { Text = "Pin" } } };
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { swipe } }, withEngine: true);
        Render(host);
        var b = Platform(swipe).Bounds;
        float y = (float)b.Center.Y, x = (float)b.Left + 10;

        // No Threshold: 60% of the open distance (60 of 100) opens.
        Drag(host, x, y, x + 70, y);
        Platform(swipe).IsOpen.Should().BeTrue("70 is past 60% of the 100 the item opens to");
        Platform(swipe).SwipeOffset.Should().Be(100, "an opened item is shown whole");
        swipe.Close(false);

        // Threshold 150 is capped to the open distance: 70 does not open.
        swipe.Threshold = 150;
        Drag(host, x, y, x + 70, y);
        Platform(swipe).IsOpen.Should().BeFalse("the threshold (capped to 100) was not reached");
        Platform(swipe).SwipeOffset.Should().Be(0);
    }

    [Fact]
    public void Execute_mode_runs_the_first_item_and_closes()
    {
        int first = 0, second = 0;
        var a = new SwipeItem { Text = "A" };
        a.Invoked += (s, e) => first++;
        var c = new SwipeItem { Text = "B" };
        c.Invoked += (s, e) => second++;
        var swipe = new SwipeView { Content = Row(), RightItems = new SwipeItems(new[] { a, c }) { Mode = SwipeMode.Execute } };
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { swipe } }, withEngine: true);
        Render(host);
        var b = Platform(swipe).Bounds;

        Drag(host, (float)b.Right - 10, (float)b.Center.Y, (float)b.Left + 10, (float)b.Center.Y);

        first.Should().Be(1, "a full swipe in Execute mode runs the first item");
        second.Should().Be(0, "and only that one");
        ((ISwipeView)swipe).IsOpen.Should().BeFalse("the view closes after the item runs (SwipeBehaviorOnInvoked Auto)");
    }

    [Fact]
    public void Remain_open_keeps_the_items_shown_after_a_tap()
    {
        int invoked = 0;
        var item = new SwipeItem { Text = "Flag" };
        item.Invoked += (s, e) => invoked++;
        var swipe = new SwipeView
        {
            Content = Row(),
            RightItems = new SwipeItems(new[] { item }) { SwipeBehaviorOnInvoked = SwipeBehaviorOnInvoked.RemainOpen },
        };
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { swipe } }, withEngine: true);
        Render(host);
        swipe.Open(OpenSwipeItem.RightItems, false);
        Render(host);
        var b = Platform(swipe).Bounds;

        Tap(host, (float)b.Right - 20, (float)b.Center.Y);

        invoked.Should().Be(1);
        ((ISwipeView)swipe).IsOpen.Should().BeTrue("RemainOpen keeps the view open");

        // A tap on the content closes it.
        Tap(host, (float)b.Left + 20, (float)b.Center.Y);
        ((ISwipeView)swipe).IsOpen.Should().BeFalse("a tap on the content of an open swipe view closes it");
        invoked.Should().Be(1);
    }

    [Fact]
    public void A_disabled_item_is_not_invoked()
    {
        int executed = 0;
        var item = new SwipeItem { Text = "Delete", Command = new Command(() => executed++), IsEnabled = false };
        var swipe = new SwipeView { Content = Row(), RightItems = new SwipeItems { item } };
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { swipe } }, withEngine: true);
        Render(host);
        swipe.Open(OpenSwipeItem.RightItems, false);
        Render(host);
        var b = Platform(swipe).Bounds;

        Tap(host, (float)b.Right - 20, (float)b.Center.Y);

        executed.Should().Be(0, "a disabled swipe item is not invoked");
    }

    [Fact]
    public void Top_items_open_downwards()
    {
        int invoked = 0;
        var item = new SwipeItem { Text = "Refresh" };
        item.Invoked += (s, e) => invoked++;
        var swipe = new SwipeView { Content = Row(), TopItems = new SwipeItems { item } };
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { swipe } }, withEngine: true);
        Render(host);
        var platform = Platform(swipe);
        var b = platform.Bounds;

        Drag(host, (float)b.Center.X, (float)b.Top + 5, (float)b.Center.X, (float)b.Bottom - 2);

        platform.IsOpen.Should().BeTrue("a downward swipe opens the top items");
        platform.SwipeOffset.Should().Be(Height, "top menu items open to the content's height");

        Tap(host, (float)b.Center.X, (float)b.Top + 10);
        invoked.Should().Be(1, "a tap on a top item invokes it");
    }

    [Fact]
    public void A_hidden_item_does_not_count()
    {
        var hidden = new SwipeItem { Text = "Hidden", IsVisible = false };
        var swipe = new SwipeView { Content = Row(), RightItems = new SwipeItems { new SwipeItem { Text = "Shown" }, hidden } };
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { swipe } }, withEngine: true);
        Render(host);

        swipe.Open(OpenSwipeItem.RightItems, false);
        Platform(swipe).SwipeOffset.Should().Be(-100, "only the visible item is shown");

        hidden.IsVisible = true;
        Platform(swipe).SwipeOffset.Should().Be(-200, "an item shown while open widens the open side");
    }

    [Fact]
    public void An_item_view_shows_its_content_and_is_invoked()
    {
        int invoked = 0;
        var label = new Label { Text = "Custom" };
        var itemView = new SwipeItemView { Content = new Grid { WidthRequest = 140, Children = { label } } };
        itemView.Invoked += (s, e) => invoked++;
        var swipe = new SwipeView { Content = Row(), RightItems = new SwipeItems { itemView } };
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { swipe } }, withEngine: true);
        Render(host);

        itemView.Handler.Should().BeOfType<SwipeItemViewHandler>("a SwipeItemView gets the Linux item view handler");
        swipe.Open(OpenSwipeItem.RightItems, false);
        Render(host);
        var platform = Platform(swipe);
        platform.SwipeOffset.Should().Be(-140, "an item view opens to its measured width");
        var itemPlatform = (SkiaView)itemView.Handler!.PlatformView!;
        itemPlatform.Parent.Should().BeSameAs(platform, "the item view is laid out by the swipe view");
        itemPlatform.Bounds.Right.Should().BeApproximately(platform.Bounds.Right, 0.5);
        itemPlatform.Bounds.Width.Should().BeApproximately(140, 0.5);
        label.Handler.Should().NotBeNull("the item view's content is realized");

        var b = platform.Bounds;
        Tap(host, (float)b.Right - 20, (float)b.Center.Y);
        invoked.Should().Be(1, "a tap on an item view invokes it");
    }

    [Fact]
    public void A_core_swipe_view_follows_its_transition_mode()
    {
        var swipe = new LibrarySwipeView { Content = new Label { Text = "row" }, HeightRequest = Height, SwipeTransitionMode = SwipeTransitionMode.Drag };
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { swipe } }, withEngine: true, configure: LinuxSwipeHandlers);
        Render(host);

        Platform(swipe).TransitionMode.Should().Be(SwipeTransitionMode.Drag);
        swipe.SwipeTransitionMode = SwipeTransitionMode.Reveal;
        swipe.Handler!.UpdateValue(nameof(ISwipeView.SwipeTransitionMode));
        Platform(swipe).TransitionMode.Should().Be(SwipeTransitionMode.Reveal);
    }

    /// <summary>A library's swipe view: MAUI's ISwipeView on a plain View, not a Controls SwipeView.</summary>
    private sealed class LibrarySwipeView : View, ISwipeView
    {
        public double Threshold { get; set; }
        public LibrarySwipeItems LeftItems { get; } = new();
        public LibrarySwipeItems RightItems { get; } = new();
        public LibrarySwipeItems TopItems { get; } = new();
        public LibrarySwipeItems BottomItems { get; } = new();
        ISwipeItems ISwipeView.LeftItems => LeftItems;
        ISwipeItems ISwipeView.RightItems => RightItems;
        ISwipeItems ISwipeView.TopItems => TopItems;
        ISwipeItems ISwipeView.BottomItems => BottomItems;
        public bool IsOpen { get; set; }
        public SwipeTransitionMode SwipeTransitionMode { get; set; }
        public object? Content { get; set; }
        public IView? PresentedContent => Content as IView;
        public Thickness Padding => Thickness.Zero;
        public Size CrossPlatformMeasure(double widthConstraint, double heightConstraint) =>
            PresentedContent?.Measure(widthConstraint, heightConstraint) ?? Size.Zero;
        public Size CrossPlatformArrange(Rect bounds)
        {
            PresentedContent?.Arrange(bounds);
            return bounds.Size;
        }
        public void SwipeStarted(SwipeViewSwipeStarted swipeStarted) { }
        public void SwipeChanging(SwipeViewSwipeChanging swipeChanging) { }
        public void SwipeEnded(SwipeViewSwipeEnded swipeEnded) { }
        public void RequestOpen(SwipeViewOpenRequest swipeOpenRequest) => Handler?.Invoke(nameof(ISwipeView.RequestOpen), swipeOpenRequest);
        public void RequestClose(SwipeViewCloseRequest swipeCloseRequest) => Handler?.Invoke(nameof(ISwipeView.RequestClose), swipeCloseRequest);
    }

    /// <summary>A library's swipe menu item: MAUI's ISwipeItemMenuItem on a Controls MenuItem.</summary>
    private sealed class LibrarySwipeItem : MenuItem, ISwipeItemMenuItem
    {
        public Paint? Background => null;
        public Visibility Visibility => Visibility.Visible;
        void Microsoft.Maui.ISwipeItem.OnInvoked() { }
    }

    private sealed class LibrarySwipeItems : List<Microsoft.Maui.ISwipeItem>, ISwipeItems
    {
        public SwipeMode Mode { get; set; }
        public SwipeBehaviorOnInvoked SwipeBehaviorOnInvoked { get; set; }
    }
}
