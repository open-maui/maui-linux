// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// A view's desired size never exceeds the space MAUI offered it (as Android's
/// AT_MOST measure and WinUI's DesiredSize), unless it asked for a size.
/// </summary>
public class DesiredSizeConstraintTests
{
    private const string Long = "A label whose single line of text is far wider than the space it is given";

    [Theory]
    [InlineData(LineBreakMode.NoWrap)]
    [InlineData(LineBreakMode.TailTruncation)]
    public void Single_line_label_is_no_wider_than_offered(LineBreakMode mode)
    {
        var label = new Label { Text = Long, LineBreakMode = mode };
        HeadlessMauiContext.Realize<SkiaLabel>(label);

        var size = label.Handler!.GetDesiredSize(100, double.PositiveInfinity);

        size.Width.Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public void Explicit_width_still_wins()
    {
        var label = new Label { Text = Long, WidthRequest = 300 };
        HeadlessMauiContext.Realize<SkiaLabel>(label);

        var size = label.Handler!.GetDesiredSize(100, double.PositiveInfinity);

        size.Width.Should().Be(300);
    }
}
