// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Services;

/// <summary>
/// The toolkit-independent parts of OpenMaui's CommunityToolkit.Maui bridges (the package
/// behaviour itself is in tests/Compat): the launcher badge, the DrawingView image renderer,
/// notifications with action callbacks, and the accessible reading order.
/// </summary>
public class CommunityToolkitBridgeTests
{
    // ---- Launcher badge -------------------------------------------------------------------

    [Fact]
    public void LauncherBadge_properties_show_the_count_and_hide_zero()
    {
        var shown = LauncherBadgeService.Properties(12);
        shown["count"].Should().BeOfType<long>().Which.Should().Be(12, "LauncherEntry's count is an int64 (D-Bus 'x')");
        shown["count-visible"].Should().Be(true);

        LauncherBadgeService.Properties(0)["count-visible"].Should().Be(false);
    }

    [Fact]
    public void LauncherBadge_app_uri_names_the_launching_desktop_file()
    {
        var previous = Environment.GetEnvironmentVariable("GIO_LAUNCHED_DESKTOP_FILE");
        try
        {
            Environment.SetEnvironmentVariable("GIO_LAUNCHED_DESKTOP_FILE", "/usr/share/applications/com.example.Notes.desktop");
            LauncherBadgeService.AppUri().Should().Be("application://com.example.Notes.desktop");

            Environment.SetEnvironmentVariable("GIO_LAUNCHED_DESKTOP_FILE", null);
            LauncherBadgeService.AppUri().Should().StartWith("application://").And.EndWith(".desktop");
        }
        finally
        {
            Environment.SetEnvironmentVariable("GIO_LAUNCHED_DESKTOP_FILE", previous);
        }
    }

    [Fact]
    public void LauncherBadge_SetCount_never_throws_and_keeps_the_count()
    {
        var previous = LauncherBadgeService.Emitter;
        LauncherBadgeService.Emitter = (_, _) => throw new InvalidOperationException("no bus");
        try
        {
            var set = () => LauncherBadgeService.SetCount(3);
            set.Should().NotThrow();
            LauncherBadgeService.Count.Should().Be(3u);
        }
        finally
        {
            LauncherBadgeService.Emitter = previous;
        }
    }

    // ---- DrawingView image export -----------------------------------------------------------

    private static SKBitmap Decode(Stream stream)
    {
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return SKBitmap.Decode(copy.ToArray());
    }

    [Fact]
    public void DrawingImage_is_the_line_bounds_grown_by_the_widest_line()
    {
        var strokes = new[]
        {
            new DrawingImageExport.Stroke(new[] { new PointF(10, 10), new PointF(60, 10) }, Colors.Red, 4),
            new DrawingImageExport.Stroke(new[] { new PointF(10, 30), new PointF(60, 30) }, Colors.Blue, 8),
        };

        using var image = Decode(DrawingImageExport.Render(strokes, new SolidPaint(Colors.White), null));

        image.Width.Should().Be(66, "50 wide plus the widest line (8) on each side");
        image.Height.Should().Be(36);
        image.GetPixel(30, 8).Should().Be(new SKColor(255, 0, 0), "points move by (minX, minY) minus the widest line");
        image.GetPixel(30, 28).Should().Be(new SKColor(0, 0, 255));
        image.GetPixel(30, 18).Should().Be(SKColors.White);
    }

    [Fact]
    public void DrawingImage_full_canvas_keeps_coordinates_and_unknown_backgrounds_use_the_default()
    {
        var strokes = new[] { new DrawingImageExport.Stroke(new[] { new PointF(100, 50), new PointF(150, 50) }, Colors.Black, 6) };

        using var image = Decode(DrawingImageExport.Render(strokes, null, new Size(200, 100)));

        image.Width.Should().Be(200);
        image.Height.Should().Be(100);
        image.GetPixel(120, 50).Should().Be(SKColors.Black);
        image.GetPixel(10, 10).Should().Be(new SKColor(211, 211, 211), "no background paint gives DrawingViewDefaults.BackgroundColor (LightGray)");
    }

    [Fact]
    public void DrawingImage_radial_background_and_empty_input()
    {
        var strokes = new[] { new DrawingImageExport.Stroke(new[] { new PointF(0, 0), new PointF(100, 100) }, Colors.Black, 1) };
        var radial = new RadialGradientPaint
        {
            Center = new Point(0.5, 0.5),
            Radius = 0.5,
            GradientStops = new[] { new PaintGradientStop(0, Colors.Red), new PaintGradientStop(1, Colors.Blue) },
        };

        using var image = Decode(DrawingImageExport.Render(strokes, radial, new Size(100, 100)));
        image.GetPixel(50, 52).Red.Should().BeGreaterThan(200, "red at the centre");
        image.GetPixel(2, 98).Blue.Should().BeGreaterThan(200, "blue past the radius");

        DrawingImageExport.Render(Array.Empty<DrawingImageExport.Stroke>(), null, null).Should().BeSameAs(Stream.Null);
        DrawingImageExport.Render(new[] { new DrawingImageExport.Stroke(Array.Empty<PointF>(), Colors.Black, 5) }, null, null).Should().BeSameAs(Stream.Null);
    }

    // ---- Notifications with action callbacks ---------------------------------------------------

    [Fact]
    public async Task Notification_with_callbacks_honours_expiry_actions_and_close()
    {
        var server = new FakeNotificationServer { NextId = 500 };
        var service = new NotificationService("App", null, new FakeDesktopPortal(), server);
        NotificationClosedEventArgs? closed = null;
        service.NotificationClosed += (_, e) => closed = e;
        int undone = 0;

        var id = await service.ShowAsync(new NotificationOptions
        {
            Title = "Deleted",
            ExpireTimeMs = 4000,
            IsTransient = true,
            Actions = new Dictionary<string, string> { ["undo"] = "Undo" },
        }, new Dictionary<string, Action?> { ["undo"] = () => undone++ });

        var sent = server.Notified.Single();
        sent.Expire.Should().Be(4000);
        sent.Actions.Should().Equal("undo", "Undo");
        sent.Hints.Should().ContainKey("transient");
        server.ActionInvoked.Should().NotBeNull("the signal watch is in place before the notification is sent");

        server.ActionInvoked!(500, "undo");
        undone.Should().Be(1);

        server.NotificationClosed!(500, 1);
        closed!.NotificationId.Should().Be(id);
    }

    // ---- Accessible reading order --------------------------------------------------------------

    private sealed class Leaf : SkiaView
    {
    }

    [Fact]
    public void Layouts_expose_their_children_to_assistive_technology()
    {
        var layout = new SkiaStackLayout();
        var a = new Leaf();
        var b = new Leaf();
        layout.AddChild(a);
        layout.AddChild(b);

        ((IAccessible)layout).Children.Should().Equal(a, b);
    }

    [Fact]
    public void Accessible_order_puts_listed_views_first_at_every_level()
    {
        var root = new SkiaStackLayout();
        var group = new SkiaStackLayout();
        var a = new Leaf();
        var b = new Leaf();
        var c = new Leaf();
        var d = new Leaf();
        group.AddChild(a);
        group.AddChild(b);
        root.AddChild(c);
        root.AddChild(group);
        root.AddChild(d);
        _ = ((IAccessible)root).Children; // cached before the order is set

        root.SetAccessibleOrder(() => new SkiaView[] { b, d, a });

        ((IAccessible)root).Children.Should().Equal(new IAccessible[] { group, d, c }, "the group holds the first listed view, d is next, c is unlisted");
        ((IAccessible)group).Children.Should().Equal(b, a);

        root.SetAccessibleOrder(null);
        ((IAccessible)root).Children.Should().Equal(c, group, d);
    }
}
