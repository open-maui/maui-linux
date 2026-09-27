// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

/// <summary>
/// A page pushed onto a Shell section gets its size (OnSizeAllocated) like any page:
/// Strikeline's pages set their column widths there, and a pushed page that never
/// heard its size stayed blank.
/// </summary>
[Collection("LinuxApplication.Current")]
public class ShellPushedPageSizeTests
{
    private sealed class SizedPage : ContentPage
    {
        public int Allocations;
        public SizedPage() { Content = new Label { Text = "pushed" }; }
        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);
            if (width > 0) Allocations++;
        }
    }

    [Fact]
    public async Task A_pushed_page_hears_its_size()
    {
        var shell = new Shell { FlyoutBehavior = FlyoutBehavior.Disabled };
        shell.Items.Add(new ShellContent { Route = "home", Content = new ContentPage { Content = new Label { Text = "home" } } });
        using var host = new HeadlessMauiHost(shell, withEngine: true);
        host.Context.Render();

        var page = new SizedPage();
        await shell.Navigation.PushAsync(page, false);
        for (int i = 0; i < 3; i++) { Microsoft.Maui.Platform.Linux.Hosting.LinuxTicker.PumpAll(); host.Context.Render(); }

        shell.CurrentPage.Should().BeSameAs(page);
        page.Allocations.Should().BeGreaterThan(0, $"page frame {page.Frame}");
        page.Width.Should().BeGreaterThan(0);
    }
}
