// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using FluentAssertions;
using Microsoft.Maui.Controls.Linux.Tests.Golden;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Handlers;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

using Rect = Microsoft.Maui.Graphics.Rect;

/// <summary>
/// VisualElement properties every view shares, verified on the rendered
/// bitmap: Shadow, Clip, AnchorX/Y with a transform, AutomationId, IsVisible
/// and Opacity. A filled BoxView is the probe because its ink is a solid
/// rectangle whose position is easy to reason about.
/// </summary>
public class ViewPropertyRenderingTests
{
    private static SKBitmap Render(SkiaView view, int x, int y, int w, int h, int sceneW = 200, int sceneH = 200)
    {
        var root = new SkiaAbsoluteLayout();
        root.AddChild(view);
        root.SetLayoutBounds(view, new SKRect(x, y, x + w, y + h));
        return GoldenHarness.Render(root, sceneW, sceneH, 1f, SKColors.Transparent);
    }

    private static SkiaBoxView RedBox() => new() { Color = Colors.Red, WidthRequest = 60, HeightRequest = 40 };

    private static bool IsColor(SKColor actual, SKColor expected, int tolerance = 8)
        => Math.Abs(actual.Red - expected.Red) <= tolerance
        && Math.Abs(actual.Green - expected.Green) <= tolerance
        && Math.Abs(actual.Blue - expected.Blue) <= tolerance
        && Math.Abs(actual.Alpha - expected.Alpha) <= tolerance;

    // ---- Shadow ---------------------------------------------------------------

    [Fact]
    public void Shadow_paints_shadow_colour_outside_the_bounds_in_the_offset_direction()
    {
        var box = RedBox();
        box.Shadow = new Shadow { Brush = new SolidColorBrush(Colors.Blue), Offset = new Point(12, 12), Radius = 0, Opacity = 1 };
        using var bmp = Render(box, 40, 40, 60, 40);

        // Bounds: x 40..100, y 40..80. Shadow rect: x 52..112, y 52..92.
        var belowRight = bmp.GetPixel(106, 86);
        belowRight.Alpha.Should().BeGreaterThan(0, "the shadow extends past the bottom-right of the view");
        belowRight.Blue.Should().BeGreaterThan(belowRight.Red, "the shadow is blue");
        bmp.GetPixel(45, 45).Alpha.Should().BeGreaterThan(0);
        IsColor(bmp.GetPixel(45, 45), SKColors.Red).Should().BeTrue("the view itself still paints on top");
        bmp.GetPixel(30, 30).Alpha.Should().Be(0, "no shadow above-left when the offset points down-right");
    }

    [Fact]
    public void Shadow_opacity_scales_its_alpha()
    {
        var box = RedBox();
        box.Shadow = new Shadow { Brush = new SolidColorBrush(Colors.Black), Offset = new Point(20, 0), Radius = 0, Opacity = 0.5f };
        using var bmp = Render(box, 40, 40, 60, 40);

        var shadow = bmp.GetPixel(110, 60); // right of the view, inside the shadow rect
        shadow.Alpha.Should().BeInRange(100, 160, "a 0.5 opacity black shadow is half-transparent");
    }

    [Fact]
    public void No_shadow_paints_nothing_outside_the_bounds()
    {
        using var bmp = Render(RedBox(), 40, 40, 60, 40);
        bmp.GetPixel(106, 86).Alpha.Should().Be(0);
    }

    // ---- Clip -----------------------------------------------------------------

    [Fact]
    public void Rectangle_clip_hides_everything_outside_the_clip_rect()
    {
        var box = RedBox();
        box.Clip = new RectangleGeometry(new Rect(10, 10, 30, 20)); // local to the view
        using var bmp = Render(box, 40, 40, 60, 40);

        IsColor(bmp.GetPixel(55, 55), SKColors.Red).Should().BeTrue("inside the clip");
        bmp.GetPixel(42, 42).Alpha.Should().Be(0, "top-left corner is clipped");
        bmp.GetPixel(98, 78).Alpha.Should().Be(0, "bottom-right corner is clipped");
    }

    [Fact]
    public void Ellipse_clip_leaves_the_corners_transparent()
    {
        var box = RedBox();
        box.Clip = new EllipseGeometry(new Point(30, 20), 30, 20);
        using var bmp = Render(box, 40, 40, 60, 40);

        IsColor(bmp.GetPixel(70, 60), SKColors.Red).Should().BeTrue("centre of the ellipse");
        bmp.GetPixel(41, 41).Alpha.Should().Be(0, "corner outside the ellipse");
        bmp.GetPixel(99, 79).Alpha.Should().Be(0);
    }

    [Fact]
    public void Round_rectangle_clip_rounds_the_corners()
    {
        var box = RedBox();
        box.Clip = new RoundRectangleGeometry(new CornerRadius(20), new Rect(0, 0, 60, 40));
        using var bmp = Render(box, 40, 40, 60, 40);

        bmp.GetPixel(41, 41).Alpha.Should().Be(0);
        IsColor(bmp.GetPixel(70, 41), SKColors.Red).Should().BeTrue("top edge midpoint stays");
    }

    [Fact]
    public void Clip_also_applies_to_children()
    {
        var parent = new SkiaAbsoluteLayout { Clip = new RectangleGeometry(new Rect(0, 0, 30, 30)) };
        var child = RedBox();
        parent.AddChild(child);
        parent.SetLayoutBounds(child, new SKRect(0, 0, 60, 40));
        using var bmp = Render(parent, 0, 0, 60, 40);

        IsColor(bmp.GetPixel(10, 10), SKColors.Red).Should().BeTrue();
        bmp.GetPixel(50, 35).Alpha.Should().Be(0, "the child is clipped by its parent's Clip");
    }

    // ---- Anchor and transforms -----------------------------------------------

    [Fact]
    public void Rotation_about_the_default_centre_anchor_keeps_the_centre()
    {
        var box = RedBox();
        box.Rotation = 90;
        using var bmp = Render(box, 70, 80, 60, 40);

        // 60x40 at (70,80), centre (100,100). Rotated 90° about the centre it
        // becomes 40x60 centred at (100,100): x 80..120, y 70..130.
        IsColor(bmp.GetPixel(100, 100), SKColors.Red).Should().BeTrue();
        IsColor(bmp.GetPixel(100, 75), SKColors.Red).Should().BeTrue("above the original top edge after rotation");
        bmp.GetPixel(75, 100).Alpha.Should().Be(0, "left of the rotated box, inside the original bounds");
    }

    [Fact]
    public void Rotation_about_a_top_left_anchor_swings_the_ink_away()
    {
        var box = RedBox();
        box.AnchorX = 0;
        box.AnchorY = 0;
        box.Rotation = 90;
        using var bmp = Render(box, 100, 100, 60, 40);

        // Rotating 90° clockwise about (100,100): the box now occupies x 60..100, y 100..160.
        IsColor(bmp.GetPixel(80, 130), SKColors.Red).Should().BeTrue();
        bmp.GetPixel(130, 120).Alpha.Should().Be(0, "the original footprint is empty");
    }

    [Fact]
    public void Scale_about_a_corner_anchor_grows_from_that_corner()
    {
        var box = RedBox();
        box.AnchorX = 0;
        box.AnchorY = 0;
        box.Scale = 2;
        using var bmp = Render(box, 20, 20, 60, 40);

        // Scaled 2x from the top-left: x 20..140, y 20..100.
        IsColor(bmp.GetPixel(130, 90), SKColors.Red).Should().BeTrue();
        IsColor(bmp.GetPixel(22, 22), SKColors.Red).Should().BeTrue("the anchored corner stays put");
    }

    [Fact]
    public void Scale_about_the_centre_grows_both_ways()
    {
        var box = RedBox();
        box.Scale = 2;
        using var bmp = Render(box, 70, 80, 60, 40);

        // Centre (100,100), scaled: x 40..160, y 60..140.
        IsColor(bmp.GetPixel(45, 65), SKColors.Red).Should().BeTrue();
        IsColor(bmp.GetPixel(155, 135), SKColors.Red).Should().BeTrue();
        bmp.GetPixel(35, 100).Alpha.Should().Be(0);
    }

    [Fact]
    public void Translation_moves_the_ink()
    {
        var box = RedBox();
        box.TranslationX = 50;
        box.TranslationY = 30;
        using var bmp = Render(box, 10, 10, 60, 40);

        IsColor(bmp.GetPixel(90, 60), SKColors.Red).Should().BeTrue("translated footprint x 60..120, y 40..80");
        bmp.GetPixel(15, 15).Alpha.Should().Be(0, "the untranslated footprint is empty");
    }

    [Fact]
    public void Anchor_defaults_to_the_centre_and_round_trips_to_the_maui_view()
    {
        var box = new SkiaBoxView();
        box.AnchorX.Should().Be(0.5);
        box.AnchorY.Should().Be(0.5);

        var maui = new BoxView { AnchorX = 0.1, AnchorY = 0.9 };
        box.MauiView = maui;
        box.AnchorX.Should().Be(0.1, "reads live from the MAUI view");
        box.AnchorY = 0.25;
        maui.AnchorY.Should().Be(0.25, "writes go to the MAUI view");
    }

    // ---- AutomationId --------------------------------------------------------

    [Fact]
    public void AutomationId_round_trips_through_the_handler_to_the_platform_view()
    {
        var label = new Label { Text = "x", AutomationId = "greeting" };
        var handler = new LabelHandler();
        handler.SetVirtualView(label);

        handler.PlatformView.AutomationId.Should().Be("greeting", "the platform view reads live from the MAUI element");

        // MAUI makes AutomationId write-once on Element; the platform view
        // forwards the write, so the same rule applies through it.
        var act = () => handler.PlatformView.AutomationId = "renamed";
        act.Should().Throw<InvalidOperationException>();
        label.AutomationId.Should().Be("greeting");
    }

    [Fact]
    public void AutomationId_set_on_the_platform_view_reaches_the_maui_element()
    {
        var label = new Label { Text = "x" };
        var handler = new LabelHandler();
        handler.SetVirtualView(label);

        handler.PlatformView.AutomationId = "from-platform";

        label.AutomationId.Should().Be("from-platform");
        handler.PlatformView.AutomationId.Should().Be("from-platform");
    }

    [Fact]
    public void AutomationId_without_a_maui_view_is_stored_locally()
    {
        var box = new SkiaBoxView();
        box.AutomationId.Should().BeEmpty();
        box.AutomationId = "local";
        box.AutomationId.Should().Be("local");
    }

    // ---- IsVisible and Opacity -------------------------------------------------

    [Fact]
    public void Invisible_view_draws_nothing()
    {
        var box = RedBox();
        box.IsVisible = false;
        using var bmp = Render(box, 40, 40, 60, 40);

        Enumerable.Range(40, 60).All(x => bmp.GetPixel(x, 60).Alpha == 0).Should().BeTrue();
    }

    [Fact]
    public void Invisible_view_is_not_hit_tested()
    {
        var box = RedBox();
        box.Arrange(new Rect(0, 0, 60, 40));
        box.HitTest(10, 10).Should().BeSameAs(box);
        box.IsVisible = false;
        box.HitTest(10, 10).Should().BeNull();
    }

    [Fact]
    public void Half_opacity_halves_the_alpha()
    {
        var box = RedBox();
        box.Opacity = 0.5f;
        using var bmp = Render(box, 40, 40, 60, 40);

        var px = bmp.GetPixel(70, 60);
        px.Alpha.Should().BeInRange(120, 136, "opacity 0.5 renders at about half alpha");
        px.Red.Should().BeGreaterThan(px.Green);
    }

    [Fact]
    public void Zero_opacity_draws_nothing_and_full_opacity_is_opaque()
    {
        var invisible = RedBox();
        invisible.Opacity = 0;
        using (var bmp = Render(invisible, 40, 40, 60, 40))
            bmp.GetPixel(70, 60).Alpha.Should().Be(0);

        var opaque = RedBox();
        opaque.Opacity = 1;
        using (var bmp = Render(opaque, 40, 40, 60, 40))
            bmp.GetPixel(70, 60).Alpha.Should().Be(255);
    }

    [Fact]
    public void Opacity_applies_to_children_as_a_group()
    {
        var parent = new SkiaAbsoluteLayout { Opacity = 0.5f };
        var child = RedBox();
        parent.AddChild(child);
        parent.SetLayoutBounds(child, new SKRect(0, 0, 60, 40));
        using var bmp = Render(parent, 0, 0, 60, 40);

        bmp.GetPixel(30, 20).Alpha.Should().BeInRange(120, 136);
    }

    [Fact]
    public void Opacity_is_clamped_to_the_unit_range()
    {
        var box = new SkiaBoxView();
        box.Opacity = 3f;
        box.Opacity.Should().Be(1f);
        box.Opacity = -1f;
        box.Opacity.Should().Be(0f);
    }
}
