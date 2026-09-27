// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

/// <summary>
/// A templated ShellContent's page is built when it is first shown, as MAUI builds it:
/// building every page at start ran constructors of pages the app never visits
/// (Strikeline's sign-out page, which needs a signed-in account, threw at start).
/// </summary>
[Collection("LinuxApplication.Current")]
public class ShellLazyPageTests
{
    private sealed class CountingPage : ContentPage
    {
        public static int Built;
        public CountingPage() { Built++; Content = new Label { Text = "later" }; }
    }

    [Fact]
    public async Task A_templated_page_is_built_when_first_shown()
    {
        CountingPage.Built = 0;
        var shell = new Shell { FlyoutBehavior = FlyoutBehavior.Disabled };
        shell.Items.Add(new ShellContent { Route = "first", Content = new ContentPage { Content = new Label { Text = "first" } } });
        shell.Items.Add(new ShellContent { Route = "later", ContentTemplate = new DataTemplate(typeof(CountingPage)) });
        using var host = new HeadlessMauiHost(shell, withEngine: true);
        host.Context.Render();

        CountingPage.Built.Should().Be(0, "the second page has not been shown");

        await Shell.Current.GoToAsync("//later");
        host.Context.Render();

        var platform = (Microsoft.Maui.Platform.SkiaShell)shell.Handler!.PlatformView!;
        platform.CurrentSectionIndex.Should().Be(1, "the platform follows MAUI's navigation");
        CountingPage.Built.Should().Be(1);
        shell.CurrentPage.Should().BeOfType<CountingPage>();
    }
}
