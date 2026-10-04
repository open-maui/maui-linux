// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using FluentAssertions;
using Microsoft.Maui.Controls.Linux.Tests.Views;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Handlers;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// MAUI's visual-tree platform lookups on the Skia tree: a point in the window finds
/// the views under it (their bounds in the window, not their parent-relative Frame),
/// and a platform view finds its element, or the nearest element above it.
/// </summary>
[Collection("LinuxApplication.Current")]
public class VisualTreeElementLookupTests
{
    public VisualTreeElementLookupTests() => VisualTreeElementPatches.Install();

    // VisualTreeElementExtensions.GetVisualTreeElement(platformView, searchAncestors) is internal.
    private static IVisualTreeElement? ElementOf(object platformView, bool searchAncestors = true) =>
        (IVisualTreeElement?)typeof(VisualTreeElementExtensions)
            .GetMethod("GetVisualTreeElement", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(object), typeof(bool) }, null)!
            .Invoke(null, new[] { platformView, (object)searchAncestors });

    private static (HeadlessMauiHost Host, Label Label, Button Button) NestedPage()
    {
        var label = new Label { Text = "Find me" };
        var button = new Button { Text = "Go" };
        var page = new ContentPage
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(0, 40, 0, 0),
                Children = { new VerticalStackLayout { Padding = new Thickness(30, 0, 0, 0), Children = { label, button } } },
            },
        };
        var host = new HeadlessMauiHost(page, withEngine: true);
        host.Context.Render();
        return (host, label, button);
    }

    [Fact]
    public void A_point_in_the_window_finds_a_nested_view()
    {
        var (host, label, _) = NestedPage();
        using (host)
        {
            var bounds = ((SkiaView)label.Handler!.PlatformView!).ScreenBounds;
            bounds.Top.Should().BeGreaterThan(30, "the label sits below the padding, so its Frame (parent-relative) is not where it is");

            host.Window.GetVisualTreeElements(new Point(bounds.Left + 1, bounds.Top + 1)).Should().Contain(label);
            host.Window.GetVisualTreeElements(bounds.Right + 5, bounds.Bottom + 200).Should().NotContain(label);
        }
    }

    [Fact]
    public void A_platform_view_finds_its_element()
    {
        var (host, label, button) = NestedPage();
        using (host)
        {
            ElementOf(button.Handler!.PlatformView!).Should().BeSameAs(button);
            ElementOf(label.Handler!.PlatformView!).Should().BeSameAs(label);
        }
    }

    [Fact]
    public void A_platform_view_with_no_element_finds_the_nearest_one_above_it()
    {
        var (host, _, button) = NestedPage();
        using (host)
        {
            var layout = (VerticalStackLayout)button.Parent!;
            var arbitrary = new SkiaLabel();
            ((SkiaView)layout.Handler!.PlatformView!).AddChild(arbitrary);

            ElementOf(arbitrary).Should().BeSameAs(layout);
            ElementOf(arbitrary, searchAncestors: false).Should().BeNull();
        }
    }
}
