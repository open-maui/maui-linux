// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.ApplicationModel;
using CommunityToolkit.Maui.Behaviors;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Core.Handlers;
using CommunityToolkit.Maui.Core.Views;
using CommunityToolkit.Maui.Media;
using CommunityToolkit.Maui.Views;
using FluentAssertions;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Services.Portal;
using SkiaSharp;
using Xunit;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// CommunityToolkit.Maui features whose platform-neutral build does nothing or throws, as OpenMaui
/// gives them on Linux (the toolkit's Windows behaviour): TouchBehavior and ImageTouchBehavior
/// states and commands, Toast and Snackbar as desktop notifications, Badge, SpeechToText where no
/// recognizer exists, DrawingView image export and line adapters, SemanticOrderView reading order.
/// </summary>
[Collection(CompatHost.Collection)]
public class CommunityToolkitPlatformFeatureTests
{
    private static CompatHost Host(View content, int width = 800, int height = 600)
        => new(new ContentPage { Content = content, BackgroundColor = Colors.White },
               b => b.UseMauiCommunityToolkit(o => o.SetShouldEnableSnackbarOnWindows(false)), width, height);

    private static Border Target() => new()
    {
        WidthRequest = 200,
        HeightRequest = 100,
        BackgroundColor = Colors.White,
        HorizontalOptions = LayoutOptions.Start,
        VerticalOptions = LayoutOptions.Start,
        Content = new Label { Text = "touch me" },
    };

    private static async Task Until(Func<bool> condition, string because)
    {
        for (int i = 0; i < 100 && !condition(); i++)
            await Task.Delay(20);
        condition().Should().BeTrue(because);
    }

    // ---- TouchBehavior -------------------------------------------------------------------

    [Fact]
    public async Task TouchBehavior_tap_runs_the_command_and_raises_completed()
    {
        object? parameter = null;
        int completed = 0;
        var target = Target();
        var behavior = new TouchBehavior { Command = new Command<object?>(p => parameter = p), CommandParameter = "tapped" };
        behavior.TouchGestureCompleted += (_, _) => completed++;
        target.Behaviors.Add(behavior);
        using var host = Host(target);
        host.Render();

        host.Tap(target);

        parameter.Should().Be("tapped", "a press and release over the view completes the touch, as on Windows");
        completed.Should().Be(1);
        behavior.CurrentTouchStatus.Should().Be(TouchStatus.Completed);
        behavior.CurrentInteractionStatus.Should().Be(TouchInteractionStatus.Completed);
    }

    [Fact]
    public async Task TouchBehavior_pressed_and_hovered_states_change_the_view()
    {
        var target = Target();
        var behavior = new TouchBehavior
        {
            DefaultBackgroundColor = Colors.White,
            HoveredBackgroundColor = Colors.Yellow,
            PressedBackgroundColor = Colors.Red,
            PressedScale = 0.9,
            PressedOpacity = 0.5,
        };
        target.Behaviors.Add(behavior);
        using var host = Host(target);
        host.Render();
        var (x, y) = CompatHost.CenterOf(target);

        host.DisplayWindow.RaisePointerMoved(x, y);
        behavior.CurrentHoverState.Should().Be(HoverState.Hovered);
        await Until(() => target.BackgroundColor == Colors.Yellow, "hovering applies HoveredBackgroundColor");

        host.DisplayWindow.RaisePointerPressed(x, y);
        behavior.CurrentTouchState.Should().Be(TouchState.Pressed);
        await Until(() => target.BackgroundColor == Colors.Red && Math.Abs(target.Scale - 0.9) < 0.01 && Math.Abs(target.Opacity - 0.5) < 0.01,
            "pressing applies the pressed colour, scale and opacity");

        host.DisplayWindow.RaisePointerReleased(x, y);
        behavior.CurrentTouchState.Should().Be(TouchState.Default);
        await Until(() => target.BackgroundColor == Colors.Yellow && Math.Abs(target.Scale - 1) < 0.01,
            "releasing over the view returns to the hovered state");

        host.DisplayWindow.RaisePointerMoved(700, 500);
        behavior.CurrentHoverState.Should().Be(HoverState.Default);
        behavior.CurrentHoverStatus.Should().Be(HoverStatus.Exited);
        await Until(() => target.BackgroundColor == Colors.White, "leaving the view returns to the default state");
    }

    [Fact]
    public void TouchBehavior_press_dragged_off_the_view_cancels_the_touch()
    {
        int commands = 0;
        var target = Target();
        var behavior = new TouchBehavior { Command = new Command(() => commands++) };
        target.Behaviors.Add(behavior);
        using var host = Host(target);
        host.Render();
        var (x, y) = CompatHost.CenterOf(target);

        host.DisplayWindow.RaisePointerPressed(x, y);
        host.DisplayWindow.RaisePointerMoved(700, 500);
        behavior.CurrentTouchStatus.Should().Be(TouchStatus.Canceled, "leaving the view while pressed cancels, as WinUI's PointerExited does");
        host.DisplayWindow.RaisePointerReleased(700, 500);

        commands.Should().Be(0);
        behavior.CurrentInteractionStatus.Should().Be(TouchInteractionStatus.Completed);
    }

    [Fact]
    public async Task TouchBehavior_long_press_runs_the_long_press_command()
    {
        int taps = 0;
        object? longPressed = null;
        var target = Target();
        var behavior = new TouchBehavior
        {
            Command = new Command(() => taps++),
            LongPressCommand = new Command<object?>(p => longPressed = p),
            LongPressCommandParameter = "held",
            LongPressDuration = 100,
        };
        target.Behaviors.Add(behavior);
        using var host = Host(target);
        host.Render();
        var (x, y) = CompatHost.CenterOf(target);

        host.DisplayWindow.RaisePointerPressed(x, y);
        await Until(() => longPressed != null, "holding past LongPressDuration runs LongPressCommand");
        host.DisplayWindow.RaisePointerReleased(x, y);

        longPressed.Should().Be("held");
        taps.Should().Be(0, "a long press does not also run Command when LongPressCommand is set");
    }

    [Fact]
    public void TouchBehavior_on_a_layout_follows_presses_on_its_children_and_detaches()
    {
        int commands = 0;
        var child = new BoxView { Color = Colors.Blue, WidthRequest = 80, HeightRequest = 40 };
        var layout = new VerticalStackLayout { Children = { child }, WidthRequest = 200, HeightRequest = 100, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
        var behavior = new TouchBehavior { Command = new Command(() => commands++) };
        layout.Behaviors.Add(behavior);
        using var host = Host(layout);
        host.Render();

        child.InputTransparent.Should().BeTrue("ShouldMakeChildrenInputTransparent hands the layout its children's presses, as on Windows");
        host.Tap(child);
        commands.Should().Be(1);

        layout.Behaviors.Remove(behavior);
        host.Tap(child);
        commands.Should().Be(1, "a removed behavior no longer receives the view's pointer events");
    }

    [Fact]
    public async Task ImageTouchBehavior_swaps_the_image_while_pressed()
    {
        var normal = ImageSource.FromFile("normal.png");
        var pressed = ImageSource.FromFile("pressed.png");
        var image = new Image { WidthRequest = 100, HeightRequest = 100, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start, Source = normal };
        var behavior = new ImageTouchBehavior { DefaultImageSource = normal, PressedImageSource = pressed };
        image.Behaviors.Add(behavior);
        using var host = Host(image);
        host.Render();
        var (x, y) = CompatHost.CenterOf(image);

        host.DisplayWindow.RaisePointerPressed(x, y);
        await Until(() => ReferenceEquals(image.Source, pressed), "PressedImageSource is shown while pressed");
        host.DisplayWindow.RaisePointerReleased(x, y);
        await Until(() => ReferenceEquals(image.Source, normal), "DefaultImageSource comes back on release");
    }

    [Fact]
    public void Expander_expanded_in_code_in_a_grid_rows_second_column_grows_the_row()
    {
        var expanders = new Dictionary<string, Expander>();
        var list = new CollectionView
        {
            ItemsLayout = new GridItemsLayout(2, Microsoft.Maui.Controls.ItemsLayoutOrientation.Vertical),
            ItemsSource = new[] { "a", "x", "b", "y" },
            ItemTemplate = new DataTemplate(() =>
            {
                var header = new Label { HeightRequest = 40 };
                header.SetBinding(Label.TextProperty, ".");
                var expander = new Expander { Header = header, Content = new BoxView { Color = Colors.Red, HeightRequest = 120 } };
                expander.BindingContextChanged += (_, _) => { if (expander.BindingContext is string key) expanders[key] = expander; };
                return expander;
            }),
        };
        using var host = Host(list);
        host.Render();
        var nextLine = CompatHost.PlatformOf(expanders["b"]);
        var before = nextLine.ScreenBounds.Top;

        expanders["x"].IsExpanded = true;
        host.Render();
        host.Render();

        nextLine.ScreenBounds.Top.Should().BeGreaterThanOrEqualTo(before + 120,
            "the line grows to its expanded item even when the other item in it stays collapsed");
        CompatHost.PlatformOf(expanders["a"]).ScreenBounds.Top.Should().BeApproximately(CompatHost.PlatformOf(expanders["x"]).ScreenBounds.Top, 1,
            "the items of a line start together");
        CompatHost.PlatformOf(expanders["a"]).Bounds.Height.Should().BeApproximately(CompatHost.PlatformOf(expanders["x"]).Bounds.Height, 1,
            "the other item fills the taller line, as MAUI's Windows grid stretches its item containers");

        expanders["x"].IsExpanded = false;
        host.Render();
        host.Render();
        nextLine.ScreenBounds.Top.Should().BeApproximately(before, 1, "collapsing gives the line its height back");
    }

    // ---- TouchBehavior in a list row ---------------------------------------------------------

    [Fact]
    public async Task TouchBehavior_in_a_CollectionView_row_is_pressed_runs_its_command_and_the_row_is_selected()
    {
        var commands = new List<object?>();
        var behaviors = new List<(TouchBehavior Behavior, Border Card)>();
        var list = new CollectionView
        {
            ItemsSource = new[] { "a", "b", "c" },
            SelectionMode = SelectionMode.Single,
            ItemTemplate = new DataTemplate(() =>
            {
                var label = new Label();
                label.SetBinding(Label.TextProperty, ".");
                var card = new Border { HeightRequest = 60, Margin = new Thickness(10, 5), BackgroundColor = Colors.White, Content = label };
                var behavior = new TouchBehavior
                {
                    DefaultBackgroundColor = Colors.White,
                    PressedBackgroundColor = Colors.Red,
                    Command = new Command(() => commands.Add(card.BindingContext)),
                };
                card.Behaviors.Add(behavior);
                behaviors.Add((behavior, card));
                return card;
            }),
        };
        using var host = Host(list);
        host.Render();
        var (behavior, card) = behaviors.Single(b => (string?)b.Card.BindingContext == "b");
        var (x, y) = CompatHost.CenterOf(card);

        host.DisplayWindow.RaisePointerMoved(x, y);
        behavior.CurrentHoverStatus.Should().Be(HoverStatus.Entered, "hovering a row's content reaches its TouchBehavior");
        host.DisplayWindow.RaisePointerPressed(x, y);
        behavior.CurrentTouchState.Should().Be(TouchState.Pressed, "pressing a row's content reaches its TouchBehavior, as on Windows");
        await Until(() => card.BackgroundColor == Colors.Red, "the pressed state applies PressedBackgroundColor");
        host.DisplayWindow.RaisePointerReleased(x, y);
        host.Render();

        commands.Should().Equal(new object?[] { "b" }, "a press and release over the row content completes the touch");
        behavior.CurrentTouchStatus.Should().Be(TouchStatus.Completed);
        list.SelectedItem.Should().Be("b", "the list still selects the item, as on Windows");
        await Until(() => card.BackgroundColor == Colors.White, "releasing leaves the pressed state");

        host.DisplayWindow.RaisePointerMoved(790, 590);
        behavior.CurrentHoverStatus.Should().Be(HoverStatus.Exited, "leaving the row's content ends the hover");
    }

    // ---- Toast and Snackbar ----------------------------------------------------------------

    private sealed class RecordingServer : INotificationServer
    {
        public readonly List<(uint Id, string Summary, string[] Actions, IDictionary<string, object> Hints, int Expire)> Shown = new();
        public readonly List<uint> Closed = new();
        private uint _next = 40;
        private Action<uint, string>? _action;
        private Action<uint, uint>? _closed;

        public Task<uint> NotifyAsync(string appName, uint replacesId, string appIcon, string summary, string body, string[] actions, IDictionary<string, object> hints, int expireTimeout, CancellationToken cancellationToken)
        {
            var id = ++_next;
            lock (Shown)
                Shown.Add((id, summary, actions, hints, expireTimeout));
            return Task.FromResult(id);
        }

        public Task CloseNotificationAsync(uint id, CancellationToken cancellationToken)
        {
            lock (Closed)
                Closed.Add(id);
            _closed?.Invoke(id, 3);
            return Task.CompletedTask;
        }

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<IDisposable> WatchAsync(Action<uint, string> actionInvoked, Action<uint, uint> closed)
        {
            _action = actionInvoked;
            _closed = closed;
            return Task.FromResult<IDisposable>(new Unwatch());
        }

        public void InvokeAction(uint id, string key) => _action?.Invoke(id, key);

        public void Expire(uint id) => _closed?.Invoke(id, 1);

        private sealed class Unwatch : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    private static RecordingServer UseRecordingServer()
    {
        var server = new RecordingServer();
        ToolkitAlertsBridge.Notifications = new NotificationService("Compat", null, NullDesktopPortal.Instance, server);
        return server;
    }

    [Fact]
    public async Task Toast_is_a_transient_desktop_notification_for_its_duration()
    {
        using var host = Host(new Label { Text = "x" });
        var server = UseRecordingServer();

        await Toast.Make("Saved", ToastDuration.Long).Show();

        server.Shown.Should().ContainSingle();
        var toast = server.Shown[0];
        toast.Summary.Should().Be("Saved");
        toast.Expire.Should().Be(3500, "a long toast lasts 3.5 s, as the toolkit's Windows build expires it");
        toast.Hints.Should().ContainKey("transient");
        toast.Actions.Should().BeEmpty();

        await Toast.Make("Again").Show();
        server.Closed.Should().Contain(toast.Id, "a new toast replaces the one on screen");
        server.Shown[1].Expire.Should().Be(2000);
    }

    [Fact]
    public async Task Snackbar_shows_its_action_button_and_runs_the_action()
    {
        using var host = Host(new Label { Text = "x" });
        var server = UseRecordingServer();
        int actions = 0, shown = 0, dismissed = 0;
        EventHandler onShown = (_, _) => shown++, onDismissed = (_, _) => dismissed++;
        Snackbar.Shown += onShown;
        Snackbar.Dismissed += onDismissed;
        try
        {
            await Snackbar.Make("Deleted", () => actions++, "Undo", TimeSpan.FromSeconds(30)).Show();

            server.Shown.Should().ContainSingle();
            var (id, summary, buttons, _, expire) = server.Shown[0];
            summary.Should().Be("Deleted");
            buttons.Should().Equal(ToolkitAlertsBridge.SnackbarActionKey, "Undo");
            expire.Should().Be(30_000);
            shown.Should().Be(1);
            Snackbar.IsShown.Should().BeTrue();

            server.InvokeAction(id, ToolkitAlertsBridge.SnackbarActionKey);
            await Until(() => actions == 1, "pressing the notification's button runs the Snackbar's Action");

            server.Expire(id);
            await Until(() => dismissed == 1, "the notification closing dismisses the Snackbar");
            Snackbar.IsShown.Should().BeFalse();
        }
        finally
        {
            Snackbar.Shown -= onShown;
            Snackbar.Dismissed -= onDismissed;
        }
    }

    [Fact]
    public async Task Snackbar_dismiss_closes_the_notification_once()
    {
        using var host = Host(new Label { Text = "x" });
        var server = UseRecordingServer();
        int dismissed = 0;
        EventHandler onDismissed = (_, _) => dismissed++;
        Snackbar.Dismissed += onDismissed;
        try
        {
            var snackbar = Snackbar.Make("Archived", duration: TimeSpan.FromSeconds(30));
            await snackbar.Show();
            await snackbar.Dismiss();
            await snackbar.Dismiss();

            server.Closed.Should().ContainSingle().Which.Should().Be(server.Shown[0].Id);
            dismissed.Should().Be(1);
        }
        finally
        {
            Snackbar.Dismissed -= onDismissed;
        }
    }

    [Fact]
    public async Task Snackbar_goes_after_its_duration()
    {
        using var host = Host(new Label { Text = "x" });
        var server = UseRecordingServer();

        await Snackbar.Make("Brief", duration: TimeSpan.FromMilliseconds(100)).Show();

        await Until(() => server.Closed.Count == 1 && !Snackbar.IsShown, "the Snackbar is taken down after Duration, as its Windows notification expires");
    }

    // ---- Badge ---------------------------------------------------------------------------

    [Fact]
    public void Badge_SetCount_updates_the_launcher_entry()
    {
        using var host = Host(new Label { Text = "x" });
        var updates = new List<(string Uri, IDictionary<string, object> Properties)>();
        var previous = LauncherBadgeService.Emitter;
        LauncherBadgeService.Emitter = (uri, properties) => updates.Add((uri, properties));
        try
        {
            var set = () => Badge.SetCount(7);
            set.Should().NotThrow("the platform-neutral build threw NotSupportedException");
            new BadgeImplementation().SetCount(0);

            updates.Should().HaveCount(2);
            updates[0].Uri.Should().StartWith("application://").And.EndWith(".desktop");
            updates[0].Properties["count"].Should().Be(7L);
            updates[0].Properties["count-visible"].Should().Be(true);
            updates[1].Properties["count-visible"].Should().Be(false, "a count of 0 clears the badge, as Windows does");
        }
        finally
        {
            LauncherBadgeService.Emitter = previous;
        }
    }

    // ---- Speech --------------------------------------------------------------------------

    [Fact]
    public async Task SpeechToText_reports_no_recognizer_as_the_toolkit_does_on_an_unsupported_device()
    {
        using var host = Host(new Label { Text = "x" });
        var options = new SpeechToTextOptions { Culture = System.Globalization.CultureInfo.GetCultureInfo("en-US") };

        foreach (var speech in new[] { SpeechToText.Default, OfflineSpeechToText.Default })
        {
            var start = () => speech.StartListenAsync(options);
            (await start.Should().ThrowAsync<FeatureNotSupportedException>()).Which.Message.Should().Be(ToolkitSpeechBridge.NotAvailableMessage);

            var stop = () => speech.StopListenAsync();
            await stop.Should().NotThrowAsync("stopping when nothing listens completes, as on Windows");
            speech.CurrentState.Should().Be(SpeechToTextState.Stopped);
            await ((IAsyncDisposable)speech).DisposeAsync();
        }
    }

    // ---- DrawingView ---------------------------------------------------------------------

    private static SKBitmap Decode(Stream stream)
    {
        stream.Should().NotBeSameAs(Stream.Null);
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        copy.Length.Should().BeGreaterThan(0);
        return SKBitmap.Decode(copy.ToArray());
    }

    [Fact]
    public async Task DrawingView_GetImageStream_returns_the_drawing_as_on_windows()
    {
        var drawing = new DrawingView { WidthRequest = 300, HeightRequest = 200, Background = Colors.White };
        drawing.Lines.Add(new DrawingLine
        {
            LineColor = Colors.Blue,
            LineWidth = 10,
            ShouldSmoothPathWhenDrawn = false,
            Points = new ObservableCollection<PointF> { new(50, 50), new(150, 50) },
        });
        using var host = Host(drawing);
        host.Render();

        using var lines = Decode(await drawing.GetImageStream(1000, 1000));
        // The lines' bounds grown by the widest line on each side; the requested size is not applied (Windows).
        lines.Width.Should().Be(120);
        lines.Height.Should().Be(20);
        lines.GetPixel(60, 10).Should().Be(new SKColor(0, 0, 255));
        lines.GetPixel(60, 1).Should().Be(SKColors.White, "the view's background fills the image");

        using var canvas = Decode(await drawing.GetImageStream(10, 10, DrawingViewOutputOption.FullCanvas));
        canvas.Width.Should().Be(300);
        canvas.Height.Should().Be(200);
        canvas.GetPixel(100, 50).Should().Be(new SKColor(0, 0, 255), "full-canvas lines keep their own coordinates");
        canvas.GetPixel(250, 150).Should().Be(SKColors.White);
    }

    [Fact]
    public async Task DrawingLine_GetImageStream_draws_one_line_and_nothing_gives_an_empty_stream()
    {
        var line = new DrawingLine
        {
            LineColor = Colors.Red,
            LineWidth = 4,
            Points = new ObservableCollection<PointF> { new(0, 0), new(40, 0) },
        };
        using var image = Decode(await line.GetImageStream(100, 100, new LinearGradientPaint
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0),
            GradientStops = new[] { new PaintGradientStop(0, Colors.Black), new PaintGradientStop(1, Colors.Lime) },
        }));
        image.Width.Should().Be(48);
        image.GetPixel(20, 4).Should().Be(new SKColor(255, 0, 0));
        image.GetPixel(1, 0).Red.Should().BeLessThan(40, "the gradient starts black on the left");
        image.GetPixel(46, 0).Green.Should().BeGreaterThan(200, "and ends lime on the right");

        var empty = await DrawingView.GetImageStream(ImageLineOptions.JustLines(new List<IDrawingLine>(), new Size(10, 10), null));
        empty.Should().BeSameAs(Stream.Null);
    }

    private sealed class StampAdapter : IDrawingLineAdapter
    {
        public int Calls;

        public IDrawingLine ConvertMauiDrawingLine(MauiDrawingLine mauiDrawingLine)
        {
            Calls++;
            return new DrawingLine { LineColor = Colors.Green, LineWidth = 2, Points = mauiDrawingLine.Points, ShouldSmoothPathWhenDrawn = false };
        }
    }

    [Fact]
    public void DrawingView_honours_a_custom_line_adapter()
    {
        var completed = new List<IDrawingLine>();
        var drawing = new DrawingView
        {
            LineColor = Colors.Red,
            LineWidth = 6,
            WidthRequest = 300,
            HeightRequest = 200,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
        };
        drawing.DrawingLineCompleted += (_, e) => completed.Add(e.LastDrawingLine);
        using var host = Host(drawing);
        host.Render();

        var handler = drawing.Handler.Should().BeAssignableTo<IDrawingViewHandler>(
            "the Linux handler implements the toolkit's IDrawingViewHandler").Subject;
        var adapter = new StampAdapter();
        handler.SetDrawingLineAdapter(adapter);

        host.DisplayWindow.RaisePointerPressed(20, 50);
        host.DisplayWindow.RaisePointerMoved(100, 50);
        host.DisplayWindow.RaisePointerReleased(150, 50);

        adapter.Calls.Should().Be(1);
        drawing.Lines.Should().ContainSingle().Which.LineColor.Should().Be(Colors.Green, "the adapter's line is the one added");
        completed.Should().ContainSingle().Which.Should().BeSameAs(drawing.Lines[0]);
    }

    [Fact]
    public void DrawingView_strokes_use_the_toolkit_line_shape_by_default()
    {
        var drawing = new DrawingView { LineColor = Colors.Red, LineWidth = 6, WidthRequest = 300, HeightRequest = 200, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
        using var host = Host(drawing);
        host.Render();

        host.DisplayWindow.RaisePointerPressed(20, 50);
        host.DisplayWindow.RaisePointerMoved(100, 60);
        host.DisplayWindow.RaisePointerReleased(150, 50);

        var line = drawing.Lines.Should().ContainSingle().Subject;
        line.Should().BeOfType<DrawingLine>();
        line.ShouldSmoothPathWhenDrawn.Should().BeTrue("the toolkit's DrawingLineAdapter makes smoothed lines");
        line.Granularity.Should().Be(5);
        line.LineWidth.Should().Be(6);
    }

    // ---- SemanticOrderView ------------------------------------------------------------------

    [Fact]
    public void SemanticOrderView_orders_the_accessible_children()
    {
        var first = new Label { Text = "first" };
        var second = new Label { Text = "second" };
        var third = new Label { Text = "third" };
        var unlisted = new Label { Text = "unlisted" };
        var stack = new VerticalStackLayout { Children = { first, second, unlisted, third } };
        var semantic = new SemanticOrderView { Content = stack };
        semantic.ViewOrder = new List<View> { third, first, second };
        using var host = Host(semantic);
        host.Render();

        semantic.Handler.Should().BeOfType<Microsoft.Maui.Platform.Linux.Handlers.SemanticOrderViewHandler>();
        var children = ((IAccessible)CompatHost.PlatformOf(stack)).Children;
        children.Should().Equal(
            new IAccessible[] { (IAccessible)CompatHost.PlatformOf(third), (IAccessible)CompatHost.PlatformOf(first), (IAccessible)CompatHost.PlatformOf(second), (IAccessible)CompatHost.PlatformOf(unlisted) },
            "listed views are read in ViewOrder, the others after, as Windows gives them TabIndex 1..n");

        semantic.ViewOrder = new List<View> { second };
        ((IAccessible)CompatHost.PlatformOf(stack)).Children[0].Should().BeSameAs(CompatHost.PlatformOf(second), "a new ViewOrder applies");
    }

    // ---- Expander in a list ------------------------------------------------------------------

    // A grid line is as tall as its tallest item, so an expanding Expander in a two-column grid
    // makes its whole line taller, as Windows re-measures every cell in the row (IterateItemsInRow).
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Expander_in_a_CollectionView_row_resizes_the_row(int span)
    {
        var headers = new List<Label>();
        var list = new CollectionView
        {
            ItemsLayout = span == 1 ? LinearItemsLayout.Vertical : new GridItemsLayout(span, Microsoft.Maui.Controls.ItemsLayoutOrientation.Vertical),
            ItemsSource = new[] { "a", "x", "b", "y" },
            ItemTemplate = new DataTemplate(() =>
            {
                var header = new Label { HeightRequest = 40, BackgroundColor = Colors.LightGray };
                header.SetBinding(Label.TextProperty, ".");
                headers.Add(header);
                return new Expander { Header = header, Content = new BoxView { Color = Colors.Red, HeightRequest = 100 } };
            }),
        };
        using var host = Host(list);
        host.Render();
        var first = headers.Single(h => h.Text == "a");
        // The item starting the next row: "x" in a list, "b" in a two-column grid.
        var next = headers.Single(h => h.Text == (span == 1 ? "x" : "b"));
        var before = CompatHost.PlatformOf(next).ScreenBounds.Top;

        host.Tap(first);
        host.Render();
        host.Render();

        CompatHost.PlatformOf(next).ScreenBounds.Top.Should().BeGreaterThanOrEqualTo(before + 100,
            "the expanded row grows and pushes the next row down (Windows forces the cell to re-measure)");

        host.Tap(first);
        host.Render();
        host.Render();

        CompatHost.PlatformOf(next).ScreenBounds.Top.Should().BeApproximately(before, 1, "collapsing gives the row its height back");
    }
}
