// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// The platform back arrow of a MAUI NavigationPage pops MAUI's own stack (and
/// through it the platform), so the two never disagree; a page's
/// NavigationPage.HasNavigationBar / HasBackButton reach its bar.
/// </summary>
[Collection(HeadlessMaui.Collection)]
public class NavigationPageParityTests
{
    [Fact]
    public async Task Platform_back_pops_the_MAUI_navigation_stack()
    {
        var root = new ContentPage { Title = "Root" };
        var detail = new ContentPage { Title = "Detail" };
        var navigationPage = new NavigationPage(root);
        HeadlessMaui.HostInWindow(navigationPage);
        var context = HeadlessMaui.CreateContext();
        var skia = (SkiaNavigationPage)Microsoft.Maui.Platform.Linux.Hosting.MauiHandlerExtensions.ToHandler(navigationPage, context).PlatformView!;

        await navigationPage.PushAsync(detail, false);
        navigationPage.Navigation.NavigationStack.Should().HaveCount(2);

        skia.OnKeyDown(new KeyEventArgs(Key.Escape, KeyModifiers.None));

        navigationPage.Navigation.NavigationStack.Should().HaveCount(1);
        skia.StackDepth.Should().Be(1, "the platform follows MAUI's pop (its page animates in)");
    }

    [Fact]
    public async Task HasNavigationBar_and_HasBackButton_reach_the_page_bar()
    {
        var root = new ContentPage();
        var detail = new ContentPage();
        var navigationPage = new NavigationPage(root);
        HeadlessMaui.HostInWindow(navigationPage);
        var skia = (SkiaNavigationPage)Microsoft.Maui.Platform.Linux.Hosting.MauiHandlerExtensions.ToHandler(navigationPage, HeadlessMaui.CreateContext()).PlatformView!;
        await navigationPage.PushAsync(detail, false);
        var detailView = (SkiaPage)detail.Handler!.PlatformView!;

        skia.IsBackButtonVisible.Should().BeTrue();
        NavigationPage.SetHasBackButton(detail, false);
        skia.IsBackButtonVisible.Should().BeFalse();

        NavigationPage.SetHasNavigationBar(detail, false);
        detailView.ShowNavigationBar.Should().BeFalse();
    }
}
