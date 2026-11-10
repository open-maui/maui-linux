// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Services;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// MAUI 11's back-button accessible name (NavigationPage.BackButtonAccessibilityLabel, Shell's
/// BackButtonBehavior.AccessibilityLabel) names the drawn back arrow for assistive technology,
/// "Back" by default as on Windows; and toolbar items' badges (BadgeText, BadgeColor,
/// BadgeTextColor) are drawn over them as Windows' InfoBadge: a number, or a dot.
/// </summary>
[Collection("LinuxApplication.Current")]
public class NavigationBarBackButtonAndBadgeTests
{
    private static IAccessible? FirstButton(IAccessible owner) =>
        owner.Children.FirstOrDefault(c => c.Role == AccessibleRole.Button);

    [Fact]
    public async Task A_NavigationPage_back_button_is_named_by_the_shown_pages_label()
    {
        var root = new ContentPage { Title = "Inbox" };
        var detail = new ContentPage { Title = "Message" };
        var navigationPage = new NavigationPage(root);
        using var host = new HeadlessMauiHost(navigationPage, withEngine: true);
        host.Context.Render();
        var skia = (SkiaNavigationPage)navigationPage.Handler!.PlatformView!;
        IAccessible accessible = skia;

        FirstButton(accessible).Should().BeNull("the root page has no back button");
        accessible.Children.Should().Contain((IAccessible)root.Handler!.PlatformView!, "the current page is read");

        // MAUI's NavigationPageToolbar reads the label from the top of the stack: the page shown.
        NavigationPage.SetBackButtonAccessibilityLabel(detail, "Back to inbox");
        await navigationPage.PushAsync(detail, false);
        host.Context.Render();

        var back = FirstButton(accessible);
        back.Should().NotBeNull();
        back!.AccessibleName.Should().Be("Back to inbox");
        back.Parent.Should().BeSameAs(accessible);
        back.States.Should().HaveFlag(AccessibleStates.Enabled);
        back.Bounds.Width.Should().Be(56);
        ((SkiaPage)detail.Handler!.PlatformView!).BackButtonAccessibilityLabel.Should().Be("Back to inbox");

        // The window's toolbar carries it too.
        var toolbar = (host.Window as IToolbarElement)!.Toolbar!;
        ((Microsoft.Maui.Platform.Linux.Handlers.SkiaToolbar)toolbar.Handler!.PlatformView!).BackButtonAccessibilityLabel.Should().Be("Back to inbox");

        NavigationPage.SetBackButtonAccessibilityLabel(detail, "Inbox");
        host.Context.Render();
        back.AccessibleName.Should().Be("Inbox", "the name follows the label");
        NavigationPage.SetBackButtonAccessibilityLabel(detail, null!);
        host.Context.Render();
        back.AccessibleName.Should().Be("Back", "WinUI's default name comes back when the label is cleared");

        back.DoAction("click").Should().BeTrue();
        navigationPage.Navigation.NavigationStack.Should().HaveCount(1, "the click action goes back");
    }

    [Fact]
    public void A_Shell_back_button_is_named_by_its_BackButtonBehavior()
    {
        var home = new ContentPage { Title = "Home" };
        var shell = new Shell { FlyoutBehavior = FlyoutBehavior.Disabled };
        shell.Items.Add(new ShellContent { Route = "home", Content = home });
        using var host = new HeadlessMauiHost(shell, withEngine: true);
        host.Context.Render();
        var platform = (SkiaShell)shell.Handler!.PlatformView!;
        IAccessible accessible = platform;
        FirstButton(accessible)?.AccessibleName.Should().NotBe("Back", "nothing to go back to");

        var detail = new ContentPage { Title = "Detail" };
        int commands = 0;
        var behavior = new BackButtonBehavior { AccessibilityLabel = "Close detail", Command = new Command(() => commands++) };
        Shell.SetBackButtonBehavior(detail, behavior);
        shell.Navigation.PushAsync(detail, false).GetAwaiter().GetResult();
        host.Context.Render();

        var back = accessible.Children.FirstOrDefault(c => c.Role == AccessibleRole.Button && c.AccessibleName == "Close detail");
        back.Should().NotBeNull("the back arrow is a named button");
        accessible.Children[0].Should().BeSameAs(back);

        behavior.AccessibilityLabel = null;
        host.Context.Render();
        back!.AccessibleName.Should().Be("Back");

        back.DoAction("click").Should().BeTrue();
        commands.Should().Be(1, "the click does what the arrow does: the behavior's Command");
    }

    [Fact]
    public void Badge_text_shows_a_number_or_a_dot_as_on_Windows()
    {
        ToolbarBadge.ValueOf("3").Should().Be(3);
        ToolbarBadge.ValueOf("120").Should().Be(120);
        ToolbarBadge.ValueOf("").Should().Be(-1, "empty text is a dot");
        ToolbarBadge.ValueOf("new").Should().Be(-1, "non-numeric text is a dot");
        ToolbarBadge.ValueOf("-2").Should().Be(-1, "a negative number is a dot");
        ToolbarBadge.BoundsFor(SkiaSharp.SKRect.Create(100, 20, 24, 24), null).IsEmpty.Should().BeTrue("null text shows no badge");

        var dot = ToolbarBadge.BoundsFor(SkiaSharp.SKRect.Create(100, 20, 24, 24), "");
        dot.Should().Be(new SkiaSharp.SKRect(124, 16, 128, 20), "a 4-pixel dot, 4 pixels out of the top-right corner");
        var number = ToolbarBadge.BoundsFor(SkiaSharp.SKRect.Create(100, 20, 24, 24), "5");
        number.Height.Should().Be(16);
        number.Width.Should().Be(16, "a single digit is round");
        number.Right.Should().Be(128);
        ToolbarBadge.BoundsFor(SkiaSharp.SKRect.Create(100, 20, 24, 24), "1234").Width.Should().BeGreaterThan(16);
    }

    [Fact]
    public void A_Shell_toolbar_item_draws_its_badge_and_follows_it()
    {
        var mail = new ToolbarItem { Text = "Mail", BadgeText = "4", BadgeColor = Colors.Red };
        var page = new ContentPage { Title = "Home", Content = new Label { Text = "a" } };
        page.ToolbarItems.Add(mail);
        var shell = new Shell { FlyoutBehavior = FlyoutBehavior.Disabled };
        shell.Items.Add(new ShellContent { Route = "home", Content = page });
        using var host = new HeadlessMauiHost(shell, withEngine: true);
        host.Context.Render();
        var platform = (SkiaShell)shell.Handler!.PlatformView!;

        platform.ToolbarBadgeAreas.Should().ContainKey(mail);
        platform.ToolbarBadgeAreas[mail].Height.Should().Be(16);

        mail.BadgeText = "";
        host.Context.Render();
        platform.ToolbarBadgeAreas[mail].Height.Should().Be(4, "empty text is a dot");

        mail.BadgeText = null;
        host.Context.Render();
        platform.ToolbarBadgeAreas.Should().NotContainKey(mail, "null text removes the badge");
    }

    [Fact]
    public async Task A_NavigationPage_toolbar_item_carries_its_badge_and_follows_it()
    {
        var root = new ContentPage { Title = "Root" };
        var bell = new ToolbarItem { Text = "Alerts", BadgeText = "2", BadgeColor = Colors.Green, BadgeTextColor = Colors.Black };
        root.ToolbarItems.Add(bell);
        var navigationPage = new NavigationPage(root);
        using var host = new HeadlessMauiHost(navigationPage, withEngine: true);
        host.Context.Render();
        var page = (SkiaContentPage)root.Handler!.PlatformView!;

        var item = page.ToolbarItems.Single();
        item.BadgeText.Should().Be("2");
        item.BadgeColor.Should().Be(Colors.Green);
        item.BadgeTextColor.Should().Be(Colors.Black);
        item.BadgeBounds.Height.Should().Be(16, "the badge is drawn over the item");

        bell.BadgeText = "x";
        bell.BadgeColor = Colors.Blue;
        host.Context.Render();
        item = page.ToolbarItems.Single();
        item.BadgeText.Should().Be("x");
        item.BadgeColor.Should().Be(Colors.Blue);
        item.BadgeBounds.Height.Should().Be(4, "non-numeric text is a dot");

        // The badge is drawn in its colour.
        bool blue = false;
        using (var bitmap = new SkiaSharp.SKBitmap(800, 600))
        using (var canvas = new SkiaSharp.SKCanvas(bitmap))
        {
            ((SkiaView)navigationPage.Handler!.PlatformView!).Draw(canvas);
            var c = bitmap.GetPixel((int)item.BadgeBounds.MidX, (int)item.BadgeBounds.MidY);
            blue = c.Blue > 200 && c.Red < 60 && c.Green < 60;
        }
        blue.Should().BeTrue();
        await Task.CompletedTask;
    }
}
