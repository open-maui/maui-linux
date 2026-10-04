// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

/// <summary>
/// SkiaNavigationPage transitions as MAUI's NavigationPage needs them: a navigation
/// during a running animation ends the animation instead of being dropped, the
/// stack can be set as a whole (pages inserted or removed beneath the current
/// one), the back arrow follows the current page's HasBackButton, and arranging
/// the navigation page arranges its pages.
/// </summary>
public class SkiaNavigationPageTransitionTests
{
    [Fact]
    public void Push_during_animated_push_is_not_dropped()
    {
        var root = new SkiaPage { Title = "Root" };
        var second = new SkiaPage { Title = "Second" };
        var third = new SkiaPage { Title = "Third" };
        var nav = new SkiaNavigationPage(root);

        nav.Push(second, animated: true);
        nav.IsTransitioning.Should().BeTrue();
        nav.Push(third, animated: false);

        nav.CurrentPage.Should().BeSameAs(third);
        nav.StackDepth.Should().Be(3);
        nav.IsTransitioning.Should().BeFalse();
    }

    [Fact]
    public void Finishing_a_transition_raises_TransitionCompleted()
    {
        var nav = new SkiaNavigationPage(new SkiaPage());
        int completed = 0;
        nav.TransitionCompleted += (_, _) => completed++;

        nav.Push(new SkiaPage(), animated: true);
        nav.Push(new SkiaPage(), animated: false); // ends the running push

        completed.Should().Be(1);
    }

    [Fact]
    public void SetNavigationStack_inserts_and_removes_pages_beneath_the_current_one()
    {
        var a = new SkiaPage { Title = "A" };
        var b = new SkiaPage { Title = "B" };
        var c = new SkiaPage { Title = "C" };
        var nav = new SkiaNavigationPage(b);

        nav.SetNavigationStack(new[] { a, b }, animated: false);
        nav.CurrentPage.Should().BeSameAs(b);
        nav.StackDepth.Should().Be(2);
        nav.IsBackButtonVisible.Should().BeTrue();

        nav.SetNavigationStack(new[] { b }, animated: false);
        nav.StackDepth.Should().Be(1);
        nav.IsBackButtonVisible.Should().BeFalse();

        nav.SetNavigationStack(new[] { a, c }, animated: false);
        nav.CurrentPage.Should().BeSameAs(c);
        nav.RootPage.Should().BeSameAs(a);
    }

    [Fact]
    public void Back_arrow_follows_the_current_pages_HasBackButton()
    {
        var root = new SkiaPage();
        var detail = new SkiaPage();
        var nav = new SkiaNavigationPage(root);
        nav.Push(detail, animated: false);

        nav.IsBackButtonVisible.Should().BeTrue();
        detail.HasBackButton = false;
        nav.IsBackButtonVisible.Should().BeFalse();
    }

    [Fact]
    public void Back_request_goes_to_the_owner_before_popping()
    {
        var nav = new SkiaNavigationPage(new SkiaPage());
        nav.Push(new SkiaPage(), animated: false);
        int asked = 0;
        nav.BackRequested = () => { asked++; return true; };

        nav.OnKeyDown(new KeyEventArgs(Key.Escape, KeyModifiers.None));

        asked.Should().Be(1);
        nav.StackDepth.Should().Be(2, "the owner handled the request (a MAUI NavigationPage pops through its own navigation)");
    }

    [Fact]
    public void Arranging_the_navigation_page_arranges_the_current_page()
    {
        var page = new SkiaPage();
        var nav = new SkiaNavigationPage(page);

        nav.Measure(new Size(400, 300));
        nav.Arrange(new Rect(0, 0, 400, 300));

        page.Bounds.Should().Be(new Rect(0, 0, 400, 300));
    }
}
