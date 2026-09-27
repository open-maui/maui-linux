// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// Scrolling reports the position back to the MAUI ScrollView (ScrollY and
/// Scrolled), which list controls use to realise rows as they scroll in.
/// </summary>
[Collection("LinuxApplication.Current")]
public class ScrollViewOffsetTests
{
    [Fact]
    public void Scrolling_updates_ScrollY_and_raises_Scrolled()
    {
        var scroll = new ScrollView { HeightRequest = 200, Content = new BoxView { HeightRequest = 2000 } };
        double reported = -1;
        scroll.Scrolled += (_, e) => reported = e.ScrollY;
        using var host = new HeadlessMauiHost(new ContentPage { Content = scroll }, withEngine: true);
        host.Context.Render();

        ((SkiaScrollView)scroll.Handler!.PlatformView!).ScrollTo(0, 300, false);

        scroll.ScrollY.Should().BeApproximately(300, 0.5);
        reported.Should().BeApproximately(300, 0.5);
    }
}
