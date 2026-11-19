// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// Since MAUI 10.0.120 a view's Visibility is its effective visibility: Collapsed inside a hidden
/// parent. Mapping it to the Skia view must not write it back to the view's own IsVisible, or the
/// children of a parent shown later stay hidden.
/// </summary>
[Collection("LinuxApplication.Current")]
public class EffectiveVisibilityTests
{
    [Fact]
    public void Children_of_a_hidden_parent_keep_their_IsVisible_and_show_with_it()
    {
        var label = new Label { Text = "shown later" };
        var stack = new VerticalStackLayout { Children = { label } };
        var parent = new Border { IsVisible = false, Content = stack };

        using var host = new HeadlessMauiHost(new ContentPage { Content = new Grid { parent } }, withEngine: true);
        host.Context.Render();

        ((IView)label).Visibility.Should().Be(Visibility.Collapsed, "its parent is hidden");
        stack.IsVisible.Should().BeTrue();
        label.IsVisible.Should().BeTrue();

        parent.IsVisible = true;
        host.Context.Render();

        ((IView)label).Visibility.Should().Be(Visibility.Visible);
        ((Microsoft.Maui.Platform.SkiaView)label.Handler!.PlatformView!).Bounds.Width.Should().BeGreaterThan(0);
    }
}
