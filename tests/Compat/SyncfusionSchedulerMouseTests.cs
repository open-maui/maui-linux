// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.Reflection;
using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Syncfusion;
using Microsoft.Maui.Platform.Linux.Window;
using Syncfusion.Maui.Scheduler;
using Xunit;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// SfScheduler with a mouse, as its Windows build handles one: a press held for 300 ms raises
/// LongPressed and, over an appointment, starts dragging it (WinUI raises no hold for a mouse,
/// so the Windows build times it); a tap that ends a hold is not raised; the bottom edge of an
/// appointment shows the resize cursor and a drag from it resizes; a right-click on the header
/// raises RightTapped. The compat host's dispatcher timers never tick on their own, so the hold
/// timer is fired explicitly; its dispatcher also runs dispatches inline, so the appointments
/// are set once the scheduler is on screen (an app's dispatcher posts them).
/// </summary>
[Collection("LinuxApplication.Current")]
public sealed class SyncfusionSchedulerMouseTests
{
    private static readonly MethodInfo FireHolds = typeof(LinuxSyncfusionBuilderExtensions).Assembly
        .GetType("Microsoft.Maui.Platform.Linux.Syncfusion.SfSchedulerPatches")!
        .GetMethod("FireHolds", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static (CompatHost Host, SfScheduler Scheduler, SchedulerAppointment Appointment, List<string> Log) Host(
        SchedulerView view = SchedulerView.Day, SchedulerAppointment? appointment = null)
    {
        var start = DateTime.Today.AddHours(2);
        appointment ??= new SchedulerAppointment { StartTime = start, EndTime = start.AddHours(2), Subject = "Meeting", Background = Microsoft.Maui.Graphics.Colors.Orange };
        var scheduler = new SfScheduler { View = view, AllowAppointmentDrag = true, AllowAppointmentResize = true };
        var log = new List<string>();
        scheduler.AppointmentDragStarting += (_, _) => log.Add("drag-starting");
        scheduler.AppointmentDrop += (_, e) => log.Add("drop " + e.DropTime.ToString("HH:mm"));
        scheduler.AppointmentResizeStart += (_, _) => log.Add("resize-start");
        scheduler.AppointmentResizeEnd += (_, e) => log.Add("resize-end " + e.ResizeEdge);
        scheduler.LongPressed += (_, e) => log.Add("long-pressed " + e.Element);
        scheduler.Tapped += (_, e) => log.Add("tapped " + e.Element);
        scheduler.RightTapped += (_, e) => log.Add("right-tapped " + e.Element);
        var host = new CompatHost(new ContentPage { Content = scheduler }, b => b.UseLinuxSyncfusion(), 600, 600);
        Pump(host);
        scheduler.AppointmentsSource = new ObservableCollection<SchedulerAppointment> { appointment };
        Pump(host);
        log.Clear();
        return (host, scheduler, appointment, log);
    }

    private static void Pump(CompatHost host, int frames = 5)
    {
        for (int i = 0; i < frames; i++)
        {
            Thread.Sleep(10);
            Microsoft.Maui.Platform.Linux.Hosting.LinuxTicker.PumpAll();
            host.Render();
        }
    }

    // The appointment's drawn rectangle (its orange fill), in window pixels.
    private static (int Left, int Top, int Right, int Bottom) Appointment(CompatHost host)
    {
        int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
        for (int y = 0; y < host.DisplayWindow.Height; y++)
        {
            for (int x = 0; x < host.DisplayWindow.Width; x++)
            {
                var (r, g, b, _) = host.DisplayWindow.PixelAt(x, y);
                if (r > 240 && g > 150 && g < 180 && b < 30)
                {
                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x);
                    bottom = Math.Max(bottom, y);
                }
            }
        }
        right.Should().BeGreaterThan(0, "the appointment is drawn");
        return (left, top, right, bottom);
    }

    [Fact]
    public void A_held_press_drags_the_appointment()
    {
        var (host, _, appointment, log) = Host();
        using (host)
        {
            var box = Appointment(host);
            float x = box.Left + 40, y = box.Top + 20;
            host.DisplayWindow.RaisePointerMoved(x, y);
            host.DisplayWindow.RaisePointerPressed(x, y);
            ((int)FireHolds.Invoke(null, null)!).Should().BeGreaterThan(0, "the press started the 300 ms hold timer");
            for (int i = 1; i <= 10; i++)
            {
                host.DisplayWindow.RaisePointerMoved(x, y + i * 10);
                Pump(host, 1);
            }
            host.DisplayWindow.RaisePointerReleased(x, y + 100);
            Pump(host);

            log.Should().Contain("long-pressed Appointment");
            log.Should().Contain("drag-starting");
            log.Should().Contain(l => l.StartsWith("drop "));
            appointment.StartTime.Should().Be(DateTime.Today.AddHours(4), "100 px is two hours at the day view's 50 px per hour");
            appointment.EndTime.Should().Be(DateTime.Today.AddHours(6));
        }
    }

    [Fact]
    public void A_short_click_neither_holds_nor_drags_and_a_tap_after_a_hold_is_not_raised()
    {
        var (host, _, appointment, log) = Host();
        using (host)
        {
            var box = Appointment(host);
            float x = box.Left + 40, y = box.Top + 20;
            host.Tap(x, y);
            log.Should().Equal("tapped Appointment");
            FireHolds.Invoke(null, null).Should().Be(0, "the release stopped the hold timer");

            log.Clear();
            host.DisplayWindow.RaisePointerPressed(x, y);
            FireHolds.Invoke(null, null);
            host.DisplayWindow.RaisePointerReleased(x, y);
            Pump(host);
            log.Should().Contain("long-pressed Appointment");
            log.Should().NotContain(l => l.StartsWith("tapped"), "WinUI raises no tap for a press that was held");
            appointment.StartTime.Should().Be(DateTime.Today.AddHours(2));
        }
    }

    [Fact]
    public void The_bottom_edge_shows_the_resize_cursor_and_a_drag_from_it_resizes()
    {
        var (host, _, appointment, log) = Host();
        using (host)
        {
            var box = Appointment(host);
            float x = box.Left + 40;
            host.DisplayWindow.RaisePointerMoved(x, box.Top + 30);
            host.DisplayWindow.LastCursor.Should().Be(CursorType.Arrow);
            host.DisplayWindow.RaisePointerMoved(x, box.Bottom);
            host.DisplayWindow.LastCursor.Should().Be(CursorType.SizeNorthSouth);

            host.DisplayWindow.RaisePointerPressed(x, box.Bottom);
            for (int i = 1; i <= 10; i++)
            {
                host.DisplayWindow.RaisePointerMoved(x, box.Bottom + i * 5);
                Pump(host, 1);
            }
            host.DisplayWindow.RaisePointerReleased(x, box.Bottom + 50);
            Pump(host);

            log.Should().Contain("resize-start");
            log.Should().Contain("resize-end Bottom");
            appointment.StartTime.Should().Be(DateTime.Today.AddHours(2));
            appointment.EndTime.Should().BeAfter(DateTime.Today.AddHours(4).AddMinutes(45), "the bottom edge moved down about an hour");
        }
    }

    [Fact]
    public void A_right_click_on_the_header_raises_RightTapped()
    {
        var (host, scheduler, _, log) = Host();
        using (host)
        {
            var header = Descendants(scheduler).OfType<VisualElement>().First(e => e.GetType().Name == "HeaderLayout");
            var (x, y) = CompatHost.CenterOf(header);
            host.DisplayWindow.RaisePointerMoved(x, y);
            host.DisplayWindow.RaisePointerPressed(x, y, Microsoft.Maui.Platform.PointerButton.Right);
            host.DisplayWindow.RaisePointerReleased(x, y);
            Pump(host);
            log.Should().Contain("right-tapped Header");
        }
    }

    [Fact]
    public void A_held_press_drags_a_month_appointment_to_another_day()
    {
        var (host, _, appointment, log) = Host(SchedulerView.Month);
        using (host)
        {
            var box = Appointment(host);
            float x = box.Left + 10, y = (box.Top + box.Bottom) / 2f;
            host.DisplayWindow.RaisePointerMoved(x, y);
            host.DisplayWindow.RaisePointerPressed(x, y);
            ((int)FireHolds.Invoke(null, null)!).Should().BeGreaterThan(0);
            // One cell to the right: the month view is 600 px wide over 7 days.
            for (int i = 1; i <= 10; i++)
            {
                host.DisplayWindow.RaisePointerMoved(x + i * 600f / 70, y);
                Pump(host, 1);
            }
            host.DisplayWindow.RaisePointerReleased(x + 600f / 7, y);
            Pump(host);

            log.Should().Contain("drag-starting");
            log.Should().Contain(l => l.StartsWith("drop "));
            appointment.StartTime.Date.Should().Be(DateTime.Today.AddDays(1));
        }
    }

    [Fact]
    public void A_timeline_appointment_resizes_from_its_right_edge_with_the_time_indicator_in_the_header()
    {
        var (host, scheduler, appointment, log) = Host(SchedulerView.TimelineDay);
        using (host)
        {
            var box = Appointment(host);
            float y = (box.Top + box.Bottom) / 2f;
            host.DisplayWindow.RaisePointerMoved(box.Left + 20, y);
            host.DisplayWindow.LastCursor.Should().Be(CursorType.Arrow);
            host.DisplayWindow.RaisePointerMoved(box.Right, y);
            host.DisplayWindow.LastCursor.Should().Be(CursorType.SizeWestEast, "the neutral build's timeline resize now shows its cursor");

            host.DisplayWindow.RaisePointerPressed(box.Right, y);
            for (int i = 1; i <= 5; i++)
            {
                host.DisplayWindow.RaisePointerMoved(box.Right + i * 10, y);
                Pump(host, 1);
            }
            // The visible timeline (the snap layout keeps the previous and next ones beside it).
            var header = Descendants(scheduler).First(e => e.GetType().Name == "TimelineHeaderLayout"
                && CompatHost.PlatformOf((VisualElement)e).ScreenBounds.Left >= 0 && CompatHost.PlatformOf((VisualElement)e).ScreenBounds.Left < 600);
            ((IVisualTreeElement)header).GetVisualChildren().Should().Contain(c => c.GetType().Name == "AppointmentResizeIndicatorView",
                "the header shows the resized time while the edge moves");
            host.DisplayWindow.RaisePointerReleased(box.Right + 50, y);
            Pump(host);

            log.Should().Contain("resize-end Right");
            appointment.EndTime.Should().BeAfter(DateTime.Today.AddHours(4));
            ((IVisualTreeElement)header).GetVisualChildren().Should().NotContain(c => c.GetType().Name == "AppointmentResizeIndicatorView");
        }
    }

    [Fact]
    public void An_all_day_appointment_shows_the_resize_cursor_over_its_edge_in_the_week_view()
    {
        var allDay = new SchedulerAppointment
        {
            StartTime = DateTime.Today, EndTime = DateTime.Today.AddDays(1).AddHours(23), IsAllDay = true,
            Subject = "Trip", Background = Microsoft.Maui.Graphics.Colors.Orange,
        };
        var (host, _, _, log) = Host(SchedulerView.Week, allDay);
        using (host)
        {
            // The compat host runs the scheduler's posted appointment update inline, before its
            // day view exists; a resize lays the all-day panel out with the appointment.
            host.DisplayWindow.RaiseResized(601, 600);
            Pump(host);
            var box = Appointment(host);
            float y = (box.Top + box.Bottom) / 2f;
            host.DisplayWindow.RaisePointerMoved((box.Left + box.Right) / 2f, y);
            host.DisplayWindow.LastCursor.Should().Be(CursorType.Arrow);
            host.DisplayWindow.RaisePointerMoved(box.Right, y);
            host.DisplayWindow.LastCursor.Should().Be(CursorType.SizeWestEast);

            host.DisplayWindow.RaisePointerPressed(box.Right, y);
            for (int i = 1; i <= 10; i++)
            {
                host.DisplayWindow.RaisePointerMoved(box.Right + i * 9, y);
                Pump(host, 1);
            }
            host.DisplayWindow.RaisePointerReleased(box.Right + 90, y);
            Pump(host);
            log.Should().Contain("resize-end Right");
        }
    }

    private static IEnumerable<Element> Descendants(Element root)
    {
        foreach (var child in ((IVisualTreeElement)root).GetVisualChildren().OfType<Element>())
        {
            yield return child;
            foreach (var d in Descendants(child))
                yield return d;
        }
    }
}

internal static class CompatHostTapExtensions
{
    public static void Tap(this CompatHost host, float x, float y)
    {
        host.DisplayWindow.RaisePointerMoved(x, y);
        host.DisplayWindow.RaisePointerPressed(x, y);
        host.DisplayWindow.RaisePointerReleased(x, y);
        host.Render();
    }
}
