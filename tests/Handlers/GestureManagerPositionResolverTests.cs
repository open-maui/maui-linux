// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Handlers;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

// The GetPosition resolver GestureManager hands to MAUI event args.
// Contract (matching Microsoft.Maui.Controls docs): GetPosition(null) →
// window coordinates; GetPosition(element) → relative to the element's
// top-left (via ScreenBounds, so scroll offsets are honored); unresolvable
// element → null. The pure translation math is factored into
// ResolvePositionCore precisely so it can be verified without a display
// server or live handlers.
public class GestureManagerPositionResolverTests
{
    [Fact]
    public void ResolvePositionCore_SubtractsElementOrigin()
    {
        var result = GestureManager.ResolvePositionCore(100, 80, new Rect(30, 20, 200, 100));

        result.Should().Be(new Point(70, 60));
    }

    [Fact]
    public void ResolvePositionCore_PointLeftOfElement_YieldsNegativeCoordinates()
    {
        // MAUI allows positions outside the element — negative is valid.
        var result = GestureManager.ResolvePositionCore(10, 5, new Rect(30, 20, 200, 100));

        result.Should().Be(new Point(-20, -15));
    }

    [Fact]
    public void ResolvePositionCore_ScrolledBounds_UsesOnScreenOrigin()
    {
        // ScreenBounds already folds ancestor scroll offsets into the origin;
        // the math must translate against that on-screen origin as-is.
        var scrolledOrigin = new Rect(30 - 0, 20 - 150, 200, 100); // content scrolled down 150

        var result = GestureManager.ResolvePositionCore(100, 80, scrolledOrigin);

        result.Should().Be(new Point(70, 210));
    }

    [Fact]
    public void ResolvePositionCore_NullBounds_ReturnsNull()
    {
        GestureManager.ResolvePositionCore(100, 80, null).Should().BeNull();
    }

    [Fact]
    public void CreatePositionResolver_NullElement_ReturnsWindowPoint()
    {
        var resolver = GestureManager.CreatePositionResolver(42.5, 17.25);

        resolver(null).Should().Be(new Point(42.5, 17.25));
    }

    [Fact]
    public void CreatePositionResolver_ElementWithoutHandler_ReturnsNull()
    {
        var resolver = GestureManager.CreatePositionResolver(42, 17);

        // A view never attached to a window has no handler/platform view —
        // per the MAUI contract the position is unresolvable.
        resolver(new Label()).Should().BeNull();
    }
}
