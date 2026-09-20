// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Input;
using FluentAssertions;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Services;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// FlyoutBase.ContextFlyout: a MenuFlyout attached to any view opens as the
/// platform context menu on secondary click, replacing the control's own.
/// </summary>
[Collection("LinuxApplication.Current")]
public class ContextFlyoutTests : IDisposable
{
    public void Dispose() => LinuxDialogService.HideContextMenu();

    private sealed class CountingCommand : ICommand
    {
        public int Executions { get; private set; }
        public object? LastParameter { get; private set; }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) { Executions++; LastParameter = parameter; }
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }

    [Fact]
    public void BuildItems_maps_items_separators_and_enabled_state()
    {
        var flyout = new MenuFlyout
        {
            new MenuFlyoutItem { Text = "Cut" },
            new MenuFlyoutItem { Text = "Copy", IsEnabled = false },
            new MenuFlyoutSeparator(),
            new MenuFlyoutItem { Text = "Paste" },
        };

        var items = ContextFlyoutBridge.BuildItems(flyout);

        items.Select(i => i.Text).Should().Equal("Cut", "Copy", "", "Paste");
        items[1].IsEnabled.Should().BeFalse();
        items[2].IsSeparator.Should().BeTrue();
    }

    [Fact]
    public void BuildItems_flattens_sub_items_under_a_heading()
    {
        var share = new MenuFlyoutSubItem { Text = "Share" };
        share.Add(new MenuFlyoutItem { Text = "Email" });
        share.Add(new MenuFlyoutItem { Text = "Link" });
        var flyout = new MenuFlyout
        {
            new MenuFlyoutItem { Text = "Open" },
            share,
            new MenuFlyoutItem { Text = "Close" },
        };

        var items = ContextFlyoutBridge.BuildItems(flyout);

        items.Select(i => i.IsSeparator ? "-" : i.Text).Should().Equal("Open", "-", "Share", "  Email", "  Link", "-", "Close");
        items[2].IsEnabled.Should().BeFalse("the sub-item heading is not clickable");
    }

    [Fact]
    public void BuildItems_drops_edge_and_doubled_separators()
    {
        var flyout = new MenuFlyout
        {
            new MenuFlyoutSeparator(),
            new MenuFlyoutItem { Text = "A" },
            new MenuFlyoutSeparator(),
            new MenuFlyoutSeparator(),
            new MenuFlyoutItem { Text = "B" },
            new MenuFlyoutSeparator(),
        };

        ContextFlyoutBridge.BuildItems(flyout).Select(i => i.IsSeparator ? "-" : i.Text).Should().Equal("A", "-", "B");
    }

    [Fact]
    public void Activating_an_item_runs_the_command_and_raises_Clicked_once_each()
    {
        var command = new CountingCommand();
        int clicked = 0;
        var item = new MenuFlyoutItem { Text = "Do", Command = command, CommandParameter = 7 };
        item.Clicked += (_, _) => clicked++;
        var flyout = new MenuFlyout { item };

        ContextFlyoutBridge.BuildItems(flyout)[0].Action!.Invoke();

        command.Executions.Should().Be(1);
        command.LastParameter.Should().Be(7);
        clicked.Should().Be(1);
    }

    [Fact]
    public void Find_walks_up_to_the_nearest_view_with_a_flyout()
    {
        var label = new Label { Text = "child" };
        var layout = new VerticalStackLayout { label };
        var flyout = new MenuFlyout { new MenuFlyoutItem { Text = "On layout" } };
        FlyoutBase.SetContextFlyout(layout, flyout);

        var platformLayout = HeadlessMauiContext.Realize<SkiaLayoutView>(layout);
        var platformLabel = platformLayout.Children.OfType<SkiaView>().First();

        ContextFlyoutBridge.Find(platformLabel).Should().BeSameAs(flyout);
        ContextFlyoutBridge.Find(platformLayout).Should().BeSameAs(flyout);
        ContextFlyoutBridge.Find(null).Should().BeNull();
    }

    [Fact]
    public void Find_prefers_the_innermost_flyout()
    {
        var inner = new MenuFlyout { new MenuFlyoutItem { Text = "inner" } };
        var outer = new MenuFlyout { new MenuFlyoutItem { Text = "outer" } };
        var label = new Label { Text = "child" };
        var layout = new VerticalStackLayout { label };
        FlyoutBase.SetContextFlyout(label, inner);
        FlyoutBase.SetContextFlyout(layout, outer);

        var platformLayout = HeadlessMauiContext.Realize<SkiaLayoutView>(layout);
        var platformLabel = platformLayout.Children.OfType<SkiaView>().First();

        ContextFlyoutBridge.Find(platformLabel).Should().BeSameAs(inner);
    }

    [Fact]
    public void TryShow_opens_the_platform_context_menu_and_reports_it()
    {
        var label = new Label { Text = "target" };
        FlyoutBase.SetContextFlyout(label, new MenuFlyout { new MenuFlyoutItem { Text = "Rename" } });
        var platform = HeadlessMauiContext.Realize<SkiaLabel>(label);

        LinuxDialogService.HasContextMenu.Should().BeFalse();
        ContextFlyoutBridge.TryShow(platform, 10, 20).Should().BeTrue();
        LinuxDialogService.HasContextMenu.Should().BeTrue();
        LinuxDialogService.ActiveContextMenu.Should().NotBeNull();
    }

    [Fact]
    public void TryShow_is_a_no_op_without_a_flyout_or_with_an_empty_one()
    {
        var plain = HeadlessMauiContext.Realize<SkiaLabel>(new Label { Text = "plain" });
        ContextFlyoutBridge.TryShow(plain, 0, 0).Should().BeFalse();

        var empty = new Label { Text = "empty" };
        FlyoutBase.SetContextFlyout(empty, new MenuFlyout());
        ContextFlyoutBridge.TryShow(HeadlessMauiContext.Realize<SkiaLabel>(empty), 0, 0).Should().BeFalse();

        LinuxDialogService.HasContextMenu.Should().BeFalse();
    }

    [Fact]
    public void Right_click_on_a_hosted_view_opens_its_flyout()
    {
        var label = new Label { Text = "hosted", WidthRequest = 200, HeightRequest = 40 };
        FlyoutBase.SetContextFlyout(label, new MenuFlyout { new MenuFlyoutItem { Text = "Inspect" } });
        var page = new ContentPage { Content = label };
        using var host = new HeadlessMauiHost(page, withEngine: true);
        host.Context.Render(); // lays the page out so hit-testing finds the label

        host.DisplayWindow.RaisePointerPressed(50, 20, PointerButton.Right);

        LinuxDialogService.HasContextMenu.Should().BeTrue("the secondary click reached the flyout");
    }

    [Fact]
    public void Left_click_on_a_hosted_view_does_not_open_its_flyout()
    {
        var label = new Label { Text = "hosted", WidthRequest = 200, HeightRequest = 40 };
        FlyoutBase.SetContextFlyout(label, new MenuFlyout { new MenuFlyoutItem { Text = "Inspect" } });
        var page = new ContentPage { Content = label };
        using var host = new HeadlessMauiHost(page, withEngine: true);
        host.Context.Render();

        host.DisplayWindow.RaisePointerPressed(50, 20, PointerButton.Left);

        LinuxDialogService.HasContextMenu.Should().BeFalse();
    }
}
