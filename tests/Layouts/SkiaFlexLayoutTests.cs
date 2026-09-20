// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Layouts;

/// <summary>FlexLayout: direction, wrap, justification, alignment, grow.</summary>
public class SkiaFlexLayoutTests
{
    private static SkiaBoxView Box(double w, double h) => new() { WidthRequest = w, HeightRequest = h, Color = Colors.Blue };

    private static SkiaFlexLayout Layout(FlexDirection direction, FlexWrap wrap = FlexWrap.NoWrap, FlexJustify justify = FlexJustify.Start, FlexAlignItems align = FlexAlignItems.Start)
    {
        var l = new SkiaFlexLayout { Direction = direction, Wrap = wrap, JustifyContent = justify, AlignItems = align };
        return l;
    }

    private static void Lay(SkiaView v, double w, double h)
    {
        v.Measure(new Size(w, h));
        v.Arrange(new Rect(0, 0, w, h));
    }

    [Fact]
    public void Row_places_children_left_to_right()
    {
        var l = Layout(FlexDirection.Row);
        var a = Box(50, 20); var b = Box(50, 20);
        l.AddChild(a); l.AddChild(b);
        Lay(l, 300, 100);

        a.Bounds.Left.Should().Be(0);
        b.Bounds.Left.Should().BeApproximately(50, 0.5);
        a.Bounds.Top.Should().Be(b.Bounds.Top);
    }

    [Fact]
    public void Column_stacks_children_top_to_bottom()
    {
        var l = Layout(FlexDirection.Column);
        var a = Box(50, 20); var b = Box(50, 20);
        l.AddChild(a); l.AddChild(b);
        Lay(l, 300, 100);

        b.Bounds.Top.Should().BeApproximately(20, 0.5);
        a.Bounds.Left.Should().Be(b.Bounds.Left);
    }

    [Fact]
    public void RowReverse_places_first_child_at_the_end()
    {
        var l = Layout(FlexDirection.RowReverse);
        var a = Box(50, 20); var b = Box(50, 20);
        l.AddChild(a); l.AddChild(b);
        Lay(l, 300, 100);

        a.Bounds.Left.Should().BeGreaterThan(b.Bounds.Left);
    }

    [Fact]
    public void Wrap_moves_overflowing_children_to_the_next_line()
    {
        var l = Layout(FlexDirection.Row, FlexWrap.Wrap);
        var boxes = Enumerable.Range(0, 4).Select(_ => Box(100, 30)).ToList();
        foreach (var b in boxes) l.AddChild(b);
        Lay(l, 250, 200); // two per line

        boxes[0].Bounds.Top.Should().Be(boxes[1].Bounds.Top);
        boxes[2].Bounds.Top.Should().BeGreaterThan(boxes[0].Bounds.Top);
        boxes[2].Bounds.Left.Should().Be(boxes[0].Bounds.Left);
    }

    [Fact]
    public void NoWrap_keeps_children_on_one_line()
    {
        var l = Layout(FlexDirection.Row, FlexWrap.NoWrap);
        var boxes = Enumerable.Range(0, 4).Select(_ => Box(100, 30)).ToList();
        foreach (var b in boxes) l.AddChild(b);
        Lay(l, 250, 200);

        boxes.Select(b => b.Bounds.Top).Distinct().Should().HaveCount(1);
    }

    [Theory]
    [InlineData(FlexJustify.Start, 0)]
    [InlineData(FlexJustify.Center, 100)]
    [InlineData(FlexJustify.End, 200)]
    public void JustifyContent_positions_the_line(FlexJustify justify, double expectedFirstLeft)
    {
        var l = Layout(FlexDirection.Row, justify: justify);
        var a = Box(50, 20); var b = Box(50, 20);
        l.AddChild(a); l.AddChild(b);
        Lay(l, 300, 100);

        a.Bounds.Left.Should().BeApproximately(expectedFirstLeft, 1);
    }

    [Fact]
    public void SpaceBetween_pushes_children_to_the_edges()
    {
        var l = Layout(FlexDirection.Row, justify: FlexJustify.SpaceBetween);
        var a = Box(50, 20); var b = Box(50, 20);
        l.AddChild(a); l.AddChild(b);
        Lay(l, 300, 100);

        a.Bounds.Left.Should().BeApproximately(0, 1);
        b.Bounds.Right.Should().BeApproximately(300, 1);
    }

    [Theory]
    [InlineData(FlexAlignItems.Start, 0)]
    [InlineData(FlexAlignItems.Center, 40)]
    [InlineData(FlexAlignItems.End, 80)]
    public void AlignItems_positions_children_on_the_cross_axis(FlexAlignItems align, double expectedTop)
    {
        var l = Layout(FlexDirection.Row, align: align);
        var a = Box(50, 20);
        l.AddChild(a);
        Lay(l, 300, 100);

        a.Bounds.Top.Should().BeApproximately(expectedTop, 1);
    }

    [Fact]
    public void Stretch_fills_the_cross_axis()
    {
        var l = Layout(FlexDirection.Row, align: FlexAlignItems.Stretch);
        var a = new SkiaBoxView { WidthRequest = 50, Color = Colors.Red };
        l.AddChild(a);
        Lay(l, 300, 100);

        a.Bounds.Height.Should().BeApproximately(100, 1);
    }

    [Fact]
    public void Grow_distributes_remaining_space()
    {
        var l = Layout(FlexDirection.Row);
        var a = Box(50, 20); var b = Box(50, 20);
        SkiaFlexLayout.SetGrow(b, 1f);
        l.AddChild(a); l.AddChild(b);
        Lay(l, 300, 100);

        a.Bounds.Width.Should().BeApproximately(50, 1);
        b.Bounds.Width.Should().BeApproximately(250, 1);
    }

    [Fact]
    public void Draws_without_throwing()
    {
        var l = Layout(FlexDirection.Row, FlexWrap.Wrap);
        for (int i = 0; i < 5; i++) l.AddChild(Box(60, 30));
        Lay(l, 200, 100);
        using var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(200, 100));
        var ex = Record.Exception(() => l.Draw(surface.Canvas));
        ex.Should().BeNull();
    }
}
