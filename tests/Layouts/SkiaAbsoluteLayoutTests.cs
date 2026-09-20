// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Handlers;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Layouts;

using AbsoluteLayoutFlags = Microsoft.Maui.Platform.AbsoluteLayoutFlags;

/// <summary>
/// AbsoluteLayout: absolute and proportional bounds and flags. Until
/// 10.0.101.2 the AbsoluteLayout handler was never registered and the control
/// silently rendered as a vertical stack.
/// </summary>
public class SkiaAbsoluteLayoutTests
{
    private static SkiaBoxView Box() => new() { Color = Colors.Green };

    private static void Lay(SkiaView v, double w, double h)
    {
        v.Measure(new Size(w, h));
        v.Arrange(new Rect(0, 0, w, h));
    }

    [Fact]
    public void Absolute_bounds_position_and_size_the_child()
    {
        var l = new SkiaAbsoluteLayout();
        var a = Box();
        l.AddChild(a, new SKRect(20, 30, 120, 80));
        Lay(l, 400, 300);

        a.Bounds.Should().Be(new Rect(20, 30, 100, 50));
    }

    [Fact]
    public void Proportional_position_uses_the_layout_size()
    {
        var l = new SkiaAbsoluteLayout();
        var a = Box();
        l.AddChild(a, new SKRect(0.5f, 0.5f, 0.5f + 100, 0.5f + 50), AbsoluteLayoutFlags.PositionProportional);
        Lay(l, 400, 300);

        // Proportional position: (layout - child) * fraction
        a.Bounds.X.Should().BeApproximately((400 - 100) * 0.5, 1);
        a.Bounds.Y.Should().BeApproximately((300 - 50) * 0.5, 1);
        a.Bounds.Width.Should().BeApproximately(100, 1);
    }

    [Fact]
    public void Proportional_size_scales_with_the_layout()
    {
        var l = new SkiaAbsoluteLayout();
        var a = Box();
        l.AddChild(a, new SKRect(0, 0, 0.5f, 0.25f), AbsoluteLayoutFlags.SizeProportional);
        Lay(l, 400, 300);

        a.Bounds.Width.Should().BeApproximately(200, 1);
        a.Bounds.Height.Should().BeApproximately(75, 1);
    }

    [Fact]
    public void All_flags_fill_the_layout_when_bounds_are_unit()
    {
        var l = new SkiaAbsoluteLayout();
        var a = Box();
        l.AddChild(a, new SKRect(0, 0, 1, 1), AbsoluteLayoutFlags.All);
        Lay(l, 400, 300);

        a.Bounds.Width.Should().BeApproximately(400, 1);
        a.Bounds.Height.Should().BeApproximately(300, 1);
    }

    [Fact]
    public void Children_overlap_instead_of_stacking()
    {
        var l = new SkiaAbsoluteLayout();
        var a = Box(); var b = Box();
        l.AddChild(a, new SKRect(10, 10, 60, 60));
        l.AddChild(b, new SKRect(10, 10, 60, 60));
        Lay(l, 200, 200);

        b.Bounds.Should().Be(a.Bounds, "absolute layouts do not stack children");
    }

    [Fact]
    public void SetLayoutBounds_moves_an_existing_child()
    {
        var l = new SkiaAbsoluteLayout();
        var a = Box();
        l.AddChild(a, new SKRect(0, 0, 50, 50));
        Lay(l, 200, 200);
        l.SetLayoutBounds(a, new SKRect(100, 100, 150, 150));
        Lay(l, 200, 200);

        a.Bounds.X.Should().Be(100);
        a.Bounds.Y.Should().Be(100);
    }

    [Fact]
    public void Handler_maps_MAUI_attached_bounds_and_flags()
    {
        AbsoluteLayoutHandler.Mapper.Should().NotBeNull();
        var handler = new AbsoluteLayoutHandler();
        handler.Should().BeAssignableTo<LayoutHandler>();
        // The platform view must be the absolute layout, not the generic stack.
        typeof(AbsoluteLayoutHandler).GetMethod("CreatePlatformView", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(handler, null).Should().BeOfType<SkiaAbsoluteLayout>();
    }

    [Fact]
    public void Draws_without_throwing()
    {
        var l = new SkiaAbsoluteLayout();
        l.AddChild(Box(), new SKRect(0, 0, 0.5f, 0.5f), AbsoluteLayoutFlags.All);
        Lay(l, 100, 100);
        using var surface = SKSurface.Create(new SKImageInfo(100, 100));
        Record.Exception(() => l.Draw(surface.Canvas)).Should().BeNull();
    }
}
