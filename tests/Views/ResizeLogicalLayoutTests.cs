// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

/// <summary>
/// A window resize lays the tree out at its logical size. The resize carries the
/// buffer's physical size, and the tree was laid out at that first (the scale
/// factor times too wide), then at the logical size when the frame rendered: a
/// width-driven layout (MAToolbar folding its items) ran twice per resize step
/// at two widths, and churned while the window was being resized.
/// </summary>
[Collection("LinuxApplication.Current")]
public class ResizeLogicalLayoutTests
{
    [Fact]
    public void A_resize_on_a_scaled_display_lays_out_at_the_logical_width()
    {
        var box = new BoxView();
        using var host = new HeadlessMauiHost(new ContentPage { Content = box }, withEngine: true);
        host.DisplayWindow.RaiseScaleChanged(2f);
        host.Context.Render();
        box.Width.Should().BeApproximately(800, 0.5);

        var widths = new List<double>();
        box.SizeChanged += (_, _) => widths.Add(box.Width);
        host.DisplayWindow.RaiseResized(1700, 1200);
        host.Context.Render();

        widths.Should().NotBeEmpty().And.OnlyContain(w => Math.Abs(w - 850) < 0.5);
    }
}
