// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// A child's LayoutBounds changed after it was added (MAToolbar places its
/// overflow menu below the button, then moves it above once it knows its
/// height) moves the child.
/// </summary>
[Collection("LinuxApplication.Current")]
public class AbsoluteLayoutBoundsTests
{
    [Fact]
    public void Changing_a_childs_LayoutBounds_moves_it()
    {
        var menu = new BoxView { Color = Colors.Red, WidthRequest = 50, HeightRequest = 30 };
        var overlay = new AbsoluteLayout();
        overlay.Children.Add(menu);
        AbsoluteLayout.SetLayoutBounds(menu, new Rect(10, 200, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
        using var host = new HeadlessMauiHost(new ContentPage { Content = overlay }, withEngine: true);
        host.Context.Render();
        ((SkiaView)menu.Handler!.PlatformView!).Bounds.Top.Should().BeApproximately(200, 0.5);

        AbsoluteLayout.SetLayoutBounds(menu, new Rect(10, 80, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
        host.Context.Render();

        ((SkiaView)menu.Handler!.PlatformView!).Bounds.Top.Should().BeApproximately(80, 0.5);
    }
}
