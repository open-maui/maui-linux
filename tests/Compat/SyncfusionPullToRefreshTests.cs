// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Syncfusion;
using Syncfusion.Maui.PullToRefresh;
using Xunit;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// SfPullToRefresh pulls with the mouse, as on Windows: dragging the content down past the
/// threshold refreshes; a short drag does not; nor does a drag that starts on content that is
/// scrolled down (it scrolls the content back instead).
/// </summary>
[Collection("LinuxApplication.Current")]
public sealed class SyncfusionPullToRefreshTests
{
    private static (CompatHost Host, SfPullToRefresh Pull, List<string> Log) Host(View content)
    {
        var pull = new SfPullToRefresh { PullableContent = content };
        var log = new List<string>();
        pull.Pulling += (_, _) => { if (!log.Contains("pulling")) log.Add("pulling"); };
        pull.Refreshing += (_, _) => log.Add("refreshing");
        var host = new CompatHost(new ContentPage { Content = pull }, b => b.UseLinuxSyncfusion(), 400, 500);
        host.Render();
        host.Render();
        return (host, pull, log);
    }

    private static void Drag(CompatHost host, float x, float y, float distance)
    {
        host.DisplayWindow.RaisePointerMoved(x, y);
        host.DisplayWindow.RaisePointerPressed(x, y);
        for (int i = 1; i <= 10; i++)
        {
            host.DisplayWindow.RaisePointerMoved(x, y + distance * i / 10);
            host.Render();
        }
        host.DisplayWindow.RaisePointerReleased(x, y + distance);
        for (int i = 0; i < 5; i++)
        {
            Thread.Sleep(20);
            Microsoft.Maui.Platform.Linux.Hosting.LinuxTicker.PumpAll();
            host.Render();
        }
    }

    private static View Content() => new Grid
    {
        BackgroundColor = Microsoft.Maui.Graphics.Colors.LightGray,
        Children = { new Label { Text = "Pull me", HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center } },
    };

    [Fact]
    public void Dragging_the_content_down_refreshes()
    {
        // The refresh spins its progress circle in an async void loop until the app ends the
        // refresh; outside xUnit's synchronization context, which would wait for that loop.
        var context = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            var (host, pull, log) = Host(Content());
            using (host)
            {
                Drag(host, 200, 100, 250);
                log.Should().Equal("pulling", "refreshing");
                pull.IsRefreshing = false;
            }
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(context);
        }
    }

    [Fact]
    public void A_short_drag_does_not_refresh()
    {
        var (host, _, log) = Host(Content());
        using (host)
        {
            Drag(host, 200, 100, 30);
            log.Should().NotContain("refreshing");
        }
    }

    [Fact]
    public void A_drag_on_scrolled_content_does_not_pull()
    {
        var stack = new VerticalStackLayout();
        for (int i = 0; i < 60; i++)
            stack.Children.Add(new Label { Text = "Row " + i, HeightRequest = 30 });
        var scroll = new ScrollView { Content = stack };
        var (host, _, log) = Host(scroll);
        using (host)
        {
            scroll.ScrollToAsync(0, 300, false).Wait(1000);
            host.Render();
            Drag(host, 200, 100, 250);
            log.Should().BeEmpty();
        }
    }
}
