// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Mopups.Hosting;
using Mopups.Pages;
using Mopups.Services;
using Xunit;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// MarketAlly.Mopups (3.0.13+, generic net10.0 asset) registered the documented way
/// (<c>ConfigureMopups()</c>) and driven through <c>MopupService.Instance</c>. Its
/// platform-neutral build asks the app's services for an <c>IPopupPlatform</c>; OpenMaui
/// registers one, so a popup is a window layer over the page, as the native Mopups
/// platforms show it, not a modal page: the page beneath is not navigated away from, a
/// click on the transparent backdrop is the popup's background click, and
/// <c>BackgroundInputTransparent</c> lets it through to the page.
/// </summary>
[Collection(CompatHost.Collection)]
public class MarketAllyMopupsCompatTests : IDisposable
{
    // Mopups resolves its IPopupPlatform once per process from IPlatformApplication.Current, which
    // a running app has from startup on (LinuxApplication.Run sets it); the headless host sets it here.
    public void Dispose() => Microsoft.Maui.IPlatformApplication.Current = null;

    private sealed class TrackedPage : ContentPage
    {
        public int Appearing, Disappearing;
        protected override void OnAppearing() { base.OnAppearing(); Appearing++; }
        protected override void OnDisappearing() { base.OnDisappearing(); Disappearing++; }
    }

    private static (CompatHost Host, TrackedPage Page, Button Beneath) Start()
    {
        var beneath = new Button { Text = "beneath", HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start, WidthRequest = 120, HeightRequest = 40 };
        var page = new TrackedPage { BackgroundColor = Colors.White, Content = beneath };
        var host = new CompatHost(page, b => b.ConfigureMopups());
        Microsoft.Maui.IPlatformApplication.Current = host.LinuxApp;
        host.Render();
        return (host, page, beneath);
    }

    // Mopups animates when the page's IsAnimationEnabled says so (PushAsync's animate argument
    // only reaches its events); the animation test turns it back on.
    private static PopupPage MakePopup(View content, bool closeOnBackground = true) => new()
    {
        BackgroundColor = Color.FromRgba(0, 0, 0, 0x80),
        CloseWhenBackgroundIsClicked = closeOnBackground,
        IsAnimationEnabled = false,
        Content = content,
    };

    /// <summary>
    /// Runs the app's loop until <paramref name="task"/> ends: animation ticks, main-loop work
    /// (Mopups posts to the main thread when it is called from another one) and frames.
    /// </summary>
    private static async Task Frames(CompatHost host, Task task)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!task.IsCompleted && DateTime.UtcNow < deadline)
        {
            Microsoft.Maui.Platform.Linux.Hosting.LinuxTicker.PumpAll();
            host.Render();
            await Task.Delay(16);
        }
        await task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    private static View Centered(View view)
    {
        view.HorizontalOptions = LayoutOptions.Center;
        view.VerticalOptions = LayoutOptions.Center;
        return view;
    }

    private static void Click(CompatHost host, float x, float y)
    {
        host.DisplayWindow.RaisePointerMoved(x, y);
        host.DisplayWindow.RaisePointerPressed(x, y);
        host.DisplayWindow.RaisePointerReleased(x, y);
        host.Render();
    }

    private static async Task Clear(CompatHost host)
    {
        if (MopupService.Instance.PopupStack.Count > 0)
            await Frames(host, MopupService.Instance.PopAllAsync(false));
    }

    [Fact]
    public async Task A_popup_is_a_window_layer_and_the_page_beneath_stays()
    {
        var (host, page, _) = Start();
        using (host)
        {
            await Clear(host);
            int appearing = page.Appearing, disappearing = page.Disappearing;
            var events = new List<string>();
            void Pushing(object? s, Mopups.Events.PopupNavigationEventArgs e) => events.Add("pushing");
            void Pushed(object? s, Mopups.Events.PopupNavigationEventArgs e) => events.Add("pushed");
            MopupService.Instance.Pushing += Pushing;
            MopupService.Instance.Pushed += Pushed;
            try
            {
                var message = new Label { Text = "Hello from the popup" };
                var popup = MakePopup(Centered(message));
                await Frames(host, MopupService.Instance.PushAsync(popup, false));
                host.Render();

                MopupService.Instance.PopupStack.Should().ContainSingle().Which.Should().BeSameAs(popup);
                host.Context.ModalViews.Should().ContainSingle("the popup is a layer over the window");
                host.Window.Navigation.ModalStack.Should().BeEmpty("a popup is not modal navigation");
                CompatHost.PlatformOf(message).Should().BeOfType<SkiaLabel>("the popup content is realised by OpenMaui's handlers");
                page.Disappearing.Should().Be(disappearing, "the page beneath is not navigated away from");
                events.Should().Equal("pushing", "pushed");

                await Frames(host, MopupService.Instance.PopAsync(false));
                host.Render();
                MopupService.Instance.PopupStack.Should().BeEmpty();
                host.Context.ModalViews.Should().BeEmpty();
                page.Appearing.Should().Be(appearing, "the page beneath never left");
            }
            finally
            {
                MopupService.Instance.Pushing -= Pushing;
                MopupService.Instance.Pushed -= Pushed;
                await Clear(host);
            }
        }
    }

    [Fact]
    public async Task The_default_animation_completes_and_the_popup_closes()
    {
        var (host, _, _) = Start();
        using (host)
        {
            await Clear(host);
            var popup = MakePopup(Centered(new Label { Text = "animated" }));
            popup.IsAnimationEnabled = true;
            await Frames(host, MopupService.Instance.PushAsync(popup));
            MopupService.Instance.PopupStack.Should().ContainSingle();

            await Frames(host, MopupService.Instance.PopAsync());
            host.Render();
            MopupService.Instance.PopupStack.Should().BeEmpty();
            host.Context.ModalViews.Should().BeEmpty("the layer goes once the exit animation has run");
        }
    }

    [Fact]
    public async Task A_click_on_the_backdrop_closes_the_popup_and_raises_BackgroundClicked()
    {
        var (host, _, _) = Start();
        using (host)
        {
            await Clear(host);
            var popup = MakePopup(Centered(new Label { Text = "click outside", WidthRequest = 200, HeightRequest = 80 }));
            int clicked = 0;
            popup.BackgroundClicked += (_, _) => clicked++;
            await Frames(host, MopupService.Instance.PushAsync(popup, false));
            host.Render();

            Click(host, 5, 500);
            for (int i = 0; i < 50 && MopupService.Instance.PopupStack.Count > 0; i++)
            {
                await Task.Delay(20);
                host.Render();
            }

            clicked.Should().Be(1);
            MopupService.Instance.PopupStack.Should().BeEmpty("CloseWhenBackgroundIsClicked closes it");
            host.Context.ModalViews.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task With_CloseWhenBackgroundIsClicked_off_a_backdrop_click_only_reports()
    {
        var (host, _, beneath) = Start();
        using (host)
        {
            await Clear(host);
            int beneathClicks = 0;
            beneath.Clicked += (_, _) => beneathClicks++;
            var popup = MakePopup(Centered(new Label { Text = "stays", WidthRequest = 200, HeightRequest = 80 }), closeOnBackground: false);
            int clicked = 0;
            popup.BackgroundClicked += (_, _) => clicked++;
            await Frames(host, MopupService.Instance.PushAsync(popup, false));
            host.Render();

            var (bx, by) = CompatHost.CenterOf(beneath);
            Click(host, bx, by);
            await Task.Delay(50);
            host.Render();

            clicked.Should().Be(1);
            beneathClicks.Should().Be(0, "the backdrop takes the click: the page beneath gets no input while the popup is up");
            MopupService.Instance.PopupStack.Should().ContainSingle();
            await Clear(host);
        }
    }

    [Fact]
    public async Task A_button_in_the_popup_works_and_does_not_count_as_a_background_click()
    {
        var (host, _, _) = Start();
        using (host)
        {
            await Clear(host);
            int commands = 0, background = 0;
            var button = new Button { Text = "OK", WidthRequest = 100, HeightRequest = 40, Command = new Command(() => commands++) };
            var popup = MakePopup(Centered(button));
            popup.BackgroundClicked += (_, _) => background++;
            await Frames(host, MopupService.Instance.PushAsync(popup, false));
            host.Render();

            var (x, y) = CompatHost.CenterOf(button);
            Click(host, x, y);

            commands.Should().Be(1);
            background.Should().Be(0);
            MopupService.Instance.PopupStack.Should().ContainSingle();
            await Clear(host);
        }
    }

    [Fact]
    public async Task BackgroundInputTransparent_lets_a_click_reach_the_page_beneath()
    {
        var (host, _, beneath) = Start();
        using (host)
        {
            await Clear(host);
            int beneathClicks = 0;
            beneath.Clicked += (_, _) => beneathClicks++;
            var toast = MakePopup(new Label { Text = "toast", HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.End, Margin = 20 }, closeOnBackground: false);
            toast.BackgroundColor = Colors.Transparent;
            toast.BackgroundInputTransparent = true;
            await Frames(host, MopupService.Instance.PushAsync(toast, false));
            host.Render();

            var (bx, by) = CompatHost.CenterOf(beneath);
            Click(host, bx, by);

            beneathClicks.Should().Be(1, "a click-through popup (a toast) leaves the page usable, as on the native platforms");
            MopupService.Instance.PopupStack.Should().ContainSingle();
            await Clear(host);
        }
    }

    [Fact]
    public async Task PopAllAsync_removes_every_popup()
    {
        var (host, _, _) = Start();
        using (host)
        {
            await Clear(host);
            await Frames(host, MopupService.Instance.PushAsync(MakePopup(Centered(new Label { Text = "first" })), false));
            await Frames(host, MopupService.Instance.PushAsync(MakePopup(Centered(new Label { Text = "second" })), false));
            host.Render();
            MopupService.Instance.PopupStack.Should().HaveCount(2);
            host.Context.ModalViews.Should().HaveCount(2);

            await Frames(host, MopupService.Instance.PopAllAsync(false));
            host.Render();

            MopupService.Instance.PopupStack.Should().BeEmpty();
            host.Context.ModalViews.Should().BeEmpty();
        }
    }
}
