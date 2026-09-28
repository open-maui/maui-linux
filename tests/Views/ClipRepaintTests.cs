// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls.Shapes;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

/// <summary>
/// Only a clip of another shape repaints: Syncfusion's chips set a new, equal clip every time
/// they draw, and repainting for it drew them again, every frame, on an idle page.
/// </summary>
[Collection("LinuxApplication.Current")]
public class ClipRepaintTests
{
    [Fact]
    public void An_equal_clip_asks_for_no_frame_and_a_different_one_does()
    {
        var box = new BoxView { Color = Colors.Red, WidthRequest = 50, HeightRequest = 50, HorizontalOptions = LayoutOptions.Start,
            Clip = new RoundRectangleGeometry(new CornerRadius(8), new Rect(0, 0, 50, 50)) };
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { box } }, withEngine: true);
        host.Context.Render();
        host.Context.Render();
        var engine = host.Context.RenderingEngine!;
        engine.NeedsFrame.Should().BeFalse();

        box.Clip = new RoundRectangleGeometry(new CornerRadius(8), new Rect(0, 0, 50, 50));
        engine.NeedsFrame.Should().BeFalse("the same shape");

        box.Clip = new RoundRectangleGeometry(new CornerRadius(20), new Rect(0, 0, 50, 50));
        engine.NeedsFrame.Should().BeTrue("another shape");
        host.Context.Render();

        box.Clip = null;
        engine.NeedsFrame.Should().BeTrue("the clip was removed");
    }
}
